using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attaches to any GameObject in the puzzle scene. After the BFS solver runs,
/// call Analyse(puzzleId, startState, goalState, fullGraph, solutionPath) to
/// compute and log all graph metrics for the current puzzle. Output goes to:
///   Application.persistentDataPath/research/puzzle_metrics.csv
///   Application.persistentDataPath/research/puzzle_[id]_[timestamp].json
///
/// DEPENDENCIES:
///   - Requires the full reachable state graph as Dictionary<ulong, List<ulong>>.
///   - If your BFS solver only tracks the solution path, add a separate enumeration
///     pass that runs BFS to completion without stopping at the goal.
/// </summary>
public class PuzzleMetricsLogger : MonoBehaviour
{
    /// <summary>
    /// Fired after Analyse() completes. PuzzleNotesWindow subscribes to this
    /// in the Editor to auto-pop a notes prompt.
    /// </summary>
    public static event Action<PuzzleMetrics> OnAnalysisComplete;

    [Header("Output")]
    public bool logToCSV = true;
    public bool logToJSON = true;
    public bool logToConsole = true;

    private string _outputDir;

    private void Awake()
    {
        _outputDir = Path.Combine(Application.persistentDataPath, "research");
        Directory.CreateDirectory(_outputDir);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <param name="puzzleId">Human-readable ID or level name</param>
    /// <param name="startState">Initial board state (bitboard)</param>
    /// <param name="goalState">Solved board state (bitboard)</param>
    /// <param name="fullGraph">
    ///     Complete reachable state graph: key = state, value = successor states.
    ///     Must include ALL reachable states, not just the solution path.
    /// </param>
    /// <param name="solutionPath">
    ///     Shortest solution as ordered states from start to goal. Pass null to
    ///     skip solution-path-dependent metrics.
    /// </param>
    public PuzzleMetrics Analyse(
        string puzzleId,
        ulong startState,
        ulong goalState,
        Dictionary<ulong, List<ulong>> fullGraph,
        List<ulong> solutionPath = null)
    {
        var metrics = new PuzzleMetrics();
        metrics.puzzleId = puzzleId;
        metrics.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Basic counts
        metrics.totalStates = fullGraph.Count;
        metrics.solutionLength = solutionPath != null ? solutionPath.Count - 1 : -1;

        metrics.deceptionRatio = metrics.solutionLength > 0
            ? (float)metrics.totalStates / metrics.solutionLength
            : -1f;

        // Branching factor
        metrics.branchingFactorAvg = fullGraph.Values.Select(v => (float)v.Count).Average();
        metrics.branchingFactorMax = fullGraph.Values.Select(v => v.Count).Max();

        // Dead ends: states from which the goal cannot be reached.
        // Computed via reverse-BFS from goalState so we catch states that have
        // moves but are in an isolated region that never connects to the solution.
        var canReachGoal = ReachableFromGoal(fullGraph, goalState);
        metrics.deadEndCount = fullGraph.Count - canReachGoal.Count;
        metrics.deadEndRatio = metrics.totalStates > 0
            ? (float)metrics.deadEndCount / metrics.totalStates
            : 0f;

        // Depth map and diameter
        var (depths, diameter) = ComputeDepths(fullGraph, startState);
        metrics.diameter = diameter;
        metrics.avgDepthToGoal = depths.ContainsKey(goalState) ? depths[goalState] : -1;

        // Shortest solution path count via DP on the BFS DAG
        metrics.solutionPathCount = CountShortestPaths(fullGraph, depths, startState, goalState);

        // In-degree map needed for bottleneck detection
        var inDegree = ComputeInDegrees(fullGraph);

        // Bottleneck nodes: high in-degree, low out-degree, not start/goal.
        // Candidate aha-moment states — many paths converge here, few paths out.
        metrics.bottleneckNodes = fullGraph
            .Where(kvp =>
                kvp.Key != startState &&
                kvp.Key != goalState &&
                inDegree.GetValueOrDefault(kvp.Key, 0) >= 3 &&
                kvp.Value.Count <= 2)
            .Select(kvp => kvp.Key)
            .ToList();
        metrics.bottleneckCount = metrics.bottleneckNodes.Count;

        // Depth of each bottleneck node — tells you *when* the aha moment occurs
        metrics.bottleneckDepths = metrics.bottleneckNodes
            .Select(n => depths.GetValueOrDefault(n, -1))
            .ToList();
        metrics.ahaDepthMin = metrics.bottleneckDepths.Count > 0
            ? metrics.bottleneckDepths.Min()
            : -1;
        metrics.ahaDepthAvg = metrics.bottleneckDepths.Count > 0
            ? (float)metrics.bottleneckDepths.Average()
            : -1f;

        // Depth distribution: how many states at each BFS depth from start
        metrics.depthDistribution = depths.Values
            .GroupBy(d => d)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        // Cluster count: should be 1 for any valid, fully reachable puzzle
        metrics.clusterCount = CountClusters(fullGraph);

        if (logToConsole) LogToConsole(metrics);
        if (logToCSV)     AppendToCSV(metrics);
        if (logToJSON)    WriteJSON(metrics);

        OnAnalysisComplete?.Invoke(metrics);

        return metrics;
    }

    // -------------------------------------------------------------------------
    // Graph analysis helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Counts distinct shortest solution paths using DP on the BFS DAG.
    /// Only edges where depths[successor] == depths[current] + 1 are included,
    /// so every counted path is guaranteed to be a shortest path.
    /// </summary>
    private int CountShortestPaths(
        Dictionary<ulong, List<ulong>> graph,
        Dictionary<ulong, int> depths,
        ulong start, ulong goal)
    {
        var pathCount = new Dictionary<ulong, int> { [start] = 1 };
        var queue = new Queue<ulong>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!graph.TryGetValue(cur, out var successors)) continue;
            int curDepth = depths[cur];

            foreach (var next in successors)
            {
                if (!depths.TryGetValue(next, out int nextDepth)) continue;
                if (nextDepth != curDepth + 1) continue;

                bool firstVisit = !pathCount.ContainsKey(next);
                pathCount[next] = pathCount.GetValueOrDefault(next, 0) + pathCount[cur];
                if (firstVisit) queue.Enqueue(next);
            }
        }

