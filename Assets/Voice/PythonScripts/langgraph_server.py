from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated
import asyncio
import json
import time

from langchain_openai import ChatOpenAI
from langchain_core.messages import SystemMessage, HumanMessage
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
    action_queue: Annotated[list[list[str]], override]   # Each element: ["actionName", "parameter"]
    timings: Annotated[dict[str, float], merge_dicts]
    clarify: Annotated[str, override]
    judgment: Annotated[str, override]


# ==========================================================
# Flask + LLM Setup
# ==========================================================

app = Flask(__name__)

# Standard LLM for most calls
llm = ChatOpenAI(model="gpt-4o-mini", temperature=0)

# JSON-mode LLM for the combined action+cypher call.
# response_format=json_object guarantees valid JSON output and avoids
# markdown wrapper stripping — also slightly faster since the model skips
# generating ```json fences.
llm_json = ChatOpenAI(
    model="gpt-4o-mini",
    temperature=0,
    model_kwargs={"response_format": {"type": "json_object"}},
)

# ==========================================================
# Six Selected Colors (hex) / this is temporary
# ==========================================================

ALLOWED_COLORS = {
    "red": "#FF0000",
    "orange": "#FFA500",
    "yellow": "#e4d00a",
    "green": "#3cb371",
    "blue": "#0000FF",
    "purple": "#800080",
    "cyan": "#00FFFF",
    "pink": "#FF69B4",
    "white": "#FFFFFF",
    "gray": "#808080",
    "grey": "#808080",
    "black": "#000000",
    "lime": "#00FF00",
    "magenta": "#FF00FF",
    "teal": "#008080",
    "navy": "#000080",
    "gold": "#FFD700",
    "brown": "#A52A2A",
    "violet": "#EE82EE",
    "steel blue": "#4682B4",
    "steelblue": "#4682B4",
}

# ==========================================================
# Prompt: Voice Error Correction  (static system msg → cache-friendly)
# ==========================================================

CORRECTION_SYSTEM = """You are correcting ASR (voice recognition) errors for graph visualization commands.

Rules:
- Fix ONLY misheard words (e.g., note→node, blew→blue, caller→color, great→grade, sacks→sex, gee pee ay→GPA, GP→GPA, geepay→GPA)
- NEVER convert color names to hex codes. Keep "red" as "red", "blue" as "blue", etc.
- NEVER change the word "color" — it is a verb meaning "to paint/color".
- DO NOT add or remove words. Only fix misheard ones.

Examples:
- "color nodes by grade" → "color nodes by grade"
- "color all nodes by grade" → "color all nodes by grade"
- "color aggression links in red" → "color aggression links in red"
- "color nodes by smoker" → "color nodes by smoker"
- "colour the notes blew" → "color the nodes blue"
- "select the notes with blew caller" → "select the nodes with blue color"
- "highlight top 3 notes by friendship" → "highlight top 3 nodes by friendship"
- "color their friendship links in blew" → "color their friendship links in blue"

Return ONLY the corrected text. Nothing else."""

# ==========================================================
# Prompt: Ambiguity Detection  (static system msg → cache-friendly)
# ==========================================================

AMBIGUITY_SYSTEM = """Determine if the user input is ambiguous.

Treat voice-misheard colors as NOT ambiguous because color correction will fix them.

Reply ONLY:
- "yes" → ambiguous
- "no" → clear"""

# ==========================================================
# Prompt: Clarification Question  (static system msg → cache-friendly)
# ==========================================================

CLARIFY_SYSTEM = """User request may be unclear.

Ask ONE short clarification question."""

# ==========================================================
# Prompt: Combined Action + Cypher  (one LLM call replaces two)
# ==========================================================
# Merging action_agent + cypher_agent into a single call eliminates one full
# sequential API round-trip (~0.7 s) while keeping the same output contract.
# The static rules go in the system message so OpenAI's prompt cache applies
# after the first few requests, shaving ~20-30 % off input-token processing.

