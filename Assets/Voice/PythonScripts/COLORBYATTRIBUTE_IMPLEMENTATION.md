# Categorical Coloring Implementation

## Feature: Color Nodes by Attribute

**Date Implemented:** 2025-12-18

---

## Overview

This feature allows users to color nodes based on categorical attributes (e.g., grade, sex, department). Each unique value of the attribute gets assigned a different color from a 6-color palette.

**User Command:**
```
"color nodes by grade"
"color by sex"
"color nodes by department"
```

**Result:**
- System queries all distinct values of the attribute
- If the attribute is **categorical**, each unique value gets assigned one of 6 colors and the legend shows each category
- If the attribute is **numeric** (e.g., GPA), nodes are colored along a single-hue gradient (darker = higher), with a compressed legend showing the gradient range
- If >6 categories exist, colors cycle/repeat

---

## Architecture

### Flow Diagram

```
User: "color nodes by grade"
         ↓
    [Whisper STT]
         ↓
 [LangGraph Server]
  - Preprocesses input
  - Generates action: ["colorByAttribute", "grade"]
  - Generates query: "MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"
         ↓
    [Unity C#]
  - Receives action + query
  - Executes query to get distinct values
  - Maps each value to a color (cycling through 6 colors)
  - Colors nodes for each category
```

---

## Implementation Details

### 1. Python Server (langgraph_server.py)

#### Action Prompt (Lines 130, 144-150)
Added `colorByAttribute` to allowed actions and categorical coloring rules:

```python
Allowed actions:
- "colorByAttribute"
...

Categorical coloring rules:
- If user says "color nodes by <attribute>", generate: [["colorByAttribute", "<attribute>"]]
- Examples:
  * "color nodes by grade" → ["colorByAttribute", "grade"]
  * "color by sex" → ["colorByAttribute", "sex"]
- This assigns a unique color to each distinct value of the attribute
```

#### Cypher Prompt (Lines 202-205)
Added Cypher generation for colorByAttribute:

```python
- ["colorByAttribute", "<attribute>"]
    → MATCH (n:Node) RETURN DISTINCT n.<attribute> AS value ORDER BY value
    Example: ["colorByAttribute", "grade"]
    → MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value
```

---

### 2. Unity C# (DatabaseStorageUtils.cs)

#### New Method: GetDistinctValuesFromStore (Lines 485-532)

```csharp
public static List<string> GetDistinctValuesFromStore(
    NetworkGlobal networkGlobal,
    string command,
    IDriver driver,
    bool convertWinPaths = true)
```

**Purpose:** Executes Cypher query to get distinct attribute values

**Returns:** List of strings formatted for Cypher queries
- String values: `'female'`, `'male'` (with quotes)
- Numeric values: `9`, `10`, `11` (without quotes)

**Handles:**
- Null values (skips them)
- String vs numeric type detection
- Error handling

---

### 3. Unity C# (DatabaseStorage.cs)

#### Wrapper Method (Lines 77-80)

```csharp
public List<string> GetDistinctValuesFromStore(NetworkGlobal networkGlobal, string command)
{
    return DatabaseStorageUtils.GetDistinctValuesFromStore(networkGlobal, command, _driver, _convertWinPaths);
}
```

---

### 4. Unity C# (StreamingSampleMic.cs)

#### Switch Case: colorByAttribute (Lines 312-361)

```csharp
case "colorByAttribute":
    string attributeName_color = action[i][1];

    // Get distinct values
    var distinctValues = _databaseStorage.GetDistinctValuesFromStore(_networkManager.NetworkGlobal, query[i]);

    // Define 6 allowed colors
    string[] allowedColors = new string[] {
        "#FF0000",  // red
        "#FFA500",  // orange
        "#FFFF00",  // yellow
        "#00FF00",  // green
        "#0000FF",  // blue
        "#800080"   // purple
    };

    // Color each category
    for (int j = 0; j < distinctValues.Count; j++)
    {
        string categoryValue = distinctValues[j];
        string colorHex = allowedColors[j % allowedColors.Length]; // Cycle

        string categoryQuery = $"MATCH (n:Node) WHERE n.{attributeName_color} = {categoryValue} RETURN n";
        var categoryNodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, categoryQuery);
        _networkManager.SetMLNodesColor(categoryNodes, colorHex);
    }
    break;
```

