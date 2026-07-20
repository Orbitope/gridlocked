using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Appends one row of graph metrics per analyzed puzzle to
/// Exports/graph_summary.csv. This is the cross-puzzle dataset for the
/// "do fun puzzles cluster?" finding (findings.md).
/// </summary>
public static class GraphMetricsCsv
{
    public static void Append(GraphAnalysis.GraphMetrics m, string puzzleId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return; // no filesystem in the browser sandbox
#else
        string dir  = Path.Combine(Application.dataPath, "..", "Exports");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "graph_summary.csv");

        bool header = !File.Exists(path);
        using var w = new StreamWriter(path, append: true);
        if (header)
            w.WriteLine("timestamp,puzzle_id,total_states,optimal,deception_ratio," +
                        "min_mobility,mean_productive_frac,subgraph_size,bottleneck_count," +
                        "counterintuitive_moves,counterintuitive_frac,dependency_depth," +
                        "dependency_width,forced_node_count,distinct_strategies");

        w.WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{puzzleId},{m.TotalStates},{m.Optimal}," +
            $"{m.DeceptionRatio:F2},{m.MinMobility},{m.MeanProductiveFraction:F3}," +
            $"{m.SubgraphSize},{m.BottleneckCount}," +
            $"{m.CounterintuitiveMoves},{m.CounterintuitiveFrac:F3},{m.DependencyDepth}," +
            $"{m.DependencyWidth},{m.ForcedNodeCount},{m.DistinctStrategies}");
#endif
    }
}
