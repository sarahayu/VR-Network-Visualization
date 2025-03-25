from flask import Flask, request, jsonify
from openai import OpenAI
import os

app = Flask(__name__)

# Initialize OpenAI client
# client = OpenAI()  # This reads OPENAI_API_KEY from environment

client = OpenAI(api_key=os.getenv("OPENAI_API_KEY").strip())

# Few-shot prompt
FEW_SHOT_PROMPT = """
You are a command classifier for a visualization system. Based on the user's input, return the exact name of the command that best matches.

Available commands:
- Highlight Node
- Select Node
- Highlight Group
- Group Nodes
- Change layout
- Make Work Surface
- Project Nodes

Examples:
User: Highlight the node with the highest degree
Command: Highlight Node

User: Select the 5 least well-connected nodes
Command: Select Node

User: Show me the 3 smallest groups
Command: Highlight Group

User: Create a new group containing the top 10 most bullied students
Command: Group Nodes

User: Change to a 3D layout
Command: Change layout

User: Make a new surface for me
Command: Make Work Surface

User: Place all the nodes in the smallest group onto my work surface
Command: Project Nodes

User: {user_input}
Command:
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
                {"role": "system", "content": "You classify visualization commands."},
                {"role": "user", "content": prompt}
            ],
            temperature=0,
            max_tokens=10
        )

        command = response.choices[0].message.content.strip()
        return jsonify({"command": command})

    except Exception as e:
        import traceback
        print("🔥 ERROR during /classify call:")
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500


if __name__ == '__main__':
    app.run(host='127.0.0.1', port=5000, debug=True)