ACTION_CYPHER_SYSTEM = """You are a graph visualization assistant. Given a user command, return a JSON object with exactly two keys:
- "actions": list of [actionName, param] pairs
- "queries": list of Cypher query strings, one per action ("" for actions that need no database query)

The arrays must be the same length and index-aligned.

════════════════════════════════════════════════════
ACTIONS
════════════════════════════════════════════════════

Allowed action names:
  selectNode, selectLink, colorNode, colorLink,
  colorByAttribute, colorByGPA, shapeByAttribute,
  sizeNode, move, layout, deselect, arithmetic

── COLOR NAME → HEX ──
When generating colorNode or colorLink, convert color names to hex:
  red→#FF0000, orange→#FFA500, yellow→#FFFF00, green→#00FF00,
  blue→#0000FF, purple→#800080, cyan→#00FFFF, pink→#FF69B4,
  white→#FFFFFF, gray/grey→#808080, black→#000000, magenta→#FF00FF,
  teal→#008080, navy→#000080, gold→#FFD700, brown→#A52A2A, violet→#EE82EE
Any hex color (e.g. #1565C0) may also be passed directly.

── GPA COLORING (highest priority) ──
ANY mention of "GPA" in a coloring context → ALWAYS use colorByGPA, never colorByAttribute.
  "color nodes by GPA" → [["colorByGPA","gpa"]]
  "visualize GPA"      → [["colorByGPA","gpa"]]

── "color BY attribute" vs "color IN a color" ──
  "color nodes by grade"      → [["colorByAttribute","grade"]]      (categorical)
  "color selected nodes red"  → [["colorNode","#FF0000"]]           (single color)
  "color aggression links red"→ [["selectLink","aggression"],["colorLink","#FF0000"]]

── LINK SELECTION ──
selectLink param = link type name ("aggression","friendship") or "all".
Append ":selected" to scope to currently selected nodes.
  "color all links gray"                  → [["selectLink","all"],["colorLink","#808080"]]
  "color aggression links of selected red"→ [["selectLink","aggression:selected"],["colorLink","#FF0000"]]

── NODE SELECTION ──
"top N nodes by metric" → always pair selectNode + colorNode.
  "highlight top 3 friendship nodes" → [["selectNode","top_3_friendship_degree"],["colorNode","#FF0000"]]
"selected nodes" / "highlighted nodes" = use current selection, do NOT add new selectNode.

── OTHER ──
  "shape nodes by <attr>"         → [["shapeByAttribute","<attr>"]]
  "size nodes by <attr>"          → [["sizeNode","<attr>:all"]]
  "size selected nodes by <attr>" → [["sizeNode","<attr>:selected"]]
  "deselect all"                  → [["deselect","all"]]

════════════════════════════════════════════════════
CYPHER QUERIES  (index-aligned with actions)
════════════════════════════════════════════════════

Use "" for: colorNode, colorLink, colorByGPA, move, layout, deselect, reset

── selectNode ──
  Simple condition: ["selectNode","n.sex = 'female'"]
    → MATCH (n:Node) WHERE n.sex = 'female' RETURN n
  Top N by degree: ["selectNode","top_3_friendship_degree"]
    → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type='friendship' WITH n,COUNT(r) AS d ORDER BY d DESC LIMIT 3 RETURN n
  Top N incoming: ["selectNode","top_3_incoming_aggression"]
    → MATCH (n:Node)<-[r:POINTS_TO]-(m) WHERE r.type='aggression' WITH n,COUNT(r) AS c ORDER BY c DESC LIMIT 3 RETURN n

── selectLink ──
  All:     ["selectLink","all"]          → MATCH (n:Node)-[r:POINTS_TO]-(m:Node) RETURN r
  By type: ["selectLink","aggression"]   → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type='aggression' RETURN r
  Scoped:  ["selectLink","aggression:selected"]
             → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE n.selected=true AND r.type='aggression' RETURN r

── colorByAttribute / shapeByAttribute ──
  ["colorByAttribute","grade"] → MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value
  ["shapeByAttribute","sex"]   → MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value

── sizeNode ──
  ["sizeNode","degree:all"]      → MATCH (n:Node) RETURN min(n.degree) AS minValue, max(n.degree) AS maxValue
  ["sizeNode","degree:selected"] → MATCH (n:Node) WHERE n.selected=true RETURN min(n.degree) AS minValue, max(n.degree) AS maxValue

── arithmetic ──
  → MATCH (n:Node) WHERE n.selected=true RETURN <expr>

════════════════════════════════════════════════════
Return ONLY a JSON object. No markdown. No explanation.
Example output:
{"actions":[["selectNode","n.grade=9"],["colorNode","#FF0000"]],"queries":["MATCH (n:Node) WHERE n.grade=9 RETURN n",""]}
════════════════════════════════════════════════════"""


# ==========================================================
# Agent Implementations
# ==========================================================

