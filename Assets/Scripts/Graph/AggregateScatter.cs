using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ContentKit;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cross-puzzle scatter plot, ContentKit-styled. Joins graph_summary.csv with
/// ratings.csv (by puzzle_id) and plots any two numeric columns. Points are
/// colored by satisfaction rating when available. CKDataGraph is line-only, so
/// this is a small custom point renderer using CKColor.
///
/// X / Y axis cyclers + Menu button in a runtime uGUI HUD.
/// Scroll = zoom, right-drag = pan.
/// </summary>
public class AggregateScatter : MonoBehaviour
{
    private static readonly string[] Columns =
    {
        // research-aligned first (defaults point at the strongest pairing)
        "dependency_depth", "difficulty", "counterintuitive_moves", "counterintuitive_frac",
        "dependency_width", "forced_node_count", "distinct_strategies", "satisfaction",
        // static baseline family
        "deception_ratio", "total_states", "optimal", "min_mobility",
        "mean_productive_frac", "subgraph_size", "bottleneck_count",
    };

    private const float PlotW = 10f;
    private const float PlotH = 6f;

    private List<Dictionary<string, string>> _rows;
    private int _xCol = 0; // dependency_depth (strongest predictor)
    private int _yCol = 1; // difficulty

    private readonly List<GameObject> _plotObjects = new();
    private Camera _cam;
    private Text _titleText;
    private Text _xLabel, _yLabel;

    private Vector3 _dragOrigin;
    private bool    _dragging;

    // -----------------------------------------------------------------------

    private void Start()
    {
        _cam = Camera.main;
        if (_cam != null)
        {
            _cam.backgroundColor = CKColor.Void;
            _cam.transform.position = new Vector3(PlotW * 0.5f, PlotH * 0.5f, -12f);
        }

        _rows = CsvTable.JoinedRows();
        BuildHUD();
        Redraw();
    }

    // -----------------------------------------------------------------------
    // Plot
    // -----------------------------------------------------------------------

    private void Redraw()
    {
        foreach (var o in _plotObjects) Destroy(o);
        _plotObjects.Clear();

        if (_titleText) _titleText.text =
            $"Cross-puzzle scatter   (n = {_rows.Count})   — small n is suggestive, not proof";
        if (_xLabel) _xLabel.text = $"X: {Columns[_xCol]}";
        if (_yLabel) _yLabel.text = $"Y: {Columns[_yCol]}";

        // Axes
        MakeLine(new Vector3(0, 0, 0), new Vector3(PlotW, 0, 0), CKColor.Border);
        MakeLine(new Vector3(0, 0, 0), new Vector3(0, PlotH, 0), CKColor.Border);
        MakeText(Columns[_xCol], new Vector3(PlotW * 0.5f, -0.6f, 0f), 0.4f, CKColor.TextBright);
        MakeText(Columns[_yCol], new Vector3(-0.8f, PlotH * 0.5f, 0f), 0.4f, CKColor.TextBright, 90f);

        var pts = CollectPoints(out float xMin, out float xMax, out float yMin, out float yMax);
        if (pts.Count == 0)
        {
            MakeText("No data yet — play & rate puzzles, open Graph on each.",
                new Vector3(PlotW * 0.5f, PlotH * 0.5f, 0f), 0.5f, CKColor.TextSecondary);
            return;
        }

        // Axis min/max ticks
        MakeText(xMin.ToString("G3", CultureInfo.InvariantCulture), new Vector3(0, -0.35f, 0), 0.28f, CKColor.TextSecondary);
        MakeText(xMax.ToString("G3", CultureInfo.InvariantCulture), new Vector3(PlotW, -0.35f, 0), 0.28f, CKColor.TextSecondary);
        MakeText(yMin.ToString("G3", CultureInfo.InvariantCulture), new Vector3(-0.45f, 0, 0), 0.28f, CKColor.TextSecondary);
        MakeText(yMax.ToString("G3", CultureInfo.InvariantCulture), new Vector3(-0.45f, PlotH, 0), 0.28f, CKColor.TextSecondary);

        foreach (var (px, py, rating) in pts)
        {
            float sx = Mathf.Approximately(xMax, xMin) ? 0.5f : (px - xMin) / (xMax - xMin);
            float sy = Mathf.Approximately(yMax, yMin) ? 0.5f : (py - yMin) / (yMax - yMin);
            var pos = new Vector3(sx * PlotW, sy * PlotH, 0f);

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.position = pos;
            dot.transform.localScale = Vector3.one * 0.28f;
            dot.GetComponent<Renderer>().material.color = RatingColor(rating);
            _plotObjects.Add(dot);
        }
    }

