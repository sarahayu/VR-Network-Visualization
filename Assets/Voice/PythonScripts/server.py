from flask import Flask, request, jsonify
from openai import OpenAI
import os
import json


app = Flask(__name__)
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY").strip())

FEW_SHOT_PROMPT = FEW_SHOT_PROMPT = """
You are a graph command interpreter. For any given user input, classify it into:
- The matching command name
- A list of function calls
- A list of visualization tasks

Output your response as JSON in the following format:
{{
  "command": "...",
  "functions": ["..."],
  "tasks": ["..."]
}}

Here are some examples:

User: Highlight the node with the highest degree
{{
  "command": "Highlight Node",
  "functions": ["Node Query", "Node Highlight"],
  "tasks": ["Create active node set using query", "Highlight currently selected nodes"]
}}

User: Change the layout of the selected group to spherical
{{
  "command": "Change Layout Spherical",
  "functions": ["Change Layout Spherical"],
  "tasks": ["Apply spherical layout to selected communities"]
}}

User: Use spider layout for the current community
{{
  "command": "Change Layout Spider",
  "functions": ["Change Layout Spider"],
  "tasks": ["Apply spider layout to selected communities"]
}}


User: Put the group layout on the floor
{{
  "command": "Change Layout Floor",
  "functions": ["Change Layout floor"],
  "tasks": ["Apply floor layout to selected communities"]
}}



User: Select the 5 least well-connected nodes
{{
  "command": "Select Node",
  "functions": ["Node Query", "Node Select"],
  "tasks": ["Create active node set using query", "Select the currently active nodes"]
}}

User: Show me the 3 smallest groups
{{
  "command": "Highlight Group",
  "functions": ["Node Query", "Group Highlight"],
  "tasks": ["Create active node set using query", "Select containing group for each selected node in active set"]
}}

User: Create a new group containing the top 10 most bullied students
{{
  "command": "Group Nodes",
  "functions": ["Node Query", "Detach Nodes", "Group Nodes", "Layout \\"Force-Directed 2D Group\\""],
  "tasks": ["Create active node set using query", "Detach the nodes from their current group", "Regroup nodes into a new group", "Compute layout for new group and recompute other layouts based on LLM specs"]
}}

User: {user_input}
"""


@app.route('/classify', methods=['POST'])
def classify():
    try:
        data = request.get_json()
        recognized_text = data.get("userText", "")
        prompt = FEW_SHOT_PROMPT.format(user_input=recognized_text)

        response = client.chat.completions.create(
            model="gpt-4",
            messages=[
                {"role": "system", "content": "You are a helpful assistant that outputs JSON."},
                {"role": "user", "content": prompt}
            ],
            temperature=0,
            max_tokens=300
        )

        raw = response.choices[0].message.content.strip()
        try:
            structured = json.loads(raw)
            return jsonify(structured)
        except json.JSONDecodeError:
            print("Error: GPT response was not valid JSON:\n", raw)
            return jsonify({"error": "Invalid JSON response from GPT", "raw": raw}), 500

    except Exception as e:
        import traceback
        print("Error: ERROR during /classify call:")
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500


if __name__ == '__main__':
    app.run(host='127.0.0.1', port=5000, debug=True)
