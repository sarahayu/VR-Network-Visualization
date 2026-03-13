"""
Test script for the LangGraph classification server
Run this after starting langgraph_server.py
"""

import requests
import json

# Server URL
SERVER_URL = "http://localhost:5000"

def test_ping():
    """Test if server is alive"""
    print("\n=== Testing /ping endpoint ===")
    try:
        response = requests.get(f"{SERVER_URL}/ping")
        print(f"Status: {response.status_code}")
        print(f"Response: {response.text}")
        return response.status_code == 200
    except Exception as e:
        print(f"Error: {e}")
        return False

def test_classify(user_input):
    """Test the /classify endpoint with a user input"""
    print(f"\n=== Testing /classify with: '{user_input}' ===")
    try:
        response = requests.post(
            f"{SERVER_URL}/classify",
            json={"userText": user_input},
            headers={"Content-Type": "application/json"}
        )

        print(f"Status: {response.status_code}")

        if response.status_code == 200:
            result = response.json()
            print("\n--- Response ---")
            print(f"Original Input: {result.get('original_input', '')}")
            print(f"Corrected Input: {result.get('corrected_input', '')}")
            print(f"Actions: {json.dumps(result.get('actions', []), indent=2)}")
            print(f"Queries: {json.dumps(result.get('queries', []), indent=2)}")
            print(f"Clarify: {result.get('clarify', '')}")

            if result.get('timings'):
                print("\n--- Timings ---")
                for agent, duration in result['timings'].items():
                    print(f"  {agent}: {duration:.3f}s")

            return True
        else:
            print(f"Error: {response.text}")
            return False

    except Exception as e:
        print(f"Error: {e}")
        return False

def main():
    print("=" * 60)
    print("LangGraph Server Test Suite")
    print("=" * 60)

    # Test 1: Server health check
    if not test_ping():
        print("\n[ERROR] Server is not running. Start it with: python langgraph_server.py")
        return

    print("\n[SUCCESS] Server is running!")

    # Test 2: Simple selection command
    test_classify("select all female students")

    # Test 3: Color command
    test_classify("color the selected nodes red")

    # Test 4: Selection + Color command
    test_classify("select grade 9 students and color them blue")

    # Test 5: Size command
    test_classify("size nodes by GPA")

    # Test 6: Voice error correction test
    test_classify("select the notes with blew caller")  # should correct "notes" to "nodes", "blew" to "blue", "caller" to "color"

    # Test 7: Layout command
    test_classify("move selected nodes here")

    # Test 8: Deselect command
    test_classify("deselect all")

    # Test 9: Ambiguous command (should trigger clarification)
    test_classify("make it bigger")

    print("\n" + "=" * 60)
    print("Test suite completed!")
    print("=" * 60)

if __name__ == "__main__":
    main()
