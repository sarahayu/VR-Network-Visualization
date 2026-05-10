"""
Neo4j query latency benchmark.

Mirrors every DatabaseStorage method used by StreamingSampleMic so the
numbers here map 1-to-1 to the Unity-side timings.

Usage:
    python tests/test_latency_neo4j.py

Neo4j must be running on localhost:7687 with the dataset already loaded.
"""

import time
import statistics
from neo4j import GraphDatabase

# ── connection ────────────────────────────────────────────────────────────────
URI      = "bolt://localhost:7687"
AUTH     = ("neo4j", "neoneoneo")
RUNS     = 5   # how many times to repeat each query

# ── queries (matching DatabaseStorageUtils.cs patterns) ──────────────────────
QUERIES = [
    # label, cypher, method_in_code
    (
        "GetNodesFromStore — all nodes",
        "MATCH (n:Node) RETURN n.GUID AS guid",
        "GetNodesFromStore (full scan)"
    ),
    (
        "GetNodesFromStore — filtered (grade=9)",
        "MATCH (n:Node) WHERE n.grade = 9 RETURN n.GUID AS guid",
        "GetNodesFromStore (WHERE clause)"
    ),
    (
        "GetLinksFromStore — all POINTS_TO",
        "MATCH ()-[r:POINTS_TO]->() RETURN r.GUID AS guid",
        "GetLinksFromStore"
    ),
    (
        "GetMinMaxFromStore — degree",
        "MATCH (n:Node) WHERE n.degree IS NOT NULL "
        "RETURN min(n.degree) AS minValue, max(n.degree) AS maxValue",
        "GetMinMaxFromStore"
    ),
    (
        "GetDistinctValuesFromStore — grade",
        "MATCH (n:Node) WHERE n.grade IS NOT NULL "
        "RETURN DISTINCT n.grade AS value",
        "GetDistinctValuesFromStore"
    ),
    (
        "GetNodesGroupedByAttribute — sex (run 1)",
        "MATCH (n:Node) WHERE n.sex IS NOT NULL "
        "RETURN n.GUID AS guid, n.sex AS value",
        "GetNodesGroupedByAttribute [sex]"
    ),
    (
        "GetNodesGroupedByAttribute — sex (run 2 / repeat)",
        "MATCH (n:Node) WHERE n.sex IS NOT NULL "
        "RETURN n.GUID AS guid, n.sex AS value",
        "GetNodesGroupedByAttribute [sex] repeat"
    ),
    (
        "GetNodesGroupedByAttribute — grade (4 categories)",
        "MATCH (n:Node) WHERE n.grade IS NOT NULL "
        "RETURN n.GUID AS guid, n.grade AS value",
        "GetNodesGroupedByAttribute [grade]"
    ),
    (
        "GetNodesWithNumericValues — degree (run 1)",
        "MATCH (n:Node) WHERE n.degree IS NOT NULL "
        "RETURN n.GUID AS guid, n.degree AS value",
        "GetNodesWithNumericValues [degree]"
    ),
    (
        "GetNodesWithNumericValues — degree (run 2 / repeat)",
        "MATCH (n:Node) WHERE n.degree IS NOT NULL "
        "RETURN n.GUID AS guid, n.degree AS value",
        "GetNodesWithNumericValues [degree] repeat"
    ),
]


def run_query(session, cypher):
    """Execute query and consume all results. Returns elapsed seconds."""
    t0 = time.perf_counter()
    result = session.run(cypher)
    # Force full result consumption (mirrors .ToList() / foreach in C#)
    _ = list(result)
    return time.perf_counter() - t0


def benchmark_all():
    driver = GraphDatabase.driver(URI, auth=AUTH)
    results = []

    print(f"Neo4j connected. Running {RUNS} runs per query...\n")

    with driver.session() as session:
        # warm-up: run each query once without recording
        for _, cypher, _ in QUERIES:
            run_query(session, cypher)

        for label, cypher, method in QUERIES:
            samples = []
            for _ in range(RUNS):
                elapsed = run_query(session, cypher)
                samples.append(elapsed * 1000)  # → ms

            results.append({
                "label":  label,
                "method": method,
                "min":    min(samples),
                "avg":    statistics.mean(samples),
                "max":    max(samples),
                "stdev":  statistics.stdev(samples) if len(samples) > 1 else 0,
                "samples": samples,
            })
            print(f"  {label}")
            print(f"    min={min(samples):.1f}ms  avg={statistics.mean(samples):.1f}ms  "
                  f"max={max(samples):.1f}ms  stdev={statistics.stdev(samples):.1f}ms")

    driver.close()
    return results


def print_table(results):
    COL = 48
    print()
    print("=" * 80)
    print("  NEO4J QUERY LATENCY  (local bolt://localhost:7687, 416 nodes / 528 links)")
    print("=" * 80)
    print(f"  {'Query':<{COL}} {'Min ms':>8} {'Avg ms':>8} {'Max ms':>8}")
    print(f"  {'-'*COL} {'--------':>8} {'--------':>8} {'--------':>8}")
    for r in results:
        lbl = r["label"] if len(r["label"]) <= COL else r["label"][:COL-1] + "…"
        print(f"  {lbl:<{COL}} {r['min']:>8.1f} {r['avg']:>8.1f} {r['max']:>8.1f}")
    print("=" * 80)
    print()


def write_results_md(results):
    import os
    from datetime import datetime

    out_path = os.path.join(os.path.dirname(__file__), "results_neo4j.md")
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    lines = [
        f"# Neo4j Query Latency Results",
        f"",
        f"**Date:** {ts}  ",
        f"**Runs per query:** {RUNS} (+ 1 warm-up run discarded)  ",
        f"**Dataset:** 416 nodes, 528 POINTS\\_TO links  ",
        f"**Connection:** bolt://localhost:7687 (local)  ",
        f"",
        f"| Query | Min ms | Avg ms | Max ms | Stdev ms |",
        f"|---|---:|---:|---:|---:|",
    ]
    for r in results:
        lines.append(
            f"| {r['label']} | {r['min']:.1f} | {r['avg']:.1f} | {r['max']:.1f} | {r['stdev']:.1f} |"
        )

    lines += [
        "",
        "## Raw samples (ms per run)",
        "",
        "| Query | " + " | ".join(f"Run {i+1}" for i in range(RUNS)) + " |",
        "|---|" + "|".join(["---:"] * RUNS) + "|",
    ]
    for r in results:
        row = " | ".join(f"{s:.1f}" for s in r["samples"])
        lines.append(f"| {r['label']} | {row} |")

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"Results written to: {out_path}")
    return out_path


if __name__ == "__main__":
    results = benchmark_all()
    print_table(results)
    write_results_md(results)
