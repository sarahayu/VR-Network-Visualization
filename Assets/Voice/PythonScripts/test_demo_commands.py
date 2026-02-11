"""
Test script for demo commands against the LangGraph server.
Sends natural language equivalents of the keyboard demo commands
and checks if the server returns the expected actions and queries.

Usage:
    python test_demo_commands.py

Requires the LangGraph server to be running at localhost:5000.
"""

import requests
import json
import sys

SERVER_URL = "http://localhost:5000"

# ANSI colors for terminal output
GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
BOLD = "\033[1m"
RESET = "\033[0m"

# ============================================================
# Expected outcomes for each demo step
# These mirror what KeyboardCommandTester.RunDemoStep produces
# ============================================================

DEMO_TESTS = [
    # === Demo P: Friendship highlight demo ===
    {
        "demo": "P",
        "step": 1,
        "description": "Highlight top 3 nodes with most friendship links",
        "natural_commands": [
            "highlight the top 3 nodes that have the most friendship links",
            "select top 3 nodes by friendship degree",
        ],
        "expected_actions": [
            ["selectNode"],   # first action should be selectNode
            ["colorNode"],    # second action should be colorNode (any color)
        ],
        "expected_query_contains": [
            "friendship",     # query should mention friendship
            "LIMIT 3",        # query should limit to 3
        ],
    },
    {
        "demo": "P",
        "step": 2,
        "description": "Color all nodes by grade",
        "natural_commands": [
            "color all nodes by grade",
            "color nodes by grade",
        ],
        "expected_actions": [
            ["colorByAttribute", "grade"],
        ],
        "expected_query_contains": [
            "grade",
        ],
    },
    {
        "demo": "P",
        "step": 3,
        "description": "Color aggression links for highlighted nodes in red",
        "natural_commands": [
            "color the aggression links for highlighted nodes in red",
            "color aggression links for selected nodes red",
        ],
        "expected_actions": [
            ["selectLink"],
            ["colorLink"],
        ],
        "expected_query_contains": [
            "aggression",
            "selected",
        ],
    },

    # === Demo L: Smoker + aggression demo ===
    {
        "demo": "L",
        "step": 1,
        "description": "Color nodes by smoker",
        "natural_commands": [
            "color nodes by smoker",
            "color all nodes by smoker attribute",
        ],
        "expected_actions": [
            ["colorByAttribute", "smoker"],
        ],
        "expected_query_contains": [
            "smoker",
        ],
    },
    {
        "demo": "L",
        "step": 2,
        "description": "Color all aggression links in red",
        "natural_commands": [
            "color all aggression links in red",
            "color aggression links red",
        ],
        "expected_actions": [
            ["selectLink"],
            ["colorLink"],
        ],
        "expected_query_contains": [
            "aggression",
        ],
    },

    # === Demo O: Gender + aggression targets + friendship links ===
    {
        "demo": "O",
        "step": 1,
        "description": "Color nodes by gender",
        "natural_commands": [
            "color nodes by gender",
            "color all nodes by sex",
        ],
        "expected_actions": [
            ["colorByAttribute"],
        ],
        "expected_query_contains": [
            "sex",
        ],
    },
    {
        "demo": "O",
        "step": 2,
        "description": "Select nodes with most incoming aggression links",
        "natural_commands": [
            "select the nodes with the most incoming aggression links",
            "highlight top 3 nodes that receive the most aggression",
        ],
        "expected_actions": [
            ["selectNode"],
            ["colorNode"],
        ],
        "expected_query_contains": [
            "aggression",
        ],
    },
    {
        "demo": "O",
        "step": 3,
        "description": "Color friendship links for selected nodes in blue",
        "natural_commands": [
            "color their friendship links in blue",
            "color friendship links for selected nodes blue",
        ],
        "expected_actions": [
            ["selectLink"],
            ["colorLink"],
        ],
        "expected_query_contains": [
            "friendship",
            "selected",
        ],
    },
]


def check_server():
    """Check if server is running."""
    try:
        r = requests.get(f"{SERVER_URL}/ping", timeout=5)
        return r.status_code == 200
    except Exception:
        return False


def send_command(text):
    """Send a command to the server and return the parsed response."""
    try:
        r = requests.post(
            f"{SERVER_URL}/classify",
            json={"userText": text},
            headers={"Content-Type": "application/json"},
            timeout=60,
        )
        if r.status_code == 200:
            return r.json()
        else:
            return {"error": f"HTTP {r.status_code}: {r.text}"}
    except Exception as e:
        return {"error": str(e)}


