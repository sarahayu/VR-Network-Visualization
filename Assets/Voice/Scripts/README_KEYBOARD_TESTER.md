# Keyboard Command Tester

## Overview

A Unity script that lets you test voice commands using keyboard shortcuts **without needing VR**. Perfect for development and debugging!

---

## Setup

### 1. Add to Scene

1. In Unity, create an empty GameObject (or use existing)
2. Add the `KeyboardCommandTester` component
3. Assign references in Inspector:
   - **Streaming Sample Mic**: Drag your StreamingSampleMic component here

### 2. Configure Settings

In the Inspector:

#### Server Settings
- **Server URL**: `http://localhost:5000/classify` (default)
- **Server Enabled**:
  - ✅ Check to test with Python LangGraph server
  - ❌ Uncheck to execute actions directly (bypass server)

#### Keyboard Shortcuts (customizable)
- **Size By Degree Key**: `1` (default)
- **Size By GPA Key**: `2` (default)
- **Color By Grade Key**: `3` (default)
- **Color By Sex Key**: `4` (default)
- **Select Female Key**: `5` (default)
- **Deselect All Key**: `6` (default)
- **Color Selected Red Key**: `7` (default)
- **Move Selected Key**: `8` (default)

#### Display
- **Show Instructions**: Shows keyboard shortcuts in console on start

---

## Usage

### Mode 1: Direct Execution (No Server Needed)

**Setup:**
1. Uncheck "Server Enabled" in Inspector
2. Press Play in Unity

**What happens:**
- Actions execute directly without calling Python server
- Fastest for testing Unity logic
- Good for when server is down or you're testing C# code

**Example:**
```
Press [3] → Directly executes colorByAttribute with grade
  - Queries: MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value
  - Colors each grade category
```

---

### Mode 2: Server Mode (Full Pipeline)

**Setup:**
1. Start Python server:
   ```bash
   cd Assets/Voice/PythonScripts
   python langgraph_server.py
   ```

2. Check "Server Enabled" in Inspector
3. Press Play in Unity

**What happens:**
- Commands sent to LangGraph server
- Server processes and returns actions
- Unity executes returned actions
- Tests full pipeline including voice correction, action planning

**Example:**
```
Press [1] → Sends "size nodes by degree" to server
  - Server returns: [["sizeNode", "degree:all"]]
  - Unity executes size encoding
```

---

## Keyboard Shortcuts

| Key | Command | Description |
|-----|---------|-------------|
| `1` | Size by degree | Sizes all nodes by degree attribute |
| `2` | Size by GPA | Sizes all nodes by GPA attribute |
| `3` | **Color by grade** | **Categorical coloring by grade** |
| `4` | **Color by sex** | **Categorical coloring by sex** |
| `5` | Select female | Selects female students |
| `6` | Deselect all | Clears all selections |
| `7` | Color selected red | Colors selected nodes red |
| `8` | Move selected | Moves selected nodes |
| `H` | Help | Shows instructions in console |

---

## Testing Categorical Coloring

### Test Key 3: Color by Grade

**Press `3`**

**What should happen:**
1. Console shows:
   ```
   [KEYBOARD] Triggered: Color by attribute (grade)
   [DIRECT] Executing action: colorByAttribute(grade)
   [EXECUTE] Processing 1 action(s)
   [ACTION 1/1] colorByAttribute(grade)
     Categorical coloring by: grade
     Found 4 distinct values
       Category 9 → #FF0000
       Category 10 → #FFA500
       Category 11 → #FFFF00
       Category 12 → #00FF00
     ✓ Categorical coloring complete
   [COMPLETE] All actions executed
   ```

2. Visual result:
   - All grade 9 nodes turn RED
   - All grade 10 nodes turn ORANGE
   - All grade 11 nodes turn YELLOW
   - All grade 12 nodes turn GREEN

### Test Key 4: Color by Sex

**Press `4`**

**Expected:**
- Female nodes → Red
- Male nodes → Orange

---

## Combining Commands

You can chain commands by pressing keys in sequence:

### Example 1: Select + Color
```
Press [5] → Select female students
Press [7] → Color selected nodes red
Result: Only female students are red
```

### Example 2: Size + Categorical Color
```
Press [1] → Size all nodes by degree
Press [3] → Color nodes by grade
Result: Nodes sized by degree AND colored by grade
```

### Example 3: Filter + Categorical
```
Press [5] → Select female students
Press [3] → Color by grade (will color ALL nodes by grade)
Press [6] → Deselect all (to see all colored nodes)
```

---

## Console Output

### Successful Execution
```
============================================================
KEYBOARD COMMAND TESTER - Instructions
============================================================
Press [Alpha1] - Size nodes by degree
Press [Alpha2] - Size nodes by GPA
Press [Alpha3] - Color nodes by grade (categorical)
...
============================================================

[KEYBOARD] Triggered: Color by attribute (grade)
[DIRECT] Executing action: colorByAttribute(grade)
[EXECUTE] Processing 1 action(s) for: colorByAttribute
[ACTION 1/1] colorByAttribute(grade)
  Categorical coloring by: grade
  Found 4 distinct values
    Category 9 → #FF0000
    Category 10 → #FFA500
    Category 11 → #FFFF00
    Category 12 → #00FF00
  ✓ Categorical coloring complete
[COMPLETE] All actions executed for: colorByAttribute
```

