from flask import Flask, request, jsonify
from openai import OpenAI
import os
import json
from examples import EXAMPLES

app = Flask(__name__)
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY").strip())


@app.route('/classify', methods=['POST'])
def classify():
    try:
        data = request.get_json()
        user_input = data.get("userText", "")

        messages = [
            {
                "role": "system",
                "content": "You are a command parser for a voice-based VR network visualization system.\
                            When a user gives a natural language instruction, your job is to convert it into structured task units that include:\
                            Task: one of \"Select\", \"Layout\", \"Surface\", \"Move\", \"Create Group\", etc.\
                            Attribute: the property you're targeting (e.g., \"Degree\", \"Color\", \"Most Bullied\", \"Smallest Group\").\
                            Number: how many to take or rank (e.g., \"5\", \"10\", \"1\"). Leave it empty if not needed.\
                            Sequence: \"0\" for first step, \"1\" for second step (if there is a sequence like select → move).\
                            If the command has multiple steps, return an array of steps, each with increasing \"Sequence\" values."
            }
        ]

        for ex in EXAMPLES:
            messages.append({"role": "user", "content": ex["user"]})
            messages.append({"role": "assistant", "content": json.dumps(ex["assistant"])})

        messages.append({"role": "user", "content": user_input})

        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=messages
        )

        raw = response.choices[0].message.content.strip()

        try:
            structured = json.loads(raw)
            return jsonify(structured)
        except json.JSONDecodeError:
            print("Error: GPT response was not valid JSON:\n", raw)
            return jsonify({"Error": "Invalid JSON response from GPT", "raw": raw}), 500

    except Exception as e:
        import traceback
        print("Error during /classify call:")
        traceback.print_exc()
        return jsonify({"Error": str(e)}), 500


if __name__ == '__main__':
    app.run(host='127.0.0.1', port=5000, debug=True)
