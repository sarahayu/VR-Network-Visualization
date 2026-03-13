# Testing Documentation

This directory contains comprehensive testing tools for the LangGraph voice command server.

## Test Scripts

### 1. `test_server.py` - Quick Test Suite
**Purpose:** Fast, simple text output for quick verification

**Usage:**
```bash
python test_server.py
```

**Output:**
- Console output with basic results
- `test_report.txt` - Plain text summary

**Use when:**
- Quick sanity checks
- CI/CD pipeline testing
- Terminal-only environments

---

### 2. `test_server_detailed.py` - Comprehensive Test Suite ⭐ RECOMMENDED
**Purpose:** Detailed markdown report with full analysis

**Usage:**
```bash
python test_server_detailed.py
```

**Output:**
- `test_results_detailed.md` - 900+ line comprehensive report

**Report includes:**
- ✓ Full request/response details
- ✓ Voice correction analysis
- ✓ Action breakdown with explanations
- ✓ Cypher query display
- ✓ Performance timing breakdowns with percentages
- ✓ Raw JSON responses (expandable)
- ✓ Summary statistics
- ✓ Average agent performance
- ✓ Feature validation list
- ✓ Voice correction examples

**Use when:**
- Documenting system behavior
- Debugging complex issues
- Sharing results with team
- Performance analysis

---

## Test Coverage

Both scripts test:
1. ✓ Server health check (`/ping`)
2. ✓ Simple node selection
3. ✓ Color commands with hex conversion
4. ✓ Multi-step commands (select + color)
5. ✓ Default sizing (all nodes)
6. ✓ Explicit selected sizing
7. ✓ Voice error correction
8. ✓ Movement commands
9. ✓ Deselection
10. ✓ Complex multi-step (select + size + color)

---

## Generated Reports

### `test_report.txt`
Simple text format:
```
=== Testing /classify with: 'select all female students' ===
Status: 200
Actions: [["selectNode", "n.sex = 'female'"]]
Queries: ["MATCH (n:Node) WHERE n.sex = 'female' RETURN n"]
Timings:
  action_agent: 0.996s
  cypher_agent: 0.546s
```

### `test_report.md`
Simple markdown format with code blocks and sections

### `test_results_detailed.md` ⭐
Comprehensive analysis:
```markdown
## Test 2: Simple Node Selection

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "select all female students"
}
```

### Voice Correction
- **Original Input:** `select all female students`
- **Corrected Input:** `select all female nodes`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[["selectNode", "n.sex = 'female'"]]
```

### Action Breakdown
#### Action 1: `selectNode`
- **Parameter:** `n.sex = 'female'`
- **Purpose:** Select nodes matching condition
- **Database Query:** Will execute Cypher query

### Performance Timings
**Total Processing Time:** 2.311s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `cypher_agent` | 0.804s | 34.8% |
| `action_agent` | 0.602s | 26.1% |
...
```

---

## Quick Start

1. **Start the server:**
   ```bash
   python langgraph_server.py
   ```

2. **Run tests** (in another terminal):
   ```bash
   # Quick test
   python test_server.py

   # Detailed report (recommended)
   python test_server_detailed.py
   ```

3. **View results:**
   ```bash
   # View detailed report in markdown viewer
   # or open test_results_detailed.md in VS Code / GitHub
   ```

---

## Manual Testing with curl

Test individual commands:

```bash
# Health check
curl http://localhost:5000/ping

# Test a command
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d '{"userText": "select female nodes and color them blue"}'
```

---

## Performance Benchmarks

From latest test run:

| Agent | Average Duration | Purpose |
|-------|------------------|---------|
| `preprocess_agent` | 1.699s | Voice correction via LLM |
| `cypher_agent` | 1.287s | Cypher query generation |
| `action_agent` | 0.769s | Action planning |
| `general_agent` | 0.584s | Ambiguity detection |

**Total average per request:** ~4.3 seconds

---

## What Gets Validated

### Voice Corrections
- students → nodes
- red/blue/etc → #FF0000/#0000FF/etc
- notes → nodes
- blew → blue
- caller → color

### Action Types
- `selectNode` - Conditional node selection
- `colorNode` - Node coloring with hex codes
- `sizeNode` - Size encoding by attribute (all/selected)
- `move` - Spatial repositioning
- `deselect` - Clear selections
- Multi-step sequences

### Cypher Generation
- Proper MATCH/WHERE/RETURN syntax
- Conditional logic (selected vs all)
- Aggregate functions (min/max)

---

## Troubleshooting

**Server won't start:**
- Check port 5000 is not in use
- Verify dependencies: `pip install flask langgraph langchain-openai`
- Set OpenAI API key: `export OPENAI_API_KEY=your_key`

**Tests fail:**
- Ensure server is running first
- Check `test_results_detailed.md` for error details
- Review server console output

**Import errors:**
- Update to `langchain_core`: `pip install --upgrade langchain-core`
- Check `utils.py` exists with `print_colored` function

---

## Files Overview

| File | Purpose | Output |
|------|---------|--------|
| `langgraph_server.py` | Main server | HTTP API |
| `test_server.py` | Quick tests | `test_report.txt`, `test_report.md` |
| `test_server_detailed.py` | Detailed tests | `test_results_detailed.md` |
| `test_commands.md` | Manual test guide | Documentation |
| `CHANGES.md` | Change log | Documentation |
| `README_TESTING.md` | This file | Documentation |

---

## Example Test Output

### Console Output
```
============================================================
LangGraph Server - Detailed Test Suite
============================================================

[1/10] Testing server health...
[SUCCESS] Server is running!

[2/10] Testing simple node selection...
[3/10] Testing color command...
...
[10/10] Testing complex multi-step command...

============================================================
Generating detailed markdown report...
============================================================

[SUCCESS] Report saved to: test_results_detailed.md
[INFO] Total Tests: 10
[INFO] Passed: 10
[INFO] Failed: 0
[INFO] Success Rate: 100.0%
```

### Report Highlights

**All 10 tests passed!** ✓
- Server health: ✓
- Voice corrections: ✓
- Multi-step actions: ✓
- Cypher generation: ✓
- Performance tracking: ✓

**Ready for Unity integration!**

---

## Next Steps

1. Review `test_results_detailed.md` for complete analysis
2. Check `CHANGES.md` for recent updates
3. Integrate with Unity using `StreamingSampleMic.cs`
4. Monitor performance metrics during VR testing

---

**Last Updated:** 2025-12-18
**Status:** All tests passing ✓
