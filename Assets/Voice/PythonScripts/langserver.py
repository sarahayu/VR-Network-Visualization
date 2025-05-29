from flask import Flask, request, jsonify
from langgraph.graph import StateGraph
from typing import TypedDict

from langchain.chat_models import ChatOpenAI
from langchain.prompts import ChatPromptTemplate

class AgentState(TypedDict):
    input: str
    code: str

app = Flask(__name__)
llm = ChatOpenAI(model="gpt-4", temperature=0)

clarify_prompt = ChatPromptTemplate.from_template(
    "The user input might be unclear. Ask a clarifying question so we can understand the user's intent better.\n\nUser input: {input}"
)

cypher_prompt = ChatPromptTemplate.from_template(
    "You are a Neo4j Cypher expert. Convert the user's request into a valid Cypher query. Only return the Cypher query, no explanations.\n\nRequest: {input}"
)

ambiguity_prompt = ChatPromptTemplate.from_template(
    "Decide whether the following user input is ambiguous for generating corresponding graph commands or not. Reply with only 'yes' if it is ambiguous and 'no' if it is clear.\n\nUser input: {input}"
)

def general_agent(state: AgentState) -> dict:
    messages = ambiguity_prompt.format_messages(input=state["input"])
    judgment = llm(messages).content.strip().lower()
    if judgment.startswith("yes"):
        return {"input": state["input"], "next": "clarify_agent"}
    return {"input": state["input"], "next": "execute_agent"}

def clarify_agent(state: AgentState) -> dict:
    messages = clarify_prompt.format_messages(input=state["input"])
    clarification_question = llm(messages).content.strip()
    return {"input": clarification_question, "code": "", "next": "return_code"}

def execute_agent(state: AgentState) -> dict:
    messages = cypher_prompt.format_messages(input=state["input"])
    cypher = llm(messages).content.strip()
    return {"input": state["input"], "code": cypher, "next": "return_code"}

def return_code(state: AgentState) -> AgentState:
    return state

graph = StateGraph(state_schema=AgentState)
graph.add_node("general_agent", general_agent)
graph.add_node("clarify_agent", clarify_agent)
graph.add_node("execute_agent", execute_agent)
graph.add_node("return_code", return_code)

graph.set_entry_point("general_agent")

graph.add_edge("general_agent", "clarify_agent")
graph.add_edge("general_agent", "execute_agent")
graph.add_edge("clarify_agent", "return_code")
graph.add_edge("execute_agent", "return_code")

langgraph_app = graph.compile()

@app.route("/classify", methods=["POST"])
def classify():
    data = request.get_json()
    user_input = data.get("userText", "")
    state = {"input": user_input, "code": ""}
    result = langgraph_app.invoke(state)
    return jsonify({"query": result.get("code", ""), "clarify": result.get("input", "")})

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
