from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated
import time
import asyncio

from langchain_openai import ChatOpenAI
from langchain.prompts import ChatPromptTemplate
from utils import print_colored


def override(_: str | None, new: str) -> str:
    return new

def merge_dicts(old: dict[str, float] | None, new: dict[str, float]) -> dict[str, float]:
    return {**(old or {}), **new}

class AgentState(TypedDict):
    input: Annotated[str, override]
    code_list: Annotated[list[str], override]
    action_queue: Annotated[list[list[str]], override]
    timings: Annotated[dict[str, float], merge_dicts]
    judgment: Annotated[str, override]


app = Flask(__name__)
llm = ChatOpenAI(model="gpt-4o-mini", temperature=0)

clarify_prompt = ChatPromptTemplate.from_template(
    "The user input might be unclear. \
    Ask a clarifying question so we can understand the user's intent better.\n\n User input: {input}"
)

cypher_prompt = ChatPromptTemplate.from_template(
    "You are a Neo4j Cypher expert. Convert the user's request into a valid Cypher query. \
    We have DB schema for nodes as below for your usage: \
    :Node,\
    nodeId, (int)\
    label, (string)\
    degree, (float)\
    selected, (bool)\
    size, (float)\
    pos, (string)\
    color, (string)\
    sex, (\"male\"|\"female\")\
    smoker, (bool)\
    drinker, (bool)\
    gpa (float)\
    grade (int) \n\n\
    \
    :Community,\
    commId, (int)\
    selected, (bool)\n\n\
    \
    :Subnetwork,\
    subnetworkId, (int)\n\n\
    \
    We also have DB schema for links as below for your usage: \
    :POINTS_TO,\
    linkId, (int)\
    selected, (bool)\
    bundlingStrength, (float)\
    width, (string)\
    colorStart, (string)\
    colorEnd, (string)\
    alpha, (float)\
    waves, (array[int])\
    type (\"friendship\"|\"aggression\")\n\n\
    \
    :PART_OF,\
    \
    Only return the valid Cypher query, no explanations. Do not format the result into a code block. \n\n User Request: {input}"
)

action_prompt = ChatPromptTemplate.from_template(
    "You are an expert in generating graph commands. \
    Return only a list of user intents in order, no other words. \
    Each intent should be a two-item list: [action, parameters]. \
    Classify the action into the certain list,\
    Actions can be 'selectNode', 'selectLink', 'deselect', 'move', 'colorNode', 'colorLink', 'layout', 'arithmetic'. \
    Synonyms like 'highlight' = 'select' = 'show' = 'present', 'bring' = 'move' and so on. \
    For example: [['selectNode', ''], ['selectLink', ''], ['move', '']], ['colorNode', '#FF0000'], ['colorLink', '#FF0000'] \
    Also there is layout command with layout types 'floor', 'cluster' and 'spherical', return things such as ['layout', 'floor'] \
    As for the arithmetic, return the corresponding value, such as 'what's the degree of the selected node?'\
    should be [['arithmetic', '']]. \
    If the command is ambiguous, still list what you can extract clearly. \
    \n\n User Request: {input}"
)

ambiguity_prompt = ChatPromptTemplate.from_template(
    "Decide whether the following user input is ambiguous for generating corresponding graph commands or not. \
    Voice detection might have mistakes such as color 'rate' actually refer to color 'red'. \
    Try to get the correct meaning, do not see these pronunciation mistakes as ambiguous. \
    Reply with only 'yes' if it is ambiguous and 'no' if it is clear.\
    If the user did not mention which node they're interacting with, assume it's the selected community or node if there's selected one. \
    \n\n User input: {input}"
)


def timed_node(node_name):
    def decorator(fn):
        async def wrapper(state: AgentState):
            start = time.time()
            result = await fn(state)
            end = time.time()

            duration = end - start
            print_colored(f"⏱️ {node_name} took {duration:.3f} seconds", 'cyan')

            if "timings" not in state or state["timings"] is None:
                state["timings"] = {}
            state["timings"][node_name] = duration
            result["timings"] = state["timings"]

            return result
        return wrapper
    return decorator


