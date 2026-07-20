using System.Collections.Generic;
using System.Linq;
using ContentKit;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Three-view graph scene, cycled with on-screen prev/next buttons.
///
///  A  Solution Path Chain  — the optimal path as a horizontal node chain.
///     Each node = one board state. Size = mobility at that step.
///     Label: move #, which car moved, mobility count. Always N+1 nodes.
///
///  B  Mobility Profile  — 2-line CKDataGraph: mobility vs productive moves.
///     Directly visualises H1 (deception) and H2 (bottleneck / aha moment).
///
///  C  Reachability Histogram  — states per distance-to-goal bar chart.
///     Visualises H4 (graph-shape difficulty tiers).
///
/// Camera pan: right-click drag. Zoom: scroll wheel.
/// </summary>
public class GraphManager : MonoBehaviour
{
    [Header("Prefabs / materials")]
    public Material lineMaterial;

    [Header("Path Chain layout")]
    public float nodeSpacing  = 8f;
    public float baseNodeSize = 0.6f;
    public float mobilityScale = 0.08f;   // extra size per available move

    // -----------------------------------------------------------------------

    private PuzzleDefinition _def;
    private PuzzleState      _start;
    private List<PuzzleState> _path;
    private Dictionary<ulong, int>   _depths;
    private Dictionary<ulong, int>   _dist;
    private List<(int mobility, int productive)> _mobility;
    private int[]  _reach;
    private HashSet<ulong> _bottlenecks;
    private HashSet<ulong> _forced;
    private Dictionary<ulong, List<ulong>> _graph;
    private int _optimal;
    private RushHourMetrics.Result _rh;
    private int _distinctStrategies;

    private enum ViewMode { PathChain, DependencyDAG, MobilityProfile, Reachability }
    private ViewMode _view = ViewMode.PathChain;

    private readonly List<GameObject> _viewObjects = new();
    private Camera _cam;
    private Vector3 _dragOriginWorld;
    private bool    _dragging;

    private Button  _prevBtn, _nextBtn;
    private Text    _titleText, _subtitleText;

    // -----------------------------------------------------------------------

    private void Start()
    {
        if (CrossSceneData.TargetDef == null)
        {
            Debug.LogError("[GraphManager] No puzzle definition in CrossSceneData.");
            return;
        }
        _def   = CrossSceneData.TargetDef;
        _start = CrossSceneData.TargetState;

        _cam = Camera.main;
        if (_cam != null) _cam.backgroundColor = CKColor.Void;

        var solver = new BitboardSolver();
        _path = solver.Solve(_def, _start);
        if (_path == null || _path.Count == 0) { Debug.LogError("Unsolvable."); return; }

        _optimal = _path.Count - 1;
        _graph   = solver.GraphEdges;

        bool IsGoal(ulong s) =>
            new PuzzleState(s).GetCarPos(BitboardSolver.GOAL_CAR_INDEX)
                == BitboardSolver.GOAL_POSITION;

        _depths     = GraphAnalysis.DepthsFromStart(_graph, _start.Data);
        var goals   = GraphAnalysis.GoalNodes(_graph, IsGoal);
        _dist       = GraphAnalysis.DistanceToGoal(_graph, goals);
        _bottlenecks= GraphAnalysis.Bottlenecks(_graph);
        _mobility   = GraphAnalysis.MobilityProfile(
                          _path.Select(p => p.Data).ToList(), _graph, _dist);
        _reach      = GraphAnalysis.ReachabilityHistogram(_dist);
        _forced     = GraphAnalysis.ForcedNodes(_graph, _depths, _dist, _start.Data, _optimal);
        _distinctStrategies = GraphAnalysis.DistinctFirstMoves(_graph, _dist, _start.Data, _optimal);
        _rh         = RushHourMetrics.Analyze(_def, _path);

        GraphMetricsCsv.Append(new GraphAnalysis.GraphMetrics
        {
            TotalStates = _graph.Count,
            Optimal     = _optimal,
            DeceptionRatio = _optimal > 0 ? (float)_graph.Count / _optimal : 0f,
            MinMobility = _mobility.Count > 0 ? _mobility.Min(m => m.mobility) : 0,
            MeanProductiveFraction = _mobility.Count > 0
                ? (float)_mobility.Average(m =>
                    m.mobility > 0 ? (double)m.productive / m.mobility : 0.0) : 0f,
            SubgraphSize   = _optimal + 1,
            BottleneckCount= _bottlenecks.Count,
            CounterintuitiveMoves = _rh.CounterintuitiveMoves,
            CounterintuitiveFrac  = _rh.CounterintuitiveFrac,
            DependencyDepth       = _rh.DependencyDepth,
            DependencyWidth       = _rh.DependencyWidth,
            ForcedNodeCount       = _forced.Count,
            DistinctStrategies    = _distinctStrategies,
        }, string.IsNullOrEmpty(CrossSceneData.PuzzleId)
              ? _start.Data.ToString() : CrossSceneData.PuzzleId);

        BuildHUD();
        ShowView(_view);
    }

