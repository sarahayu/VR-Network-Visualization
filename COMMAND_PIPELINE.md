# Command Pipeline — VR Network Visualization

This document covers the full lifecycle of a user command: from voice or text input to a rendered network change. It is intended for engineers joining the project.

---

## Architecture Overview

```
[User]
  │
  ├─ Push-to-talk (XR controller) ──► Whisper STT ──► recognized text ─┐
  ├─ Text field (keyboard / desktop)                                     ├──► ClassifyUserCommand()
  └─ Keyboard shortcuts (KeyboardCommandTester, testing only) ───────────┘
                                                                          │
                                                      HTTP POST localhost:5000/classify
                                                                          │
                                                               LangGraph server
                                                     (parallel preprocess+general → action+cypher)
                                                                          │
                                                         ClassificationResponse (JSON)
                                                                          │
                                                              switch(action[i][0])
                                                                          │
                                         ┌──────────────────────────────────────────────┐
                                         │  DatabaseStorage (Neo4j)  │  NetworkManager   │
                                         └──────────────────────────────────────────────┘
                                                                          │
                                                         UpdateRenderElements() → GPU
```

---

## Latency Profile

Each stage is logged to the Unity Console with `[Label]` tags. To see timings, open the Console window.

| Stage | Label in Console | Measured range | Notes |
|---|---|---|---|
| Whisper STT | logged inline as `whisper:Xs` | 0.5 – 3 s | Not benchmarked here; depends on utterance length and CPU |
| HTTP to LangGraph | `[HTTP/LangGraph]` | **2 – 4 s** | Parallel preprocess+general stage + combined action+cypher call |
| LangGraph preprocess agent | `Timing - Preprocess Agent:` | **~0.5 – 1 s** | Runs in parallel with general_agent; benefits from OpenAI prompt cache |
| LangGraph general agent | `Timing - General Agent:` | **0.3 – 0.8 s** | Parallel with preprocess — wall time = max of both |
| LangGraph action+cypher agent | `Timing - action_cypher_agent:` | **0.4 – 0.7 s** | Combined single call (JSON mode); replaced old sequential action → cypher chain |
| GetNodesFromStore (all 416 nodes) | `[SetSelectedNodes]` | **18 – 23 ms** | Full node scan over bolt |
| GetNodesFromStore (filtered WHERE) | `[SetSelectedNodes]` | **7 ms** | Indexed WHERE clause, very fast |
| GetLinksFromStore (528 links) | `[SetSelectedLinks]` | **21 – 22 ms** | |
| GetMinMaxFromStore | — | **5 – 6 ms** | Aggregation query |
| GetDistinctValuesFromStore | — | **5 ms** | DISTINCT on 416 rows |
| GetNodesGroupedByAttribute (sex/grade) | `[ColorByAttribute]` etc. | **11 – 17 ms** | Single query, all categories in one round-trip |
| GetNodesWithNumericValues (degree) | `[ColorByGPA]` etc. | **10 – 12 ms** | Single query, all values in one round-trip |
| Total end-to-end | `[TotalPipeline]` | **2.5 – 7 s** | Dominated by LangGraph. DB is <25 ms per call. |

**Dominant latency source is the LangGraph server** (2–4 s), not the database. The previous N-per-category Neo4j bottleneck has been eliminated — `colorByAttribute`, `shapeByAttribute`, `colorByGPA`, and `colorByValue` all now use a single query with client-side grouping, cached for the session lifetime.

**Note:** `KeyboardCommandTester.cs` (dev-only) still uses the old N-query pattern for `colorByAttribute`, `shapeByAttribute`, `colorByGPA`, and `colorByValue`. If those code paths become performance-sensitive, apply the same `GetNodesGroupedByAttribute` / `GetNodesWithNumericValues` pattern from `StreamingSampleMic`.

---

## Entry Points

### `StreamingSampleMic.cs` — Voice + Text commands
Located at `Assets/Voice/Scripts/StreamingSampleMic.cs`.

**Push-to-talk (VR):**
```
Update() detects CommandPress.ReadWasPerformedThisFrame()
→ _stream.StartStream()
→ OnSegmentFinished() fires when Whisper recognizes text
→ StartCoroutine(ClassifyUserCommand(text, whisperTime))
```

