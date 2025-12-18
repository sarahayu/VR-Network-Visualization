# Manual Testing Commands for LangGraph Server

## 1. Start the Server

```bash
cd Assets/Voice/PythonScripts
python langgraph_server.py
```

The server should start on `http://localhost:5000`

---

## 2. Test with curl (Command Line)

### Test if server is alive:
```bash
curl http://localhost:5000/ping
```

### Test a simple selection command:
```bash
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d "{\"userText\": \"select all female students\"}"
```

### Test a color command:
```bash
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d "{\"userText\": \"color the selected nodes red\"}"
```

### Test voice error correction:
```bash
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d "{\"userText\": \"select notes with blew caller\"}"
```

### Test size command:
```bash
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d "{\"userText\": \"size nodes by GPA\"}"
```

---

## 3. Test with Python Script

```bash
python test_server.py
```

This will run a comprehensive test suite automatically.

---

## 4. Test with Postman or similar tools

**Endpoint:** `POST http://localhost:5000/classify`

**Headers:**
```
Content-Type: application/json
```

**Body (raw JSON):**
```json
{
  "userText": "select all female students and color them blue"
}
```

**Expected Response:**
```json
{
  "input": "select all female students and color them blue",
  "original_input": "select all female students and color them blue",
  "corrected_input": "select all female students and color them blue",
  "queries": [
    "MATCH (n:Node) WHERE n.sex = 'female' RETURN n",
    "RETURN \"\""
  ],
  "actions": [
    ["selectNode", "n.sex = 'female'"],
    ["colorNode", "#0000FF"]
  ],
  "clarify": "",
  "timings": {
    "preprocess_agent": 0.234,
    "general_agent": 0.156,
    "action_agent": 0.445,
    "cypher_agent": 0.289,
    "return_code": 0.001
  }
}
```

---

## Expected Outputs for Different Commands

| User Input | Expected Actions | Expected Queries |
|------------|-----------------|------------------|
| "select female students" | `[["selectNode", "n.sex = 'female'"]]` | `["MATCH (n:Node) WHERE n.sex = 'female' RETURN n"]` |
| "color them red" | `[["colorNode", "#FF0000"]]` | `["RETURN \"\""]` |
| "size by GPA" | `[["sizeNode", "gpa"]]` | `["MATCH (n:Node) RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue"]` |
| "move selected nodes" | `[["move", ""]]` | `["RETURN \"\""]` |
| "deselect all" | `[["deselect", ""]]` | `["RETURN \"\""]` |

---

## Troubleshooting

1. **Server won't start:**
   - Check if port 5000 is already in use
   - Make sure you have all dependencies installed: `pip install flask langgraph langchain-openai`

2. **OpenAI API errors:**
   - Make sure your `OPENAI_API_KEY` environment variable is set
   - Check if you have API credits

3. **Import errors:**
   - Check if `utils.py` with `print_colored` function exists
   - Or comment out the import if not needed