# Preprocess + General run in parallel — both only need the raw input text.
# Using explicit SystemMessage + HumanMessage so OpenAI's prompt cache can
# lock on the static system content across requests.
async def preprocess_general_agent(state: AgentState):
    original = state["input"]

    async def do_preprocess():
        t = time.time()
        r = await llm.ainvoke([SystemMessage(CORRECTION_SYSTEM), HumanMessage(original)])
        return r.content.strip(), time.time() - t

    async def do_general():
        t = time.time()
        r = await llm.ainvoke([SystemMessage(AMBIGUITY_SYSTEM), HumanMessage(original)])
        return r.content.strip(), time.time() - t

    (corrected, pre_t), (judgment, gen_t) = await asyncio.gather(
        do_preprocess(), do_general()
    )

    print_colored(f"preprocess_agent took {pre_t:.3f}s (parallel)", "cyan")
    print_colored(f"general_agent took {gen_t:.3f}s (parallel)", "cyan")

    return {
        "input": corrected,
        "original_input": original,
        "judgment": judgment.lower(),
        "timings": {"preprocess_agent": pre_t, "general_agent": gen_t},
    }


async def clarify_agent(state: AgentState):
    t = time.time()
    r = await llm.ainvoke([SystemMessage(CLARIFY_SYSTEM), HumanMessage(state["input"])])
    elapsed = time.time() - t
    print_colored(f"clarify_agent took {elapsed:.3f}s", "cyan")
    return {"clarify": r.content.strip(), "timings": {"clarify_agent": elapsed}}


# Combined action + cypher: one API call returns both arrays.
# Replaces the old sequential action_agent → cypher_agent chain.
async def action_cypher_agent(state: AgentState):
    t = time.time()
    r = await llm_json.ainvoke([
        SystemMessage(ACTION_CYPHER_SYSTEM),
        HumanMessage(state["input"]),
    ])
    elapsed = time.time() - t

    data = json.loads(r.content)
    actions = data.get("actions", [])
    queries = data.get("queries", [""] * len(actions))

    # Ensure arrays are same length
    while len(queries) < len(actions):
        queries.append("")

    # Normalize color names → hex for colorNode / colorLink
    import re as _re
    for a in actions:
        if a[0] in ("colorNode", "colorLink"):
            color_val = a[1].strip().lower()
            if color_val in ALLOWED_COLORS:
                a[1] = ALLOWED_COLORS[color_val]
            if not _re.fullmatch(r'#[0-9A-Fa-f]{6}', a[1].strip()):
                raise ValueError(f"Invalid color: {a[1]}")

    print_colored(f"action_cypher_agent took {elapsed:.3f}s", "cyan")
    return {
        "action_queue": actions,
        "code_list": queries,
        "timings": {"action_cypher_agent": elapsed},
    }


async def return_code(_: AgentState):
    return {"timings": {"return_code": 0.0}}


# ==========================================================
# Conditional Routing
# ==========================================================

def decide_clarify(state: AgentState):
    return "clarify_agent" if state["judgment"].startswith("yes") else "action_cypher"


# ==========================================================
# Graph Construction
# ==========================================================
#
#  preprocess_general ──[clarify?]──► clarify_agent   ──► return_code
#                                └──► action_cypher   ──► return_code
#
graph = StateGraph(state_schema=AgentState)

graph.add_node("preprocess_general", preprocess_general_agent)
graph.add_node("clarify_agent",      clarify_agent)
graph.add_node("action_cypher",      action_cypher_agent)
graph.add_node("return_code",        return_code)

graph.set_entry_point("preprocess_general")

graph.add_conditional_edges(
    "preprocess_general",
    decide_clarify,
    {"clarify_agent": "clarify_agent", "action_cypher": "action_cypher"},
)

graph.add_edge("clarify_agent", "return_code")
graph.add_edge("action_cypher", "return_code")

langgraph_app = graph.compile()


# ==========================================================
# Flask API — /classify
# ==========================================================

@app.route("/classify", methods=["POST"])
def classify():
    try:
        data       = request.get_json(force=True)
        user_input = data.get("userText", "")

        state = {
            "input":          user_input,
            "original_input": user_input,
            "code_list":      [],
            "action_queue":   [],
            "timings":        {},
            "clarify":        "",
            "judgment":       "",
        }

        result = asyncio.run(langgraph_app.ainvoke(state))

        print_colored(f"{result}", "green")

        return jsonify({
            "input":           result.get("input", ""),
            "original_input":  result.get("original_input", ""),
            "corrected_input": result.get("input", ""),
            "queries":         result.get("code_list", []),
            "actions":         result.get("action_queue", []),
            "clarify":         result.get("clarify", ""),
            "timings":         result.get("timings", {}),
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