**Text field:**
```
OnTextCommandSubmit(inputText)
→ StartCoroutine(ClassifyUserCommand(inputText, 0f))
```

### `KeyboardCommandTester.cs` — Desktop testing only
Located at `Assets/Voice/Scripts/KeyboardCommandTester.cs`. Maps keyboard keys to canned commands and demo sequences. Also exposes an HTTP listener on port 5001 for external command injection. **Not active in VR builds.**

---

## Keyboard Shortcuts (Desktop / Editor only)

All shortcuts are active when `KeyboardCommandTester` is in the scene. Key bindings are configurable via Inspector fields under **Keyboard Shortcuts** and **Text Input**.

### Individual Command Shortcuts

| Key | Inspector field | Action | What it does |
|---|---|---|---|
| `1` | `sizeByDegreeKey` | Size by grade | Select all nodes → `sizeNode grade:all` |
| `2` | `sizeByGPAKey` | Size by GPA | Select all nodes → `sizeNode gpa:all` |
| `3` | `colorByGradeKey` | Color by grade | `colorByAttribute grade` |
| `4` | `colorBySexKey` | Color by sex | `colorByAttribute sex` |
| `G` | `colorByGPAKey` | Color by GPA | `colorByGPA` (blue gradient, 5 buckets) |
| `5` | `shapeByGradeKey` | Shape by grade | `shapeByAttribute grade` |
| `6` | `selectFemaleKey` | Select female nodes | `selectNode` where `n.sex = 'F'` |
| `7` | `deselectAllKey` | Deselect all | `deselect` |
| `8` | `colorSelectedRedKey` | Color selected red | `colorNode #FF0000` on selected nodes |
| `9` | `moveSelectedKey` | Move selected | `move` on selected nodes |
| `R` | `resetKey` | Reset all | `NetworkManager.ResetAll()` — clears all colors, selections, annotations |
| `H` | — | Help | Prints full shortcut list to Unity Console |
| `T` | `submitCommandKey` | Submit typed command | Sends the text in the **Test Command** Inspector field to the LangGraph server |

> Shortcuts `1`–`9`, `G`, `5` bypass the LangGraph server and execute actions directly. `T` and demo keys **P/L/O** go through the server (natural language → classification → execution). If the server is offline, toggle **Server Enabled = false** in the Inspector to run hardcoded fallback actions instead.

---

### Demo Sequences

Demo sequences are step-by-step walkthroughs of the dataset. Press the key to start, then **Enter** to advance to the next step. All demos reset the legend at step 0 and create a working session with all nodes before executing.

#### Step-by-step demos (press Enter to advance)

**`P` — Friendship highlight demo** (3 steps)
1. Select top-3 nodes with most friendship links → color them red
2. Color all nodes by grade (categorical palette)
3. Color aggression links of selected nodes red

**`L` — Smoker + aggression demo** (2 steps)
1. Select smoker nodes → color purple → deselect
2. Color all links gray, then color smoker aggression links pink, smoker friendship links green

**`O` — Gender + aggression targets demo** (3 steps)
1. Color nodes by sex (categorical)
2. Select top-3 nodes with most incoming aggression links → color them red
3. Color friendship links of selected nodes blue

**`K` — Smoker steel blue + aggression orange demo** (2 steps)
1. Select smoker nodes → color steel blue (`#4682B4`) → deselect
2. Color all links dark gray → color smoker aggression links light orange (`#FFA040`). Also selects those aggression links in the working subgraph so selection-blend color is visible.

**`Z` — Smoker purple + aggression pink + friendship green demo** (2 steps)
1. Select smoker nodes → color purple (`#800080`) → deselect
2. Color smoker aggression links pink (`#FF69B4`), smoker friendship links green (`#00FF00`)

**`D` — Smoker step-by-step with camera movement** (4 steps, Escape to exit)
- Interactive demo with keyboard-controlled camera movement while walking through smoker analysis steps

#### Continuous demos (auto-advance, no Enter needed)

**`Q` — Continuous friendship + GPA + aggression** (3 steps, ~1.5 s between steps)
1. Highlight top-3 friendship nodes in gold
2. Color all nodes by GPA (blue gradient)
3. Color all aggression links red

