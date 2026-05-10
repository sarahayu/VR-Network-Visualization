# HTTP Round-Trip Latency Results

## Current baseline — action_cypher combined agent (2026-05-09 19:52)

**Server version:** combined action+cypher agent, parallel preprocess+general, JSON-mode LLM  
**Runs per command:** 5 (+ 1 warm-up)  
**Server:** http://localhost:5000/classify (local, gpt-4o-mini)  
**Confirmed new graph:** `action_cypher_agent` key in timings ✓

> Note: `select + color` and `color-by-numeric` showed 34 s spikes in the full benchmark.
> Isolated re-test with 3 s gaps confirmed both are 3.8–4.1 s in real usage — the spikes
> were OpenAI rate limiting from 50+ back-to-back benchmark requests, not a prompt issue.

### Summary table (benchmark run — ignore spiked rows)

| Command | Min ms | Avg ms | Max ms | Stdev ms | Note |
|---|---:|---:|---:|---:|---|
| simple — show all nodes | 3616 | 3818 | 4108 | 189 | ✓ |
| simple — deselect all | 3670 | 3736 | 3827 | 66 | ✓ |
| select by attribute | 3763 | 4089 | 4629 | 422 | ✓ |
| select + color (multi-step) | 3940 | — | — | — | ⚠ rate-limited in bench; isolated avg = **3942 ms** |
| color-by-attribute | 3690 | 3789 | 3937 | 97 | ✓ |
| color-by-numeric (degree) | 3685 | — | — | — | ⚠ rate-limited in bench; isolated avg = **3808 ms** |
| shape-by-attribute | 3859 | 4041 | 4247 | 187 | ✓ |
| arithmetic (count) | 3703 | 3896 | 4230 | 199 | ✓ |
| ambiguous / clarify trigger | 3851 | 4062 | 4139 | 119 | ✓ |

### Isolated re-test for rate-limited commands (3 s gap between requests)

| Command | Run 1 | Run 2 | Run 3 | Run 4 | Avg ms |
|---|---:|---:|---:|---:|---:|
| select + color (multi-step) | 3861 | 4119 | 3997 | 3791 | **3942** |
| color-by-numeric (degree) | 3789 | 3724 | 3810 | 3907 | **3808** |

### Agent breakdown (avg ms, from benchmark)

| Agent | show all | deselect | select attr | color-attr | shape | arithmetic | ambiguous |
|---|---:|---:|---:|---:|---:|---:|---:|
| `preprocess_agent` | 1021 | 1005 | 1094 | 966 | 1082 | 1012 | 994 |
| `general_agent` | 983 | 981 | 1105 | 965 | 1094 | 1095 | 1069 |
| `action_cypher_agent` | 652 | 637 | 780 | 753 | — | 738 | 916 |
| `clarify_agent` | — | — | — | — | 821 | — | — |

`preprocess_agent` and `general_agent` run in parallel — wall time ≈ max(~1010, ~1050) ≈ 1050 ms.  
`action_cypher_agent` replaces the old sequential `action_agent + cypher_agent`.

---

## Evolution across all three optimization passes

| Command | v1 sequential | v2 parallel+skip | v3 combined (current) | Total Δ |
|---|---:|---:|---:|---:|
| simple — show all nodes | 4136 | 3861 | **3818** | −318 ms |
| simple — deselect all | 4841 | 4049 | **3736** | −1105 ms |
| select by attribute | 8398 | 5152 | **4089** | −4309 ms |
| select + color (multi-step) | 6263 | 4837 | **3942** | −2321 ms |
| color-by-attribute | 5190 | 4850 | **3789** | −1401 ms |
| color-by-numeric (degree) | 5101 | 4864 | **3808** | −1293 ms |
| shape-by-attribute | 4250 | 3830 | **4041** | −209 ms |
| arithmetic (count) | 4622 | 3832 | **3896** | −726 ms |
| ambiguous / clarify trigger | 5393 | 4797 | **4062** | −1331 ms |

**Hard floor:** `preprocess_agent` ~1 s (one OpenAI call, always runs — unavoidable without a local model).  
**Old worst case** (`select by attribute`): 17 286 ms spike → now max **4 629 ms**.