    // -----------------------------------------------------------------------
    // View switching
    // -----------------------------------------------------------------------

    private void ShowView(ViewMode v)
    {
        _view = v;
        foreach (var obj in _viewObjects) Destroy(obj);
        _viewObjects.Clear();

        if (_cam != null)
        {
            _cam.transform.position = new Vector3(0f, 0f, -20f);
            _cam.orthographic       = false;
            _cam.fieldOfView        = 60f;
        }

        switch (v)
        {
            case ViewMode.PathChain:         BuildPathChain();     break;
            case ViewMode.DependencyDAG:     BuildDependencyDAG(); break;
            case ViewMode.MobilityProfile:   BuildMobilityChart(); break;
            case ViewMode.Reachability:      BuildReachChart();    break;
        }

        UpdateHUD();
    }

    // -----------------------------------------------------------------------
    // View A — Solution Path Chain
    // -----------------------------------------------------------------------

    private void BuildPathChain()
    {
        int maxMobility = _mobility.Count > 0 ? _mobility.Max(m => m.mobility) : 1;

        // Position chain so it's centred on x=0.
        float totalWidth = (_path.Count - 1) * nodeSpacing;
        float startX     = -totalWidth * 0.5f;

        GameObject[] nodeObjs = new GameObject[_path.Count];

        for (int i = 0; i < _path.Count; i++)
        {
            ulong data = _path[i].Data;
            float x    = startX + i * nodeSpacing;
            bool isStart     = i == 0;
            bool isGoal      = i == _path.Count - 1;
            bool isBottleneck= !isStart && !isGoal && _bottlenecks.Contains(data);
            var (mob, prod)  = i < _mobility.Count ? _mobility[i] : (0, 0);

            float size = baseNodeSize + mob * mobilityScale;

            var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = $"Step_{i}";
            node.transform.position   = new Vector3(x, 0f, 0f);
            node.transform.localScale = Vector3.one * size;

            Color c;
            if (isStart)         c = CKColor.Sage;
            else if (isGoal)     c = CKColor.Coral;
            else if (isBottleneck) { c = CKColor.AmberBright; node.transform.localScale *= 1.4f; }
            else
            {
                float t = maxMobility > 0 ? (float)mob / maxMobility : 0.5f;
                c = Color.Lerp(CKColor.Steel, CKColor.Amber, t);
            }
            node.GetComponent<Renderer>().material.color = c;
            _viewObjects.Add(node);
            nodeObjs[i] = node;

            // Labels are placed in fixed rows clear of the largest node and the
            // edge line (which runs along y=0), so nothing overlaps.
            float maxRadius = (baseNodeSize + maxMobility * mobilityScale) * 1.4f * 0.5f;
            float topY =  maxRadius + 0.7f;
            float botY = -(maxRadius + 0.7f);

            // TOP row: move # + mobility count on a second line.
            string topLabel = isStart ? "Start"
                            : isGoal  ? "Goal ✓"
                            : $"Move {i}\n⬆{mob} moves";
            MakeLabel(topLabel, new Vector3(x, topY, 0f), 0.32f, CKColor.TextBright);

            // BOTTOM row: which car moved to reach this state.
            if (i > 0)
            {
                int movedCar = FindMovedCar(_path[i - 1], _path[i]);
                if (movedCar >= 0)
                {
                    string carLabel = movedCar == BitboardSolver.GOAL_CAR_INDEX
                        ? "★ Goal car" : $"Car {movedCar}";
                    MakeLabel(carLabel, new Vector3(x, botY, 0f), 0.28f, CKColor.TextSecondary);
                }
            }

            // AHA tag below the car row for bottleneck states.
            if (isBottleneck)
                MakeLabel("◆ AHA", new Vector3(x, botY - 0.55f, 0f), 0.34f, CKColor.AmberBright);

            // Counterintuitive step: this move didn't reduce the greedy heuristic
            // (you had to make it look worse). Marked in Coral above the node.
            if (i > 0 && _rh.StepCounterintuitive != null
                && i - 1 < _rh.StepCounterintuitive.Length
                && _rh.StepCounterintuitive[i - 1])
                MakeLabel("↩ against greedy", new Vector3(x, topY + 0.55f, 0f), 0.26f, CKColor.Coral);
        }

        // Edges
        for (int i = 0; i < nodeObjs.Length - 1; i++)
            MakeEdge(nodeObjs[i].transform.position, nodeObjs[i + 1].transform.position,
                     CKColor.TextPrimary, 0.08f);

        // Reframe camera
        if (_cam != null)
        {
            float mid = (startX + startX + totalWidth) * 0.5f;
            _cam.transform.position = new Vector3(mid, 0f, -Mathf.Max(12f, totalWidth * 0.6f));
        }
    }

