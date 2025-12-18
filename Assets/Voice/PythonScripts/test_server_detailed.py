"""
Detailed test script for the LangGraph classification server
Generates a comprehensive markdown report with all test details
"""

import requests
import json
from datetime import datetime

# Server URL
SERVER_URL = "http://localhost:5000"

# Store all test results
test_results = []
total_tests = 0
passed_tests = 0
failed_tests = 0

def test_ping():
    """Test if server is alive"""
    global total_tests, passed_tests, failed_tests
    total_tests += 1

    result = {
        "test_name": "Server Health Check",
        "endpoint": "/ping",
        "method": "GET",
        "success": False,
        "status_code": None,
        "response": None,
        "error": None
    }

    try:
        response = requests.get(f"{SERVER_URL}/ping", timeout=5)
        result["status_code"] = response.status_code
        result["response"] = response.text
        result["success"] = response.status_code == 200

        if result["success"]:
            passed_tests += 1
        else:
            failed_tests += 1

    except Exception as e:
        result["error"] = str(e)
        failed_tests += 1

    test_results.append(result)
    return result["success"]

def test_classify(user_input, description=""):
    """Test the /classify endpoint with a user input"""
    global total_tests, passed_tests, failed_tests
    total_tests += 1

    result = {
        "test_name": description or f"Classify: {user_input}",
        "user_input": user_input,
        "endpoint": "/classify",
        "method": "POST",
        "success": False,
        "status_code": None,
        "original_input": None,
        "corrected_input": None,
        "actions": None,
        "queries": None,
        "clarify": None,
        "timings": None,
        "error": None,
        "raw_response": None
    }

    try:
        response = requests.post(
            f"{SERVER_URL}/classify",
            json={"userText": user_input},
            headers={"Content-Type": "application/json"},
            timeout=60
        )

        result["status_code"] = response.status_code
        result["raw_response"] = response.text

        if response.status_code == 200:
            data = response.json()
            result["original_input"] = data.get("original_input", "")
            result["corrected_input"] = data.get("corrected_input", "")
            result["actions"] = data.get("actions", [])
            result["queries"] = data.get("queries", [])
            result["clarify"] = data.get("clarify", "")
            result["timings"] = data.get("timings", {})
            result["success"] = True
            passed_tests += 1
        else:
            result["error"] = f"HTTP {response.status_code}: {response.text}"
            failed_tests += 1

    except Exception as e:
        result["error"] = str(e)
        failed_tests += 1

    test_results.append(result)
    return result["success"]

