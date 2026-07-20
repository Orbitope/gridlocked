using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Puzzle Definition")]
    // Since we generate at runtime, this will hold the current puzzle.
    public PuzzleDefinition Def;
    public PuzzleState CurrentState;
    
    [Header("State Tracking")]
    public Stack<PuzzleState> UndoStack = new Stack<PuzzleState>();

    [Header("Visual Prefabs")]
    public GameObject carPrefab;
    public Transform boardContainer;

    private Dictionary<int, CarController> _carVisuals = new Dictionary<int, CarController>();
    private PuzzleGenerator _generator;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        
        if (carPrefab == null) 
            carPrefab = Resources.Load<GameObject>("CarPrefab");
            
        if (boardContainer == null) 
            boardContainer = GameObject.Find("BoardContainer")?.transform;
            
        _generator = new PuzzleGenerator();
    }

    public LevelData? CurrentLevelData;
    public int CurrentMoveCount => UndoStack.Count;

    // Stable ID for the currently-loaded puzzle (start state), used to join
    // graph metrics with satisfaction ratings. Set on every puzzle load.
    private ulong _initialStateData;
    public string PuzzleId => CurrentLevelData.HasValue
        ? $"level_{CurrentLevelData.Value.ID}"
        : _initialStateData.ToString();

    private void Start()
    {
        GenerateGrid();
        
        // If no level was passed in (e.g. testing the scene directly), pick a
        // starting puzzle. Web/player build uses a curated level (no heavy
        // runtime generation); research build generates a random one.
        if (CurrentLevelData == null)
        {
            if (BuildConfig.ResearchEnabled) LoadNewPuzzle();
            else                             LoadLevelByIndex(0);
        }
        else
        {
            LoadLevel(CurrentLevelData.Value);
        }
    }

    private void GenerateGrid()
    {
        if (boardContainer.childCount > 0 && boardContainer.GetChild(0).name.StartsWith("Cell_")) return; // Already generated
        
        float cellSize = 100f;
        Color colorA = new Color32(0x1E,0x1C,0x16,255);
        Color colorB = new Color32(0x2C,0x2A,0x22,255);

        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                cell.transform.SetParent(boardContainer, false);
                cell.transform.SetAsFirstSibling();

                RectTransform cellRT = cell.AddComponent<RectTransform>();
                cellRT.anchorMin = Vector2.zero;
                cellRT.anchorMax = Vector2.zero;
                cellRT.sizeDelta = new Vector2(cellSize, cellSize);
                cellRT.anchoredPosition = new Vector2(x * cellSize + (cellSize/2), y * cellSize + (cellSize/2));

                UnityEngine.UI.Image img = cell.AddComponent<UnityEngine.UI.Image>();
                img.color = ((x + y) % 2 == 0) ? colorA : colorB;
            }
        }
    }

    // Index of the current curated level in the database (-1 if random/custom).
    private int _currentLevelIndex = -1;

    /// <summary>Load curated level by its index in the LevelDatabase (tracks index for auto-advance).</summary>
    public void LoadLevelByIndex(int index)
    {
        var db = Resources.Load<LevelDatabase>("LevelDatabase");
        if (db == null || db.Levels.Count == 0) return;
        index = ((index % db.Levels.Count) + db.Levels.Count) % db.Levels.Count;
        _currentLevelIndex = index;
        LoadLevel(db.Levels[index]);
    }

    /// <summary>Advance to the next curated level (web build auto-advance on solve).</summary>
    public void LoadNextCuratedLevel() => LoadLevelByIndex(_currentLevelIndex + 1);

    public void LoadLevel(LevelData level)
    {
        UndoStack.Clear();
        CurrentLevelData = level;
        Def = level.GetDefinition();
        CurrentState = new PuzzleState(level.InitialStateData);
        _initialStateData = CurrentState.Data;
        SpawnCars();
    }

    public void ClearBoard()
    {
        foreach (var car in _carVisuals.Values)
        {
            Destroy(car.gameObject);
        }
        _carVisuals.Clear();
    }

    public void LoadCustomPuzzle(int carCount, int minMoves)
    {
        UndoStack.Clear();
        CurrentLevelData = null; _currentLevelIndex = -1; // Mark as random puzzle
        
        if (_generator.TryGenerateDensityFirst(carCount, minMoves, out PuzzleDefinition def, out PuzzleState state, out PuzzleQualityMetrics metrics))
        {
            Def = def;
            CurrentState = state;
            _initialStateData = CurrentState.Data;
            SpawnCars();
        }
        else
        {
            Debug.LogError("Failed to generate puzzle.");
        }
    }

    [Header("Random puzzle ranges (auto-advance)")]
    public int minCars = 6;
    public int maxCars = 12;
    public int minMovesLow = 6;
    public int minMovesHigh = 18;

    public void LoadNewPuzzle() => LoadRandomPuzzle();

    /// <summary>
    /// Generates a puzzle with randomized car count + minimum-move target, so the
    /// research corpus spreads across the difficulty space without manual slider
    /// picking. Retries other random params if a combo fails to generate.
    /// </summary>
    public void LoadRandomPuzzle()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int cars     = Random.Range(minCars, maxCars + 1);
            int minMoves = Random.Range(minMovesLow, minMovesHigh + 1);

            UndoStack.Clear();
            CurrentLevelData = null; _currentLevelIndex = -1;
            if (_generator.TryGenerateDensityFirst(cars, minMoves,
                    out PuzzleDefinition def, out PuzzleState state, out _))
            {
                Def = def;
                CurrentState = state;
                _initialStateData = CurrentState.Data;
                SpawnCars();
                Debug.Log($"[GameManager] Random puzzle: {cars} cars, min {minMoves} moves.");
                return;
            }
        }
        Debug.LogWarning("[GameManager] Random generation failed 12×; falling back to 8/8.");
        LoadCustomPuzzle(8, 8);
    }

    private void SpawnCars()
    {
        // Cleanup old cars
        foreach (var car in _carVisuals.Values)
        {
            Destroy(car.gameObject);
        }
        _carVisuals.Clear();

        // Spawn new cars
        for (int i = 0; i < Def.CarCount; i++)
        {
            GameObject carObj = Instantiate(carPrefab, boardContainer);
            CarController controller = carObj.GetComponent<CarController>();
            
            controller.Initialize(i, Def.Lengths[i], Def.IsHorizontal[i], Def.FixedAxis[i]);
            _carVisuals[i] = controller;
        }

        SyncCarVisuals();
    }

    public void CommitMove(int carIndex, int newPos)
    {
        if (CurrentState.GetCarPos(carIndex) != newPos)
        {
            UndoStack.Push(CurrentState);
            CurrentState.SetCarPos(carIndex, newPos);
            SyncCarVisuals();

            // Check Win
            if (carIndex == BitboardSolver.GOAL_CAR_INDEX && newPos == BitboardSolver.GOAL_POSITION)
            {
                HandleVictory();
            }
        }
    }

    private int _victoryMoves;
    private int _victoryOptimal;

    private void HandleVictory()
    {
        Debug.Log("Puzzle Solved!");
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySuccess();

        _victoryMoves   = CurrentMoveCount;
        _victoryOptimal = CurrentLevelData.HasValue ? CurrentLevelData.Value.OptimalMoves : -1;

        if (CurrentLevelData.HasValue)
            SaveManager.RecordVictory(CurrentLevelData.Value.ID, CurrentMoveCount);

        if (!BuildConfig.ResearchEnabled)
        {
            // Web/player build: no rating capture. Auto-advance to the next
            // curated level (or replay if this wasn't curated).
            if (_currentLevelIndex >= 0) Invoke(nameof(LoadNextCuratedLevel), 1.0f);
            else Invoke(nameof(LoadNewPuzzle), 1.0f);
            return;
        }

        // Research build: capture difficulty + satisfaction, then advance.
        ShowRatingPrompt();
    }

    // ---- Satisfaction rating overlay (research data capture) ----

    private GameObject _ratingOverlay;

    private int _pendingDifficulty = -1;
    private System.Action<int> _ratingCallback;

    // Two sequential 1–5 prompts: difficulty (mirrors the research), then
    // satisfaction (the video angle). Both keyed to the same PuzzleId.
    private void ShowRatingPrompt()
    {
        BuildPrompt($"Solved in {_victoryMoves} moves!\nHow HARD did that feel?",
            difficulty =>
            {
                _pendingDifficulty = difficulty;
                BuildPrompt("How SATISFYING was it?", satisfaction =>
                {
                    RatingsCsv.Append(PuzzleId, _pendingDifficulty, satisfaction,
                                      _victoryMoves, _victoryOptimal);
                    Debug.Log($"[GameManager] difficulty={_pendingDifficulty} " +
                              $"satisfaction={satisfaction} (puzzle {PuzzleId}).");
                    Invoke("LoadNewPuzzle", 0.3f);
                });
            });
    }

    private void BuildPrompt(string question, System.Action<int> onPick)
    {
        if (_ratingOverlay != null) Destroy(_ratingOverlay);
        _ratingCallback = onPick;

        _ratingOverlay = new GameObject("RatingOverlay",
            typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = _ratingOverlay.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var bg = new GameObject("BG", typeof(UnityEngine.UI.Image));
        bg.transform.SetParent(_ratingOverlay.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<UnityEngine.UI.Image>().color = new Color32(0x11, 0x10, 0x09, 220);

        var label = new GameObject("Prompt", typeof(UnityEngine.UI.Text));
        label.transform.SetParent(_ratingOverlay.transform, false);
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0.5f); lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(0, 90); lrt.sizeDelta = new Vector2(700, 90);
        var ltxt = label.GetComponent<UnityEngine.UI.Text>();
        ltxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ltxt.fontSize = 36; ltxt.alignment = TextAnchor.MiddleCenter;
        ltxt.color = new Color32(0xED, 0xE8, 0xDC, 255);
        ltxt.text = question;

        for (int i = 1; i <= 5; i++)
        {
            int val = i;
            var btn = new GameObject($"Rate{i}",
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            btn.transform.SetParent(_ratingOverlay.transform, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(90, 90);
            rt.anchoredPosition = new Vector2((i - 3) * 110, -30);
            btn.GetComponent<UnityEngine.UI.Image>().color = new Color32(0x2C, 0x2A, 0x22, 255);
            btn.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => PickRating(val));

            var t = new GameObject("n", typeof(UnityEngine.UI.Text));
            t.transform.SetParent(btn.transform, false);
            var trt = t.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tt = t.GetComponent<UnityEngine.UI.Text>();
            tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tt.fontSize = 40; tt.alignment = TextAnchor.MiddleCenter;
            tt.color = new Color32(0xC4, 0x9A, 0x3C, 255);
            tt.text = i.ToString();
        }
    }

    private void Update()
    {
        // Number keys 1–5 while a rating prompt is up.
        if (_ratingOverlay != null)
            for (int i = 1; i <= 5; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) { PickRating(i); break; }
    }

    private void PickRating(int val)
    {
        var cb = _ratingCallback;
        _ratingCallback = null;
        if (_ratingOverlay != null) { Destroy(_ratingOverlay); _ratingOverlay = null; }
        cb?.Invoke(val);
    }

    public void UndoMove()
    {
        if (UndoStack.Count > 0)
        {
            CurrentState = UndoStack.Pop();
            SyncCarVisuals();
        }
    }

    public void SyncCarVisuals()
    {
        for (int i = 0; i < Def.CarCount; i++)
        {
            int currentPos = CurrentState.GetCarPos(i);
            _carVisuals[i].UpdateVisualPosition(currentPos);
        }
    }

    public void GetDragBounds(int carIndex, out int minPos, out int maxPos)
    {
        // 1. Get full mask
        ulong mask = Def.ComputeOccupancyMask(CurrentState);
        
        // 2. Clear this specific car from the mask
        ulong carMask = Def.GetCarMask(carIndex, CurrentState.GetCarPos(carIndex));
        mask &= ~carMask;

        int length = Def.Lengths[carIndex];
        int currentPos = CurrentState.GetCarPos(carIndex);
        bool isHoriz = Def.IsHorizontal[carIndex];
        int fixedAx = Def.FixedAxis[carIndex];

        // 3. Scan Left/Down for minBound
        minPos = currentPos;
        for (int p = currentPos - 1; p >= 0; p--)
        {
            ulong checkMask = Def.GetCarMask(carIndex, p);
            if ((mask & checkMask) != 0) break; // Hit something
            minPos = p;
        }

        // 4. Scan Right/Up for maxBound
        maxPos = currentPos;
        for (int p = currentPos + 1; p <= Def.Width - length; p++)
        {
            ulong checkMask = Def.GetCarMask(carIndex, p);
            if ((mask & checkMask) != 0) break; // Hit something
            maxPos = p;
        }
    }

    private void CheckWinCondition()
    {
        if (CurrentState.GetCarPos(BitboardSolver.GOAL_CAR_INDEX) == BitboardSolver.GOAL_POSITION)
        {
            Debug.Log("Puzzle Solved!");
            // Here we could trigger a win UI and then LoadNewPuzzle()
            LoadNewPuzzle();
        }
    }

    public void ShowHint()
    {
        BitboardSolver solver = new BitboardSolver();
        var path = solver.Solve(Def, CurrentState);
        if (path != null && path.Count > 1)
        {
            PuzzleState nextState = path[1];
            for (int i = 0; i < Def.CarCount; i++)
            {
                if (CurrentState.GetCarPos(i) != nextState.GetCarPos(i))
                {
                    if (_carVisuals.TryGetValue(i, out CarController car))
                    {
                        car.FlashHint();
                    }
                    break;
                }
            }
        }
    }
}