@timed_node("general_agent")
async def general_agent(state: AgentState) -> dict:
    messages = ambiguity_prompt.format_messages(input=state["input"])
    judgment = (await llm.ainvoke(messages)).content.strip().lower()
    print_colored(f"Judgment on ambiguity: {judgment}", 'blue')
    return {"input": state["input"], "judgment": judgment}


@timed_node("clarify_agent")
async def clarify_agent(state: AgentState) -> dict:
    messages = clarify_prompt.format_messages(input=state["input"])
    clarification_question = (await llm.ainvoke(messages)).content.strip()
    print_colored(f"Clarification question: {clarification_question}", 'yellow')
    return {"clarify": clarification_question, "code": "", "__next__": "return_code"}


@timed_node("action_agent")
async def action_agent(state: AgentState) -> dict:
    action_messages = action_prompt.format_messages(input=state["input"])
    action_list_str = (await llm.ainvoke(action_messages)).content.strip()
    print_colored(f"Generated action list: {action_list_str}", 'green')

    try:
        action_queue = eval(action_list_str)
        if not isinstance(action_queue, list):
            raise ValueError("Invalid format")
    except Exception:
        raise ValueError("Failed to parse action list from LLM")

    return {"action_queue": action_queue}


@timed_node("cypher_agent")
async def cypher_agent(state: AgentState) -> dict:
    action_queue = state.get("action_queue", [])
    code_list = []

    for action, param in action_queue:
        msg = cypher_prompt.format_messages(input=f"{action} {param}")
        cypher_code = (await llm.ainvoke(msg)).content.strip()
        code_list.append(cypher_code)
        print(f"Got {cypher_code}")

    return {"code_list": code_list}


@timed_node("return_code")
async def return_code(state: AgentState) -> AgentState:
    print_colored("Returning code list and action queue", 'magenta')
    return state


def decide_clarify(state: AgentState) -> str:
    if state['judgment'].startswith("yes"):
        return "clarify_agent"
    else:
        return "action_agent"


# Graph Construction
graph = StateGraph(state_schema=AgentState)

graph.add_node("general_agent", general_agent)
graph.add_node("clarify_agent", clarify_agent)
graph.add_node("action_agent", action_agent)
graph.add_node("cypher_agent", cypher_agent)
graph.add_node("return_code", return_code)

graph.set_entry_point("general_agent")

graph.add_conditional_edges(
    "general_agent", decide_clarify,
    {"clarify_agent": "clarify_agent", "action_agent": "action_agent"}
)

# Parallel execution: action_agent → cypher_agent and return_code
graph.add_edge("action_agent", "cypher_agent")
# graph.add_edge("action_agent", "return_code")
graph.add_edge("cypher_agent", "return_code")
graph.add_edge("clarify_agent", "return_code")

langgraph_app = graph.compile()


@app.route("/classify", methods=["POST"])
def classify():
    try:
        data = request.get_json(force=True)
        user_input = data.get("userText", "")
        print(f"Received input: {user_input}")
        state = {
            "input": user_input,
            "code_list": [],
            "action_queue": [],
            "timings": {}
        }

        # Use asyncio to run the async graph
        result = asyncio.run(langgraph_app.ainvoke(state))

        print_colored(f"Result from langgraph: {result}", 'green')
        return jsonify({
            "queries": result.get("code_list", []),
            "actions": result.get("action_queue", []),
            "clarify": result.get("clarify", ""),
            "timings": result.get("timings", {})
        })
    except Exception as e:
        print_colored(f"ERROR in /classify: {e}", 'red')
        return jsonify({"error": str(e)}), 500


@app.route("/ping", methods=["GET"])
def ping():
    return "Server is alive!", 200


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
