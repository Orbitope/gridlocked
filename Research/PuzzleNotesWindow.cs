#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pops up automatically after PuzzleMetricsLogger.Analyse() completes.
/// Fill in qualitative observations, then Save Entry to append a completed
/// block to Research/puzzle_analysis.md.
/// </summary>
[InitializeOnLoad]
public class PuzzleNotesWindow : EditorWindow
{
    // --- State ---
    private PuzzleMetrics _metrics;
    private Vector2 _scroll;

    // Graph shape
    private bool _shapeTree;
    private bool _shapeWide;
    private bool _shapeBottlenecked;
    private bool _shapeDeceptive;
    private bool _shapeOther;
    private string _shapeOtherText = "";

    // Qualitative text
    private string _firstImpression = "";
    private string _whereStuck = "";
    private string _ahaMoment = "";
    private string _solveTime = "";
    private string _notes = "";

    // Ratings
    private int _ratingDeception = 3;
    private int _ratingSatisfaction = 3;
    private int _ratingClarity = 3;
    private bool _includeInCurated;

    // Paths
    private static string ResearchDir =>
        Path.Combine(Application.dataPath, "..", "Research");

    private static string AnalysisPath =>
        Path.GetFullPath(Path.Combine(ResearchDir, "puzzle_analysis.md"));

    // -------------------------------------------------------------------------
    // Auto-subscribe on Editor load
    // -------------------------------------------------------------------------

    static PuzzleNotesWindow()
    {
        PuzzleMetricsLogger.OnAnalysisComplete += OnAnalysisComplete;
    }

    private static void OnAnalysisComplete(PuzzleMetrics metrics)
    {
        // Must open Editor UI on the main thread
        EditorApplication.delayCall += () =>
        {
            var window = GetWindow<PuzzleNotesWindow>("Puzzle Notes");
            window.minSize = new Vector2(500, 640);
            window.LoadMetrics(metrics);
            window.Focus();
        };
    }

    // -------------------------------------------------------------------------
    // Window lifecycle
    // -------------------------------------------------------------------------

    private void LoadMetrics(PuzzleMetrics metrics)
    {
        _metrics = metrics;
        // Reset qualitative fields for each new puzzle
        _shapeTree = _shapeWide = _shapeBottlenecked = _shapeDeceptive = _shapeOther = false;
        _shapeOtherText = _firstImpression = _whereStuck = _ahaMoment = _solveTime = _notes = "";
        _ratingDeception = _ratingSatisfaction = _ratingClarity = 3;
        _includeInCurated = false;
    }