def generate_markdown_report():
    """Generate a comprehensive markdown report"""

    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    md = f"""# LangGraph Server - Comprehensive Test Report

**Generated:** {timestamp}
**Total Tests:** {total_tests}
**Passed:** {passed_tests} ✓
**Failed:** {failed_tests} ✗
**Success Rate:** {(passed_tests/total_tests*100) if total_tests > 0 else 0:.1f}%

---

"""

    # Add detailed results for each test
    for i, result in enumerate(test_results, 1):
        status = "✓ PASSED" if result["success"] else "✗ FAILED"

        md += f"""## Test {i}: {result['test_name']}

**Status:** {status}
**Endpoint:** `{result['method']} {result['endpoint']}`
"""

        if result["endpoint"] == "/ping":
            md += f"""**HTTP Status:** {result['status_code']}
**Response:** `{result['response']}`
"""
            if result["error"]:
                md += f"""**Error:** {result['error']}
"""
        else:
            # Detailed classification test
            md += f"""
### Request
```json
{{
  "userText": "{result['user_input']}"
}}
```

**HTTP Status:** {result['status_code']}

"""
            if result["success"]:
                md += f"""### Voice Correction
- **Original Input:** `{result['original_input']}`
- **Corrected Input:** `{result['corrected_input']}`
"""

                # Show what was corrected
                if result['original_input'] != result['corrected_input']:
                    md += f"""- **Changes Made:** Voice recognition errors corrected
"""
                else:
                    md += f"""- **Changes Made:** None (input was clear)
"""

                md += f"""
### Generated Actions
```json
{json.dumps(result['actions'], indent=2)}
```

**Action Count:** {len(result['actions'])}

"""

                # Explain each action
                if result['actions']:
                    md += "### Action Breakdown\n"
                    for idx, action in enumerate(result['actions'], 1):
                        action_name = action[0] if len(action) > 0 else "unknown"
                        action_param = action[1] if len(action) > 1 else ""

                        md += f"""
#### Action {idx}: `{action_name}`
- **Parameter:** `{action_param}`
"""

                        # Add action-specific explanation
                        if action_name == "selectNode":
                            md += f"- **Purpose:** Select nodes matching condition\n"
                            md += f"- **Database Query:** Will execute Cypher query to find matching nodes\n"
                        elif action_name == "colorNode":
                            md += f"- **Purpose:** Change color of selected nodes\n"
                            md += f"- **Color:** {action_param}\n"
                        elif action_name == "sizeNode":
                            if ":" in action_param:
                                attr, scope = action_param.split(":")
                                md += f"- **Purpose:** Size nodes by attribute `{attr}`\n"
                                md += f"- **Scope:** {'All nodes' if scope == 'all' else 'Selected nodes only'}\n"
                            else:
                                md += f"- **Purpose:** Size nodes by attribute `{action_param}`\n"
                        elif action_name == "selectLink":
                            md += f"- **Purpose:** Select links matching condition\n"
                        elif action_name == "move":
                            md += f"- **Purpose:** Move selected nodes\n"
                        elif action_name == "layout":
                            md += f"- **Purpose:** Change layout to `{action_param}`\n"
                        elif action_name == "deselect":
                            md += f"- **Purpose:** Clear current selection\n"
                        elif action_name == "arithmetic":
                            md += f"- **Purpose:** Perform calculation `{action_param}`\n"

                md += f"""
### Generated Cypher Queries
"""

                if result['queries']:
                    for idx, query in enumerate(result['queries'], 1):
                        if query and query.strip() and query.strip() != '""':
                            md += f"""
#### Query {idx}
```cypher
{query}
```
"""
                        else:
                            md += f"""
#### Query {idx}
```
No database query needed (Unity handles this action)
```
"""
                else:
                    md += "No queries generated.\n"

                md += f"""
### Clarification
"""
                if result['clarify']:
                    md += f"""**System needs clarification:**
> {result['clarify']}
"""
                else:
                    md += "No clarification needed - command was understood.\n"

                md += f"""
### Performance Timings
"""
                if result['timings']:
                    total_time = sum(result['timings'].values())
                    md += f"**Total Processing Time:** {total_time:.3f}s\n\n"
                    md += "| Agent | Duration | Percentage |\n"
                    md += "|-------|----------|------------|\n"

                    for agent, duration in sorted(result['timings'].items(), key=lambda x: x[1], reverse=True):
                        percentage = (duration / total_time * 100) if total_time > 0 else 0
                        md += f"| `{agent}` | {duration:.3f}s | {percentage:.1f}% |\n"
                else:
                    md += "No timing data available.\n"

                md += f"""
### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{json.dumps(json.loads(result['raw_response']), indent=2)}
```

</details>
"""
            else:
                md += f"""### Error
**Error Message:** {result['error']}

**Raw Response:**
```
{result['raw_response']}
```
"""

        md += "\n---\n\n"

    # Add summary statistics
    md += f"""## Summary Statistics

### Overall Performance
"""

    if passed_tests > 0:
        # Calculate average timings
        all_timings = {}
        timing_counts = {}

        for result in test_results:
            if result.get('timings'):
                for agent, duration in result['timings'].items():
                    if agent not in all_timings:
                        all_timings[agent] = 0
                        timing_counts[agent] = 0
                    all_timings[agent] += duration
                    timing_counts[agent] += 1

        if all_timings:
            md += "\n### Average Agent Performance\n"
            md += "| Agent | Average Duration | Sample Size |\n"
            md += "|-------|------------------|-------------|\n"

            for agent in sorted(all_timings.keys()):
                avg = all_timings[agent] / timing_counts[agent]
                md += f"| `{agent}` | {avg:.3f}s | {timing_counts[agent]} tests |\n"

    # Add feature validation summary
    md += f"""
### Features Validated

"""

    features_found = set()
    for result in test_results:
        if result.get('actions'):
            for action in result['actions']:
                if len(action) > 0:
                    features_found.add(action[0])

    feature_descriptions = {
        "selectNode": "Node selection with conditions",
        "selectLink": "Link selection with conditions",
        "colorNode": "Node color modification",
        "colorLink": "Link color modification",
        "sizeNode": "Node size encoding by attribute",
        "move": "Spatial repositioning",
        "layout": "Layout algorithm changes",
        "deselect": "Clear selections",
        "arithmetic": "Arithmetic operations on graph data"
    }

    for feature in sorted(features_found):
        desc = feature_descriptions.get(feature, "Unknown feature")
        md += f"- ✓ **{feature}**: {desc}\n"

    md += f"""
### Voice Corrections Detected

"""

    corrections = []
    for result in test_results:
        if result.get('original_input') and result.get('corrected_input'):
            if result['original_input'] != result['corrected_input']:
                corrections.append({
                    'original': result['original_input'],
                    'corrected': result['corrected_input']
                })

    if corrections:
        for corr in corrections:
            md += f"- `{corr['original']}` → `{corr['corrected']}`\n"
    else:
        md += "No voice corrections were needed in these tests.\n"

    md += f"""

---

## Conclusion

"""

    if failed_tests == 0:
        md += "**All tests passed successfully!** ✓\n\n"
        md += "The LangGraph server is functioning correctly and ready for Unity integration.\n"
    else:
        md += f"**{failed_tests} test(s) failed.** ✗\n\n"
        md += "Please review the failed tests above and address any issues before deployment.\n"

    return md