**`W` — Continuous friendship + shape + aggression** (3 steps, ~1.5 s between steps)
1. Highlight top-5 friendship nodes in red
2. Shape all nodes by sex (sphere/cube/tetrahedron)
3. Color selected nodes' aggression links red

**`E` — Continuous two-session demo** (2 steps, ~1.5 s between)
1. Session 1: Highlight top-3 friendship nodes gold
2. Session 2: Create a **new** session with only smoker nodes, make them larger (`demoENodeSize`, default 1.5×), color them dodger blue (`#1E90FF`), color all links white, select all links in the subgraph

**`S` — Continuous smoker analysis** (2 steps, ~1.5 s between steps)
1. Color smokers steel blue, deselect
2. Dim all links dark gray → highlight smokers' aggression links vivid orange (`#FF5500`)

---

### External HTTP Command Listener

`KeyboardCommandTester` starts an `HttpListener` on **port 5001** (configurable via `listenerPort`). This lets external scripts inject commands at runtime without keyboard interaction.

**POST** `http://localhost:5001/command`
```json
{ "userText": "color the smoker nodes in purple" }
```
Response: `{"status": "queued"}` — command is queued and processed on the next Unity frame.

**GET** `http://localhost:5001/ping`
Response: `{"status": "ready"}` — use to check if the listener is alive before sending commands.

Commands submitted via HTTP go through the LangGraph server (same path as `T` key). If the server is unavailable they will fail silently with a console error.

---

## `ClassifyUserCommand(recognizedText, whisperTime)` — The Main Dispatcher

A Unity coroutine that handles the complete pipeline after text is available.

```
1. loadingIcon.SetLoading(true)
2. Build ClassificationRequest JSON
3. HTTP POST to localhost:5000/classify
   └─ TimerUtils: [HTTP/LangGraph]
4. Parse ClassificationResponse
5. Log per-agent timings from classification.timings
6. Instantiate command text UI prefab
7. if (classification.clarify != null) → show retry message, return
8. else → for each action in classification.actions:
       switch (action[0]) → execute handler (see below)
9. Log [TotalPipeline] end-to-end time
10. loadingIcon.SetLoading(false)
```

### `ClassificationResponse` fields

| Field | Type | Description |
|---|---|---|
| `input` | `string` | Raw recognized text |
| `corrected_input` | `string` | LangGraph-corrected command text for display |
| `queries` | `string[]` | Cypher query per action |
| `actions` | `string[][]` | Each entry is `[actionName, param1, param2, ...]` |
| `clarify` | `string` | Non-null if the server needs clarification; blocks execution |
| `timings` | `Timing` | Per-agent timing breakdown (seconds) |

---

## Supported Commands (Action Handlers)

### `selectNode`
**Action params:** `[actionName, attributeDescription]`
**Query:** Cypher returning nodes matching a condition

Selects a subset of nodes. Creates a working session if none exists (or if `OnQueryMode` is active), then selects the queried nodes within it.

```
Timer: [SetSelectedNodes]
DB calls: 1 (+ 1 extra if no session exists)
```

---

### `sizeNode`
**Action params:** `["sizeNode", "attribute:scope"]` (e.g. `"gpa:all"`, `"gpa:selected"`)
**Query:** Cypher returning min/max values

Encodes node size linearly by a numeric attribute.

```
DB calls: 1 (GetMinMaxFromStore)
```

---

### `selectLink`
**Action params:** `["selectLink", linkType]`
**Query:** Cypher returning links

Selects a subset of links. If the query references `n.selected = true`, filters client-side using `WorkingSelectedNodeGUIDs` (because Unity selection state is not stored in Neo4j).

```
Timer: [SetSelectedLinks]
DB calls: 1 (+ 1 extra if filtering by selected nodes)
```

---

### `deselect`
**Action params:** `["deselect"]`

Clears all node/link selection state and the `_lastSelectedLinkGUIDs` buffer.

```
Timer: [Deselect Nodes]
DB calls: 0
```

---

### `move`
**Action params:** `["move"]`

Calls `BringMLNodes` on currently selected or all working-subgraph nodes to animate them to a new layout position.

```
Timer: [Move Nodes]
DB calls: 0
```

---

### `layout`
**Action params:** `["layout", layoutName]`

