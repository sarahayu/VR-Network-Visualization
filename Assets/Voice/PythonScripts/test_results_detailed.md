# LangGraph Server - Comprehensive Test Report

**Generated:** 2025-12-18 02:30:37
**Total Tests:** 10
**Passed:** 10 ✓
**Failed:** 0 ✗
**Success Rate:** 100.0%

---

## Test 1: Server Health Check

**Status:** ✓ PASSED
**Endpoint:** `GET /ping`
**HTTP Status:** 200
**Response:** `Server alive!`

---

## Test 2: Simple Node Selection

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "select all female students"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `select all female students`
- **Corrected Input:** `select all female nodes`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "selectNode",
    "n.sex = 'female'"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `selectNode`
- **Parameter:** `n.sex = 'female'`
- **Purpose:** Select nodes matching condition
- **Database Query:** Will execute Cypher query to find matching nodes

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) WHERE n.sex = 'female' RETURN n
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 2.311s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `cypher_agent` | 0.804s | 34.8% |
| `action_agent` | 0.602s | 26.1% |
| `preprocess_agent` | 0.594s | 25.7% |
| `general_agent` | 0.311s | 13.4% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "selectNode",
      "n.sex = 'female'"
    ]
  ],
  "clarify": "",
  "corrected_input": "select all female nodes",
  "input": "select all female nodes",
  "original_input": "select all female students",
  "queries": [
    "MATCH (n:Node) WHERE n.sex = 'female' RETURN n"
  ],
  "timings": {
    "action_agent": 0.6020984649658203,
    "cypher_agent": 0.8044755458831787,
    "general_agent": 0.31061816215515137,
    "preprocess_agent": 0.593801736831665,
    "return_code": 7.152557373046875e-07
  }
}
```

</details>

---

## Test 3: Color Selected Nodes

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "color the selected nodes red"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `color the selected nodes red`
- **Corrected Input:** `color the selected nodes #FF0000`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "colorNode",
    "#FF0000"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `colorNode`
- **Parameter:** `#FF0000`
- **Purpose:** Change color of selected nodes
- **Color:** #FF0000

### Generated Cypher Queries

#### Query 1
```cypher
RETURN ""
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 2.784s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `preprocess_agent` | 1.269s | 45.6% |
| `action_agent` | 0.616s | 22.1% |
| `cypher_agent` | 0.492s | 17.7% |
| `general_agent` | 0.407s | 14.6% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "colorNode",
      "#FF0000"
    ]
  ],
  "clarify": "",
  "corrected_input": "color the selected nodes #FF0000",
  "input": "color the selected nodes #FF0000",
  "original_input": "color the selected nodes red",
  "queries": [
    "RETURN \"\""
  ],
  "timings": {
    "action_agent": 0.6162724494934082,
    "cypher_agent": 0.4924027919769287,
    "general_agent": 0.4070751667022705,
    "preprocess_agent": 1.2685811519622803,
    "return_code": 7.152557373046875e-07
  }
}
```

</details>

---

## Test 4: Multi-Step: Select + Color

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "select grade 9 students and color them blue"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `select grade 9 students and color them blue`
- **Corrected Input:** `select grade 9 students and color them #0000FF`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "selectNode",
    "n.grade = 9"
  ],
  [
    "colorNode",
    "#0000FF"
  ]
]
```

**Action Count:** 2

### Action Breakdown

#### Action 1: `selectNode`
- **Parameter:** `n.grade = 9`
- **Purpose:** Select nodes matching condition
- **Database Query:** Will execute Cypher query to find matching nodes

#### Action 2: `colorNode`
- **Parameter:** `#0000FF`
- **Purpose:** Change color of selected nodes
- **Color:** #0000FF

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) WHERE n.grade = 9 RETURN n
```

#### Query 2
```cypher
RETURN ""
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 4.524s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `cypher_agent` | 1.337s | 29.6% |
| `preprocess_agent` | 1.223s | 27.0% |
| `action_agent` | 1.093s | 24.2% |
| `general_agent` | 0.871s | 19.3% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "selectNode",
      "n.grade = 9"
    ],
    [
      "colorNode",
      "#0000FF"
    ]
  ],
  "clarify": "",
  "corrected_input": "select grade 9 students and color them #0000FF",
  "input": "select grade 9 students and color them #0000FF",
  "original_input": "select grade 9 students and color them blue",
  "queries": [
    "MATCH (n:Node) WHERE n.grade = 9 RETURN n",
    "RETURN \"\""
  ],
  "timings": {
    "action_agent": 1.093193531036377,
    "cypher_agent": 1.3372204303741455,
    "general_agent": 0.8712458610534668,
    "preprocess_agent": 1.2226228713989258,
    "return_code": 9.5367431640625e-07
  }
}
```

</details>

---

## Test 5: Size All Nodes (Default)

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "size nodes by GPA"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `size nodes by GPA`
- **Corrected Input:** `resize nodes by GPA`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "sizeNode",
    "gpa:all"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `sizeNode`
- **Parameter:** `gpa:all`
- **Purpose:** Size nodes by attribute `gpa`
- **Scope:** All nodes

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 3.347s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `preprocess_agent` | 1.711s | 51.1% |
| `action_agent` | 0.714s | 21.3% |
| `cypher_agent` | 0.613s | 18.3% |
| `general_agent` | 0.309s | 9.2% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "sizeNode",
      "gpa:all"
    ]
  ],
  "clarify": "",
  "corrected_input": "resize nodes by GPA",
  "input": "resize nodes by GPA",
  "original_input": "size nodes by GPA",
  "queries": [
    "MATCH (n:Node) RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue"
  ],
  "timings": {
    "action_agent": 0.7138679027557373,
    "cypher_agent": 0.6134657859802246,
    "general_agent": 0.3091118335723877,
    "preprocess_agent": 1.710967779159546,
    "return_code": 9.5367431640625e-07
  }
}
```

