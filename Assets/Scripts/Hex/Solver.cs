using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Gridlocked
{

public sealed class SolveResult
{
    public bool Solved;
    public int MoveCount = -1;           // BFS depth; -1 if unsolvable
    public List<StateKey>? Path;         // start..goal, null if unsolvable
    public int StatesVisited;
    public int MaxFrontier;
    public double ElapsedMs;
    public int GoalDepthStateCount;      // states sitting at goal depth (cheap multiplicity proxy)
    public bool Capped;                  // true if search hit the state cap before exhausting
}

/// <summary>
/// Breadth-first solver over the single-cell-slide move graph.
/// One move = one piece sliding one cell along its axis.
/// </summary>
public static class Solver
{
    public static SolveResult Solve(Puzzle puzzle, int maxStates = 5_000_000)
    {
        var sw = Stopwatch.StartNew();
        var board = puzzle.Board;
        int n = puzzle.Pieces.Length;

        var start = (int[])puzzle.StartAnchors.Clone();
        var startKey = StateKey.Pack(start);

        var depth = new Dictionary<StateKey, int> { [startKey] = 0 };
        var parent = new Dictionary<StateKey, StateKey>();
        var anchorsByKey = new Dictionary<StateKey, int[]> { [startKey] = start };

        var frontier = new Queue<StateKey>();
        frontier.Enqueue(startKey);

        int maxFrontier = 1;
        StateKey goalKey = default;
        bool found = false;

        if (puzzle.IsGoal(start))
        {
            // Already solved.
            return new SolveResult
            {
                Solved = true, MoveCount = 0,
                Path = new List<StateKey> { startKey },
                StatesVisited = 1, MaxFrontier = 1,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                GoalDepthStateCount = 1
            };
        }

        while (frontier.Count > 0)
        {
            maxFrontier = Math.Max(maxFrontier, frontier.Count);
            var curKey = frontier.Dequeue();
            int curDepth = depth[curKey];
            var anchors = anchorsByKey[curKey];
            ulong occ = puzzle.Occupancy(anchors);

            for (int i = 0; i < n; i++)
            {
                var piece = puzzle.Pieces[i];
                ulong mask = piece.MaskAt(anchors[i]);
                ulong others = occ ^ mask;

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    // Slide as far as possible — each reachable position is 1 move.
                    ulong sliding = mask;
                    int   slideAnchor = anchors[i];
                    while (board.CanSlide(sliding, others, piece.Axis, dir))
                    {
                    sliding     = board.Shift(sliding, piece.Axis, dir);
                    slideAnchor += dir * piece.Step(board);

                    var next = (int[])anchors.Clone();
                    next[i] = slideAnchor;
                    var nextKey = StateKey.Pack(next);

                    if (depth.ContainsKey(nextKey)) continue;

                    depth[nextKey] = curDepth + 1;
                    parent[nextKey] = curKey;
                    anchorsByKey[nextKey] = next;

                    if (!found && puzzle.IsGoal(next))
                    {
                        found = true;
                        goalKey = nextKey;
                        // Don't break early: finish this depth to count goal-depth states.
                    }

                    frontier.Enqueue(nextKey);
                    } // end while (slide)
                }
            }

            // Once found, we can stop after draining states shallower-or-equal to goal.
            if (found && curDepth + 1 > depth[goalKey]) break;

            if (depth.Count >= maxStates)
            {
                return new SolveResult
                {
                    Solved = false, Capped = true,
                    StatesVisited = depth.Count, MaxFrontier = maxFrontier,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        }

        var result = new SolveResult
        {
            StatesVisited = depth.Count,
            MaxFrontier = maxFrontier,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
        };

        if (!found)
        {
            result.Solved = false;
            return result;
        }

        result.Solved = true;
        result.MoveCount = depth[goalKey];

        // Reconstruct path.
        var path = new List<StateKey>();
        var k = goalKey;
        path.Add(k);
        while (parent.TryGetValue(k, out var p)) { path.Add(p); k = p; }
        path.Reverse();
        result.Path = path;

        // Cheap multiplicity proxy: how many distinct states sit at goal depth.
        int gd = result.MoveCount, count = 0;
        foreach (var d in depth.Values) if (d == gd) count++;
        result.GoalDepthStateCount = count;

        return result;
    }
}

internal static class PieceExtensions
{
    public static int Step(this Piece p, Board b) => b.Step[p.Axis];
}
}
