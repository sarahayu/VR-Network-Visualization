from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated

import time
from langchain_openai import ChatOpenAI
from langchain.prompts import ChatPromptTemplate
from utils import print_colored


def override(_: str | None, new: str) -> str:
    return new

def merge_dicts(old: dict[str, float] | None, new: dict[str, float]) -> dict[str, float]:
    return {**(old or {}), **new}

class AgentState(TypedDict):
    input: Annotated[str, override]
    code: Annotated[str, override]
    action: Annotated[str, override]
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
    We have DB schema as below for your usage: \
    :Node,\
    nodeId, (int)\
    label, (string)\
    degree, (float)\
    selected, (bool)\
    render_size, (float)\
    render_pos, (string)\
    render_color (string)\
    smoker, (bool)\
    drinker, (bool)\
    gpa (float)\
    grade (int) \n\n\
    Only return the Cypher query, no explanations.\n\n User Request: {input}"
)

action_prompt = ChatPromptTemplate.from_template(
    "You are an expert in generating graph commands. \
        Return only the action and the required parameters that user ask for such as 'Select', 'Deselect', 'Move', format should be like ['select', ""], ['deselect', ""], ['move', ""]\" \
        Moreover users say 'highlight' is the same with 'select', 'bring' is the same with 'move', etc \
        Some required parameters format should be like: ['layout', 'layout_type'] \
        \n\n User Request: {input}"
)

ambiguity_prompt = ChatPromptTemplate.from_template(
    "Decide whether the following user input is ambiguous for generating corresponding graph commands or not. \
    Voice detection might have mistakes such as color 'rate' actually refer to color 'red'. \
    Try to get the correct meaning, do not see these pronunciation mistakes as ambiguous. \
    Reply with only 'yes' if it is ambiguous and 'no' if it is clear.\n\n User input: {input}"
)


def timed_node(node_name):
    def decorator(fn):
        def wrapper(state: AgentState):
            start = time.time()
            result = fn(state)
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
def general_agent(state: AgentState) -> dict:
    messages = ambiguity_prompt.format_messages(input=state["input"])
    judgment = llm(messages).content.strip().lower()
    print_colored(f"Judgment on ambiguity: {judgment}", 'blue')
    return {"input": state["input"], "judgment": judgment}

@timed_node("clarify_agent")
def clarify_agent(state: AgentState) -> dict:
    messages = clarify_prompt.format_messages(input=state["input"])
    clarification_question = llm(messages).content.strip()
    print_colored(f"Clarification question: {clarification_question}", 'yellow')
    return {"input": clarification_question, "code": "", "__next__": "return_code"}

@timed_node("execute_agent")
def execute_agent(state: AgentState) -> dict:
    messages1 = cypher_prompt.format_messages(input=state["input"])
    messages2 = action_prompt.format_messages(input=state["input"])
    cypher = llm(messages1).content.strip()
    action = llm(messages2).content.strip()
    print_colored(f"Generated Cypher query: {cypher}", 'green')
    return {"input": "", "code": cypher, "action": action, "__next__": "return_code"}

@timed_node("return_code")
def return_code(state: AgentState) -> AgentState:
    print_colored(f"Returning code: {state['code']}", 'magenta')
    return state

def decide_clarify(state: AgentState) -> str:
    if state['judgment'].startswith("yes"):
        return "clarify_agent"
    else:
        return "execute_agent"

# The graph structure

graph = StateGraph(state_schema=AgentState)
graph.add_node("general_agent", general_agent)
graph.add_node("clarify_agent", clarify_agent)
graph.add_node("execute_agent", execute_agent)
graph.add_node("return_code", return_code)

graph.set_entry_point("general_agent")

graph.add_conditional_edges(
    "general_agent", decide_clarify, 
    {"clarify_agent": "clarify_agent", "execute_agent": "execute_agent"}
)

graph.add_edge("clarify_agent", "return_code")
graph.add_edge("execute_agent", "return_code")

langgraph_app = graph.compile()

@app.route("/classify", methods=["POST"])
def classify():
    try:
        data = request.get_json(force=True)
        user_input = data.get("userText", "")
        print(f"Received input: {user_input}")
        state = {"input": user_input, "code": "", "timings": {}}
        result = langgraph_app.invoke(state)
        print_colored(f"Result from langgraph: {result}", 'green')
        return jsonify({
            "type": result.get("action", ""), # Could be 'Select', 'Deselect', 'Move', etc.
            "query": result.get("code", ""),
            "clarify": result.get("input", ""),
            "timings": result.get("timings", {})
        })
    except Exception as e:
        print_colored(f"🔥 ERROR in /classify: {e}", 'red')
        return jsonify({"error": str(e)}), 500

@app.route("/ping", methods=["GET"])
def ping():
    return "Server is alive!", 200

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)