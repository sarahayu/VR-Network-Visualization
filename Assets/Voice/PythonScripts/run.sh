# activate virtual environment if not yet
if [[ "$VIRTUAL_ENV" == "" ]]; then
    source .venv/bin/activate
fi

# set openai api key environment variable
[[ -f .env ]] && export $(cat .env)

python3 langgraph_server.py
