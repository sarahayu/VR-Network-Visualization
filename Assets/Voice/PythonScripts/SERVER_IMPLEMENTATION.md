# LangGraph Server — Implementation Notes

`langgraph_server.py` — Flask + LangGraph classification server for VR Network Visualization.  
Converts free-form voice/text commands into structured actions and Cypher queries for Unity to execute.

---

## Graph Architecture

```
[preprocess_general] ──[ambiguous?]──► [clarify_agent]   ──► [return_code]
                                   └──► [action_cypher]  ──► [return_code]
```

Three agents total on the happy path. The graph was progressively optimized from five sequential agents down to this structure — see the **Optimization History** section for the reasoning.

---

## Agents

### `preprocess_general` (parallel)
Runs two LLM calls concurrently with `asyncio.gather`:

- **Preprocess**: corrects ASR errors in voice input (note→node, blew→blue, sacks→sex, etc.). Returns the corrected text that all downstream agents use.
- **General**: detects whether the input is ambiguous (`"yes"` / `"no"`). Runs on the *original* text (not corrected) because ASR-mangled color names are explicitly treated as unambiguous.

Both calls only need the raw input, so they are independent — `asyncio.gather` cuts ~0.5–1 s off every request compared to running them sequentially.

Uses `SystemMessage` + `HumanMessage` instead of `ChatPromptTemplate` so the static system content is cache-eligible under OpenAI's prompt caching (static prefix must come first; dynamic user text last).

### `clarify_agent`
Only reached when `general_agent` returns `"yes"`. Asks one short clarification question. Result is returned to Unity as `clarify` field; Unity shows it in the command log and does not execute any actions.

### `action_cypher` (combined)
Single LLM call using `llm_json` (JSON-mode: `response_format={"type": "json_object"}`).

Returns:
```json
{
  "actions": [["actionName", "param"], ...],
  "queries": ["cypher string or empty string", ...]
}
```

The arrays are index-aligned — `queries[i]` is the Cypher for `actions[i]`. Actions that need no database query (colorNode, colorLink, colorByGPA, deselect, move, layout, reset) get `""`.

**Why combined?** The previous design had `action_agent` → `cypher_agent` as two sequential calls (~0.9 s + 0.7 s = 1.6 s). Merging them into one prompt eliminates a full API round-trip. Measured saving: ~0.7–1 s per request.

**JSON mode** avoids markdown fence stripping and makes output parsing more reliable.

### `return_code`
No-op terminal node. Records `return_code: 0.0` in timings for consistency.

---

## Key Constants

```python
ALLOWED_COLORS   # color name → hex mapping used in action_cypher output normalization
```

`action_cypher_agent` normalizes color names to hex after parsing the JSON response, handling the case where the LLM returns `"red"` instead of `"#FF0000"` despite prompt instructions.

---

## LLM Instances

| Variable | Model | Mode | Used by |
|---|---|---|---|
| `llm` | gpt-4o-mini, temp=0 | standard | preprocess, general, clarify |
| `llm_json` | gpt-4o-mini, temp=0 | JSON object | action_cypher |

---

## Flask API

**POST `/classify`**
```json
// Request
{ "userText": "color nodes by grade" }

// Response
{
  "corrected_input": "color nodes by grade",
  "actions": [["colorByAttribute", "grade"]],
  "queries": ["MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"],
  "clarify": "",
  "timings": {
    "preprocess_agent": 1.02,
    "general_agent": 0.98,
    "action_cypher_agent": 0.75,
    "return_code": 0.0
  }
}
```

**GET `/ping`** — health check, returns `"Server alive!"`.

State is initialized per-request inside `classify()` with `asyncio.run(langgraph_app.ainvoke(state))`.

---

## Optimization History

### v1 — original sequential graph
```
preprocess → general → [clarify | action → cypher] → return_code
```
Five agents, all sequential. Measured: **4.1–8.4 s**, with `select by attribute` spiking to **17 s** due to sequential per-action Cypher calls.

### v2 — parallel preprocess+general, skip_cypher, parallel cypher
- Merged `preprocess` and `general` into one `asyncio.gather` node (saved ~0.5 s/request).
- Added `skip_cypher` route for commands that need no DB query, bypassing `cypher_agent` entirely.
- Parallelized per-action LLM calls inside `cypher_agent` with `asyncio.gather`.
- Measured: **3.7–5.2 s**. 17 s spike eliminated (max became 5.9 s).

### v3 — combined action+cypher (current)
- Replaced sequential `action_agent → cypher_agent` with a single `action_cypher` call using JSON mode.
- Switched all prompts to `SystemMessage` + `HumanMessage` for better prompt cache hit rate.
- Measured: **3.7–4.6 s** in normal usage.

**Hard floor**: `preprocess_agent` costs ~1 s on every voice request (one OpenAI API call, unavoidable with the online model). Total end-to-end voice latency = Whisper STT (0.5–3 s) + LangGraph (3.7–4.6 s) + Neo4j (5–23 ms) + Unity render (<1 frame).