Changes the layout of the selected communities (e.g. `"cluster"`, `"spherical"`, `"hairball"`, `"floor"`).

```
Timer: [Layout Change]
DB calls: 0
```

---

### `colorNode`
**Action params:** `["colorNode", "#hexColor"]`

Colors selected nodes (or all nodes if none selected) with a flat hex color. Creates a working session with all nodes if none exists.

```
Timer: [SetColor]
DB calls: 0 (uses Unity-side selection state)
```

---

### `colorLink`
**Action params:** `["colorLink", "#hexColor"]`

Colors a previously selected set of links (from `_lastSelectedLinkGUIDs`). Falls back to selected-node links, then all subgraph links.

```
Timer: [SetColor]
DB calls: 0 (uses cached _lastSelectedLinkGUIDs)
```

---

### `colorByAttribute`
**Action params:** `["colorByAttribute", "attributeName", "category:color", ...]`
**Query:** Cypher returning distinct attribute values

Colors nodes categorically by a string attribute. Auto-assigns colors from a 4-color palette if not specified in action params.

```
Timer: [ColorByAttribute]
DB calls: 1 (GetNodesGroupedByAttribute — groups all nodes in one query, cached per session)
Previously N+1 round trips; now always 1 after the first call for a given attribute.
```

---

### `colorByGPA`
**Action params:** `["colorByGPA"]`

Linear gradient coloring by the `gpa` node attribute, divided into 5 equal buckets.

```
Timer: [ColorByGPA]
DB calls: 1 (GetNodesWithNumericValues — all GPA values in one query, cached per session)
Bucketing is done client-side. Previously 6 round trips; now always 1 after the first call.
```

---

### `colorByValue`
**Action params:** `["colorByValue", "attributeName"]`
**Query:** Cypher returning min/max values

Linear gradient coloring by any numeric attribute. Tries `SetMLNodeColorEncoding` first; falls back to 5-bucket queries.

```
Timer: [ColorByValue]
DB calls: 1 (GetMinMaxFromStore) for primary path (SetMLNodeColorEncoding)
Fallback bucket path: 1 (GetNodesWithNumericValues, cached) — client-side bucketing, previously 5 extra queries.
```

---

### `shapeByAttribute`
**Action params:** `["shapeByAttribute", "attributeName"]`
**Query:** Cypher returning distinct values

Assigns shapes (`sphere`, `cube`, `tetrahedron`) to nodes by categorical attribute. Max 3 shapes — cycles if more than 3 categories.

```
Timer: [ShapeByAttribute]
DB calls: 1 (GetNodesGroupedByAttribute — groups all nodes in one query, cached per session)
Previously N+1 round trips; now always 1 after the first call for a given attribute.
```

---

### `widthLink`
**Action params:** `["widthLink", "floatValue"]`

Sets the global link width for the current working subgraph (or main network if no session). The width value is parsed as a float using invariant culture.

```
DB calls: 0
Available in: KeyboardCommandTester only (not yet wired in StreamingSampleMic)
```

---

### `arithmetic`
**Action params:** `["arithmetic", description]`
**Query:** Cypher aggregation returning a single value

Runs a Cypher computation (e.g. average, count) and displays the result in the command log.

```
Timer: [Arithmetic Operation]
DB calls: 1
```

---

### `reset`
**Action params:** `["reset"]`

Resets all nodes to yellow, all links to gray, clears selection and legend. Creates a working session if none exists; calls `BringMLNodes` only if a new session was created.

```
Timer: [Reset]
DB calls: 0 (uses existing session) or 1 (if creating fresh session)
```

---

## How to Add a New Command

1. **Server side**: add a new action name to the LangGraph classification server. The server must return `actions: [["myNewCommand", "param1"]]` and a corresponding `queries: ["MATCH ..."]`.

2. **Client side** in `ClassifyUserCommand()` switch block, add:
   ```csharp
   case "myNewCommand":
       string param = action[i][1];
       TimerUtils.StartTime("MyNewCommand");
       // call _networkManager or _databaseStorage methods here
       TimerUtils.EndTime("MyNewCommand");
       break;
   ```

3. If the command needs a working session, call the helper at the top of the case:
   ```csharp
   if (!EnsureWorkingSession("My label", "My short label")) break;
   ```
   Pass `selectAll: true` if you also need all nodes to be marked as selected initially.

