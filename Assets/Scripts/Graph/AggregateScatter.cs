using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ContentKit;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cross-puzzle scatter, rendered in screen-space uGUI (a 2D plot doesn't need
/// 3D). Joins graph_summary.csv with ratings.csv by puzzle_id and plots any two
/// numeric columns; points are colored by satisfaction. Palette B (CKColor).
/// </summary>
public class AggregateScatter : MonoBehaviour
{
    private static readonly string[] Columns =
    {
        "dependency_depth", "difficulty", "counterintuitive_moves", "counterintuitive_frac",
        "dependency_width", "forced_node_count", "distinct_strategies", "satisfaction",
        "deception_ratio", "total_states", "optimal", "min_mobility",
        "mean_productive_frac", "subgraph_size", "bottleneck_count",
    };

    private List<Dictionary<string, string>> _rows;
    private int _xCol = 0; // dependency_depth
    private int _yCol = 1; // difficulty

    private RectTransform _plot;      // inset plot area
    private Text _title, _xLabel, _yLabel, _xMin, _xMax, _yMin, _yMax, _empty;
    private readonly List<GameObject> _points = new();
    private Sprite _dot;

    // -----------------------------------------------------------------------

    private void Start()
    {
        if (Camera.main != null) Camera.main.backgroundColor = CKColor.Void;
        _rows = CsvTable.JoinedRows();
        BuildUI();
        Redraw();
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Full-screen Void background.
        var bg = Panel(canvasGo.transform, "BG", new Vector2(0, 0), new Vector2(1, 1), CKColor.Void);

        // Plot area (inset margins).
        _plot = Panel(canvasGo.transform, "Plot",
            new Vector2(0.10f, 0.14f), new Vector2(0.93f, 0.86f), CKColor.Surface).GetComponent<RectTransform>();
        // subtle border
        Panel(_plot, "Frame", Vector2.zero, Vector2.one, new Color(0,0,0,0))
            .GetComponent<Image>().color = new Color(0,0,0,0);

        // Axes lines (thin bars on left + bottom of plot).
        Bar(_plot, "YAxis", new Vector2(0, 0), new Vector2(0, 1), 2.5f, true, CKColor.Border);
        Bar(_plot, "XAxis", new Vector2(0, 0), new Vector2(1, 0), 2.5f, false, CKColor.Border);

        // Labels
        _title = Label(canvasGo.transform, "Title", new Vector2(0.1f, 0.93f), new Vector2(0.9f, 0.99f),
            24, TextAnchor.MiddleCenter, CKColor.TextBright);
        _xLabel = Label(canvasGo.transform, "XLabel", new Vector2(0.10f, 0.06f), new Vector2(0.93f, 0.11f),
            22, TextAnchor.MiddleCenter, CKColor.Amber);
        _yLabel = Label(canvasGo.transform, "YLabel", new Vector2(0.005f, 0.14f), new Vector2(0.055f, 0.86f),
            22, TextAnchor.MiddleCenter, CKColor.Amber);
        _yLabel.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 90);

        _xMin = Corner(canvasGo.transform, new Vector2(0.10f, 0.115f));
        _xMax = Corner(canvasGo.transform, new Vector2(0.90f, 0.115f));
        _yMin = Corner(canvasGo.transform, new Vector2(0.065f, 0.15f));
        _yMax = Corner(canvasGo.transform, new Vector2(0.065f, 0.84f));

        _empty = Label(canvasGo.transform, "Empty", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f),
            22, TextAnchor.MiddleCenter, CKColor.TextSecondary);
        _empty.gameObject.SetActive(false);

        // Axis cyclers + menu
        Btn(canvasGo.transform, "◀", new Vector2(0.02f, 0.02f), new Vector2(0.05f, 0.07f), () => Cycle(ref _xCol, -1));
        Btn(canvasGo.transform, "X ▶", new Vector2(0.055f, 0.02f), new Vector2(0.10f, 0.07f), () => Cycle(ref _xCol, +1));
        Btn(canvasGo.transform, "◀", new Vector2(0.14f, 0.02f), new Vector2(0.17f, 0.07f), () => Cycle(ref _yCol, -1));
        Btn(canvasGo.transform, "Y ▶", new Vector2(0.175f, 0.02f), new Vector2(0.22f, 0.07f), () => Cycle(ref _yCol, +1));
        Btn(canvasGo.transform, "← Menu", new Vector2(0.87f, 0.02f), new Vector2(0.98f, 0.07f),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));
    }

    // -----------------------------------------------------------------------

    private void Redraw()
    {
        foreach (var p in _points) Destroy(p);
        _points.Clear();

        int rated = _rows.Count(r => r.TryGetValue("difficulty", out var d) && !string.IsNullOrEmpty(d));
        _title.text = $"Cross-puzzle scatter   ·   {_rows.Count} puzzles, {rated} rated   ·   small n is suggestive, not proof";
        _xLabel.text = Columns[_xCol];
        _yLabel.text = Columns[_yCol];

        var pts = Collect(out float xMin, out float xMax, out float yMin, out float yMax);

        if (pts.Count == 0)
        {
            _empty.gameObject.SetActive(true);
            _empty.text = rated == 0 && (Columns[_xCol] == "difficulty" || Columns[_yCol] == "difficulty"
                             || Columns[_xCol] == "satisfaction" || Columns[_yCol] == "satisfaction")
                ? "No rated puzzles yet.\nSolve puzzles (rate difficulty + satisfaction),\nthen open Graph on each to record metrics."
                : "No data for these axes yet.\nOpen Graph on some puzzles first.";
            _xMin.text = _xMax.text = _yMin.text = _yMax.text = "";
            return;
        }
        _empty.gameObject.SetActive(false);

        _xMin.text = xMin.ToString("G3", CultureInfo.InvariantCulture);
        _xMax.text = xMax.ToString("G3", CultureInfo.InvariantCulture);
        _yMin.text = yMin.ToString("G3", CultureInfo.InvariantCulture);
        _yMax.text = yMax.ToString("G3", CultureInfo.InvariantCulture);

        foreach (var (x, y, rating) in pts)
        {
            float sx = Mathf.Approximately(xMax, xMin) ? 0.5f : (x - xMin) / (xMax - xMin);
            float sy = Mathf.Approximately(yMax, yMin) ? 0.5f : (y - yMin) / (yMax - yMin);

            var dot = new GameObject("pt", typeof(Image));
            dot.transform.SetParent(_plot, false);
            var rt = dot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(sx, sy);
            rt.sizeDelta = new Vector2(18, 18);
            rt.anchoredPosition = Vector2.zero;
            var img = dot.GetComponent<Image>();
            img.sprite = Dot();
            img.color  = RatingColor(rating);
            _points.Add(dot);
        }
    }

    private List<(float x, float y, int rating)> Collect(
        out float xMin, out float xMax, out float yMin, out float yMax)
    {
        var list = new List<(float, float, int)>();
        xMin = yMin = float.MaxValue; xMax = yMax = float.MinValue;
        foreach (var row in _rows)
        {
            if (!Num(row, Columns[_xCol], out float x)) continue;
            if (!Num(row, Columns[_yCol], out float y)) continue;
            int rating = Num(row, "satisfaction", out float rf) ? Mathf.RoundToInt(rf) : -1;
            list.Add((x, y, rating));
            xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
            yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
        }
        if (list.Count > 0)
        {
            // Pad so points don't sit exactly on the axes / degenerate ranges.
            if (Mathf.Approximately(xMin, xMax)) { xMin -= 1; xMax += 1; } else { float p = (xMax-xMin)*0.08f; xMin -= p; xMax += p; }
            if (Mathf.Approximately(yMin, yMax)) { yMin -= 1; yMax += 1; } else { float p = (yMax-yMin)*0.08f; yMin -= p; yMax += p; }
        }
        return list;
    }

    private static bool Num(Dictionary<string, string> row, string col, out float v)
    {
        v = 0;
        return row.TryGetValue(col, out var s) && !string.IsNullOrEmpty(s)
            && float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }

    private static Color RatingColor(int rating)
    {
        if (rating < 0) return CKColor.Steel;
        float t = Mathf.Clamp01((rating - 1) / 4f);
        return t < 0.5f ? Color.Lerp(CKColor.Coral, CKColor.Amber, t * 2f)
                        : Color.Lerp(CKColor.Amber, CKColor.Sage, (t - 0.5f) * 2f);
    }

    private void Cycle(ref int idx, int dir)
    {
        idx = (idx + dir + Columns.Length) % Columns.Length;
        Redraw();
    }

    // -----------------------------------------------------------------------
    // uGUI builders
    // -----------------------------------------------------------------------

    private static GameObject Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void Bar(Transform parent, string name, Vector2 aMin, Vector2 aMax,
        float thickness, bool vertical, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        if (vertical) rt.sizeDelta = new Vector2(thickness, 0);
        else          rt.sizeDelta = new Vector2(0, thickness);
        go.GetComponent<Image>().color = color;
    }

    private static Text Label(Transform parent, string name, Vector2 aMin, Vector2 aMax,
        int size, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.alignment = align; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Text Corner(Transform parent, Vector2 anchor)
    {
        var t = Label(parent, "Tick", anchor - new Vector2(0.03f, 0.02f), anchor + new Vector2(0.03f, 0.02f),
            15, TextAnchor.MiddleCenter, CKColor.TextSecondary);
        return t;
    }

    private static void Btn(Transform parent, string label, Vector2 aMin, Vector2 aMax,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = CKColor.Raised;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = Label(go.transform, "L", Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter, CKColor.TextPrimary);
        t.text = label;
    }

    private Sprite Dot()
    {
        if (_dot != null) return _dot;
        int res = 32; var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = res / 2f, r = res / 2f - 1f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
        }
        tex.Apply();
        _dot = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        return _dot;
    }
}
