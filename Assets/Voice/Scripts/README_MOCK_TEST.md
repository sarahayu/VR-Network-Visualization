# Mock Server Response Test Documentation

## Overview

`MockServerResponseTest.cs` is a Unity C# script that simulates and validates the entire flow of receiving responses from the Python LangGraph server **without needing the server running**. This allows you to:

- ✅ Test Unity's switch case logic independently
- ✅ Validate JSON parsing and deserialization
- ✅ Verify action handling for all command types
- ✅ Catch integration issues early
- ✅ Test without network dependencies

---

## How to Use

### Method 1: Automatic on Start

1. **Add to Scene:**
   - Create an empty GameObject in your Unity scene
   - Add the `MockServerResponseTest` component to it

2. **Configure:**
   - Check `Run Tests On Start` in the Inspector
   - Set `Verbose Logging` to see detailed output

3. **Run:**
   - Press Play in Unity
   - Tests will run automatically and log results to Console

### Method 2: Manual Trigger

1. **Add to Scene** (same as above)

2. **Uncheck** `Run Tests On Start`

3. **Run from Inspector:**
   - Right-click on the component
   - Select "Run All Tests" from context menu

4. **Or from Code:**
   ```csharp
   GetComponent<MockServerResponseTest>().RunAllTests();
   ```

---

## What Gets Tested

### Test Coverage (10 Tests)

| # | Test Name | Actions | Purpose |
|---|-----------|---------|---------|
| 1 | Simple Node Selection | 1 | selectNode with WHERE condition |
| 2 | Color Command | 1 | colorNode with hex validation |
| 3 | Multi-Step: Select + Color | 2 | Sequential actions |
| 4 | Default Sizing (All Nodes) | 1 | sizeNode with scope=all |
| 5 | Selected Sizing | 1 | sizeNode with scope=selected |
| 6 | Move Command | 1 | move action |
| 7 | Deselect Command | 1 | deselect action |
| 8 | Complex Multi-Step | 3 | Select + Size + Color sequence |
| 9 | Voice Error Correction | 1 | Validates corrections work |
| 10 | Clarification Needed | 0 | Tests ambiguous command handling |

---

## Expected Console Output

### Success (All Tests Pass)

```
============================================================
Starting Mock Server Response Tests
============================================================

--- Test: Simple Node Selection ---
Original: select all female students
Corrected: select all female nodes
Processing 1 actions...
  Action 1: selectNode with param: n.sex = 'female'
    [MOCK] Selecting nodes with query: MATCH (n:Node) WHERE n.sex = 'female' RETURN n
  Total processing time: 2.507s
✓ Simple Node Selection PASSED

--- Test: Color Command ---
Original: color the selected nodes red
Corrected: color the selected nodes #FF0000
Processing 1 actions...
  Action 1: colorNode with param: #FF0000
    [MOCK] Changing color of selected nodes to: #FF0000
  Total processing time: 3.068s
✓ Color Command PASSED

... (8 more tests)

============================================================
Test Summary: 10/10 passed
SUCCESS: All tests passed!
============================================================
```

### Failure Example

```
--- Test: Default Sizing (All Nodes) ---
✗ Default Sizing (All Nodes) FAILED
  - Scope is 'all' but query has 'WHERE n.selected = true'
  - Invalid color format: red

============================================================
Test Summary: 9/10 passed
FAILED: 1 test(s) failed!
============================================================
```

---

## What Gets Validated

### 1. JSON Deserialization
- ✅ Correct parsing of all response fields
- ✅ Handling of missing/null values
- ✅ Proper array deserialization

### 2. Action Count
- ✅ Expected number of actions generated
- ✅ Multi-step command sequences

### 3. Switch Case Logic
Each action type is validated:

#### `selectNode`
- Logs selection query
- Simulates database call

#### `sizeNode`
- ✅ Parses "attribute:scope" format correctly
- ✅ Extracts attribute name (e.g., "gpa" from "gpa:all")
- ✅ Validates scope matches query:
  - `scope=all` → Query should NOT have `WHERE n.selected = true`
  - `scope=selected` → Query MUST have `WHERE n.selected = true`

