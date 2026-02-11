from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated
import time
import asyncio
import json

from langchain_openai import ChatOpenAI
from langchain_core.prompts import ChatPromptTemplate
from utils import print_colored


# ==========================================================
# Utility Functions for State Updates
# ==========================================================

def override(_: str | None, new: str) -> str:
    return new

def merge_dicts(old: dict[str, float] | None, new: dict[str, float]) -> dict[str, float]:
    return {**(old or {}), **new}


# ==========================================================
# LangGraph Shared State Definition
# ==========================================================

class AgentState(TypedDict):
    input: Annotated[str, override]
    original_input: Annotated[str, override]
    code_list: Annotated[list[str], override]
    action_queue: Annotated[list[list[str]], override]   # Each element would be like: ["actionName", "parameter"]
    timings: Annotated[dict[str, float], merge_dicts]
    clarify: Annotated[str, override]
    judgment: Annotated[str, override]


# ==========================================================
# Flask + LLM Setup
# ==========================================================

app = Flask(__name__)
llm = ChatOpenAI(model="gpt-4o-mini", temperature=0)

# ==========================================================
# Six Selected Colors (hex) / this is temporary
# ==========================================================

ALLOWED_COLORS = {
    "red": "#FF0000",
    "orange": "#FFA500",
    "yellow": "#FFFF00",
    "green": "#00FF00",
    "blue": "#0000FF",
    "purple": "#800080"
}

# ==========================================================
# Prompt: Voice Error Correction
# ==========================================================

correction_prompt = ChatPromptTemplate.from_template(
"""
You are correcting ASR (voice recognition) errors for graph visualization commands.

Tasks:
- Fix voice mishearing (e.g., note→node, blew→blue, rate→red, caller→color, colour→color)
- Normalize synonyms: colour/caller→color, resize/scale→size
- Fix misheard node/link attribute names (e.g., "great"→"grade", "sacks"→"sex", "GBA"→"GPA")
- Fix misheard link types (e.g., "aggression", "friendship")
- DO NOT add meaning. Fix only recognition errors.

IMPORTANT:
- Do NOT convert color names to hex codes. Keep color names as words (red, blue, purple, etc.).
- The word "color" is often a VERB (meaning "to color/paint"). Do NOT replace it with a hex code.
  Example: "color nodes by grade" → keep as "color nodes by grade"
  Example: "color all aggression links in red" → keep as "color all aggression links in red"
- Only fix words that are clearly misheard by voice recognition.

Return ONLY the corrected text, no explanations.

User said: {input}
"""
)

# ==========================================================
# Prompt: Ambiguity Detection
# ==========================================================

ambiguity_prompt = ChatPromptTemplate.from_template(
"""
Determine if the user input is ambiguous.

Treat voice-misheard colors as NOT ambiguous because color correction will fix them.

Reply ONLY:
- "yes" → ambiguous
- "no" → clear

User input: {input}
"""
)

# ==========================================================
# Prompt: Clarification Question
# ==========================================================

clarify_prompt = ChatPromptTemplate.from_template(
"""
User request may be unclear.

Ask ONE short clarification question.

User input: {input}
"""
)

# ==========================================================
# Prompt: Action Planner
# ==========================================================

