"""
Sends demo commands to Unity via the external command listener (port 5001).
Unity receives each command, forwards it to the LangGraph server,
then executes the returned actions in the scene.

Usage:
    python send_demo_commands.py

Requires:
    - Unity scene running with KeyboardCommandTester (listener on port 5001)
    - LangGraph server running on port 5000
"""

import requests
import time
import sys

UNITY_URL = "http://localhost:5001"

# ANSI colors
GREEN = "\033[92m"
RED = "\033[91m"
CYAN = "\033[96m"
BOLD = "\033[1m"
RESET = "\033[0m"

# The 8 demo commands matching the 3 demos (P, L, O)
DEMO_COMMANDS = [
    # Demo P: Friendship highlight demo
    {"demo": "P", "step": 1, "command": "Highlight the top 3 nodes that have the most friendship links"},
    {"demo": "P", "step": 2, "command": "Color the nodes by grade"},
    {"demo": "P", "step": 3, "command": "Color their aggression links for highlighted nodes"},

    # Demo L: Smoker/drinker + aggression demo
    {"demo": "L", "step": 1, "command": "Color nodes by smoker or drinker"},
    {"demo": "L", "step": 2, "command": "Color their aggression links in red"},

    # Demo O: Gender + aggression targets + friendship links
    {"demo": "O", "step": 1, "command": "Color nodes by gender"},
    {"demo": "O", "step": 2, "command": "Select the nodes with many incoming aggression links"},
    {"demo": "O", "step": 3, "command": "Color their friendship links in blue"},
]


def check_unity():
    """Check if Unity's command listener is running."""
    try:
        r = requests.get(f"{UNITY_URL}/ping", timeout=5)
        return r.status_code == 200
    except Exception:
        return False


def send_command(text):
    """Send a command to Unity's listener."""
    try:
        r = requests.post(
            f"{UNITY_URL}/command",
            json={"userText": text},
            headers={"Content-Type": "application/json"},
            timeout=10,
        )
        return r.status_code == 200, r.text
    except Exception as e:
        return False, str(e)


def main():
    print(f"{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Send Demo Commands to Unity{RESET}")
    print(f"{BOLD}{'='*60}{RESET}")
    print(f"Unity listener: {UNITY_URL}")

    # Check Unity listener
    print("\nChecking Unity listener...", end=" ")
    if not check_unity():
        print(f"{RED}NOT RUNNING{RESET}")
        print("Make sure Unity scene is running with KeyboardCommandTester.")
        print("The listener should be on port 5001.")
        sys.exit(1)
    print(f"{GREEN}OK{RESET}")

    # Parse optional args for which demo to run
    demos_to_run = None
    if len(sys.argv) > 1:
        demos_to_run = [a.upper() for a in sys.argv[1:]]
        print(f"Running demos: {', '.join(demos_to_run)}")

    # Send commands with pause between each
    wait_seconds = 5  # Wait between commands for Unity to process
    current_demo = None

    for entry in DEMO_COMMANDS:
        demo = entry["demo"]
        step = entry["step"]
        command = entry["command"]

        # Skip if not in selected demos
        if demos_to_run and demo not in demos_to_run:
            continue

        # Print demo header
        if demo != current_demo:
            current_demo = demo
            print(f"\n{BOLD}--- Demo {demo} ---{RESET}")

        print(f"\n  {CYAN}[Step {step}]{RESET} {command}")
        ok, response = send_command(command)

        if ok:
            print(f"  {GREEN}Sent to Unity{RESET}")
        else:
            print(f"  {RED}Failed: {response}{RESET}")
            continue

        # Wait for Unity to process (server call + action execution)
        if step < max(e["step"] for e in DEMO_COMMANDS if e["demo"] == demo):
            print(f"  Waiting {wait_seconds}s for Unity to process...")
            time.sleep(wait_seconds)

    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{GREEN}All commands sent! Check Unity for results.{RESET}")
    print(f"{'='*60}")

    # Wait for last command
    input("\nPress Enter after verifying results in Unity...")


if __name__ == "__main__":
    main()
