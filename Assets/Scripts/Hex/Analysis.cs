using System;
using System.Collections.Generic;

namespace Gridlocked
{

public sealed class PuzzleMetrics
{
    public bool Solvable;
    public int SolutionLength = -1;   // BFS depth to nearest goal state
    public long ShortestPathCount;    // distinct shortest paths to the goal CONDITION
    public int TotalStates;           // full reachable state space
    public double BranchingAvg;       // avg legal moves per state
}

/// <summary>
/// Enumerates the full reachable state graph once (BFS to completion) and
/// derives the metrics the hex-vs-square decision needs: solution length,
/// shortest-path multiplicity (exact, via DP on the BFS layers), reachable
/// state count, and average branching factor. Goal may be satisfied by many
/// states (any config with the target on an exit cell), so multiplicity sums
/// path counts over all minimum-depth goal states.
/// </summary>
public static class Analysis
{
    public static PuzzleMetrics Analyze(Puzzle puzzle, int maxStates = 5_000_000)
    {
        var board = puzzle.Board;
        int n = puzzle.Pieces.Length;
        var m = new PuzzleMetrics();

        var start = (int[])puzzle.StartAnchors.Clone();
        var startKey = StateKey.Pack(start);

        var depth = new Dictionary<StateKey, int> { [startKey] = 0 };
        var anchorsByKey = new Dictionary<StateKey, int[]> { [startKey] = start };
        var order = new List<StateKey> { startKey }; // BFS visitation order (layered)
        var queue = new Queue<StateKey>();
        queue.Enqueue(startKey);

        long totalOutDegree = 0;

        while (queue.Count > 0)
        {
            var curKey = queue.Dequeue();
            int curDepth = depth[curKey];
            var anchors = anchorsByKey[curKey];
            ulong occ = puzzle.Occupancy(anchors);
            int outDeg = 0;

            for (int i = 0; i < n; i++)
            {
                var piece = puzzle.Pieces[i];
                ulong mask = piece.MaskAt(anchors[i]);
                ulong others = occ ^ mask;

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    // Slide until blocked — each reachable position is 1 move.
                    ulong sliding = mask;
                    int   slideAnchor = anchors[i];
                    while (board.CanSlide(sliding, others, piece.Axis, dir))
                    {
                    sliding      = board.Shift(sliding, piece.Axis, dir);
                    slideAnchor += dir * board.Step[piece.Axis];
                    outDeg++;

                    var next = (int[])anchors.Clone();
                    next[i] = slideAnchor;
                    var nextKey = StateKey.Pack(next);
                    if (depth.ContainsKey(nextKey)) continue;

                    depth[nextKey] = curDepth + 1;
                    anchorsByKey[nextKey] = next;
                    order.Add(nextKey);
                    queue.Enqueue(nextKey);
                    } // end while (slide)
                }
            }

            totalOutDegree += outDeg;
            if (depth.Count >= maxStates) { m.TotalStates = depth.Count; return m; }
        }

        m.TotalStates = depth.Count;
        m.BranchingAvg = depth.Count > 0 ? (double)totalOutDegree / depth.Count : 0;

        // Find minimum goal depth.
        int goalDepth = int.MaxValue;
        foreach (var kv in anchorsByKey)
            if (puzzle.IsGoal(kv.Value) && depth[kv.Key] < goalDepth)
                goalDepth = depth[kv.Key];

        if (goalDepth == int.MaxValue) { m.Solvable = false; return m; }
        m.Solvable = true;
        m.SolutionLength = goalDepth;

        // Exact shortest-path count via DP on BFS layers.
        // pathCount[start]=1; process states in BFS order; for each DAG edge
        // (depth+1) add the source count to the successor.
        var pathCount = new Dictionary<StateKey, long> { [startKey] = 1 };
        foreach (var key in order)
        {
            int d = depth[key];
            if (d >= goalDepth) continue;           // goal-depth states are sinks here
            long cur = pathCount.GetValueOrDefault(key, 0);
            if (cur == 0) continue;

            var anchors = anchorsByKey[key];
            ulong occ = puzzle.Occupancy(anchors);
            for (int i = 0; i < n; i++)
            {
                var piece = puzzle.Pieces[i];
                ulong mask = piece.MaskAt(anchors[i]);
                ulong others = occ ^ mask;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    ulong sliding = mask;
                    int   slideAnchor = anchors[i];
                    while (board.CanSlide(sliding, others, piece.Axis, dir))
                    {
                        sliding      = board.Shift(sliding, piece.Axis, dir);
                        slideAnchor += dir * board.Step[piece.Axis];
                        var next = (int[])anchors.Clone();
                        next[i] = slideAnchor;
                        var nextKey = StateKey.Pack(next);
                        if (!depth.ContainsKey(nextKey) || depth[nextKey] != d + 1) continue;
                        pathCount[nextKey] = pathCount.GetValueOrDefault(nextKey, 0) + cur;
                    }
                }
            }
        }

        long mult = 0;
        foreach (var kv in anchorsByKey)
            if (depth[kv.Key] == goalDepth && puzzle.IsGoal(kv.Value))
                mult += pathCount.GetValueOrDefault(kv.Key, 0);
        m.ShortestPathCount = mult;

        return m;
    }
}
}