action_prompt = ChatPromptTemplate.from_template(
"""
You are generating a pipeline of graph visualization actions.

Return ONLY a valid JSON list of actions.
Each action must be a 2-element list: ["actionName", "param"]

Allowed actions:
- "selectNode" - select nodes by condition
- "selectLink" - select links by condition
- "colorNode" - color currently selected nodes a single color
- "colorLink" - color currently selected links a single color
- "colorByAttribute" - categorical coloring: assign different colors per unique attribute value
- "shapeByAttribute" - categorical shape: assign different shapes per unique attribute value
- "sizeNode" - size nodes by numeric attribute
- "move" - move selected nodes
- "layout" - change layout
- "deselect" - clear selection
- "arithmetic" - perform calculation

=== COLOR NAME TO HEX MAPPING ===
When generating colorNode or colorLink actions, convert color names to hex:
  red → #FF0000, orange → #FFA500, yellow → #FFFF00,
  green → #00FF00, blue → #0000FF, purple → #800080

=== CRITICAL: "color BY attribute" vs "color IN a color" ===
These are COMPLETELY DIFFERENT operations:

1. "color nodes by <attribute>" / "color by <attribute>" → CATEGORICAL coloring
   Generate: [["colorByAttribute", "<attribute>"]]
   Examples:
     "color nodes by grade" → [["colorByAttribute", "grade"]]
     "color by sex" → [["colorByAttribute", "sex"]]
     "color all nodes by smoker" → [["colorByAttribute", "smoker"]]
     "color nodes by gender" → [["colorByAttribute", "sex"]]

2. "color nodes <color>" / "color X links in <color>" → SINGLE COLOR
   Generate: [["selectNode/selectLink", "..."], ["colorNode/colorLink", "#HEX"]]
   Examples:
     "color selected nodes red" → [["colorNode", "#FF0000"]]
     "color all aggression links in red" → [["selectLink", "aggression"], ["colorLink", "#FF0000"]]

The KEY difference: "by <attribute>" = categorical, "in <color>" or just "<color>" = single color.

=== LINK SELECTION ===
- "color <type> links" → select links by type, then color:
  [["selectLink", "<type>"], ["colorLink", "#HEX"]]
- "color <type> links for selected/highlighted nodes" → select links scoped to selected nodes:
  [["selectLink", "<type>:selected"], ["colorLink", "#HEX"]]
- "their <type> links" or "the <type> links for those nodes" also means scoped to selected nodes.

=== NODE SELECTION ===
- "top N nodes by <metric>" → ["selectNode", "top_N_by_<metric>"]
  Examples:
    "highlight top 3 nodes with most friendship links" → [["selectNode", "top_3_friendship_degree"], ["colorNode", "#FF0000"]]
    "select nodes with most incoming aggression" → [["selectNode", "top_3_incoming_aggression"], ["colorNode", "#FF0000"]]
- If user describes a group ("female", "grade 9"), create: ["selectNode", "n.sex = 'female'"]
- "selected nodes" / "highlighted nodes" = use current selection, do NOT create a new selectNode

=== CATEGORICAL SHAPE ===
- "shape nodes by <attribute>" → [["shapeByAttribute", "<attribute>"]]

=== SIZING ===
- "size nodes by <attr>" → ["sizeNode", "<attr>:all"]
- "size selected nodes by <attr>" → ["sizeNode", "<attr>:selected"]

=== ACTION ORDER ===
Actions execute sequentially. Later actions operate on state from earlier ones.

Return ONLY JSON. No explanations.

User request: {input}
"""
)

# ==========================================================
# Prompt: Cypher Generator
# ==========================================================

cypher_prompt = ChatPromptTemplate.from_template(
"""
You are a Neo4j Cypher expert. Convert the action into a Cypher query.

Database schema:
- Nodes have label :Node with properties like sex, grade, smoker, drinker, gpa, selected (boolean)
- Links are relationship :POINTS_TO with properties: type (e.g., "friendship", "aggression"), selected

Rules for each action type:

=== selectNode ===
- Simple condition: ["selectNode", "n.sex = 'female'"]
  → MATCH (n:Node) WHERE n.sex = 'female' RETURN n

- Top N by degree: ["selectNode", "top_3_friendship_degree"]
  → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'friendship' WITH n, COUNT(r) AS degree ORDER BY degree DESC LIMIT 3 RETURN n

- Top N by incoming: ["selectNode", "top_3_incoming_aggression"]
  → MATCH (n:Node)<-[r:POINTS_TO]-(m) WHERE r.type = 'aggression' WITH n, COUNT(r) AS cnt ORDER BY cnt DESC LIMIT 3 RETURN n

- If the param contains "top" and a number, ALWAYS use ORDER BY ... DESC LIMIT N pattern.

=== selectLink ===
- By type only: ["selectLink", "aggression"]
  → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type = 'aggression' RETURN r

- By type scoped to selected nodes: ["selectLink", "aggression:selected"]
  → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected = true AND r.type = 'aggression' RETURN r

- By type scoped to selected: ["selectLink", "friendship:selected"]
  → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected = true AND r.type = 'friendship' RETURN r

- IMPORTANT: If param contains ":selected", add WHERE n.selected = true to scope to currently selected nodes.

=== colorNode / colorLink ===
→ ""  (no Cypher needed, Unity handles the coloring)

=== colorByAttribute ===
["colorByAttribute", "<attribute>"]
→ MATCH (n:Node) RETURN DISTINCT n.<attribute> AS value ORDER BY value

=== shapeByAttribute ===
["shapeByAttribute", "<attribute>"]
→ MATCH (n:Node) RETURN DISTINCT n.<attribute> AS value ORDER BY value

=== sizeNode ===
["sizeNode", "<attribute>:<scope>"]
If scope is "selected":
  → MATCH (n:Node) WHERE n.selected = true RETURN min(n.<attribute>) AS minValue, max(n.<attribute>) AS maxValue
If scope is "all":
  → MATCH (n:Node) RETURN min(n.<attribute>) AS minValue, max(n.<attribute>) AS maxValue

=== move / layout / deselect ===
→ ""  (Unity handles these)

=== arithmetic ===
→ MATCH (n:Node) WHERE n.selected = true RETURN <expr>

Return ONLY the cypher string. No code block markers. No explanations.
Action: {input}
"""
)


# ==========================================================
# Timing Decorator
# ==========================================================

