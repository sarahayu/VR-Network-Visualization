# Neo4j Query Latency Results

**Date:** 2026-05-09 18:48:04  
**Runs per query:** 5 (+ 1 warm-up run discarded)  
**Dataset:** 416 nodes, 528 POINTS\_TO links  
**Connection:** bolt://localhost:7687 (local)  

| Query | Min ms | Avg ms | Max ms | Stdev ms |
|---|---:|---:|---:|---:|
| GetNodesFromStore — all nodes | 18.4 | 19.7 | 22.6 | 1.7 |
| GetNodesFromStore — filtered (grade=9) | 7.0 | 7.1 | 7.4 | 0.2 |
| GetLinksFromStore — all POINTS_TO | 21.1 | 21.9 | 22.4 | 0.5 |
| GetMinMaxFromStore — degree | 5.3 | 5.7 | 6.1 | 0.3 |
| GetDistinctValuesFromStore — grade | 5.0 | 5.3 | 5.5 | 0.2 |
| GetNodesGroupedByAttribute — sex (run 1) | 11.6 | 13.3 | 15.7 | 2.1 |
| GetNodesGroupedByAttribute — sex (run 2 / repeat) | 14.3 | 18.5 | 28.3 | 6.0 |
| GetNodesGroupedByAttribute — grade (4 categories) | 12.1 | 14.5 | 17.2 | 2.1 |
| GetNodesWithNumericValues — degree (run 1) | 10.8 | 11.4 | 12.3 | 0.6 |
| GetNodesWithNumericValues — degree (run 2 / repeat) | 9.9 | 12.5 | 19.0 | 3.7 |

## Raw samples (ms per run)

| Query | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |
|---|---:|---:|---:|---:|---:|
| GetNodesFromStore — all nodes | 19.6 | 18.5 | 22.6 | 18.4 | 19.5 |
| GetNodesFromStore — filtered (grade=9) | 7.2 | 7.0 | 7.0 | 7.4 | 7.0 |
| GetLinksFromStore — all POINTS_TO | 22.1 | 21.9 | 22.0 | 22.4 | 21.1 |
| GetMinMaxFromStore — degree | 5.6 | 5.3 | 6.1 | 5.7 | 6.0 |
| GetDistinctValuesFromStore — grade | 5.5 | 5.2 | 5.4 | 5.4 | 5.0 |
| GetNodesGroupedByAttribute — sex (run 1) | 11.7 | 11.9 | 11.6 | 15.5 | 15.7 |
| GetNodesGroupedByAttribute — sex (run 2 / repeat) | 20.2 | 14.3 | 14.5 | 28.3 | 15.3 |
| GetNodesGroupedByAttribute — grade (4 categories) | 17.2 | 15.9 | 14.6 | 12.8 | 12.1 |
| GetNodesWithNumericValues — degree (run 1) | 11.2 | 12.3 | 10.8 | 11.5 | 11.4 |
| GetNodesWithNumericValues — degree (run 2 / repeat) | 10.6 | 9.9 | 11.3 | 19.0 | 11.4 |