    // -----------------------------------------------------------------------
    // View — Dependency DAG (the strongest research-backed predictor, ~0.82)
    // -----------------------------------------------------------------------

    private void BuildDependencyDAG()
    {
        var pieces = _rh.MovedPieces.OrderBy(p => _rh.DepRank.GetValueOrDefault(p, 1)).ToList();
        if (pieces.Count == 0)
        {
            MakeLabel("No moving pieces.", Vector3.zero, 0.5f, CKColor.TextSecondary);
            return;
        }

        // Layout: x = dependency rank (longest chain to this piece), y = spread
        // within rank. Rank runs deep→shallow left→right so chains read as flow.
        int maxRank = pieces.Max(p => _rh.DepRank.GetValueOrDefault(p, 1));
        var byRank = pieces.GroupBy(p => _rh.DepRank.GetValueOrDefault(p, 1))
                           .ToDictionary(g => g.Key, g => g.ToList());

        var nodePos = new Dictionary<int, Vector3>();
        const float xGap = 4.5f, yGap = 2.6f;
        foreach (var grp in byRank)
        {
            var list = grp.Value;
            float x = (grp.Key - 1) * xGap;
            float y0 = (list.Count - 1) * yGap * 0.5f;
            for (int j = 0; j < list.Count; j++)
            {
                int piece = list[j];
                var pos = new Vector3(x, y0 - j * yGap, 0f);
                nodePos[piece] = pos;

                var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.transform.position   = pos;
                node.transform.localScale = Vector3.one * 1.1f;
                node.GetComponent<Renderer>().material.color =
                    piece == BitboardSolver.GOAL_CAR_INDEX ? CKColor.Coral : CKColor.DataSeries(piece % 5);
                _viewObjects.Add(node);

                string tag = piece == BitboardSolver.GOAL_CAR_INDEX ? "★ Goal car" : $"Car {piece}";
                MakeLabel(tag, pos + new Vector3(0, 0.9f, 0), 0.32f, CKColor.TextBright);
            }
        }

        // Dependency edges A→B ("must move A before B").
        foreach (var (a, b) in _rh.DepEdges)
            if (nodePos.TryGetValue(a, out var pa) && nodePos.TryGetValue(b, out var pb))
                MakeEdge(pa, pb, CKColor.TextPrimary, 0.06f);

        // Callout — the metrics that best predict difficulty.
        MakeLabel(
            $"Dependency depth: {_rh.DependencyDepth}\n" +
            $"Independent chains: {_rh.DependencyWidth}\n" +
            $"(chain length ≈ Jarušek 0.82 difficulty predictor)",
            new Vector3((maxRank) * xGap * 0.5f, -yGap * 2.4f, 0f), 0.34f, CKColor.TextSecondary);

        if (_cam != null)
            _cam.transform.position = new Vector3(
                (maxRank - 1) * xGap * 0.5f, 0f, -Mathf.Max(12f, maxRank * xGap * 0.7f));
    }

