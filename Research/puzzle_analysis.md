# Gridlocked — Puzzle Analysis Research

## Purpose

This document records structured observations about individual puzzles to identify what graph properties correlate with felt difficulty, deception, and satisfaction. Fill out one entry per puzzle investigated. Keep observations honest and immediate — first impressions before you know the solution are more useful than post-hoc rationalisations.

See `findings.md` for patterns observed across puzzles. See `PuzzleMetricsLogger.cs` for the instrumentation that generates the quantitative fields.

---

## Metrics reference

| Field | Description |
|---|---|
| `total_states` | Total reachable board states from the starting position |
| `solution_length` | Shortest path (in moves) to solution |
| `solution_paths` | Number of distinct shortest solution paths (DP on BFS DAG) |
| `diameter` | Max BFS depth reached from start (longest shortest path in graph) |
| `deception_ratio` | `total_states / solution_length` — how large the state space is relative to solution length |
| `branching_factor_avg` | Average number of valid moves available across all states |
| `branching_factor_max` | Max moves available from any single state |
| `dead_end_count` | States from which the goal is **unreachable** (reverse-BFS from goal) |
| `dead_end_ratio` | `dead_end_count / total_states` — fraction of the graph that is a trap |
| `bottleneck_count` | Number of states with `in_degree >= 3` and `out_degree <= 2` — candidate aha moments |
| `aha_depth_min` | BFS depth of the shallowest bottleneck node |
| `aha_depth_avg` | Average BFS depth across all bottleneck nodes |
| `cluster_count` | Number of disconnected subgraphs (should be 1 for a valid puzzle) |

---

## Puzzle entry template

Copy this block for each puzzle investigated.

```
---

## Puzzle [ID / Name]

**Source:** [Generated / Curated Level N / Hand-crafted]
**Date investigated:** YYYY-MM-DD

### Quantitative metrics
<!-- Copy from console log or CSV after running PuzzleMetricsLogger.Analyse() -->
- total_states:
- solution_length:
- solution_paths:
- diameter:
- deception_ratio:
- branching_factor_avg:
- branching_factor_max:
- dead_end_count:
- dead_end_ratio:
- bottleneck_count:
- aha_depth_min:
- aha_depth_avg:
- cluster_count:

### Graph capture
- Screen recording: [filename or path]
- Key screenshot (bottleneck): [filename or path]
- Key screenshot (solution path highlighted): [filename or path]

### Qualitative — solve first, then fill this out
**First impression of the board (before attempting):**

**Where did I get stuck (describe the moment):**

**What was the aha moment (which car, which move):**

**How long did it take (rough):**

### Graph shape
<!-- Pick the closest description, add notes -->
- [ ] Tree-like — few branches, one clear path
- [ ] Wide and flat — many states at similar depth, lots of options
- [ ] Bottlenecked — dense clusters connected by narrow bridges
- [ ] Deceptive — large state space, short solution buried inside
- [ ] Other:

### Ratings (1–5, gut feel)
- Deception (felt harder than it was):
- Satisfaction on solve:
- Clarity of aha moment:
- Would include in a curated level set (y/n):

### Notes

```

---

## Investigated puzzles

<!-- Paste filled entries below this line -->

---

## Quick reference table

Update this table as entries are added. Useful for spotting patterns at a glance.

| ID | States | Sol len | Sol paths | Diam | Deception ratio | Dead end % | Bottlenecks | Aha depth | Shape | Deception | Satisfaction |
|---|---|---|---|---|---|---|---|---|---|---|---|
| | | | | | | | | | | | |