        return pathCount.GetValueOrDefault(goal, 0);
    }

    /// <summary>
    /// Reverse-BFS from goalState. Returns the set of all states from which
    /// the goal is reachable. States NOT in this set are dead ends.
    /// </summary>
    private HashSet<ulong> ReachableFromGoal(
        Dictionary<ulong, List<ulong>> graph, ulong goal)
    {
        // Build reverse adjacency: successor -> list of predecessors
        var reverse = new Dictionary<ulong, List<ulong>>();
        foreach (var kvp in graph)
        {
            if (!reverse.ContainsKey(kvp.Key))
                reverse[kvp.Key] = new List<ulong>();

            foreach (var succ in kvp.Value)
            {
                if (!reverse.ContainsKey(succ))
                    reverse[succ] = new List<ulong>();
                reverse[succ].Add(kvp.Key);
            }
        }

        var reachable = new HashSet<ulong>();
        var queue = new Queue<ulong>();
        reachable.Add(goal);
        queue.Enqueue(goal);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!reverse.TryGetValue(cur, out var preds)) continue;
            foreach (var pred in preds)
                if (reachable.Add(pred))
                    queue.Enqueue(pred);
        }

        return reachable;
    }

    /// <summary>
    /// BFS from start. Returns per-node depths and the graph diameter
    /// (max BFS depth reached from start).
    /// </summary>
    private (Dictionary<ulong, int> depths, int diameter) ComputeDepths(
        Dictionary<ulong, List<ulong>> graph, ulong start)
    {
        var depths = new Dictionary<ulong, int>();
        var queue = new Queue<ulong>();
        depths[start] = 0;
        queue.Enqueue(start);

        int maxDepth = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDepth = depths[current];

            if (!graph.TryGetValue(current, out var successors)) continue;

            foreach (var next in successors)
            {
                if (depths.ContainsKey(next)) continue;
                depths[next] = currentDepth + 1;
                if (depths[next] > maxDepth) maxDepth = depths[next];
                queue.Enqueue(next);
            }
        }

        return (depths, maxDepth);
    }

    private Dictionary<ulong, int> ComputeInDegrees(Dictionary<ulong, List<ulong>> graph)
    {
        var inDegree = new Dictionary<ulong, int>();

        foreach (var kvp in graph)
        {
            if (!inDegree.ContainsKey(kvp.Key))
                inDegree[kvp.Key] = 0;

            foreach (var successor in kvp.Value)
            {
                if (!inDegree.ContainsKey(successor))
                    inDegree[successor] = 0;
                inDegree[successor]++;
            }
        }

        return inDegree;
    }

    /// <summary>
    /// Counts connected components via union-find.
    /// </summary>
    private int CountClusters(Dictionary<ulong, List<ulong>> graph)
    {
        var parent = new Dictionary<ulong, ulong>();

        ulong Find(ulong x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        void Union(ulong a, ulong b) => parent[Find(a)] = Find(b);

        foreach (var kvp in graph)
        {
            Find(kvp.Key);
            foreach (var successor in kvp.Value)
                Union(kvp.Key, successor);
        }

        return parent.Keys.Select(Find).Distinct().Count();
    }

    // -------------------------------------------------------------------------
    // Output writers
    // -------------------------------------------------------------------------

    private void LogToConsole(PuzzleMetrics m)
    {
        string bottleneckDepthStr = m.bottleneckDepths.Count > 0
            ? $"min={m.ahaDepthMin} avg={m.ahaDepthAvg:F1}"
            : "none";

        Debug.Log($"[PuzzleMetrics] {m.puzzleId}\n" +
                  $"  States: {m.totalStates:N0}  |  Solution: {m.solutionLength} moves  |  " +
                  $"Solution paths: {m.solutionPathCount}  |  Deception ratio: {m.deceptionRatio:F1}\n" +
                  $"  Diameter: {m.diameter}  |  Branching avg: {m.branchingFactorAvg:F2}  |  " +
                  $"Dead ends: {m.deadEndCount} ({m.deadEndRatio:P1})\n" +
                  $"  Bottlenecks: {m.bottleneckCount}  |  Aha depth: {bottleneckDepthStr}  |  " +
                  $"Clusters: {m.clusterCount}");
    }

    private void AppendToCSV(PuzzleMetrics m)
    {
        string path = Path.Combine(_outputDir, "puzzle_metrics.csv");
        bool writeHeader = !File.Exists(path);

        using var writer = new StreamWriter(path, append: true);

        if (writeHeader)
            writer.WriteLine(
                "timestamp,puzzle_id,total_states,solution_length,solution_paths,diameter," +
                "deception_ratio,branching_avg,branching_max," +
                "dead_end_count,dead_end_ratio,bottleneck_count," +
                "aha_depth_min,aha_depth_avg,cluster_count");

        writer.WriteLine(
            $"{m.timestamp},{m.puzzleId},{m.totalStates},{m.solutionLength},{m.solutionPathCount},{m.diameter}," +
            $"{m.deceptionRatio:F3},{m.branchingFactorAvg:F3},{m.branchingFactorMax}," +
            $"{m.deadEndCount},{m.deadEndRatio:F4},{m.bottleneckCount}," +
            $"{m.ahaDepthMin},{m.ahaDepthAvg:F2},{m.clusterCount}");
    }

    private void WriteJSON(PuzzleMetrics m)
    {
        string path = Path.Combine(_outputDir, $"puzzle_{m.puzzleId}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, JsonUtility.ToJson(m, prettyPrint: true));
    }
}

// -------------------------------------------------------------------------
// Data class
// -------------------------------------------------------------------------

[Serializable]
public class PuzzleMetrics
{
    [Header("Identity")]
    public string puzzleId;
    public string timestamp;

    [Header("Core metrics")]
    public int totalStates;
    public int solutionLength;
    public int solutionPathCount; // distinct shortest solution paths (DP on BFS DAG)
    public int diameter;
    public int avgDepthToGoal;

    [Header("Derived metrics")]
    public float deceptionRatio;
    public float branchingFactorAvg;
    public int branchingFactorMax;

    [Header("Dead ends")]
    public int deadEndCount;      // states from which goal is unreachable
    public float deadEndRatio;    // deadEndCount / totalStates

    [Header("Bottleneck / aha analysis")]
    public int bottleneckCount;
    public List<ulong> bottleneckNodes;
    public List<int> bottleneckDepths; // BFS depth of each bottleneck node
    public int ahaDepthMin;            // shallowest bottleneck
    public float ahaDepthAvg;          // average bottleneck depth

    [Header("Structure")]
    public int clusterCount;
    public Dictionary<int, int> depthDistribution;
}
