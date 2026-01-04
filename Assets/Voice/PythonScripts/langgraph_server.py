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
You are correcting ASR (voice recognition) errors for graph commands.

Tasks:
- Fix voice mishearing (e.g., note→node, blew→blue, rate→red, coral→color)
- Normalize any color into these EXACT hex values:
  red=#FF0000, orange=#FFA500, yellow=#FFFF00,
  green=#00FF00, blue=#0000FF, purple=#800080
- Normalize synonyms of "color" (colour, caller, etc.)
- Normalize synonyms of "size" (resize, scale)
- Normalize references to nodes/links
- DO NOT add meaning. Fix only recognition errors.

Return ONLY corrected text, no explanations.

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
You are generating a pipeline of graph actions.

Return ONLY a valid JSON list of actions.
Each action must be a 2-element list: ["actionName", "param"]

Allowed actions:
- "selectNode"
- "selectLink"
- "colorNode"
- "colorLink"
- "colorByAttribute"
- "shapeByAttribute"
- "sizeNode"
- "move"
- "layout"
- "deselect"
- "arithmetic"

Color rules:
- Must output ONLY these hex values:
  #FF0000, #FFA500, #FFFF00, #00FF00, #0000FF, #800080
- If user says "color X nodes blue", generate: [["selectNode", "..."], ["colorNode", "#0000FF"]]
- If user says "color selected nodes", generate: [["colorNode", "#0000FF"]] (apply to current selection)
- If user only says "color blue" without specifying, apply to currently selected nodes

Categorical coloring rules:
- CRITICAL: "color BY attribute" means categorical coloring (assign different colors per category)
- If user says "color nodes by <attribute>" or "color by <attribute>", generate: [["colorByAttribute", "<attribute>"]]
- This is DIFFERENT from:
  * "size by" which is sizing (use sizeNode)
  * "color nodes <color>" which is single color (use colorNode)
- Examples:
  * "color nodes by grade" → ["colorByAttribute", "grade"] (categorical - different color per grade)
  * "color by sex" → ["colorByAttribute", "sex"] (categorical)
  * "categorically color by department" → ["colorByAttribute", "department"]
- This assigns a unique color from the palette to each distinct value of the attribute

Categorical shape encoding rules:
- CRITICAL: "shape BY attribute" means categorical shape encoding (assign different shapes per category)
- If user says "shape nodes by <attribute>" or "change shape by <attribute>", generate: [["shapeByAttribute", "<attribute>"]]
- Available shapes: sphere, cube, tetrahedron (3 shapes for 3 categories maximum)
- Examples:
  * "shape nodes by grade" → ["shapeByAttribute", "grade"] (categorical - different shape per grade)
  * "change shape by sex" → ["shapeByAttribute", "sex"] (categorical)
  * "make shapes different by department" → ["shapeByAttribute", "department"]
- This assigns a unique shape (sphere/cube/tetrahedron) to each distinct value of the attribute

Sizing rules:
- sizing command should output ["sizeNode", "<attributeName>:<scope>"]
- <scope> can be "all" or "selected"
- DEFAULT: If user does NOT mention "selected", use "all"
- Examples:
  * "size nodes by gpa" → ["sizeNode", "gpa:all"]
  * "size selected nodes by gpa" → ["sizeNode", "gpa:selected"]

Selection defaults:
- If user describes a group ("female", "smoker", "grade 9"), create a selectNode step:
    ["selectNode", "n.sex = 'female'"]
- "selected nodes" means use current selection, do NOT create a new selectNode action

Action execution order:
- Actions execute sequentially in the order you specify
- Later actions operate on the state created by earlier actions
- Example: selectNode → colorNode means "select these nodes, then color the selected ones"

Return ONLY JSON. Example:

[
  ["selectNode", "n.sex = 'female'"],
  ["colorNode", "#FF0000"]
]

User request: {input}
"""
)

# ==========================================================
# Prompt: Cypher Generator
# ==========================================================

cypher_prompt = ChatPromptTemplate.from_template(
"""
You are a Neo4j Cypher expert. Convert the action into a Cypher query.

For:
- ["selectNode", "<condition>"]
    → MATCH (n:Node) WHERE <condition> RETURN n

- ["selectLink", "<condition>"]
    → MATCH ()-[l:POINTS_TO]->() WHERE <condition> RETURN l

- ["colorNode", "<hex>"]
    → RETURN ""  (Unity handles the color; no Cypher required)

- ["colorLink", "<hex>"]
    → RETURN ""  (Unity handles color)

- ["colorByAttribute", "<attribute>"]
    → MATCH (n:Node) RETURN DISTINCT n.<attribute> AS value ORDER BY value
    Example: ["colorByAttribute", "grade"]
    → MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value

- ["shapeByAttribute", "<attribute>"]
    → MATCH (n:Node) RETURN DISTINCT n.<attribute> AS value ORDER BY value
    Example: ["shapeByAttribute", "sex"]
    → MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value

- ["move", "<param>"]
    → RETURN ""  (Unity handles layout)

- ["layout", "<type>"]
    → RETURN ""

- ["deselect", ""]
    → RETURN ""  (Unity handles deselection)

- ["sizeNode", "<attribute>:<scope>"]
    Parse the parameter as "attribute:scope"
    If scope is "selected":
        MATCH (n:Node) WHERE n.selected = true RETURN min(n.<attribute>) AS minValue, max(n.<attribute>) AS maxValue
    If scope is "all" (DEFAULT):
        MATCH (n:Node) RETURN min(n.<attribute>) AS minValue, max(n.<attribute>) AS maxValue

- ["arithmetic", "<expr>"]
    Example: degree, min, max:
    MATCH (n:Node) WHERE n.selected = true RETURN <expr>

Return ONLY cypher string. No code block.
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
            # Validate color hex codes
            if a[0] in ["colorNode", "colorLink"]:
                color = a[1].upper()
                if color not in ALLOWED_COLORS.values():
                    raise ValueError(f"Invalid color hex: {a[1]}. Must be one of {list(ALLOWED_COLORS.values())}")
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
