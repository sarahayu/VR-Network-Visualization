# "Status Struggle" Demo — Setup

## Overview

A keyboard-driven showcase (`DemoSequence.cs`) that walks through the "Status
Struggle" case study: it isolates the school's top-15 aggressors inside a
force-directed working subgraph, then layers on findings step by step
(friendship-degree comparison, gender-by-shape encoding, and the aggressors'
friendship ties) as you press Enter. Two standalone keys (`1`/`2`) additionally
demo blue-saturation degree encoding on their own.

No VR headset or Neo4j connection is required to run it — it's driven entirely
by keyboard input and reads all its data from the already-loaded network file.

---

## Setup

### 1. Add the component to the scene

`DemoSequence` is not attached to any GameObject in the scene by default.

1. In the **Main Network Scene**, create an empty GameObject (e.g. name it
   `Demo Sequence`).
2. Add the `DemoSequence` component to it.
3. Leave **Network Manager** and **Database** empty in the Inspector — `Start()`
   resolves them automatically via `GameObject.Find("/Network Manager")` and
   `GameObject.Find("/Database")`, both of which already exist at the scene
   root. Only assign them manually if you've renamed those objects.
4. (Optional but recommended) Assign a `TextMeshProUGUI` to **Step Label** —
   e.g. a world-space or screen-space Text (TMP) element — so the current
   step and its finding ("Top-15 avg friends: 5.3 vs school-wide: 4.0", etc.)
   are visible in-headset, not just in the console. The demo runs fine
   without one; step text just won't be displayed anywhere but `Debug.Log`.

### 2. Confirm the dataset

The demo needs a dataset whose links are typed `"aggression"` /
`"friendship"` and whose nodes have a `"sex"` property (`"male"`/`"female"`) —
this is what step 1's aggressor ranking and step 2's shape encoding read.

Check the **Network Manager → File Loader → Dataset Name** field in the
Inspector. As of this writing it's set to `school_18_merged`, which has both.
Not every bundled dataset does — for example `school_16_wave_4` (used
elsewhere in the scene) has neither `type` nor `sex` populated, and the demo
will silently show 0 aggressors / no shape changes if you point it at that
one. To check any dataset yourself:

```python
import json
with open("Assets/StreamingAssets/<name>-layout.json-spherical.json") as f:
    d = json.load(f)
print({l.get("props", {}).get("type") for l in d["links"]})
print({n.get("props", {}).get("sex") for n in d["nodes"]})
```

You want to see `{"aggression", "friendship"}` and `{"male", "female"}` (a
stray `None` alongside them is fine — not every node needs a value).

### 3. (Optional) Materials for transparency

Step 1 fades out non-aggressor/non-neighbor nodes by swapping them onto a
runtime-generated transparent material clone
(`NetworkManager.SetMLNodesTransparent`). This works with the default node
material out of the box — no manual material setup needed.

---

## Controls

| Key         | Effect |
|-------------|--------|
| `B`         | Start/restart the demo — wipes any session, opens a fresh working subgraph over the whole network in force-directed layout. |
| `Enter`     | Advance to the next step (3 steps total after `B`). |
| `1`         | Standalone demo — new subgraph, all nodes colored by **friendship** degree (blue saturation). |
| `2`         | Standalone demo — new subgraph, all nodes colored by **aggression** degree (blue saturation). |

### The 3 `Enter` steps after `B`

1. **Top-15 aggressors → red.** Selects the 15 nodes with the most outgoing
   aggression links, colors them vivid red, and logs their average friendship
   degree against the school-wide average. Every other node fades to a
   transparent ghost gray; links not touching the top-15 (or their direct
   neighbors) fade to near-invisible.
2. **Gender shapes + aggression links.** Encodes sex as node shape (sphere =
   girl, cube = boy) and colors the top-15's outgoing aggression links orange
   at full opacity.
3. **Friendship links → green.** Colors the top-15's friendship links green,
   layered on top of the still-visible orange aggression links.

---

## Troubleshooting

- **"Top-15 avg friends: 0.0 vs school-wide: 0.0" / no red nodes** — the
  active dataset has no links typed `"aggression"`/`"friendship"`. See
  Setup step 2.
- **No shape change on Enter 2** — the dataset has no `"sex"` property on its
  nodes. Console will log `sex values in file: [...]`; an empty list confirms
  this.
- **Nothing happens on any key** — check the console for
  `[DemoSequence] Press B first.` (you pressed Enter before B) or confirm the
  GameObject with `DemoSequence` is active in the scene.