def check_actions(response, expected_actions):
    """
    Check if the response actions match expected patterns.
    expected_actions is a list of lists. Each inner list has [action_name] or [action_name, param].
    We check:
      - Same number of actions
      - Each action name matches
      - If param is specified, it matches too
    Returns (passed, details)
    """
    actual = response.get("actions", [])
    issues = []

    if len(actual) != len(expected_actions):
        issues.append(f"Expected {len(expected_actions)} action(s), got {len(actual)}")

    for i, expected in enumerate(expected_actions):
        if i >= len(actual):
            issues.append(f"Missing action {i+1}: expected {expected[0]}")
            continue

        act = actual[i]
        # Check action name
        if act[0] != expected[0]:
            issues.append(f"Action {i+1}: expected '{expected[0]}', got '{act[0]}'")

        # Check param if specified
        if len(expected) > 1 and len(act) > 1:
            if expected[1].lower() != act[1].lower():
                issues.append(f"Action {i+1} param: expected '{expected[1]}', got '{act[1]}'")

    return len(issues) == 0, issues


def check_queries(response, expected_contains):
    """
    Check if the response queries contain expected keywords.
    Returns (passed, details)
    """
    queries = response.get("queries", [])
    all_queries_text = " ".join(q for q in queries if q).lower()
    issues = []

    for keyword in expected_contains:
        if keyword.lower() not in all_queries_text:
            issues.append(f"Query missing keyword: '{keyword}'")

    return len(issues) == 0, issues


def run_test(test_case):
    """Run a single test case. Try each natural command variant until one passes."""
    demo = test_case["demo"]
    step = test_case["step"]
    desc = test_case["description"]

    print(f"\n{BOLD}[Demo {demo} - Step {step}] {desc}{RESET}")
    print(f"  Expected actions: {test_case['expected_actions']}")
    print(f"  Expected query keywords: {test_case['expected_query_contains']}")

    best_result = None
    best_score = -1

    for cmd in test_case["natural_commands"]:
        print(f"\n  {CYAN}Sending:{RESET} \"{cmd}\"")
        response = send_command(cmd)

        if "error" in response:
            print(f"  {RED}Error: {response['error']}{RESET}")
            continue

        # Show what server returned
        actions = response.get("actions", [])
        queries = response.get("queries", [])
        corrected = response.get("corrected_input", "")
        timings = response.get("timings", {})

        if corrected and corrected != cmd:
            print(f"  Corrected to: \"{corrected}\"")

        print(f"  Actions:  {json.dumps(actions)}")
        for qi, q in enumerate(queries):
            if q and q.strip():
                print(f"  Query {qi+1}: {q}")

        if timings:
            total = sum(timings.values())
            print(f"  Time:    {total:.2f}s")

        # Validate
        act_ok, act_issues = check_actions(response, test_case["expected_actions"])
        qry_ok, qry_issues = check_queries(response, test_case["expected_query_contains"])

        score = (1 if act_ok else 0) + (1 if qry_ok else 0)
        if score > best_score:
            best_score = score
            best_result = {
                "command": cmd,
                "response": response,
                "act_ok": act_ok,
                "act_issues": act_issues,
                "qry_ok": qry_ok,
                "qry_issues": qry_issues,
            }

        # If both pass, no need to try more variants
        if act_ok and qry_ok:
            break

    if best_result is None:
        print(f"  {RED}FAIL - All command variants failed to reach server{RESET}")
        return False

    # Report best result
    all_ok = best_result["act_ok"] and best_result["qry_ok"]

    if all_ok:
        print(f"  {GREEN}PASS{RESET}")
    else:
        if not best_result["act_ok"]:
            for issue in best_result["act_issues"]:
                print(f"  {RED}Action issue: {issue}{RESET}")
        if not best_result["qry_ok"]:
            for issue in best_result["qry_issues"]:
                print(f"  {YELLOW}Query issue: {issue}{RESET}")
        print(f"  {RED}FAIL{RESET}")

    return all_ok


def main():
    print(f"{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Demo Commands - LangGraph Server Test{RESET}")
    print(f"{BOLD}{'='*60}{RESET}")
    print(f"Server: {SERVER_URL}")

    # Check server
    print("\nChecking server...", end=" ")
    if not check_server():
        print(f"{RED}NOT RUNNING{RESET}")
        print("Start the server first: python langgraph_server.py")
        sys.exit(1)
    print(f"{GREEN}OK{RESET}")

    # Run all tests
    passed = 0
    failed = 0

    for test in DEMO_TESTS:
        if run_test(test):
            passed += 1
        else:
            failed += 1

    # Summary
    total = passed + failed
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Results: {passed}/{total} passed{RESET}")
    if failed == 0:
        print(f"{GREEN}All demo commands produced expected server responses!{RESET}")
    else:
        print(f"{YELLOW}{failed} test(s) returned unexpected actions/queries.{RESET}")
        print("This may mean the LLM agent needs prompt tuning for these commands.")
    print(f"{'='*60}")

    sys.exit(0 if failed == 0 else 1)


if __name__ == "__main__":
    main()