    // -----------------------------------------------------------------------
    // View B — Mobility Profile
    // -----------------------------------------------------------------------

    private void BuildMobilityChart()
    {
        if (_cam != null) _cam.transform.position = new Vector3(2.5f, 1.5f, -12f);

        var go = new GameObject("MobilityGraph");
        go.transform.position = Vector3.zero;
        var graph = go.AddComponent<CKDataGraph>();
        graph.width  = 8f;
        graph.height = 5f;
        _viewObjects.Add(go);

        graph.SetWindowSize(Mathf.Max(2, _mobility.Count));
        graph.AddSeries("mobility",   0);  // Amber
        graph.AddSeries("productive", 2);  // Sage
        foreach (var (m, p) in _mobility)
        {
            graph.Push("mobility",   m);
            graph.Push("productive", p);
        }
        graph.SetTitle("Mobility along solution path");
        graph.SetXLabel("move #");
        graph.SetYLabel("legal moves");

        // Annotate aha steps
        for (int i = 0; i < _path.Count; i++)
            if (_bottlenecks.Contains(_path[i].Data))
                MakeLabel($"AHA\n↑ move {i}", new Vector3(
                    (float)i / Mathf.Max(1, _mobility.Count - 1) * graph.width,
                    _mobility[i].mobility + 0.3f, 0f), 0.25f, CKColor.AmberBright);
    }

    // -----------------------------------------------------------------------
    // View C — Reachability Histogram
    // -----------------------------------------------------------------------

    private void BuildReachChart()
    {
        if (_cam != null) _cam.transform.position = new Vector3(2.5f, 1.5f, -12f);

        var go = new GameObject("ReachabilityGraph");
        go.transform.position = Vector3.zero;
        var graph = go.AddComponent<CKDataGraph>();
        graph.width  = 8f;
        graph.height = 5f;
        _viewObjects.Add(go);

        graph.SetWindowSize(Mathf.Max(2, _reach.Length));
        graph.AddSeries("states", 1);  // Steel
        foreach (var c in _reach) graph.Push("states", c);
        graph.SetTitle($"States by distance to goal  ({_graph.Count:N0} total)");
        graph.SetXLabel("distance to goal");
        graph.SetYLabel("# states (log10)");

        MakeLabel($"Total states: {_graph.Count:N0}\nOptimal: {_optimal} moves\nDeception: {(float)_graph.Count / _optimal:F0}x",
            new Vector3(graph.width + 0.3f, graph.height * 0.75f, 0f), 0.28f, CKColor.TextSecondary);
    }

    // -----------------------------------------------------------------------
    // HUD (UGUI — simpler than UIToolkit for runtime spawning)
    // -----------------------------------------------------------------------

    private void BuildHUD()
    {
        var canvasGo = new GameObject("HUD_Canvas");
        _viewObjects.Add(canvasGo);   // will be rebuilt on view switch — exclude from clearing:
        _viewObjects.RemoveAt(_viewObjects.Count - 1); // always keep HUD; rebuild manually

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // GraphScene has no EventSystem — UI buttons can't receive clicks without
        // one. Create it at runtime so the scene works standalone.
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Title bar at top
        _titleText    = MakeUIText(canvasGo, "TitleText", 26, TextAnchor.MiddleCenter,
            new Rect(0.1f, 0.92f, 0.8f, 0.07f), CKColor.TextBright);
        _subtitleText = MakeUIText(canvasGo, "SubtitleText", 16, TextAnchor.MiddleCenter,
            new Rect(0.1f, 0.86f, 0.8f, 0.06f), CKColor.TextMuted);

        // Prev / Next buttons
        _prevBtn = MakeUIButton(canvasGo, "◀ Prev", new Rect(0.01f, 0.01f, 0.12f, 0.07f),
            () => CycleView(-1));
        _nextBtn = MakeUIButton(canvasGo, "Next ▶", new Rect(0.87f, 0.01f, 0.12f, 0.07f),
            () => CycleView(+1));

        // Instruction hint
        MakeUIText(canvasGo, "HintText", 14, TextAnchor.MiddleCenter,
            new Rect(0.15f, 0.01f, 0.7f, 0.06f), CKColor.TextMuted)
            .text = "← / → or Space = switch view  |  Scroll = zoom  |  Right-drag = pan";
    }

