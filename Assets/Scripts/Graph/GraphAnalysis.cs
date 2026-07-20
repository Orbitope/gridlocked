using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure graph metrics over a reachable-state adjacency map
/// (Dictionary&lt;ulong, List&lt;ulong&gt;&gt; — state → successor states).
/// Shared by the GraphScene renderer and the cross-puzzle CSV. Adjacency-generic:
/// the only game-specific input is the goal predicate.
/// </summary>
public static class GraphAnalysis
{
    /// <summary>BFS depth (moves from start) for every reachable node.</summary>
    public static Dictionary<ulong, int> DepthsFromStart(
        Dictionary<ulong, List<ulong>> graph, ulong start)
    {
        var depth = new Dictionary<ulong, int> { [start] = 0 };
        var queue = new Queue<ulong>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!graph.TryGetValue(cur, out var succ)) continue;
            foreach (var n in succ)
                if (!depth.ContainsKey(n))
                {
                    depth[n] = depth[cur] + 1;
                    queue.Enqueue(n);
                }
        }
        return depth;
    }

    /// <summary>
    /// Reverse-BFS distance to the nearest goal node for every node that can reach
    /// one. Nodes absent from the result are dead ends (goal unreachable).
    /// </summary>
    public static Dictionary<ulong, int> DistanceToGoal(
        Dictionary<ulong, List<ulong>> graph, IEnumerable<ulong> goalNodes)
    {
        // Reverse adjacency: successor → predecessors.
        var reverse = new Dictionary<ulong, List<ulong>>();
        foreach (var kvp in graph)
            foreach (var succ in kvp.Value)
            {
                if (!reverse.TryGetValue(succ, out var preds))
                    reverse[succ] = preds = new List<ulong>();
                preds.Add(kvp.Key);
            }

        var dist = new Dictionary<ulong, int>();
        var queue = new Queue<ulong>();
        foreach (var g in goalNodes)
            if (dist.TryAdd(g, 0)) queue.Enqueue(g);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!reverse.TryGetValue(cur, out var preds)) continue;
            foreach (var p in preds)
                if (!dist.ContainsKey(p))
                {
                    dist[p] = dist[cur] + 1;
                    queue.Enqueue(p);
                }
        }
        return dist;
    }

    /// <summary>All nodes satisfying <paramref name="isGoal"/>.</summary>
    public static List<ulong> GoalNodes(
        Dictionary<ulong, List<ulong>> graph, Func<ulong, bool> isGoal)
    {
        var goals = new List<ulong>();
        foreach (var key in graph.Keys)
            if (isGoal(key)) goals.Add(key);
        return goals;
    }

    public sealed class Subgraph
    {
        public HashSet<ulong> Nodes = new();
        // edge (u,v) with a flag: true = exact-shortest-DAG edge, false = slack edge.
        public List<(ulong u, ulong v, bool dag)> Edges = new();
    }

    /// <summary>
    /// Nodes on near-optimal solution paths: depth[n] + dist[n] &lt;= optimal + slack.
    /// Induced edges are tagged as exact-shortest-DAG vs slack.
    /// </summary>
    public static Subgraph SolutionSubgraph(
        Dictionary<ulong, List<ulong>> graph,
        Dictionary<ulong, int> depth,
        Dictionary<ulong, int> dist,
        int optimal, int slack)
    {
        var sg = new Subgraph();
        foreach (var n in graph.Keys)
            if (depth.TryGetValue(n, out int d) && dist.TryGetValue(n, out int g)
                && d + g <= optimal + slack)
                sg.Nodes.Add(n);

        foreach (var u in sg.Nodes)
        {
            if (!graph.TryGetValue(u, out var succ)) continue;
            foreach (var v in succ)
            {
                if (!sg.Nodes.Contains(v)) continue;
                bool dag = depth[v] == depth[u] + 1 && dist[u] == dist[v] + 1;
                sg.Edges.Add((u, v, dag));
            }
        }
        return sg;
    }

    /// <summary>
    /// Per optimal-path step: mobility (legal moves) and productive moves
    /// (successors strictly closer to the goal). The "fun signature."
    /// </summary>
    public static List<(int mobility, int productive)> MobilityProfile(
        IReadOnlyList<ulong> path,
        Dictionary<ulong, List<ulong>> graph,
        Dictionary<ulong, int> dist)
    {
        var profile = new List<(int, int)>(path.Count);
        foreach (var node in path)
        {
            int mobility = graph.TryGetValue(node, out var succ) ? succ.Count : 0;
            int productive = 0;
            if (succ != null && dist.TryGetValue(node, out int here))
                foreach (var v in succ)
                    if (dist.TryGetValue(v, out int dv) && dv < here) productive++;
            profile.Add((mobility, productive));
        }
        return profile;
    }

    /// <summary>Count of states at each distance-to-goal (index = distance).</summary>
    public static int[] ReachabilityHistogram(Dictionary<ulong, int> dist)
    {
        if (dist.Count == 0) return Array.Empty<int>();
        int max = dist.Values.Max();
        var hist = new int[max + 1];
        foreach (var d in dist.Values) hist[d]++;
        return hist;
    }

    /// <summary>Bottleneck nodes: in-degree ≥ 3 and out-degree ≤ 2 (candidate aha states).</summary>
    public static HashSet<ulong> Bottlenecks(Dictionary<ulong, List<ulong>> graph)
    {
        var inDeg = new Dictionary<ulong, int>();
        foreach (var kvp in graph)
            foreach (var v in kvp.Value)
                inDeg[v] = inDeg.GetValueOrDefault(v, 0) + 1;

        var result = new HashSet<ulong>();
        foreach (var kvp in graph)
            if (inDeg.GetValueOrDefault(kvp.Key, 0) >= 3 && kvp.Value.Count <= 2)
                result.Add(kvp.Key);
        return result;
    }

    /// <summary>
    /// Nodes every optimal path must pass through (true bottlenecks). A node v on
    /// the solution DAG is forced iff pathsTo(v) * pathsFrom(v) == totalOptimalPaths.
    /// More principled than the in/out-degree heuristic.
    /// </summary>
    public static HashSet<ulong> ForcedNodes(
        Dictionary<ulong, List<ulong>> graph,
        Dictionary<ulong, int> depths,
        Dictionary<ulong, int> dist,
        ulong start, int optimal)
    {
        // Solution-DAG membership: depth[n] + dist[n] == optimal.
        bool OnDag(ulong n) =>
            depths.TryGetValue(n, out int d) && dist.TryGetValue(n, out int g) && d + g == optimal;

        // pathsTo: number of optimal paths start->v (DP in depth order).
        var order = depths.Where(kv => OnDag(kv.Key))
                          .OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        var toV = new Dictionary<ulong, long> { [start] = 1 };
        foreach (var u in order)
        {
            long cu = toV.GetValueOrDefault(u, 0);
            if (cu == 0 || !graph.TryGetValue(u, out var succ)) continue;
            foreach (var v in succ)
                if (OnDag(v) && depths[v] == depths[u] + 1)
                    toV[v] = toV.GetValueOrDefault(v, 0) + cu;
        }
        // pathsFrom: number of optimal paths v->goal (DP in reverse depth order).
        var fromV = new Dictionary<ulong, long>();
        foreach (var v in order) if (dist[v] == 0) fromV[v] = 1;
        for (int i = order.Count - 1; i >= 0; i--)
        {
            var u = order[i];
            if (!graph.TryGetValue(u, out var succ)) continue;
            long acc = fromV.GetValueOrDefault(u, 0);
            foreach (var v in succ)
                if (OnDag(v) && depths[v] == depths[u] + 1)
                    acc += fromV.GetValueOrDefault(v, 0);
            if (acc > 0) fromV[u] = acc;
        }

        long total = toV.Where(kv => dist.GetValueOrDefault(kv.Key, -1) == 0).Sum(kv => kv.Value);
        var forced = new HashSet<ulong>();
        if (total <= 0) return forced;
        foreach (var v in order)
            if (v != start && dist[v] != 0 &&
                toV.GetValueOrDefault(v, 0) * fromV.GetValueOrDefault(v, 0) == total)
                forced.Add(v);
        return forced;
    }

    /// <summary>
    /// Number of genuinely different opening strategies: start-successors that lie
    /// on an optimal path (depth 1 and dist == optimal-1). 1 = "spherical" graph.
    /// </summary>
    public static int DistinctFirstMoves(
        Dictionary<ulong, List<ulong>> graph,
        Dictionary<ulong, int> dist,
        ulong start, int optimal)
    {
        if (!graph.TryGetValue(start, out var succ)) return 0;
        return succ.Count(v => dist.TryGetValue(v, out int g) && g == optimal - 1);
    }

    /// <summary>Summary row for the cross-puzzle CSV.</summary>
    public struct GraphMetrics
    {
        // --- weak "static" family (kept as baseline contrast per literature) ---
        public int TotalStates;
        public int Optimal;                 // Jarušek Spearman ~0.47
        public float DeceptionRatio;        // totalStates / optimal
        public int MinMobility;
        public float MeanProductiveFraction;
        public int SubgraphSize;
        public int BottleneckCount;         // heuristic (in/out-degree)

        // --- research-aligned structural family ---
        public int CounterintuitiveMoves;   // Jarušek ~0.69
        public float CounterintuitiveFrac;
        public int DependencyDepth;         // ≈ decomposition, Jarušek ~0.82
        public int DependencyWidth;         // independent subproblems
        public int ForcedNodeCount;         // true bottlenecks (must-pass states)
        public int DistinctStrategies;      // spherical(1) vs multi-path
    }
}