4. If the command needs previously selected links, use `_lastSelectedLinkGUIDs` (populated by `selectLink`).

5. If the command produces output for the command log, instantiate a prefab:
   ```csharp
   var entry = Instantiate(command_prefab, command_parent.transform);
   entry.GetComponent<TMP_Text>().text = "your output text";
   ScrollToBottom();
   ```

---

## Helper Methods

### `EnsureWorkingSession(label, shortLabel, selectAll = false) → bool`
Creates a working session with all nodes if one doesn't exist. Returns `false` if the main network is not found (caller should `break`). Pass `selectAll: true` to also mark all subgraph nodes as selected after creation.

### `ScrollToBottom()`
Forces canvas layout recalculation and scrolls the command log to the latest entry.

### `GetColorName(hexColor) → string`
Maps the 4 palette hex colors to human-readable names for legend display.

---

## Key Dependencies

| Symbol | Type | Role |
|---|---|---|
| `_networkManager` | `NetworkManager` | All Unity-side node/link/community state |
| `_databaseStorage` | `DatabaseStorage` | Neo4j Cypher query execution |
| `legendManager` | `LegendManager` | Updates the persistent legend panel |
| `loadingIcon` | `LoadingIcon` | Shows/hides loading spinner during HTTP wait |
| `_lastSelectedLinkGUIDs` | `HashSet<string>` | Buffer from last `selectLink` for `colorLink` |
| `_pipelineStartTime` | `float` | Wall time at command start for total timing |

---

## Performance Improvements (Implemented)

### Batched attribute queries
`DatabaseStorageUtils` has two new methods:

- **`GetNodesGroupedByAttribute(networkGlobal, attribute, driver)`** → `Dictionary<string, List<string>>`
  Single Cypher query: `MATCH (n:Node) WHERE n.{attr} IS NOT NULL RETURN n.GUID, n.{attr}`. Groups results client-side. Keys are Cypher-safe strings (same format as `GetDistinctValuesFromStore`). Used by `colorByAttribute` and `shapeByAttribute`.

- **`GetNodesWithNumericValues(networkGlobal, attribute, driver)`** → `Dictionary<string, float>`
  Single Cypher query returning all node GUIDs with their numeric attribute value. Client-side bucketing replaces N per-bucket queries. Used by `colorByGPA` and `colorByValue` fallback.

Both are exposed on `DatabaseStorage` as `GetNodesGroupedByAttribute(networkGlobal, attribute)` and `GetNodesWithNumericValues(networkGlobal, attribute)`.

### Session-long result cache
`StreamingSampleMic` holds two dictionaries keyed by attribute name:

```csharp
Dictionary<string, Dictionary<string, List<string>>> _groupedNodesCache   // categorical
Dictionary<string, Dictionary<string, float>>        _numericValuesCache   // numeric
```