    private void UpdateHUD()
    {
        // Order must match the ViewMode enum:
        // PathChain, DependencyDAG, MobilityProfile, Reachability.
        string[] titles =
        {
            "Solution Path Chain",
            "Dependency Structure",
            "Mobility Profile",
            "Reachability Histogram",
        };
        string[] subs =
        {
            $"{_optimal + 1} nodes  |  {_forced.Count} forced state(s)  |  {_rh.CounterintuitiveMoves} counterintuitive move(s)",
            $"depth {_rh.DependencyDepth} · {_rh.DependencyWidth} independent chain(s)  |  best predictor of human difficulty (~0.82)",
            $"Mobility = legal moves available  |  Productive = moves that reduce distance to goal",
            $"{_graph.Count:N0} reachable states  |  Optimal: {_optimal}  |  Strategies: {_distinctStrategies}  |  Deception: {(float)_graph.Count / _optimal:F0}×",
        };
        if (_titleText)    _titleText.text    = titles[(int)_view];
        if (_subtitleText) _subtitleText.text = subs[(int)_view];
    }

    private void CycleView(int dir)
    {
        int count = System.Enum.GetValues(typeof(ViewMode)).Length;
        _view = (ViewMode)(((int)_view + dir + count) % count);
        ShowView(_view);
    }

    // -----------------------------------------------------------------------
    // Camera controls (pan + zoom)
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_cam == null) return;

        // Keyboard view cycling (fallback / convenience).
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space))
            CycleView(+1);
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            CycleView(-1);

        // Scroll to zoom (FOV)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - scroll * 15f, 5f, 90f);
        }

        // Right-click drag to pan
        if (Input.GetMouseButtonDown(1))
        {
            _dragging = true;
            _dragOriginWorld = _cam.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
        }
        if (Input.GetMouseButtonUp(1)) _dragging = false;
        if (_dragging)
        {
            var curWorld = _cam.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            _cam.transform.position += _dragOriginWorld - curWorld;
            _dragOriginWorld = _cam.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private int FindMovedCar(PuzzleState a, PuzzleState b)
    {
        for (int i = 0; i < _def.CarCount; i++)
            if (a.GetCarPos(i) != b.GetCarPos(i)) return i;
        return -1;
    }

    private GameObject MakeLabel(string text, Vector3 pos, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text          = text;
        tm.fontSize      = 24;
        tm.characterSize = size;
        tm.color         = color;
        tm.alignment     = TextAlignment.Center;
        tm.anchor        = TextAnchor.MiddleCenter;
        _viewObjects.Add(go);
        return go;
    }

    private void MakeEdge(Vector3 from, Vector3 to, Color color, float width)
    {
        var go = new GameObject("Edge");
        var lr = go.AddComponent<LineRenderer>();
        if (lineMaterial) lr.material = lineMaterial;
        lr.startWidth = width; lr.endWidth = width * 0.6f;
        lr.startColor = color; lr.endColor = Color.Lerp(color, Color.clear, 0.4f);
        lr.SetPosition(0, from); lr.SetPosition(1, to);
        _viewObjects.Add(go);
    }

    private static Text MakeUIText(GameObject canvas, string name, int fontSize,
        TextAnchor align, Rect anchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y);
        rt.anchorMax = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.alignment = align;
        txt.color     = color;
        return txt;
    }

    private static Button MakeUIButton(GameObject canvas, string label, Rect anchor,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y);
        rt.anchorMax = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = CKColor.Raised;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = CKColor.Raised;
        colors.highlightedColor = CKColor.Border;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 22;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = CKColor.TextPrimary;
        txt.text      = label;

        return btn;
    }
}