    /// <summary>Returns (x, y, rating) for rows where both axes parse. rating = -1 if unrated.</summary>
    private List<(float x, float y, int rating)> CollectPoints(
        out float xMin, out float xMax, out float yMin, out float yMax)
    {
        var list = new List<(float, float, int)>();
        xMin = yMin = float.MaxValue; xMax = yMax = float.MinValue;

        foreach (var row in _rows)
        {
            if (!TryGet(row, Columns[_xCol], out float x)) continue;
            if (!TryGet(row, Columns[_yCol], out float y)) continue;
            int rating = TryGet(row, "satisfaction", out float rf) ? Mathf.RoundToInt(rf) : -1;

            list.Add((x, y, rating));
            xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
            yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
        }

        // Pad degenerate ranges a touch.
        if (list.Count > 0)
        {
            if (Mathf.Approximately(xMin, xMax)) { xMin -= 1; xMax += 1; }
            if (Mathf.Approximately(yMin, yMax)) { yMin -= 1; yMax += 1; }
        }
        return list;
    }

    private static bool TryGet(Dictionary<string, string> row, string col, out float v)
    {
        v = 0;
        return row.TryGetValue(col, out var s)
            && !string.IsNullOrEmpty(s)
            && float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }

    private static Color RatingColor(int rating)
    {
        if (rating < 0) return CKColor.Steel;          // unrated
        float t = Mathf.Clamp01((rating - 1) / 4f);    // 1..5 → 0..1
        return t < 0.5f
            ? Color.Lerp(CKColor.Coral, CKColor.Amber, t * 2f)
            : Color.Lerp(CKColor.Amber, CKColor.Sage, (t - 0.5f) * 2f);
    }

    // -----------------------------------------------------------------------
    // HUD
    // -----------------------------------------------------------------------

    private void BuildHUD()
    {
        var canvasGo = new GameObject("HUD",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _titleText = UIText(canvasGo, "Title", 22, TextAnchor.MiddleCenter,
            new Rect(0.1f, 0.93f, 0.8f, 0.06f), CKColor.TextBright);

        // X cyclers
        UIButton(canvasGo, "◀", new Rect(0.02f, 0.02f, 0.04f, 0.06f), () => Cycle(ref _xCol, -1));
        _xLabel = UIText(canvasGo, "XLabel", 18, TextAnchor.MiddleCenter,
            new Rect(0.06f, 0.02f, 0.20f, 0.06f), CKColor.Amber);
        UIButton(canvasGo, "▶", new Rect(0.26f, 0.02f, 0.04f, 0.06f), () => Cycle(ref _xCol, +1));

        // Y cyclers
        UIButton(canvasGo, "◀", new Rect(0.40f, 0.02f, 0.04f, 0.06f), () => Cycle(ref _yCol, -1));
        _yLabel = UIText(canvasGo, "YLabel", 18, TextAnchor.MiddleCenter,
            new Rect(0.44f, 0.02f, 0.20f, 0.06f), CKColor.Amber);
        UIButton(canvasGo, "▶", new Rect(0.64f, 0.02f, 0.04f, 0.06f), () => Cycle(ref _yCol, +1));

        // Menu
        UIButton(canvasGo, "← Menu", new Rect(0.86f, 0.02f, 0.12f, 0.06f),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));

        UIText(canvasGo, "Hint", 13, TextAnchor.MiddleCenter,
            new Rect(0.3f, 0.02f, 0.1f, 0.06f), CKColor.TextMuted).text = "Scroll=zoom";
    }

    private void Cycle(ref int idx, int dir)
    {
        idx = (idx + dir + Columns.Length) % Columns.Length;
        Redraw();
    }

    // -----------------------------------------------------------------------
    // Camera
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_cam == null) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - scroll * 15f, 5f, 90f);

        if (Input.GetMouseButtonDown(1))
        { _dragging = true; _dragOrigin = ScreenWorld(); }
        if (Input.GetMouseButtonUp(1)) _dragging = false;
        if (_dragging)
        {
            var cur = ScreenWorld();
            _cam.transform.position += _dragOrigin - cur;
            _dragOrigin = ScreenWorld();
        }
    }

    private Vector3 ScreenWorld() =>
        _cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));

    // -----------------------------------------------------------------------
    // Primitive helpers
    // -----------------------------------------------------------------------

    private void MakeLine(Vector3 a, Vector3 b, Color color)
    {
        var go = new GameObject("Axis");
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = lr.endWidth = 0.04f;
        lr.startColor = lr.endColor = color;
        lr.SetPosition(0, a); lr.SetPosition(1, b);
        _plotObjects.Add(go);
    }

    private void MakeText(string text, Vector3 pos, float size, Color color, float zRot = 0f)
    {
        var go = new GameObject("Lbl");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, zRot);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text; tm.fontSize = 24; tm.characterSize = size;
        tm.color = color; tm.alignment = TextAlignment.Center; tm.anchor = TextAnchor.MiddleCenter;
        _plotObjects.Add(go);
    }

    private static Text UIText(GameObject canvas, string name, int fontSize,
        TextAnchor align, Rect anchor, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y);
        rt.anchorMax = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize; txt.alignment = align; txt.color = color;
        return txt;
    }

    private static void UIButton(GameObject canvas, string label, Rect anchor,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y);
        rt.anchorMax = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = CKColor.Raised;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var t = new GameObject("Label", typeof(Text));
        t.transform.SetParent(go.transform, false);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = t.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 18; txt.alignment = TextAnchor.MiddleCenter;
        txt.color = CKColor.TextPrimary; txt.text = label;
    }
}