Node-attribute relationships are static for the lifetime of the scene (nodes don't change), so no invalidation is needed. The second call to `colorByAttribute sex` or `colorByGPA` costs zero DB round trips.

### Round-trip reduction summary

| Command | Before | After |
|---|---|---|
| `colorByAttribute` (N categories) | N+1 queries | 1 query (cached: 0) |
| `shapeByAttribute` (N categories) | N+1 queries | 1 query (cached: 0) |
| `colorByGPA` | 6 queries | 1 query (cached: 0) |
| `colorByValue` fallback | 5 queries | 1 query (cached: 0) |

## Remaining Performance Opportunity

**Async Neo4j**: `DatabaseStorage` uses the Neo4j driver synchronously on the Unity main thread. Every DB call blocks rendering for its duration (5–23 ms per query on localhost). The Neo4j .NET driver supports `async/await` (`session.RunAsync`, `result.ToListAsync`). Moving queries off the main thread would eliminate frame hitches during command execution, at the cost of more complex coroutine/async interop in Unity.

**Whisper model size**: STT latency (0.5–3 s) depends heavily on the local Whisper model variant. Switching from a larger model to `whisper-base.en` or `whisper-small.en` can reduce recognition time to ~0.2–0.5 s for short voice commands, making it a comparable contributor to end-to-end latency alongside LangGraph.

---

## Latency Profiler

A runtime benchmark tool is available at `Assets/Scripts/utils/LatencyProfiler.cs`.

### Setup

1. Add a `LatencyProfiler` component to any GameObject in the scene.
2. Wire up the `databaseStorage` and `networkManager` Inspector fields to the live instances.
3. Optionally override the query strings and attribute names in Inspector to match your dataset.
4. Enter Play mode, ensure Neo4j and the LangGraph server are running.
5. **Press `I`** — results print to the Unity Console.

### What it measures

| Section | Method | Runs |
|---|---|---|
| DB — all nodes | `GetNodesFromStore` (full scan) | 3 |
| DB — filtered nodes | `GetNodesFromStore` (WHERE clause) | 3 |
| DB — all links | `GetLinksFromStore` | 3 |
| DB — min/max | `GetMinMaxFromStore` | 3 |
| DB — distinct values | `GetDistinctValuesFromStore` | 3 |
| DB — grouped (run 1) | `GetNodesGroupedByAttribute` | 3 |
| DB — grouped (run 2, repeat) | `GetNodesGroupedByAttribute` | 3 |
| DB — numeric values (run 1) | `GetNodesWithNumericValues` | 3 |
| DB — numeric values (run 2, repeat) | `GetNodesWithNumericValues` | 3 |
| Unity — translate GUIDs | `TranslateToWorkingSubgraphNodeGUIDs` | 3 |
| Unity — set node color | `SetMLNodesColor` | 3 |
| HTTP — simple select | `"show all nodes"` → LangGraph | 5 |
| HTTP — color-by-attribute | `"color nodes by major"` → LangGraph | 5 |
| HTTP — color-by-GPA | `"color nodes by GPA"` → LangGraph | 5 |
| HTTP — show links | `"show all links"` → LangGraph | 5 |

### Measured latency (2026-05-09, local server)

Measured by `tests/test_latency_neo4j.py` and `tests/test_latency_http.py` (5 runs each, 1 warm-up discarded). Dataset: 416 nodes, 528 links. Full raw data in `tests/results_neo4j.md` and `tests/results_http.md`.

#### Neo4j queries

| Query | Min ms | Avg ms | Max ms |
|---|---:|---:|---:|
| GetNodesFromStore — all 416 nodes | 18.4 | 19.7 | 22.6 |
| GetNodesFromStore — filtered (grade=9) | 7.0 | 7.1 | 7.4 |
| GetLinksFromStore — all 528 links | 21.1 | 21.9 | 22.4 |
| GetMinMaxFromStore — degree | 5.3 | 5.7 | 6.1 |
| GetDistinctValuesFromStore — grade | 5.0 | 5.3 | 5.5 |
| GetNodesGroupedByAttribute — sex | 11.6 | 13.3 | 15.7 |
| GetNodesGroupedByAttribute — grade (4 cats) | 12.1 | 14.5 | 17.2 |
| GetNodesWithNumericValues — degree | 10.8 | 11.4 | 12.3 |

All Neo4j queries complete in **5–23 ms** on localhost. The DB is not a bottleneck.

#### HTTP → LangGraph (localhost:5000/classify)

Three rounds of optimization were applied to the LangGraph graph (sequential → parallel stages → combined agent):

| Command type | Before avg ms | After avg ms | Δ | Notes |
|---|---:|---:|---:|---|
| Simple — show all nodes | 4136 | **~2200** | −47% | prompt cache warm; no Cypher needed |
| Simple — deselect all | 4841 | **~2100** | −57% | prompt cache warm; no Cypher needed |
| Select by attribute | 8398 | **~3200** | −62% | biggest win; combined action+cypher call |
| Select + color multi-step | 6263 | **~3000** | −52% | |
| Color-by-attribute | 5190 | **~2800** | −46% | |
| Color-by-numeric | 5101 | **~2900** | −43% | |
| Shape-by-attribute | 4250 | **~2400** | −43% | |
| Arithmetic | 4622 | **~2300** | −50% | no Cypher needed |
| Ambiguous (clarify) | 5393 | **~3500** | −35% | all agents run |

The `select by attribute` 17-second spike is gone. Worst-case is now under 4 s.

**Optimizations applied:**
1. `preprocess_agent` + `general_agent` run in parallel with `asyncio.gather` — saves ~0.5 s per request
2. `action_agent` + `cypher_agent` merged into a single JSON-mode call — eliminates one full API round-trip
3. Static system prompts use `SystemMessage` + `HumanMessage` split so OpenAI's prompt cache applies to the large rule sections after warm-up

**Remaining opportunity**: moving Neo4j queries off the Unity main thread with `async/await` would eliminate frame hitches during command execution (~5–23 ms currently blocks rendering).

## Jev changes

This section documents the changes to the original pipeline described above. Where the earlier sections differ, the behavior documented here applies to the Jev pipeline.

### Architecture and historical measurements

The `/classify` endpoint now uses the Jev server, which routes requests to the direct compiler, clarification, or a bounded planner. The old LangGraph graph remains available as legacy code.

The latency measurements and performance claims in the earlier sections predate Jev routing and planning. They are historical baselines, not latency promises for the new pipeline.

**Shared execution:** `KeyboardCommandTester` now delegates to `StreamingSampleMic.ExecuteActionSteps`, including the cached attribute-query path and automatic command feedback. This supersedes the earlier note about the desktop tester using the old N-query pattern.

### Automatic command feedback and sequential execution

Voice, typed commands, and desktop demos use `StreamingSampleMic.ExecuteActionSteps` inside the existing visual scheduler. The legacy `actions`/`queries` arrays are adapted into a `CommandExecutionReport`, available as `LastExecutionReport` and logged as JSON. No additional model call is needed for automatic feedback.

Each step has a command-local ID, preceding-step dependency, operation, parameter, query, status, and affected GUIDs. Supported selection/color operations also record matched counts, actual changes, graph scope, and denominator. Other operations currently return completion status without quantitative metrics. The legacy adapter executes sequentially alongside the typed planner described below. Neither path runs parallel agents.

- Node selection reports the resulting selection as a fraction of the working graph, with newly selected and deselected counts in the receipt.
- Node coloring (single color, attribute, GPA, or numeric value) counts distinct assigned nodes and compares their live colors before/after execution. Counts cover assignments actually performed, not missing-value guesses.
- Link coloring distinguishes matches from actual color changes. A selected single-node scope also reports distinct neighbors and the fraction of all incident links of the same direction in the working graph. Self-loops count once as links and do not count as neighbors; parallel relationships count separately as links.
- `n.selected = true` is resolved using current Unity selection, not Neo4j state. Incoming/outgoing relationships are respected. An explicit empty link query or empty node scope never falls back to all links.
- A failed step stops the remaining steps. Successful earlier receipts are retained; failures warn that changes may be partial. There is no rollback or retry implementation in this milestone.

`StatisticalReportDisplay` shows one combined command result, even when `stats` is empty. Explicitly requested statistics appear underneath. Versioning prevents older asynchronous reports from overwriting a newer command's display. The existing scene references are reused.

#### Verification

Run `dotnet run --project Tests/CommandFeedback/CommandFeedback.csproj` with .NET 9 or a compatible newer SDK. The standalone harness compiles the production receipt and display code against lightweight graph/UI fixtures; it covers counts, scope, direction, repeated coloring, empty queries, partial failure, and asynchronous display behavior. It does not replace Unity play-mode testing or a live Neo4j integration check.

For a live smoke check, run a node selection and aggression-link coloring command through both voice and desktop input. Repeat the color, try an isolated node, try incoming/outgoing links, and include a requested statistic. Confirm the report matches the graph and that unrelated links remain untouched after an empty query.

### Jev routing and bounded planning

`/classify` now accepts `userText`, `conversationId`, optional `clarificationId`, and an interaction `context`. Unity captures the dataset/session identity, scope node/link IDs, selected node IDs, available attributes and numeric attributes, relationship types, and supported direct operations. Large scope ID arrays remain execution data; Jev receives counts plus the selection and schema.

The response route is `direct`, `clarify`, `plan`, or `unsupported`:

- `direct` retains the legacy `actions`, `queries`, and `stats` response. The existing compiler and ASR correction remain in use.
- `clarify` returns the actual question and an opaque clarification ID. A reply continues the original request against its original snapshot. Pending state is isolated per conversation, bounded to 128 entries, and expires after 30 minutes. Say/type `cancel` to discard the pending request, or prefix an unrelated request with `new request:`. A changed graph requires a fresh request.
- `plan` returns a plan ID, overview, and typed steps with descriptions, arguments, dependencies, and named outputs. Both Python and Unity validate the entire plan before mutations. Unknown operations, forward references, wrong set types, malformed arguments, and excessive step counts are rejected.
- `unsupported` explains the missing capability and has no executable actions.

Supported planner operations:

| Operation | Input and result |
|---|---|
| `rank_nodes` | Rank a saved node set by incoming/outgoing/both relationship count; save top N |
| `filter_numeric` | Filter a saved node set by `gt/gte/lt/lte/eq` on a numeric attribute |
| `select_nodes` | Select an explicit saved node set |
| `find_links` | Save links of a specified type/direction touching a saved node set |
| `color_nodes`, `color_links` | Apply a validated hex color to a saved set |
| `compare_numeric` | Compare a saved node set with its complement in the original scope |

`scope` and `selection` are immutable starting sets. Each other input references a prior named output and declares a dependency on its producing step. No arbitrary generated Cypher or code is accepted in a plan. Relationship and numeric reads use existing database APIs and are cached within the plan. Ranking counts unique relationship IDs (parallel links remain separate); ties break by ascending application node ID, and zero-degree nodes remain eligible. Numeric comparisons report totals, valid/missing counts, means, and signed mean differences. Missing or non-finite values are excluded; empty groups produce `no data`, never zero. These are descriptive comparisons, not significance tests.

Voice, the text field, desktop server commands, and the HTTP testing bridge share `SubmitCommand` and the same response handler. Supported plans execute internally without a plan preview or step-by-step narration; the user receives the final execution report. If the planner proposes a different supported analysis, its explanation and proposed outline are returned through the clarification route for acceptance before execution. The saved clarification token preserves the proposal and graph snapshot for the reply. Saved sets are not recomputed from live selection, so hand selection changes cannot redirect later steps. A graph/session change aborts the plan.

Say or type `cancel` (also `stop`, `cancel plan`, or `stop analysis`) to stop at the next step boundary. The current step finishes and completed changes remain. Cancellation bypasses the normal command scheduler. Failure stops later steps, retains successful receipts, and does not roll back earlier effects.

After execution, `/summarize` receives the actual receipts and numerical evidence. Jev chooses up to three verified result sentences to emphasize; it cannot author new numbers or claims. Failures/cancellations are always retained. The Unity report remains available if summarization times out or fails, and command IDs prevent stale explanations from being appended to another command's report.

#### Configuration

Existing API-key setup is unchanged. Optional variables (also in `example.env`):

```
JEV_FAST_MODEL=gpt-4o-mini
JEV_PLANNER_MODEL=gpt-4o
JEV_MAX_STEPS=8
```

Model names are configurable rather than an assertion about current model availability. Use chat models compatible with the existing JSON-mode integration. The server caps the limit at 12; the Unity `maxPlanSteps` Inspector setting also defaults to 8, and the lower limit wins. No new dependencies are required. Restart the Python server after updating the code/environment and allow Unity to recompile.

#### Testing the new workflow

Example: **“Select the five nodes with the most outgoing aggression links, color those links red, and compare their average GPA with the remaining nodes.”** The plan should rank, select, find links, color, and compare. The original snapshot defines both the ranked population and comparison remainder.

Automated checks:

- `python -m unittest discover -s Tests -p 'test_jev*.py' -v`: routing, clarification, invalid plans, and evidence-constrained summaries. Flask contract checks require the project's server environment; all model calls are mocked and no API key is read from `.env`.
- `dotnet run --project Tests/CommandFeedback/CommandFeedback.csproj`: production plan validator, executor, statistics, and display logic compiled against lightweight graph/UI/database fixtures. Covers saved-set stability, ties, missing values, empty sets, cancellation, late failures, repeated coloring, and graph changes.

These tests do not validate live model routing quality or headset interaction. In Unity Play mode with Neo4j and the restarted server, run the example by typing and voice, change selection during the plan, cancel between steps, and inspect the report. Parallel agents, significance tests, automatic retries, rollback, and open-ended replanning remain outside this milestone.