def main():
    print("=" * 60)
    print("LangGraph Server - Detailed Test Suite")
    print("=" * 60)
    print()

    # Test 1: Server health check
    print("[1/10] Testing server health...")
    if not test_ping():
        print("[ERROR] Server is not running!")
        print("Please start the server with: python langgraph_server.py")

        # Generate report even on failure
        markdown = generate_markdown_report()
        with open("test_results_detailed.md", "w", encoding="utf-8") as f:
            f.write(markdown)
        print("\nReport saved to: test_results_detailed.md")
        return

    print("[SUCCESS] Server is running!\n")

    # Test 2: Simple selection
    print("[2/10] Testing simple node selection...")
    test_classify("select all female students", "Simple Node Selection")

    # Test 3: Color command
    print("[3/10] Testing color command...")
    test_classify("color the selected nodes red", "Color Selected Nodes")

    # Test 4: Multi-step command
    print("[4/10] Testing multi-step command...")
    test_classify("select grade 9 students and color them blue", "Multi-Step: Select + Color")

    # Test 5: Default sizing (all nodes)
    print("[5/10] Testing default sizing behavior...")
    test_classify("size nodes by GPA", "Size All Nodes (Default)")

    # Test 6: Explicit selected sizing
    print("[6/10] Testing explicit selected sizing...")
    test_classify("size selected nodes by GPA", "Size Selected Nodes Only")

    # Test 7: Voice error correction
    print("[7/10] Testing voice error correction...")
    test_classify("select the notes with blew caller", "Voice Error Correction")

    # Test 8: Move command
    print("[8/10] Testing move command...")
    test_classify("move selected nodes here", "Move Nodes")

    # Test 9: Deselect
    print("[9/10] Testing deselection...")
    test_classify("deselect all", "Deselect All")

    # Test 10: Complex multi-step
    print("[10/10] Testing complex multi-step command...")
    test_classify("select female students and size them by GPA and color them purple",
                  "Complex: Select + Size + Color")

    print("\n" + "=" * 60)
    print("Generating detailed markdown report...")
    print("=" * 60)

    # Generate markdown report
    markdown = generate_markdown_report()

    # Save to file
    output_file = "test_results_detailed.md"
    with open(output_file, "w", encoding="utf-8") as f:
        f.write(markdown)

    print(f"\n[SUCCESS] Report saved to: {output_file}")
    print(f"[INFO] Total Tests: {total_tests}")
    print(f"[INFO] Passed: {passed_tests}")
    print(f"[INFO] Failed: {failed_tests}")
    print(f"[INFO] Success Rate: {(passed_tests/total_tests*100) if total_tests > 0 else 0:.1f}%")
    print("\n" + "=" * 60)

if __name__ == "__main__":
    main()
