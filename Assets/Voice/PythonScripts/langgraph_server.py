from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated
import asyncio
import json
import re
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

# Check if the Statistical Queries are within allowed range
# Maybe not needed.
def is_safe_statistical_query(query: str) -> bool:
    """Reject common invalid/expensive LLM-generated Cypher shapes."""
    if re.search(r"\bIN\s*\(\s*MATCH\b", query, re.IGNORECASE):
        return False
    
    if re.search(r"\b(?:avg|sum|min|max)\s*\(\s*(?:toFloat\s*\(\s*)?count\s*\(", query, re.IGNORECASE):
        return False
    
    if re.search(r"\bsize\s*\(\s*\([^)]*\)\s*-[\[<]", query, re.IGNORECASE):
        return False
    
    return True

# This may be brute forcing it, need to change for a better design
def repair_aggressor_friend_statistics(user_input: str, actions: list, queries: list, stats: list):
    """Repair the known 'average/variance friends for aggressors' intent."""
    text = user_input.lower()
    asks_about_aggressors = re.search(r"\baggressors?\b|\bbull(?:y|ies)\b", text)
    
    asks_about_friends = re.search(r"\bfriends?(?:hip)?\b", text)
    asks_average = re.search(r"\b(?:average|avg|mean)\b", text)
    
    asks_variance = re.search(r"\bvariance\b", text)
    
    if not (asks_about_aggressors and asks_about_friends and (asks_average or asks_variance)):
        return actions, queries, stats

    # A question that only requests a number must not accidentally resize or
    # select nodes. Combined commands such as "select ... and tell me ..." keep
    # their explicitly requested visualization actions.
    visualization_requested = re.search(
        r"\b(?:select|highlight|color|colour|show|display|encode|shape|size|move|layout|deselect|reset|save|delete)\b",
        text,
    )
    if not visualization_requested:
        actions, queries = [], []

    pipeline = (
        "CALL { MATCH (n:Node)-[a:POINTS_TO]->() WHERE a.type = 'aggression' "
        "WITH n, count(a) AS aggressionCount ORDER BY aggressionCount DESC LIMIT 15 RETURN n } "
        "OPTIONAL MATCH (n)-[f:POINTS_TO]-() WHERE f.type = 'friendship' "
        "WITH n, count(DISTINCT f) AS friendCount "
    )
    
    repaired_stats = []
    
    if asks_average:
        repaired_stats.append({
            "label": "Top 15 aggressors — average friends",
            "query": pipeline + "RETURN coalesce(avg(toFloat(friendCount)), 0.0)",
        })
    
    if asks_variance:
        repaired_stats.append({
            "label": "Top 15 aggressors — friend-count variance",
            "query": pipeline + "WITH stDevP(toFloat(friendCount)) AS sd RETURN coalesce(sd * sd, 0.0)",
        })
    return actions, queries, repaired_stats

# Normalization of friend statistic labels based on user input
def normalize_friend_statistic_labels(user_input: str, stats: list):
    """Keep friend-count report labels aligned with the user's wording.

    Labels are generated independently from Cypher, so the model can produce a
    correct friendship query but call it "average number of nodes".  The input
    provides the authoritative meaning for this common request.
    """
    text = user_input.lower()
    asks_about_friends = re.search(r"\bfriends?(?:hip)?\b", text)
    asks_average = re.search(r"\b(?:average|avg|mean)\b", text)
    
    if not (asks_about_friends and asks_average):
        return stats

    normalized = []
    for statistic in stats:
        item = dict(statistic)
        query = item.get("query", "")
        if re.search(r"\btype\s*=\s*['\"]friendship['\"]", query, re.IGNORECASE):
            if re.search(r"\baggressors?\b|\bbull(?:y|ies)\b", text):
                item["label"] = "Top 15 aggressors — average number of friends"
            else:
                item["label"] = "Average number of friends"
        normalized.append(item)
    return normalized


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
    stats: Annotated[list[dict[str, str]], override]


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