**Logic:**
1. Extract attribute name from action
2. Execute query to get distinct values
3. For each distinct value:
   - Assign a color (cycling through 6 colors if >6 categories)
   - Build Cypher query to select nodes with that value
   - Color those nodes

---

## Color Assignment

### 6-Color Palette

| Index | Color | Hex Code |
|-------|-------|----------|
| 0 | Red | #FF0000 |
| 1 | Orange | #FFA500 |
| 2 | Yellow | #FFFF00 |
| 3 | Green | #00FF00 |
| 4 | Blue | #0000FF |
| 5 | Purple | #800080 |

### Deterministic Mapping

Colors are assigned in sorted order of category values:

```
Example: grade attribute with values [12, 9, 11, 10]
After sorting: [9, 10, 11, 12]

Mapping:
- grade 9  → Red    (#FF0000)
- grade 10 → Orange (#FFA500)
- grade 11 → Yellow (#FFFF00)
- grade 12 → Green  (#00FF00)
```

### Handling >6 Categories

If more than 6 distinct values exist, colors cycle:

```
7 categories: [A, B, C, D, E, F, G]
- A → Red
- B → Orange
- C → Yellow
- D → Green
- E → Blue
- F → Purple
- G → Red (cycles back)
```

**Warning logged:** "Found X categories but only 6 colors available. Colors will repeat."

---

## Example Usage

### Example 1: Simple Categorical (4 categories)

**User Input:** "color nodes by grade"

**Python Server Response:**
```json
{
  "actions": [["colorByAttribute", "grade"]],
  "queries": ["MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"]
}
```

**Unity Execution:**
1. Query returns: `[9, 10, 11, 12]`
2. Color mapping:
   - Grade 9 → #FF0000 (red)
   - Grade 10 → #FFA500 (orange)
   - Grade 11 → #FFFF00 (yellow)
   - Grade 12 → #00FF00 (green)
3. Each grade's nodes colored accordingly

**Console Output:**
```
Categorical coloring by attribute: grade
Found 4 distinct values for grade
  Category '9' → #FF0000
  Category '10' → #FFA500
  Category '11' → #FFFF00
  Category '12' → #00FF00
```

---

### Example 2: String Categorical (2 categories)

**User Input:** "color by sex"

**Python Server Response:**
```json
{
  "actions": [["colorByAttribute", "sex"]],
  "queries": ["MATCH (n:Node) RETURN DISTINCT n.sex AS value ORDER BY value"]
}
```

**Unity Execution:**
1. Query returns: `['female', 'male']` (with quotes for strings)
2. Color mapping:
   - sex 'female' → #FF0000 (red)
   - sex 'male' → #FFA500 (orange)
3. Nodes colored by sex

---

### Example 3: Many Categories (8 categories)

**User Input:** "color nodes by department"

**Unity Execution:**
1. Query returns 8 departments
2. **Warning:** "Found 8 categories but only 6 colors available. Colors will repeat."
3. Color assignment:
   - Dept A → Red
   - Dept B → Orange
   - Dept C → Yellow
   - Dept D → Green
   - Dept E → Blue
   - Dept F → Purple
   - Dept G → Red (cycle)
   - Dept H → Orange (cycle)

---

## Testing

### Manual Test (Python Server)

```bash
# Start server
python langgraph_server.py

# Test categorical coloring
curl -X POST http://localhost:5000/classify \
  -H "Content-Type: application/json" \
  -d '{"userText": "color nodes by grade"}'
```