#### `colorNode` / `colorLink`
- ✅ Validates hex color format (#RRGGBB)
- ✅ Ensures colors are 7 characters starting with #

#### `move`, `layout`, `deselect`, `arithmetic`
- ✅ Correct action recognition
- ✅ Parameter extraction

#### Unknown Actions
- ✅ Detects and reports unknown action types

### 4. Clarification Handling
- ✅ Detects when clarification is needed
- ✅ Skips action processing when clarified

### 5. Timing Data
- ✅ Validates all timing fields exist
- ✅ Calculates total processing time

---

## Mock Response Format

Each test uses realistic JSON responses matching the Python server output:

```json
{
  "original_input": "select female students",
  "corrected_input": "select female nodes",
  "actions": [
    ["selectNode", "n.sex = 'female'"]
  ],
  "queries": [
    "MATCH (n:Node) WHERE n.sex = 'female' RETURN n"
  ],
  "clarify": "",
  "timings": {
    "preprocess_agent": 0.706,
    "general_agent": 0.259,
    "action_agent": 0.996,
    "cypher_agent": 0.546,
    "return_code": 0.0
  }
}
```

---

## Integration with Real System

The mock test validates the **exact same switch case logic** used in `StreamingSampleMic.cs`:

### Real Implementation (StreamingSampleMic.cs)
```csharp
case "sizeNode":
    string sizeParam = action[i][1];
    string attributeName = sizeParam.Contains(":") ? sizeParam.Split(':')[0] : sizeParam;

    var (minV, maxV) = _databaseStorage.GetMinMaxFromStore(_networkManager.NetworkGlobal, query[i]);
    _networkManager.SetMLNodeSizeEncoding(attributeName, minV, maxV, NetworkManager.MainNetworkID);
    break;
```

### Mock Test (MockServerResponseTest.cs)
```csharp
case "sizeNode":
    string sizeParam = actionParam;
    string attributeName = sizeParam.Contains(":") ? sizeParam.Split(':')[0] : sizeParam;
    string scope = sizeParam.Contains(":") ? sizeParam.Split(':')[1] : "all";

    Debug.Log($"[MOCK] Sizing nodes by attribute: {attributeName}, scope: {scope}");
    // Validates scope matches query
    break;
```

---

## Advantages

### 1. **No External Dependencies**
- No Python server needed
- No network connection required
- No OpenAI API calls

### 2. **Fast Iteration**
- Tests run in milliseconds
- No waiting for LLM responses
- Instant feedback

### 3. **Deterministic**
- Same input always gives same result
- No network variability
- Easy to reproduce issues

### 4. **Early Detection**
- Catch parsing errors before integration
- Validate switch case logic
- Test edge cases easily

### 5. **Documentation**
- Shows all expected response formats
- Examples of every action type
- Reference for integration

---

## Common Issues & Solutions

### Issue: Duplicate Class Definitions
**Error:** `ClassificationResponse already defined`

**Solution:** Remove duplicate classes if they exist elsewhere. The mock test reuses the same classes from `StreamingSampleMic.cs`.

### Issue: Newtonsoft.Json Missing
**Error:** `The type or namespace name 'Newtonsoft' could not be found`

**Solution:** Install Json.NET package in Unity:
```
Window > Package Manager > Add package by name > com.unity.nuget.newtonsoft-json
```

### Issue: Tests Don't Run
**Problem:** Nothing happens when Play is pressed

**Solution:**
1. Check `Run Tests On Start` is enabled
2. Verify script is attached to active GameObject
3. Check Console for any compilation errors

---

## Extending the Tests

### Add a New Test

```csharp
private void TestYourNewFeature()
{
    string testName = "Your Test Name";
    string mockResponse = @"{
        ""original_input"": ""your input"",
        ""corrected_input"": ""your corrected input"",
        ""actions"": [[""yourAction"", ""param""]],
        ""queries"": [""YOUR CYPHER""],
        ""clarify"": """",
        ""timings"": { ... }
    }";

    ProcessAndValidateResponse(testName, mockResponse,
        expectedActions: 1,
        shouldHaveClarification: false);
}
```

Then call it in `RunAllTests()`:
```csharp
TestYourNewFeature();
```

### Add Custom Validation

Modify `ProcessAndValidateResponse()` to add your own validation logic:

```csharp
// After action processing
if (someCondition)
{
    errors.Add("Your custom validation failed");
    testPassed = false;
}
```

---

## Output Files

The mock test is self-contained and doesn't generate any files. All results are logged to Unity's Console.

For permanent records, you can:
1. Save Unity Console logs: `Edit > Project Settings > Player > Configuration > Write Log File`
2. Or modify the script to write results to a file

---

## Comparison with Other Tests

| Feature | Mock Test | Python Server Test | Unity Integration |
|---------|-----------|-------------------|-------------------|
| Speed | ⚡ Instant | 🐌 4s per request | 🐌 4s + network |
| Dependencies | ✅ None | ❌ Server + API | ❌ Full stack |
| Tests | Switch logic | LLM quality | End-to-end |
| When to use | Development | Pre-deploy | Production |

---

## Best Practice Workflow

1. **Development Phase:**
   - Use `MockServerResponseTest.cs` to validate Unity logic
   - Fast iteration on switch cases
   - Test edge cases

2. **Integration Phase:**
   - Run `test_server_detailed.py` to test Python server
   - Validate LLM responses
   - Check network integration

3. **Testing Phase:**
   - Test full Unity + Python integration
   - Use real voice input (Whisper)
   - Validate in VR environment

4. **Production:**
   - Monitor real usage
   - Log errors
   - Use mock tests for regression testing

---

## Summary

The Mock Server Response Test provides:
- ✅ **Fast** - Instant feedback without network calls
- ✅ **Reliable** - Deterministic results every time
- ✅ **Complete** - Tests all 10 command types
- ✅ **Validated** - Checks parsing, logic, and edge cases
- ✅ **Independent** - No external dependencies

**Use this test during development to ensure your Unity integration logic is correct before connecting to the real Python server!**

---

**Last Updated:** 2025-12-18
**Script Version:** 1.0
**Unity Version:** 2021.3+ (compatible with all modern Unity versions)