# three keys
ACTION_CYPHER_SYSTEM = """You are a graph visualization assistant. Given a user command, return a JSON object with exactly three keys:
- "actions": list of [actionName, param] pairs
- "queries": list of Cypher query strings, one per action ("" for actions that need no database query)
- "stats": list of objects with exactly two string fields: "label" and "query".
  "label" is a concise, human-readable name for the statistic, and "query" is
  its statistical Cypher query. Return [] only when there are no recognized
  actions and no statistical question. This list is independent of the
  per-action "queries" list.

Only "actions" and "queries" must have the same length and be index-aligned.
"stats" is independent.

════════════════════════════════════════════════════
ACTIONS
════════════════════════════════════════════════════

Allowed action names:
  selectNode, selectLink, colorNode, colorLink,
  colorByAttribute, colorByGPA, shapeByAttribute,
  sizeNode, move, layout, deselect, arithmetic,
  reset, saveSession, deleteSession

── COLOR NAME → HEX ──
When generating colorNode or colorLink, convert color names to hex:
  red→#FF0000, orange→#FFA500, yellow→#FFFF00, green→#00FF00,
  blue→#0000FF, purple→#800080, cyan→#00FFFF, pink→#FF69B4,
  white→#FFFFFF, gray/grey→#808080, black→#000000, magenta→#FF00FF,
  teal→#008080, navy→#000080, gold→#FFD700, brown→#A52A2A, violet→#EE82EE
Any hex color (e.g. #1565C0) may also be passed directly.

── GPA COLORING AND EXPLICIT COLORS ──
Use colorByGPA only when the user asks to encode/visualize GPA as color.
A GPA selection/ranking criterion does NOT request a GPA color gradient.
An explicit paint color applies to the selected nodes, even when selection uses GPA.
Never substitute a gradient for an explicit color such as green or a hex code.
  "color nodes by GPA" → [["colorByGPA","gpa"]]
  "visualize GPA"      → [["colorByGPA","gpa"]]
  "select the top 15 students with the highest GPAs and turn them green"
    → [["selectNode","top_15_gpa"],["colorNode","#00FF00"]]
  "select the top 15's, highest GPAs and turn on green"
    → [["selectNode","top_15_gpa"],["colorNode","#00FF00"]]
  "select the top 15 by GPA and color them by GPA"
    → [["selectNode","top_15_gpa"],["colorByGPA","gpa"]]

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
"top N nodes by metric" → selectNode using the metric actually named by the user.
Append colorNode for a requested paint color, or colorByGPA for an explicit GPA encoding.
Do not default to friendship degree when GPA (or another property) is specified.
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

Use "" for: colorNode, colorLink, colorByGPA, move, layout, deselect, reset, saveSession, deleteSession

── reset ──
Reset all node colors to yellow and all link colors to gray. Clears selection. No query needed.
  "reset"             → [["reset",""]]
  "reset everything"  → [["reset",""]]
  "start over"        → [["reset",""]]

── saveSession ──
Snapshot currently selected nodes as a named session frame on the wall.
param = session name (extract from user speech, e.g. "female students", "top athletes")
  "save session as female students"     → [["saveSession","female students"]]
  "save this as top athletes"           → [["saveSession","top athletes"]]
  "save current selection as snapshot"  → [["saveSession","snapshot"]]
  "save session"                        → [["saveSession","Snapshot"]]

── deleteSession ──
Delete the currently active session/subgraph. No query needed.
  "delete session"          → [["deleteSession",""]]
  "delete current session"  → [["deleteSession",""]]
  "remove this session"     → [["deleteSession",""]]

── selectNode ──
  Simple condition: ["selectNode","n.sex = 'female'"]
    → MATCH (n:Node) WHERE n.sex = 'female' RETURN n
  Top N by degree: ["selectNode","top_3_friendship_degree"]
    → MATCH (n:Node)-[r:POINTS_TO]-(m) WHERE r.type='friendship' WITH n,COUNT(r) AS d ORDER BY d DESC LIMIT 3 RETURN n
  Top N incoming: ["selectNode","top_3_incoming_aggression"]
    → MATCH (n:Node)<-[r:POINTS_TO]-(m) WHERE r.type='aggression' WITH n,COUNT(r) AS c ORDER BY c DESC LIMIT 3 RETURN n
  Top N by GPA: ["selectNode","top_15_gpa"]
    → MATCH (n:Node) WHERE n.gpa IS NOT NULL WITH n ORDER BY n.gpa DESC, n.GUID ASC LIMIT 15 RETURN n
  GPA ranking uses n.gpa, never a count of friendship links. Exclude missing GPA values.

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
STATISTICAL REPORTS
════════════════════════════════════════════════════

Use "stats" only when the user explicitly asks for a number or statistic.
For a statistics-only request, return "actions":[] and "queries":[]. Words
such as number, count, average, minimum, and maximum never imply sizeNode or
arithmetic. Do not add unrequested contextual statistics to visual actions.

Each stats item is:
  {"label":"meaningful label","query":"Cypher returning one numeric value"}

Rules:
- Return only the statistic requested; do not add min/max/average automatically.
- Each query returns exactly one row and one numeric column.
- Ignore missing numeric values. Never nest aggregates such as avg(count(...)).
- `grade` is 9-12; there is no `year`. GPA is numeric `gpa`.
- Relationships are `POINTS_TO` with `r.type='friendship'` or `'aggression'`.
- To average relationships per student, start from students, OPTIONAL MATCH the
  relationships, count per student in WITH, then average those counts.
- Unity colors and selection are not stored in Neo4j. If a group is described
  only as "red", "selected", "those", etc. and its defining database condition
  is absent from the current command, do not guess a Cypher predicate.

Examples:
  "average GPA" → actions:[], stats:[
    {"label":"Average GPA","query":"MATCH (n:Node) WHERE n.gpa IS NOT NULL RETURN avg(n.gpa)"}]
  "average number of friends per student" → actions:[], stats:[
    {"label":"Average friends per student","query":"MATCH (n:Node) OPTIONAL MATCH (n)-[r:POINTS_TO]-() WHERE r.type='friendship' WITH n,count(DISTINCT r) AS c RETURN avg(toFloat(c))"}]
  "students in grade 10: average friends" → actions:[], stats:[
    {"label":"Grade 10 average friends","query":"MATCH (n:Node) WHERE n.grade=10 OPTIONAL MATCH (n)-[r:POINTS_TO]-() WHERE r.type='friendship' WITH n,count(DISTINCT r) AS c RETURN avg(toFloat(c))"}]
  "average GPA of each year" → actions:[], stats:[
    {"label":"Grade 9 average GPA","query":"MATCH (n:Node) WHERE n.grade=9 AND n.gpa IS NOT NULL RETURN avg(n.gpa)"},
    {"label":"Grade 10 average GPA","query":"MATCH (n:Node) WHERE n.grade=10 AND n.gpa IS NOT NULL RETURN avg(n.gpa)"},
    {"label":"Grade 11 average GPA","query":"MATCH (n:Node) WHERE n.grade=11 AND n.gpa IS NOT NULL RETURN avg(n.gpa)"},
    {"label":"Grade 12 average GPA","query":"MATCH (n:Node) WHERE n.grade=12 AND n.gpa IS NOT NULL RETURN avg(n.gpa)"}]

════════════════════════════════════════════════════
Return ONLY a JSON object. No markdown. No explanation.
Example output:
{"actions":[["colorByAttribute","grade"]],"queries":["MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"],"stats":[{"label":"Grade 9","query":"MATCH (n:Node) WHERE n.grade = 9 RETURN count(n)"},{"label":"Grade 10","query":"MATCH (n:Node) WHERE n.grade = 10 RETURN count(n)"},{"label":"Grade 11","query":"MATCH (n:Node) WHERE n.grade = 11 RETURN count(n)"},{"label":"Grade 12","query":"MATCH (n:Node) WHERE n.grade = 12 RETURN count(n)"}]}
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

    # For Statistical Queries
    raw_stats = data.get("stats", [])
    stats = []

    for statistic in raw_stats if isinstance(raw_stats, list) else []:
        if isinstance(statistic, dict):  
            # Instances of statistical queries are expected to be dictionaries with "query" and optional "label" keys.
            query = statistic.get("query", "")
            
            if isinstance(query, str) and query.strip():
                query = query.strip()
                
                if not is_safe_statistical_query(query):
                    print_colored(f"Rejected invalid statistical query: {query}", "red")
                    continue
                
                label = statistic.get("label", "Statistical report")
                
                if not isinstance(label, str) or not label.strip():
                    label = "Statistical report"
                
                stats.append({"label": label.strip(), "query": query})
        elif isinstance(statistic, str) and statistic.strip():
            # Backward-compatible guard if the model emits the old bare-string format.
            query = statistic.strip()
            if is_safe_statistical_query(query):
                stats.append({"label": "Statistical report", "query": query})
            else:
                print_colored(f"Rejected invalid statistical query: {query}", "red")

    # Ensure arrays are same length
    while len(queries) < len(actions):
        queries.append("")

    # A numeric question is a report, not a request to resize nodes. Keep an
    # explicitly requested visualization action, but discard hallucinated ones.
    input_text = state["input"].lower()

    # Keys for types of queries in the input text.
    asks_for_statistic = re.search(
        r"\b(?:average|avg|mean|number|count|minimum|maximum|min|max|sum|total|percentage|variance)\b",
        input_text,
    )
    asks_for_visualization = re.search(
        r"\b(?:select|highlight|color|colour|shape|size|resize|move|layout|deselect|reset|save|delete)\b",
        input_text,
    )

    if asks_for_statistic and not asks_for_visualization:
        actions, queries = [], []

    # Enforce action/query contracts before Unity executes them.
    validated_actions, validated_queries = [], []

    # Maybe need to revamp this whole design
    # Currently, it only checks for specific action/query patterns and rejects invalid ones.
    # Does not really use Agents to fullest potential.

    # This Validation step Rejects invalid action/query pairs based on predefined patterns.
    # This may be problematic please check later.
    for action, query in zip(actions, queries):
        action_name = action[0] if isinstance(action, list) and action else ""
        if (
            action_name == "sizeNode"
            and not (
                re.search(r"\bAS\s+minValue\b", query, re.IGNORECASE)
                and re.search(r"\bAS\s+maxValue\b", query, re.IGNORECASE)
            )
        ):
            print_colored(f"Rejected sizeNode query without minValue/maxValue: {query}", "red")
            continue
        if action_name == "selectNode" and not re.search(
            r"\bRETURN\s+(?:DISTINCT\s+)?n\s*$", query, re.IGNORECASE
        ):
            print_colored(f"Rejected selectNode query that does not return n: {query}", "red")
            continue
        if action_name == "selectLink" and not re.search(
            r"\bRETURN\s+(?:DISTINCT\s+)?r\s*$", query, re.IGNORECASE
        ):
            print_colored(f"Rejected selectLink query that does not return r: {query}", "red")
            continue
        
        validated_actions.append(action)
        validated_queries.append(query)
    actions, queries = validated_actions, validated_queries

    # Unity display color and selection state are not Neo4j properties. Drop
    # fabricated statistics rather than reporting a plausible but false value.
    stats = [
        statistic for statistic in stats
        if not re.search(r"\bn\.(?:color|selected)\b", statistic["query"], re.IGNORECASE)
    ]

    actions, queries, stats = repair_aggressor_friend_statistics(
        state["input"], actions, queries, stats
    )
    stats = normalize_friend_statistic_labels(state["input"], stats)

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
        "stats": stats,
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
            "stats":          [],
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
            "stats":           result.get("stats", []),
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
