# Gridlocked — Research Findings

## Purpose

Running observations and emerging hypotheses about what makes puzzles feel hard, satisfying, or deceptive. Updated as patterns emerge from `puzzle_analysis.md`. This is the document that feeds the video narrative — keep it honest and update it when something surprises you.

---

## Core question

> What properties of a puzzle's state-space graph predict whether it *feels* hard or satisfying to solve — independent of its minimum solution length?

---

## Working hypotheses

Track hypotheses here as they form. Mark each as Supported / Refuted / Inconclusive as data accumulates.

### H1 — Deception ratio predicts felt difficulty better than solution length alone
**Status:** Untested  
**Rationale:** A puzzle with 15 moves but 80k states should feel harder than one with 15 moves and 3k states, because the solver spends more time in unproductive regions.  
**What would support it:** High correlation between `deception_ratio` and qualitative deception rating across 10+ puzzles.  
**What would refute it:** Puzzles with high deception ratio consistently feel easy, or solution length alone explains the ratings.

---

### H2 — Aha moments correspond to bottleneck nodes in the graph
**Status:** Untested  
**Rationale:** The subjective experience of "suddenly seeing it" should correspond to a node that many paths pass through — a state that, once reached, makes the solution obvious.  
**What would support it:** Qualitative aha moments (described car/move) match the `bottleneck_nodes` identified computationally.  
**What would refute it:** Aha moments occur at high-branching nodes, or there's no consistent graph signature.

---

### H3 — Satisfying puzzles have exactly one clear bottleneck, not zero or many
**Status:** Untested  
**Rationale:** Zero bottlenecks = no aha moment, feels mechanical. Many bottlenecks = confusing, feels arbitrary. One bottleneck = the "designed" feeling.  
**What would support it:** High satisfaction ratings cluster around `bottleneck_nodes = 1` or `2`.  
**What would refute it:** No relationship between bottleneck count and satisfaction.

---

### H4 — Graph shape (not just size) is the distinguishing feature between difficulty tiers
**Status:** Untested  
**Rationale:** Expert puzzles might not just have larger graphs — they might have structurally different graphs (more clusters, deeper bottlenecks, longer dead-end branches).  
**What would support it:** Beginner and Expert puzzles cluster into visually distinct graph shape categories.  
**What would refute it:** Graphs across difficulty tiers look qualitatively similar, only differ in scale.

---

## Emerging patterns

*Add observations here as entries accumulate in puzzle_analysis.md. Minimum 5 puzzles before drawing conclusions.*

---

## Surprising findings

*Things that contradicted intuition. These are gold for the video narrative.*

---

## Proposed metrics to implement next

*Based on gaps noticed during investigation.*

- [x] **Deception ratio** (`total_states / solution_length`) — logged
- [x] **Bottleneck count** — states with `in_degree >= 3` and `out_degree <= 2` — logged
- [x] **Dead-end ratio** (`dead_end_count / total_states`) — dead ends defined as states unreachable from goal (reverse-BFS), logged
- [x] **Aha depth** — min and avg BFS depth of bottleneck nodes — logged
- [ ] **Solution density** — requires counting all paths to goal; deferred (DFS approach was unreliable on large graphs)

---

## Visualization & analysis approaches

*The raw state-space graph is the wrong primitive. A force/depth layout of 10k–80k
nodes is a hairball — it shows that the space is big, but "big" isn't the thesis.
The thesis is deception, bottlenecks, and the shape that makes a puzzle feel
designed. These views encode those directly. All are computable from the existing
adjacency + BFS depths in `Analysis.cs` / `PuzzleMetricsLogger.cs`.*

### 1. Mobility / progress profile along the solution path  ★ primary
Walk the optimal path; at each step plot two curves:
- **Mobility** = legal moves available (or # of pieces that can move)
- **Productive** = how many of those moves *reduce* distance-to-goal

A ~15-point, 2-line chart. The gap between the curves **is** deception — steps with
many moves but few productive ones are the "feels hard but is short" beats. A
bottleneck shows as mobility collapsing to 1–2 then re-opening. Encodes **H1**
(deception) and **H2** (aha moment) on one screen. This is the per-puzzle "fun
signature."

### 2. Pruned solution subgraph / k-shortest paths  ★ primary
Keep the graph form but filter for legibility:
- **Solution DAG**: edges where `depth[v] == depth[u]+1` AND both endpoints are
  forward-reachable from start ∩ backward-reachable from goal. Often 100s of nodes
  vs 80k.
- **k-shortest distinct paths**: render the top-k optimal/near-optimal routes
  (shortest-path count already computed via BFS-DAG DP).
- **Optimal+slack ball**: states reachable to goal within `optimal + N` moves
  (reuses the reverse-reachability from the dead-end metric).

The faithful "it's still a graph" view, small enough to actually read. Shows
solution multiplicity → **H3**.

### 3. Distance-to-goal reachability profile  ★ free
Bar/line of "states at each distance from goal" (already have `depthDistribution`).
Long thin neck vs fat blob looks completely different → difficulty-tier shape (**H4**).

### 4. Coarsened skeleton via bottlenecks  ◦ stretch
Contract dense regions between articulation/bottleneck nodes into super-nodes;
render a ~10–20 node skeleton of clusters joined by bridges. Literally a picture of
**H3** ("one clear bottleneck = designed feeling") — the puzzle's "chapters."
Needs articulation-point detection + contraction.

### 5. Dimensionality reduction (UMAP/PCA)  ◦ hero image only
Each state = car-position vector; embed to 2D, color by distance-to-goal. Pretty
"landscape" of basins/clusters. **Caveat:** DR distorts edges and implies adjacency
that isn't there — eye-candy for a title shot, not evidence. UMAP > t-SNE (global
structure); PCA is honest but usually too blobby.

**Recommendation:** build #1 + #2 + #3 (carry the whole argument); save #4/#5 as
stretch/hero visuals. The deeper finding: plot these signatures across many puzzles
and check whether *fun-rated* ones cluster (e.g. a single sharp mobility trough) —
turns the visuals from illustration into an actual result.

---

## Video narrative notes

*Observations that feel like good story beats. Copy to the narrative brief when ready.*

---

## References

- Fogleman (2018) — complete Rush Hour database via simulated annealing: https://www.michaelfogleman.com/rush/
- Stamp — Rush Hour and Dijkstra's Algorithm (graph properties paper): https://www.cs.sjsu.edu/~stamp/cv/papers/rh.pdf
- Wikipedia — Rush Hour PSPACE-completeness: https://en.wikipedia.org/wiki/Rush_Hour_(puzzle)
- van Assema — On the Hardness of 6x6 Rush Hour (configuration space exploration)
