"""
LangGraph HTTP round-trip latency benchmark.

Sends real classify requests to localhost:5000 and records wall-clock time
from request dispatch to first byte of response — matching what Unity's
UnityWebRequest measures in the pipeline.

Usage:
    python tests/test_latency_http.py

langgraph_server.py must be running.
"""

import time
import statistics
import requests
from datetime import datetime

SERVER_URL = "http://localhost:5000/classify"
RUNS       = 5   # requests per command type

# Commands that exercise different LangGraph agent paths
COMMANDS = [
    ("simple — show all nodes",      "show all nodes"),
    ("simple — deselect all",        "deselect all"),
    ("select by attribute",          "select all female students"),
    ("select + color (multi-step)",  "select grade 9 students and color them blue"),
    ("color-by-attribute",           "color nodes by grade"),
    ("color-by-numeric (degree)",    "color nodes by degree"),
    ("shape-by-attribute",           "shape nodes by sex"),
    ("arithmetic (count)",           "how many smoker nodes are there"),
    ("ambiguous / clarify trigger",  "make it bigger"),
]


def classify(text):
    """POST to /classify, return elapsed seconds and parsed JSON."""
    t0 = time.perf_counter()
    resp = requests.post(
        SERVER_URL,
        json={"userText": text},
        headers={"Content-Type": "application/json"},
        timeout=60,
    )
    elapsed = time.perf_counter() - t0
    resp.raise_for_status()
    return elapsed, resp.json()


def benchmark_all():
    # ping first
    try:
        pong = requests.get("http://localhost:5000/ping", timeout=5)
        print(f"Server: {pong.text.strip()}\n")
    except Exception as e:
        print(f"[ERROR] Server unreachable: {e}")
        raise

    results = []
    print(f"Running {RUNS} requests per command...\n")

    for label, text in COMMANDS:
        # warm-up
        try:
            classify(text)
        except Exception:
            pass

        samples = []
        agent_totals = {}
        last_response = None

        for _ in range(RUNS):
            try:
                elapsed, data = classify(text)
                samples.append(elapsed * 1000)  # → ms
                last_response = data
                if data.get("timings"):
                    for k, v in data["timings"].items():
                        agent_totals.setdefault(k, []).append(v * 1000)
            except Exception as ex:
                samples.append(None)
                print(f"  [WARN] {label}: {ex}")

        valid = [s for s in samples if s is not None]
        if not valid:
            print(f"  {label}: ALL FAILED")
            continue

        result = {
            "label":        label,
            "text":         text,
            "min":          min(valid),
            "avg":          statistics.mean(valid),
            "max":          max(valid),
            "stdev":        statistics.stdev(valid) if len(valid) > 1 else 0,
            "samples":      samples,
            "agent_avgs":   {k: statistics.mean(v) for k, v in agent_totals.items()},
            "actions":      last_response.get("actions", []) if last_response else [],
            "clarify":      last_response.get("clarify", "") if last_response else "",
        }
        results.append(result)

        print(f"  {label}")
        print(f"    min={result['min']:.0f}ms  avg={result['avg']:.0f}ms  "
              f"max={result['max']:.0f}ms  stdev={result['stdev']:.0f}ms")
        if result["agent_avgs"]:
            breakdown = "  |  ".join(
                f"{k}: {v:.0f}ms" for k, v in sorted(result["agent_avgs"].items())
            )
            print(f"    agents → {breakdown}")

    return results


def print_table(results):
    COL = 40
    print()
    print("=" * 80)
    print("  HTTP ROUND-TRIP LATENCY  (localhost:5000/classify)")
    print("=" * 80)
    print(f"  {'Command':<{COL}} {'Min ms':>8} {'Avg ms':>8} {'Max ms':>8}")
    print(f"  {'-'*COL} {'--------':>8} {'--------':>8} {'--------':>8}")
    for r in results:
        lbl = r["label"] if len(r["label"]) <= COL else r["label"][:COL-1] + "…"
        print(f"  {lbl:<{COL}} {r['min']:>8.0f} {r['avg']:>8.0f} {r['max']:>8.0f}")
    print("=" * 80)
    print()


def write_results_md(results):
    import os
    out_path = os.path.join(os.path.dirname(__file__), "results_http.md")
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    lines = [
        f"# HTTP Round-Trip Latency Results",
        f"",
        f"**Date:** {ts}  ",
        f"**Runs per command:** {RUNS} (+ 1 warm-up run discarded)  ",
        f"**Server:** http://localhost:5000/classify (local)  ",
        f"",
        f"| Command | Min ms | Avg ms | Max ms | Stdev ms |",
        f"|---|---:|---:|---:|---:|",
    ]
    for r in results:
        lines.append(
            f"| {r['label']} | {r['min']:.0f} | {r['avg']:.0f} | {r['max']:.0f} | {r['stdev']:.0f} |"
        )

    lines += ["", "## LangGraph agent breakdown (avg ms across runs)", ""]
    for r in results:
        if r["agent_avgs"]:
            lines.append(f"### {r['label']}")
            lines.append("")
            lines.append("| Agent | Avg ms |")
            lines.append("|---|---:|")
            for k, v in sorted(r["agent_avgs"].items()):
                lines.append(f"| `{k}` | {v:.0f} |")
            lines.append("")

    lines += [
        "## Raw samples (ms per run)",
        "",
        "| Command | " + " | ".join(f"Run {i+1}" for i in range(RUNS)) + " |",
        "|---|" + "|".join(["---:"] * RUNS) + "|",
    ]
    for r in results:
        row = " | ".join(f"{s:.0f}" if s is not None else "ERR" for s in r["samples"])
        lines.append(f"| {r['label']} | {row} |")

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"Results written to: {out_path}")
    return out_path


if __name__ == "__main__":
    results = benchmark_all()
    print_table(results)
    write_results_md(results)