### Server Mode Output
```
[KEYBOARD] Triggered: Size nodes by degree
[SERVER] Sending to server: size nodes by degree
[SERVER RESPONSE] {"actions":[["sizeNode","degree:all"]],...}
[EXECUTE] Processing 1 action(s)
[ACTION 1/1] sizeNode(degree:all)
  Sizing nodes by: degree (scope: all)
  Min/Max: 1.0, 10.0
  ✓ Size encoding applied
[COMPLETE] All actions executed
```

### Error Cases
```
// Missing reference
StreamingSampleMic reference is null! Please assign it in the Inspector.

// Server down (Server Mode)
[SERVER ERROR] Connection refused
Make sure the Python server is running: python langgraph_server.py

// Unknown action
⚠ Unknown action: unknownAction
```

---

## Troubleshooting

### Issue: Nothing happens when pressing keys

**Solutions:**
1. Make sure Unity window has focus (click on Game or Scene view)
2. Check that KeyboardCommandTester is attached to active GameObject
3. Verify StreamingSampleMic reference is assigned
4. Look for errors in Console

### Issue: "StreamingSampleMic reference is null"

**Solution:**
1. In Unity Inspector, find KeyboardCommandTester component
2. Drag your StreamingSampleMic GameObject into the "Streaming Sample Mic" field

### Issue: Server errors in Server Mode

**Solutions:**
1. Make sure Python server is running:
   ```bash
   python langgraph_server.py
   ```
2. Check server URL is correct: `http://localhost:5000/classify`
3. Try Direct Mode instead (uncheck "Server Enabled")

### Issue: Nodes don't change color/size

**Possible causes:**
1. No nodes loaded in scene
2. Database not connected (check DatabaseStorage settings)
3. Neo4j not running
4. Attribute doesn't exist (e.g., no "grade" property on nodes)

---

## Advanced: Adding Custom Commands

### Step 1: Add KeyCode Field
```csharp
public KeyCode myCustomKey = KeyCode.Alpha9;
```

### Step 2: Add Update Check
```csharp
if (Input.GetKeyDown(myCustomKey))
{
    Debug.Log("[KEYBOARD] Triggered: My custom command");
    ExecuteDirectAction("myAction", "myParam");
}
```

### Step 3: Handle in Switch Case
Add your action handling in `ExecuteActionsDirectly()` method.

---

## Best Practices

### For Testing New Features
1. Use **Direct Mode** first to test Unity logic
2. Verify console output shows expected behavior
3. Switch to **Server Mode** to test full pipeline
4. Check that server returns correct actions

### For Debugging
1. Enable "Show Instructions" to see shortcuts
2. Watch console for detailed action logs
3. Use Direct Mode to isolate Unity vs Server issues

### For Demonstrations
1. Use keyboard shortcuts for quick demos
2. Combine commands to show complex workflows
3. Server Mode shows full AI pipeline

---

## Comparison: VR vs Keyboard vs Server

| Feature | VR Voice | Keyboard Direct | Keyboard Server |
|---------|----------|-----------------|-----------------|
| **Speed** | ~6s (voice + LLM) | Instant | ~4s (LLM) |
| **Setup** | VR headset | None | Python server |
| **Tests** | End-to-end | Unity logic | Full pipeline |
| **Iteration** | Slow | Fast | Medium |
| **Use case** | Production | Development | Integration testing |

---

## Example Workflow

### Testing Categorical Coloring Feature

1. **Start in Direct Mode**
   ```
   - Uncheck "Server Enabled"
   - Press Play
   - Press [3] to test colorByAttribute
   - Verify nodes colored correctly
   - Check console for category mapping
   ```

2. **Test with Different Attributes**
   ```
   - Press [3] → Color by grade
   - Press [4] → Color by sex
   - Compare results
   ```

3. **Test Server Integration**
   ```
   - Start Python server
   - Check "Server Enabled"
   - Press [3] again
   - Verify server returns correct actions
   ```

4. **Test Edge Cases**
   ```
   - Modify data to have >6 categories
   - Press [3]
   - Check warning appears in console
   ```

---

## Quick Reference Card

```
┌─────────────────────────────────────────┐
│   KEYBOARD COMMAND TESTER SHORTCUTS     │
├─────────────────────────────────────────┤
│  1  │ Size by degree                    │
│  2  │ Size by GPA                       │
│  3  │ Color by grade (categorical) ⭐   │
│  4  │ Color by sex (categorical) ⭐     │
│  5  │ Select female                     │
│  6  │ Deselect all                      │
│  7  │ Color selected red                │
│  8  │ Move selected                     │
│  H  │ Show help                         │
└─────────────────────────────────────────┘

⭐ = New categorical coloring feature
```

---

## Summary

The Keyboard Command Tester provides:
- ✅ Fast iteration without VR
- ✅ Direct execution mode for Unity testing
- ✅ Server mode for full pipeline testing
- ✅ Detailed console logging
- ✅ Easy testing of categorical coloring
- ✅ Customizable keyboard shortcuts

**Perfect for development and debugging your voice command system!**

---

**Created:** 2025-12-18
**Version:** 1.0
**Compatibility:** Unity 2021.3+