    private void OnGUI()
    {
        if (_metrics == null)
        {
            EditorGUILayout.HelpBox("No puzzle analysed yet. Run PuzzleMetricsLogger.Analyse() during Play Mode.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawMetricsSummary();
        EditorGUILayout.Space(8);
        DrawGraphShape();
        EditorGUILayout.Space(8);
        DrawQualitativeFields();
        EditorGUILayout.Space(8);
        DrawRatings();
        EditorGUILayout.Space(12);
        DrawSaveButton();

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Section renderers
    // -------------------------------------------------------------------------

    private void DrawMetricsSummary()
    {
        EditorGUILayout.LabelField($"Puzzle: {_metrics.puzzleId}", EditorStyles.boldLabel);

        var style = new GUIStyle(EditorStyles.helpBox) { richText = true };
        string summary =
            $"<b>States:</b> {_metrics.totalStates:N0}   " +
            $"<b>Sol length:</b> {_metrics.solutionLength}   " +
            $"<b>Sol paths:</b> {_metrics.solutionPathCount}\n" +
            $"<b>Diameter:</b> {_metrics.diameter}   " +
            $"<b>Deception ratio:</b> {_metrics.deceptionRatio:F1}   " +
            $"<b>Branching avg:</b> {_metrics.branchingFactorAvg:F2}\n" +
            $"<b>Dead ends:</b> {_metrics.deadEndCount} ({_metrics.deadEndRatio:P1})   " +
            $"<b>Bottlenecks:</b> {_metrics.bottleneckCount}   " +
            $"<b>Aha depth:</b> min={_metrics.ahaDepthMin} avg={_metrics.ahaDepthAvg:F1}";

        EditorGUILayout.LabelField(summary, style);
    }

    private void DrawGraphShape()
    {
        EditorGUILayout.LabelField("Graph shape", EditorStyles.boldLabel);
        _shapeTree         = EditorGUILayout.ToggleLeft("Tree-like — few branches, one clear path", _shapeTree);
        _shapeWide         = EditorGUILayout.ToggleLeft("Wide and flat — many states at similar depth", _shapeWide);
        _shapeBottlenecked = EditorGUILayout.ToggleLeft("Bottlenecked — dense clusters connected by narrow bridges", _shapeBottlenecked);
        _shapeDeceptive    = EditorGUILayout.ToggleLeft("Deceptive — large state space, short solution buried inside", _shapeDeceptive);

        EditorGUILayout.BeginHorizontal();
        _shapeOther = EditorGUILayout.ToggleLeft("Other:", _shapeOther, GUILayout.Width(60));
        GUI.enabled = _shapeOther;
        _shapeOtherText = EditorGUILayout.TextField(_shapeOtherText);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawQualitativeFields()
    {
        EditorGUILayout.LabelField("Qualitative", EditorStyles.boldLabel);

        _firstImpression = TextArea("First impression of the board (before solving):", _firstImpression);
        _whereStuck      = TextArea("Where did I get stuck:", _whereStuck);
        _ahaMoment       = TextArea("Aha moment (which car, which move):", _ahaMoment);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Approx solve time:", GUILayout.Width(140));
        _solveTime = EditorGUILayout.TextField(_solveTime);
        EditorGUILayout.EndHorizontal();

        _notes = TextArea("Notes:", _notes);
    }

    private void DrawRatings()
    {
        EditorGUILayout.LabelField("Ratings (1–5)", EditorStyles.boldLabel);
        _ratingDeception   = RatingField("Deception (felt harder than it was):", _ratingDeception);
        _ratingSatisfaction = RatingField("Satisfaction on solve:", _ratingSatisfaction);
        _ratingClarity     = RatingField("Clarity of aha moment:", _ratingClarity);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Include in curated set:", GUILayout.Width(160));
        _includeInCurated = EditorGUILayout.Toggle(_includeInCurated);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSaveButton()
    {
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Save Entry", GUILayout.Height(36)))
            SaveEntry();
        GUI.backgroundColor = Color.white;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string TextArea(string label, string value)
    {
        EditorGUILayout.LabelField(label);
        return EditorGUILayout.TextArea(value, GUILayout.MinHeight(48));
    }

    private static int RatingField(string label, int value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(240));
        value = EditorGUILayout.IntSlider(value, 1, 5);
        EditorGUILayout.EndHorizontal();
        return value;
    }

    private string BuildShapeString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (_shapeTree)         parts.Add("Tree-like");
        if (_shapeWide)         parts.Add("Wide and flat");
        if (_shapeBottlenecked) parts.Add("Bottlenecked");
        if (_shapeDeceptive)    parts.Add("Deceptive");
        if (_shapeOther && !string.IsNullOrWhiteSpace(_shapeOtherText))
            parts.Add(_shapeOtherText);
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    private void SaveEntry()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string shapeStr = BuildShapeString();

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"## Puzzle {_metrics.puzzleId}");
        sb.AppendLine();
        sb.AppendLine($"**Date investigated:** {date}");
        sb.AppendLine();
        sb.AppendLine("### Quantitative metrics");
        sb.AppendLine($"- total_states: {_metrics.totalStates}");
        sb.AppendLine($"- solution_length: {_metrics.solutionLength}");
        sb.AppendLine($"- solution_paths: {_metrics.solutionPathCount}");
        sb.AppendLine($"- diameter: {_metrics.diameter}");
        sb.AppendLine($"- deception_ratio: {_metrics.deceptionRatio:F3}");
        sb.AppendLine($"- branching_factor_avg: {_metrics.branchingFactorAvg:F3}");
        sb.AppendLine($"- branching_factor_max: {_metrics.branchingFactorMax}");
        sb.AppendLine($"- dead_end_count: {_metrics.deadEndCount}");
        sb.AppendLine($"- dead_end_ratio: {_metrics.deadEndRatio:F4}");
        sb.AppendLine($"- bottleneck_count: {_metrics.bottleneckCount}");
        sb.AppendLine($"- aha_depth_min: {_metrics.ahaDepthMin}");
        sb.AppendLine($"- aha_depth_avg: {_metrics.ahaDepthAvg:F2}");
        sb.AppendLine($"- cluster_count: {_metrics.clusterCount}");
        sb.AppendLine();
        sb.AppendLine("### Graph shape");
        sb.AppendLine(shapeStr);
        sb.AppendLine();
        sb.AppendLine("### Qualitative");
        sb.AppendLine($"**First impression:** {_firstImpression}");
        sb.AppendLine();
        sb.AppendLine($"**Where I got stuck:** {_whereStuck}");
        sb.AppendLine();
        sb.AppendLine($"**Aha moment:** {_ahaMoment}");
        sb.AppendLine();
        sb.AppendLine($"**Solve time:** {_solveTime}");
        sb.AppendLine();
        sb.AppendLine("### Ratings");
        sb.AppendLine($"- Deception: {_ratingDeception}");
        sb.AppendLine($"- Satisfaction: {_ratingSatisfaction}");
        sb.AppendLine($"- Clarity of aha: {_ratingClarity}");
        sb.AppendLine($"- Include in curated set: {(_includeInCurated ? "y" : "n")}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(_notes))
        {
            sb.AppendLine("### Notes");
            sb.AppendLine(_notes);
            sb.AppendLine();
        }

        AppendToAnalysisFile(sb.ToString(), shapeStr);

        Debug.Log($"[PuzzleNotes] Entry saved for {_metrics.puzzleId}");
        Close();
    }

    private void AppendToAnalysisFile(string entry, string shapeStr)
    {
        if (!File.Exists(AnalysisPath))
        {
            Debug.LogError($"[PuzzleNotes] Could not find puzzle_analysis.md at {AnalysisPath}");
            return;
        }

        string content = File.ReadAllText(AnalysisPath);

        // Insert entry before the quick-reference table
        const string tableMarker = "## Quick reference table";
        int tableIdx = content.IndexOf(tableMarker, StringComparison.Ordinal);
        if (tableIdx >= 0)
            content = content.Insert(tableIdx, entry);
        else
            content += entry;

        // Append row to quick-reference table
        string row =
            $"| {_metrics.puzzleId} " +
            $"| {_metrics.totalStates:N0} " +
            $"| {_metrics.solutionLength} " +
            $"| {_metrics.solutionPathCount} " +
            $"| {_metrics.diameter} " +
            $"| {_metrics.deceptionRatio:F1} " +
            $"| {_metrics.deadEndRatio:P0} " +
            $"| {_metrics.bottleneckCount} " +
            $"| {_metrics.ahaDepthMin} " +
            $"| {shapeStr} " +
            $"| {_ratingDeception} " +
            $"| {_ratingSatisfaction} |";

        // Find the last row of the table and append after it
        int lastPipe = content.LastIndexOf("\n| ", StringComparison.Ordinal);
        if (lastPipe >= 0)
        {
            int endOfRow = content.IndexOf('\n', lastPipe + 1);
            if (endOfRow >= 0)
                content = content.Insert(endOfRow, "\n" + row);
            else
                content += "\n" + row;
        }

        File.WriteAllText(AnalysisPath, content);
    }
}
#endif