def timed_node(name):
    def wrapper(fn):
        async def inner(state: AgentState):
            start = time.time()
            result = await fn(state)
            end = time.time()
            duration = end - start

            state["timings"][name] = duration
            result["timings"] = state["timings"]

            print_colored(f"{name} took {duration:.3f}s", "cyan")
            return result
        return inner
    return wrapper


# ==========================================================
# Agent Implementations
# ==========================================================

# Record Time

@timed_node("preprocess_agent")
async def preprocess_agent(state: AgentState):
    original = state["input"]
    msg = correction_prompt.format_messages(input=original)
    corrected = (await llm.ainvoke(msg)).content.strip()

    return {"input": corrected, "original_input": original}


@timed_node("general_agent")
async def general_agent(state: AgentState):
    msg = ambiguity_prompt.format_messages(input=state["input"])
    judgment = (await llm.ainvoke(msg)).content.strip().lower()
    return {"judgment": judgment}


@timed_node("clarify_agent")
async def clarify_agent(state: AgentState):
    msg = clarify_prompt.format_messages(input=state["input"])
    question = (await llm.ainvoke(msg)).content.strip()
    return {"clarify": question}


@timed_node("action_agent")
async def action_agent(state: AgentState):
    msg = action_prompt.format_messages(input=state["input"])
    raw = (await llm.ainvoke(msg)).content.strip()

    try:
        actions = json.loads(raw)
        if not isinstance(actions, list):
            raise ValueError
        for a in actions:
            if not isinstance(a, list) or len(a) != 2:
                raise ValueError
            # Normalize color: if LLM returned a color name instead of hex, convert it
            if a[0] in ["colorNode", "colorLink"]:
                color_val = a[1].strip().lower()
                if color_val in ALLOWED_COLORS:
                    a[1] = ALLOWED_COLORS[color_val]
                color = a[1].upper()
                if color not in [v.upper() for v in ALLOWED_COLORS.values()]:
                    raise ValueError(f"Invalid color: {a[1]}. Must be one of {list(ALLOWED_COLORS.values())}")
    except Exception as e:
        raise ValueError(f"Action JSON invalid: {raw}. Error: {str(e)}")

    return {"action_queue": actions}


@timed_node("cypher_agent")
async def cypher_agent(state: AgentState):
    code_list = []
    actions = state["action_queue"]

    for action_name, param in actions:
        formatted = cypher_prompt.format_messages(input=f'["{action_name}", "{param}"]')
        cypher = (await llm.ainvoke(formatted)).content.strip()
        code_list.append(cypher)

    return {"code_list": code_list}


@timed_node("return_code")
async def return_code(state: AgentState):
    return state


# ==========================================================
# Conditional Logic
# ==========================================================

def decide_clarify(state: AgentState):
    return "clarify_agent" if state["judgment"].startswith("yes") else "action_agent"


# ==========================================================
# Graph Construction
# ==========================================================

graph = StateGraph(state_schema=AgentState)

graph.add_node("preprocess_agent", preprocess_agent)
graph.add_node("general_agent", general_agent)
graph.add_node("clarify_agent", clarify_agent)
graph.add_node("action_agent", action_agent)
graph.add_node("cypher_agent", cypher_agent)
graph.add_node("return_code", return_code)

graph.set_entry_point("preprocess_agent")
graph.add_edge("preprocess_agent", "general_agent")

graph.add_conditional_edges(
    "general_agent",
    decide_clarify,
    {
        "clarify_agent": "clarify_agent",
        "action_agent": "action_agent"
    }
)

graph.add_edge("action_agent", "cypher_agent")
graph.add_edge("cypher_agent", "return_code")
graph.add_edge("clarify_agent", "return_code")

langgraph_app = graph.compile()


# ==========================================================
# Flask API — /classify
# ==========================================================

@app.route("/classify", methods=["POST"])
def classify():
    try:
        data = request.get_json(force=True)
        user_input = data.get("userText", "")

        state = {
            "input": user_input,
            "original_input": user_input,
            "code_list": [],
            "action_queue": [],
            "timings": {},
            "clarify": "",
            "judgment": ""
        }

        result = asyncio.run(langgraph_app.ainvoke(state))

        print_colored(f"{result}", "green")

        return jsonify({
            "input": result.get("input", ""),
            "original_input": result.get("original_input", ""),
            "corrected_input": result.get("input", ""),
            "queries": result.get("code_list", []),
            "actions": result.get("action_queue", []),
            "clarify": result.get("clarify", ""),
            "timings": result.get("timings", {})
        })

    except Exception as e:
        print_colored(f"SERVER ERROR: {e}", "red")
        return jsonify({"error": str(e)}), 500


@app.route("/ping", methods=["GET"])
def ping():
    return "Server alive!", 200


# ==========================================================
# Run the server! :)
# ==========================================================

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