</details>

---

## Test 6: Size Selected Nodes Only

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "size selected nodes by GPA"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `size selected nodes by GPA`
- **Corrected Input:** `resize selected nodes by GPA`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "sizeNode",
    "gpa:selected"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `sizeNode`
- **Parameter:** `gpa:selected`
- **Purpose:** Size nodes by attribute `gpa`
- **Scope:** Selected nodes only

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) WHERE n.selected = true RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 3.923s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `preprocess_agent` | 1.675s | 42.7% |
| `cypher_agent` | 1.346s | 34.3% |
| `action_agent` | 0.635s | 16.2% |
| `general_agent` | 0.267s | 6.8% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "sizeNode",
      "gpa:selected"
    ]
  ],
  "clarify": "",
  "corrected_input": "resize selected nodes by GPA",
  "input": "resize selected nodes by GPA",
  "original_input": "size selected nodes by GPA",
  "queries": [
    "MATCH (n:Node) WHERE n.selected = true RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue"
  ],
  "timings": {
    "action_agent": 0.6354615688323975,
    "cypher_agent": 1.3460731506347656,
    "general_agent": 0.26720166206359863,
    "preprocess_agent": 1.6745975017547607,
    "return_code": 9.5367431640625e-07
  }
}
```

</details>

---

## Test 7: Voice Error Correction

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "select the notes with blew caller"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `select the notes with blew caller`
- **Corrected Input:** `select the nodes with blue color`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "selectNode",
    "n.color = '#0000FF'"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `selectNode`
- **Parameter:** `n.color = '#0000FF'`
- **Purpose:** Select nodes matching condition
- **Database Query:** Will execute Cypher query to find matching nodes

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) WHERE n.color = '#0000FF' RETURN n
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 4.208s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `preprocess_agent` | 1.887s | 44.8% |
| `cypher_agent` | 1.010s | 24.0% |
| `action_agent` | 0.927s | 22.0% |
| `general_agent` | 0.384s | 9.1% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "selectNode",
      "n.color = '#0000FF'"
    ]
  ],
  "clarify": "",
  "corrected_input": "select the nodes with blue color",
  "input": "select the nodes with blue color",
  "original_input": "select the notes with blew caller",
  "queries": [
    "MATCH (n:Node) WHERE n.color = '#0000FF' RETURN n"
  ],
  "timings": {
    "action_agent": 0.9268219470977783,
    "cypher_agent": 1.0100438594818115,
    "general_agent": 0.38405728340148926,
    "preprocess_agent": 1.8866586685180664,
    "return_code": 1.1920928955078125e-06
  }
}
```

</details>

---

## Test 8: Move Nodes

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "move selected nodes here"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `move selected nodes here`
- **Corrected Input:** `move selected nodes here`
- **Changes Made:** None (input was clear)

### Generated Actions
```json
[
  [
    "move",
    "here"
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `move`
- **Parameter:** `here`
- **Purpose:** Move selected nodes

### Generated Cypher Queries

#### Query 1
```cypher
RETURN ""
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 4.915s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `preprocess_agent` | 3.185s | 64.8% |
| `cypher_agent` | 0.740s | 15.1% |
| `action_agent` | 0.627s | 12.8% |
| `general_agent` | 0.363s | 7.4% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "move",
      "here"
    ]
  ],
  "clarify": "",
  "corrected_input": "move selected nodes here",
  "input": "move selected nodes here",
  "original_input": "move selected nodes here",
  "queries": [
    "RETURN \"\""
  ],
  "timings": {
    "action_agent": 0.627016544342041,
    "cypher_agent": 0.7399120330810547,
    "general_agent": 0.36286258697509766,
    "preprocess_agent": 3.1848652362823486,
    "return_code": 7.152557373046875e-07
  }
}
```

</details>

---

## Test 9: Deselect All

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "deselect all"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `deselect all`
- **Corrected Input:** `deselect all`
- **Changes Made:** None (input was clear)

### Generated Actions
```json
[
  [
    "deselect",
    ""
  ]
]
```

**Action Count:** 1

### Action Breakdown

#### Action 1: `deselect`
- **Parameter:** ``
- **Purpose:** Clear current selection

### Generated Cypher Queries

#### Query 1
```cypher
RETURN ""
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 4.804s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `general_agent` | 2.105s | 43.8% |
| `preprocess_agent` | 1.782s | 37.1% |
| `action_agent` | 0.490s | 10.2% |
| `cypher_agent` | 0.428s | 8.9% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "deselect",
      ""
    ]
  ],
  "clarify": "",
  "corrected_input": "deselect all",
  "input": "deselect all",
  "original_input": "deselect all",
  "queries": [
    "RETURN \"\""
  ],
  "timings": {
    "action_agent": 0.4897489547729492,
    "cypher_agent": 0.42821812629699707,
    "general_agent": 2.1045076847076416,
    "preprocess_agent": 1.7819948196411133,
    "return_code": 4.76837158203125e-07
  }
}
```

</details>

---

## Test 10: Complex: Select + Size + Color

**Status:** ✓ PASSED
**Endpoint:** `POST /classify`

### Request
```json
{
  "userText": "select female students and size them by GPA and color them purple"
}
```

**HTTP Status:** 200

### Voice Correction
- **Original Input:** `select female students and size them by GPA and color them purple`
- **Corrected Input:** `select female students and size them by GPA and color them #800080`
- **Changes Made:** Voice recognition errors corrected

