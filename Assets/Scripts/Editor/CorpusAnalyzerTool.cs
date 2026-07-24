using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-analyzes a corpus of Rush Hour boards (36-char encoding) and writes the
/// research metrics to Exports/corpus_metrics.csv with a `source` label.
///
/// The point is a matched comparison: sample human-curated puzzles vs randomly
/// enumerated ones (Fogleman's DB) at the SAME optimal move count, then see which
/// metrics separate the two populations.
///
/// Menu: Gridlocked/Corpus Analyzer
/// </summary>
public class CorpusAnalyzerTool : EditorWindow
{
    private string _sourceFile = "Research/corpora/fogleman.txt";
    private string _sourceLabel = "random";
    private int _sampleSize = 100;
    private bool _filterByMoves = false;
    private int _movesMin = 10;
    private int _movesMax = 20;
    private int _seed = 12345;

    private static string ExportsDir =>
        Path.Combine(Application.dataPath, "..", "Exports");
    private static string MetricsPath => Path.Combine(ExportsDir, "corpus_metrics.csv");
    private static string ComparePath => Path.Combine(ExportsDir, "corpus_comparison.csv");

    [MenuItem("Gridlocked/Corpus Analyzer")]
    public static void Open() => GetWindow<CorpusAnalyzerTool>("Corpus Analyzer");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Corpus source", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _sourceFile = EditorGUILayout.TextField("File (rel. to project)", _sourceFile);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string picked = EditorUtility.OpenFilePanel("Corpus file", "", "txt");
            if (!string.IsNullOrEmpty(picked)) _sourceFile = picked;
        }
        EditorGUILayout.EndHorizontal();

        _sourceLabel = EditorGUILayout.TextField("Source label", _sourceLabel);
        EditorGUILayout.HelpBox(
            "Label goes in the CSV. Use e.g. 'curated' for a published pack and " +
            "'random' for Fogleman rows, so the compare step can pair them.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sampling", EditorStyles.boldLabel);
        _sampleSize = EditorGUILayout.IntField("Sample size", _sampleSize);
        _seed = EditorGUILayout.IntField("RNG seed", _seed);
        _filterByMoves = EditorGUILayout.Toggle("Filter by file moves", _filterByMoves);
        if (_filterByMoves)
        {
            EditorGUI.indentLevel++;
            _movesMin = EditorGUILayout.IntField("Min moves", _movesMin);
            _movesMax = EditorGUILayout.IntField("Max moves", _movesMax);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Each puzzle needs a full state-space BFS — expect minutes for a few " +
            "hundred. Appends to Exports/corpus_metrics.csv.", MessageType.Info);

        if (GUILayout.Button("Analyze corpus", GUILayout.Height(30)))
            AnalyzeCorpus();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Comparison", EditorStyles.boldLabel);
        if (GUILayout.Button("Compare sources (matched on optimal)", GUILayout.Height(26)))
            CompareSources();
    }

    // -----------------------------------------------------------------------
    // Analyze
    // -----------------------------------------------------------------------

    private void AnalyzeCorpus()
    {
        string path = Path.IsPathRooted(_sourceFile)
            ? _sourceFile
            : Path.Combine(Application.dataPath, "..", _sourceFile);

        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Corpus Analyzer",
                $"File not found:\n{path}", "OK");
            return;
        }

        // Reservoir-sample importable boards so we don't hold 2.5M lines.
        var rng = new System.Random(_seed);
        var reservoir = new List<(string board, int fileMoves)>();
        int seen = 0, skippedFilter = 0;
        var rejects = new Dictionary<string, int>();

        foreach (var line in File.ReadLines(path))
        {
            if (!RushBoardParser.TryParseLine(line, out string board, out int fileMoves)) continue;

            if (_filterByMoves && fileMoves >= 0 &&
                (fileMoves < _movesMin || fileMoves > _movesMax)) { skippedFilter++; continue; }

            // Cheap pre-check so the reservoir only holds importable boards.
            if (!RushBoardParser.TryParse(board, out _, out _, out string reason))
            {
                rejects[reason] = rejects.GetValueOrDefault(reason, 0) + 1;
                continue;
            }

            seen++;
            if (reservoir.Count < _sampleSize) reservoir.Add((board, fileMoves));
            else
            {
                int j = rng.Next(seen);
                if (j < _sampleSize) reservoir[j] = (board, fileMoves);
            }
        }

        if (reservoir.Count == 0)
        {
            EditorUtility.DisplayDialog("Corpus Analyzer",
                "No importable boards found. Check the file format and filters.\n\n" +
                DescribeRejects(rejects), "OK");
            return;
        }

        Directory.CreateDirectory(ExportsDir);
        bool header = !File.Exists(MetricsPath);
        int mismatches = 0, analyzed = 0;

        using (var w = new StreamWriter(MetricsPath, append: true))
        {
            if (header)
                w.WriteLine("timestamp,source,puzzle_id,file_moves,optimal,total_states," +
                            "deception_ratio,min_mobility,mean_productive_frac," +
                            "bottleneck_count,counterintuitive_moves,counterintuitive_frac," +
                            "dependency_depth,dependency_width,forced_node_count," +
                            "distinct_strategies,car_count");

            for (int i = 0; i < reservoir.Count; i++)
            {
                var (board, fileMoves) = reservoir[i];

                if (EditorUtility.DisplayCancelableProgressBar("Analyzing corpus",
                        $"{i + 1} / {reservoir.Count}", (float)i / reservoir.Count))
                    break;

                if (!RushBoardParser.TryParse(board, out var def, out var state, out _)) continue;

                var solver = new BitboardSolver();
                var path2 = solver.Solve(def, state);
                if (path2 == null || path2.Count == 0) continue; // unsolvable

                var m = BuildMetrics(def, state, path2, solver);
                if (fileMoves >= 0 && m.Optimal != fileMoves) mismatches++;

                w.WriteLine(string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    _sourceLabel,
                    board,
                    fileMoves,
                    m.Optimal,
                    m.TotalStates,
                    m.DeceptionRatio.ToString("F2", CultureInfo.InvariantCulture),
                    m.MinMobility,
                    m.MeanProductiveFraction.ToString("F3", CultureInfo.InvariantCulture),
                    m.BottleneckCount,
                    m.CounterintuitiveMoves,
                    m.CounterintuitiveFrac.ToString("F3", CultureInfo.InvariantCulture),
                    m.DependencyDepth,
                    m.DependencyWidth,
                    m.ForcedNodeCount,
                    m.DistinctStrategies,
                    def.CarCount));
                analyzed++;
            }
        }
        EditorUtility.ClearProgressBar();

        string msg = $"Analyzed {analyzed} '{_sourceLabel}' puzzles → Exports/corpus_metrics.csv\n\n" +
                     $"Filtered out by move range: {skippedFilter}\n" +
                     DescribeRejects(rejects);
        if (mismatches > 0)
            msg += $"\n\n⚠ {mismatches} puzzle(s) where our optimal != file moves — " +
                   "move semantics may differ from the reference. Worth investigating " +
                   "before publishing numbers.";
        else if (analyzed > 0)
            msg += "\n\n✓ Our optimal matched the reference move count on every puzzle.";

        Debug.Log("[CorpusAnalyzer] " + msg);
        EditorUtility.DisplayDialog("Corpus Analyzer", msg, "OK");
    }

    private static string DescribeRejects(Dictionary<string, int> rejects)
    {
        if (rejects.Count == 0) return "No boards rejected.";
        var lines = rejects.OrderByDescending(k => k.Value)
                           .Select(k => $"  {k.Value} × {k.Key}");
        return "Rejected (unsupported by the engine):\n" + string.Join("\n", lines);
    }

    /// <summary>Runs the full metric suite for one puzzle (mirrors GraphManager).</summary>
    private static GraphAnalysis.GraphMetrics BuildMetrics(
        PuzzleDefinition def, PuzzleState start,
        List<PuzzleState> path, BitboardSolver solver)
    {
        var graph = solver.GraphEdges;
        int optimal = path.Count - 1;

        bool IsGoal(ulong s) =>
            new PuzzleState(s).GetCarPos(BitboardSolver.GOAL_CAR_INDEX)
                == BitboardSolver.GOAL_POSITION;

        var depths = GraphAnalysis.DepthsFromStart(graph, start.Data);
        var goals  = GraphAnalysis.GoalNodes(graph, IsGoal);
        var dist   = GraphAnalysis.DistanceToGoal(graph, goals);
        var necks  = GraphAnalysis.Bottlenecks(graph);
        var mob    = GraphAnalysis.MobilityProfile(
                         path.Select(p => p.Data).ToList(), graph, dist);
        var rh     = RushHourMetrics.Analyze(def, path);

        return new GraphAnalysis.GraphMetrics
        {
            TotalStates    = graph.Count,
            Optimal        = optimal,
            DeceptionRatio = optimal > 0 ? (float)graph.Count / optimal : 0f,
            MinMobility    = mob.Count > 0 ? mob.Min(x => x.mobility) : 0,
            MeanProductiveFraction = mob.Count > 0
                ? (float)mob.Average(x => x.mobility > 0
                    ? (double)x.productive / x.mobility : 0.0) : 0f,
            SubgraphSize    = optimal + 1,
            BottleneckCount = necks.Count,

            CounterintuitiveMoves = rh.CounterintuitiveMoves,
            CounterintuitiveFrac  = rh.CounterintuitiveFrac,
            DependencyDepth       = rh.DependencyDepth,
            DependencyWidth       = rh.DependencyWidth,
            ForcedNodeCount       = GraphAnalysis.ForcedNodes(graph, depths, dist, start.Data, optimal).Count,
            DistinctStrategies    = GraphAnalysis.DistinctFirstMoves(graph, dist, start.Data, optimal),
        };
    }

    // -----------------------------------------------------------------------
    // Compare
    // -----------------------------------------------------------------------

    private static readonly string[] CompareMetrics =
    {
        "dependency_depth", "counterintuitive_moves", "counterintuitive_frac",
        "dependency_width", "forced_node_count", "distinct_strategies",
        "total_states", "deception_ratio", "min_mobility",
        "mean_productive_frac", "bottleneck_count", "car_count",
    };

    private void CompareSources()
    {
        if (!File.Exists(MetricsPath))
        {
            EditorUtility.DisplayDialog("Corpus Analyzer",
                "No corpus_metrics.csv yet — analyze at least two sources first.", "OK");
            return;
        }

        var rows = CsvTable.Load(MetricsPath);
        var sources = rows.Select(r => r.GetValueOrDefault("source", "")).Distinct()
                          .Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (sources.Count < 2)
        {
            EditorUtility.DisplayDialog("Corpus Analyzer",
                $"Need two sources to compare; found: {string.Join(", ", sources)}", "OK");
            return;
        }

        // Pair the two most common labels.
        string a = sources.OrderByDescending(s => rows.Count(r => r["source"] == s)).First();
        string b = sources.First(s => s != a);

        var byOptimal = rows.GroupBy(r => r.GetValueOrDefault("optimal", "-1"))
                            .OrderBy(g => int.TryParse(g.Key, out int k) ? k : -1);

        Directory.CreateDirectory(ExportsDir);
        var summary = new List<string>();

        using (var w = new StreamWriter(ComparePath, append: false))
        {
            w.WriteLine($"optimal,n_{a},n_{b},metric,mean_{a},mean_{b},delta");

            // Overall (all move counts pooled) plus per-optimal breakdown.
            WriteGroup(w, "ALL", rows, a, b, summary);
            foreach (var g in byOptimal)
            {
                var list = g.ToList();
                if (list.Count(r => r["source"] == a) == 0) continue;
                if (list.Count(r => r["source"] == b) == 0) continue;
                WriteGroup(w, g.Key, list, a, b, null);
            }
        }

        string msg = $"Compared '{a}' vs '{b}' → Exports/corpus_comparison.csv\n\n" +
                     "Pooled deltas (largest separation first):\n" +
                     string.Join("\n", summary.Take(6));
        Debug.Log("[CorpusAnalyzer] " + msg);
        EditorUtility.DisplayDialog("Corpus Analyzer", msg, "OK");
    }

    private static void WriteGroup(StreamWriter w, string optimalKey,
        List<Dictionary<string, string>> rows, string a, string b,
        List<string> summaryOut)
    {
        var rowsA = rows.Where(r => r.GetValueOrDefault("source", "") == a).ToList();
        var rowsB = rows.Where(r => r.GetValueOrDefault("source", "") == b).ToList();

        var deltas = new List<(string metric, double delta, double ma, double mb)>();

        foreach (var metric in CompareMetrics)
        {
            double? ma = Mean(rowsA, metric);
            double? mb = Mean(rowsB, metric);
            if (!ma.HasValue || !mb.HasValue) continue;

            double delta = ma.Value - mb.Value;
            w.WriteLine(string.Join(",", optimalKey, rowsA.Count, rowsB.Count, metric,
                ma.Value.ToString("F3", CultureInfo.InvariantCulture),
                mb.Value.ToString("F3", CultureInfo.InvariantCulture),
                delta.ToString("F3", CultureInfo.InvariantCulture)));
            deltas.Add((metric, delta, ma.Value, mb.Value));
        }

        if (summaryOut != null)
        {
            // Rank by relative separation so scale differences don't dominate.
            foreach (var d in deltas
                .OrderByDescending(x => Math.Abs(x.delta) /
                    Math.Max(1e-6, Math.Abs(x.mb) + Math.Abs(x.ma)))
                .Take(8))
            {
                summaryOut.Add($"  {d.metric}: {a}={d.ma:F2}  {b}={d.mb:F2}  Δ={d.delta:+0.00;-0.00}");
            }
        }
    }

    private static double? Mean(List<Dictionary<string, string>> rows, string col)
    {
        var vals = new List<double>();
        foreach (var r in rows)
            if (r.TryGetValue(col, out var s) && !string.IsNullOrEmpty(s) &&
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                vals.Add(v);
        return vals.Count == 0 ? (double?)null : vals.Average();
    }
}