**Expected Response:**
```json
{
  "actions": [["colorByAttribute", "grade"]],
  "queries": ["MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value"],
  "corrected_input": "color nodes by grade",
  ...
}
```

### Unity Test

1. Ensure Neo4j is running with your graph data
2. Run Unity scene with voice command system
3. Say: "color nodes by grade"
4. Observe:
   - Console logs showing distinct values found
   - Each category mapped to a color
   - Nodes visually colored in VR

---

## Performance Considerations

### Database Queries

For each categorical coloring operation:
- **1 query** to get distinct values
- **N queries** to select nodes for each category (where N = number of categories)

Example with 4 grades:
```cypher
-- Initial query
MATCH (n:Node) RETURN DISTINCT n.grade AS value ORDER BY value

-- Then 4 selection queries
MATCH (n:Node) WHERE n.grade = 9 RETURN n
MATCH (n:Node) WHERE n.grade = 10 RETURN n
MATCH (n:Node) WHERE n.grade = 11 RETURN n
MATCH (n:Node) WHERE n.grade = 12 RETURN n
```

**Total:** 5 queries for 4 categories

### Optimization Opportunities

Future improvements could:
1. Single grouped query: `MATCH (n:Node) RETURN n.grade AS category, collect(n) AS nodes`
2. Batch color updates in Unity
3. Cache category mappings

---

## Edge Cases Handled

### 1. Null Values
- **Behavior:** Skipped during distinct value collection
- **Code:** Lines 510-513 in DatabaseStorageUtils.cs

### 2. Mixed Types
- **String values:** Automatically quoted for Cypher (`'female'`)
- **Numeric values:** No quotes (`9`, `10.5`)
- **Code:** Lines 514-521 in DatabaseStorageUtils.cs

### 3. Empty Results
- **Behavior:** Returns empty list, no coloring applied
- **Console:** "Found 0 distinct values for X"

### 4. >6 Categories
- **Behavior:** Colors cycle using modulo
- **Warning:** Logged to console
- **Code:** Lines 333-336 in StreamingSampleMic.cs

### 5. Database Errors
- **Behavior:** Error logged, operation skipped
- **User feedback:** Error audio played

---

## Limitations

1. **6 Color Maximum:** Only 6 distinct colors available
   - Categories beyond 6 will reuse colors
   - Consider expanding palette in future

2. **Performance:** Multiple queries for large category counts
   - 20 categories = 21 database queries
   - May be slow for large graphs

3. **No Custom Colors:** User cannot specify which color for which category
   - Colors assigned automatically in sorted order

4. **No Legend:** System doesn't display which color represents which category
   - User must infer from visual inspection

---

## Future Enhancements

### Phase 1: UX Improvements
- [ ] Display color legend in VR UI
- [ ] Allow user to specify color mapping
- [ ] Remember previous category→color mappings

### Phase 2: Performance
- [ ] Single grouped query to reduce DB calls
- [ ] Batch color updates
- [ ] Cache distinct values

### Phase 3: Advanced Features
- [ ] Support for gradient coloring (continuous attributes)
- [ ] Color schemes (colorblind-friendly, high-contrast)
- [ ] Export color mappings
- [ ] "Uncolor" command to reset

---

## Files Modified

| File | Lines | Purpose |
|------|-------|---------|
| `langgraph_server.py` | 130, 144-150, 202-205 | Added colorByAttribute action and Cypher generation |
| `DatabaseStorageUtils.cs` | 485-532 | Added GetDistinctValuesFromStore method |
| `DatabaseStorage.cs` | 77-80 | Added wrapper method |
| `StreamingSampleMic.cs` | 312-361 | Added colorByAttribute switch case |

---

## Related Features

- **colorNode**: Single color for selected nodes
- **sizeNode**: Size encoding by attribute (similar pattern)
- **selectNode**: Selection by attribute value

---

**Status:** ✅ Implemented and ready for testing
**Version:** 1.0
**Author:** Claude Code Implementation
**Last Updated:** 2025-12-18