### Generated Actions
```json
[
  [
    "selectNode",
    "n.sex = 'female'"
  ],
  [
    "sizeNode",
    "gpa:selected"
  ],
  [
    "colorNode",
    "#800080"
  ]
]
```

**Action Count:** 3

### Action Breakdown

#### Action 1: `selectNode`
- **Parameter:** `n.sex = 'female'`
- **Purpose:** Select nodes matching condition
- **Database Query:** Will execute Cypher query to find matching nodes

#### Action 2: `sizeNode`
- **Parameter:** `gpa:selected`
- **Purpose:** Size nodes by attribute `gpa`
- **Scope:** Selected nodes only

#### Action 3: `colorNode`
- **Parameter:** `#800080`
- **Purpose:** Change color of selected nodes
- **Color:** #800080

### Generated Cypher Queries

#### Query 1
```cypher
MATCH (n:Node) WHERE n.sex = 'female' RETURN n
```

#### Query 2
```cypher
MATCH (n:Node) WHERE n.selected = true RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue
```

#### Query 3
```cypher
RETURN ""
```

### Clarification
No clarification needed - command was understood.

### Performance Timings
**Total Processing Time:** 8.235s

| Agent | Duration | Percentage |
|-------|----------|------------|
| `cypher_agent` | 4.811s | 58.4% |
| `preprocess_agent` | 1.965s | 23.9% |
| `action_agent` | 1.217s | 14.8% |
| `general_agent` | 0.242s | 2.9% |
| `return_code` | 0.000s | 0.0% |

### Raw Response
<details>
<summary>Click to expand full JSON response</summary>

```json
{
  "actions": [
    [
      "selectNode",
      "n.sex = 'female'"
    ],
    [
      "sizeNode",
      "gpa:selected"
    ],
    [
      "colorNode",
      "#800080"
    ]
  ],
  "clarify": "",
  "corrected_input": "select female students and size them by GPA and color them #800080",
  "input": "select female students and size them by GPA and color them #800080",
  "original_input": "select female students and size them by GPA and color them purple",
  "queries": [
    "MATCH (n:Node) WHERE n.sex = 'female' RETURN n",
    "MATCH (n:Node) WHERE n.selected = true RETURN min(n.gpa) AS minValue, max(n.gpa) AS maxValue",
    "RETURN \"\""
  ],
  "timings": {
    "action_agent": 1.2165570259094238,
    "cypher_agent": 4.811455011367798,
    "general_agent": 0.24236774444580078,
    "preprocess_agent": 1.965000867843628,
    "return_code": 4.76837158203125e-07
  }
}
```

</details>

---

## Summary Statistics

### Overall Performance

### Average Agent Performance
| Agent | Average Duration | Sample Size |
|-------|------------------|-------------|
| `action_agent` | 0.769s | 9 tests |
| `cypher_agent` | 1.287s | 9 tests |
| `general_agent` | 0.584s | 9 tests |
| `preprocess_agent` | 1.699s | 9 tests |
| `return_code` | 0.000s | 9 tests |

### Features Validated

- ✓ **colorNode**: Node color modification
- ✓ **deselect**: Clear selections
- ✓ **move**: Spatial repositioning
- ✓ **selectNode**: Node selection with conditions
- ✓ **sizeNode**: Node size encoding by attribute

### Voice Corrections Detected

- `select all female students` → `select all female nodes`
- `color the selected nodes red` → `color the selected nodes #FF0000`
- `select grade 9 students and color them blue` → `select grade 9 students and color them #0000FF`
- `size nodes by GPA` → `resize nodes by GPA`
- `size selected nodes by GPA` → `resize selected nodes by GPA`
- `select the notes with blew caller` → `select the nodes with blue color`
- `select female students and size them by GPA and color them purple` → `select female students and size them by GPA and color them #800080`


---

## Conclusion

**All tests passed successfully!** ✓

The LangGraph server is functioning correctly and ready for Unity integration.
