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

    private void Start()
    {
        GenerateGrid();
        
        // If no level was passed in (e.g. testing the scene directly), load a random one
        if (CurrentLevelData == null)
        {
            LoadNewPuzzle();
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
        Color colorA = new Color(0.1f, 0.15f, 0.25f, 1f);
        Color colorB = new Color(0.15f, 0.2f, 0.3f, 1f);

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

    public void LoadLevel(LevelData level)
    {
        UndoStack.Clear();
        CurrentLevelData = level;
        Def = level.GetDefinition();
        CurrentState = new PuzzleState(level.InitialStateData);
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
        CurrentLevelData = null; // Mark as random puzzle
        
        if (_generator.TryGenerateDensityFirst(carCount, minMoves, out PuzzleDefinition def, out PuzzleState state, out PuzzleQualityMetrics metrics))
        {
            Def = def;
            CurrentState = state;
            SpawnCars();
        }
        else
        {
            Debug.LogError("Failed to generate puzzle.");
        }
    }

    public void LoadNewPuzzle()
    {
        LoadCustomPuzzle(8, 8); // Default
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

    private void HandleVictory()
    {
        Debug.Log("Puzzle Solved!");
        if (CurrentLevelData.HasValue)
        {
            SaveManager.RecordVictory(CurrentLevelData.Value.ID, CurrentMoveCount);
            Debug.Log($"Level Complete! Your Moves: {CurrentMoveCount} | Optimal: {CurrentLevelData.Value.OptimalMoves}");
        }
        else
        {
            Debug.Log($"Random Puzzle Complete! Your Moves: {CurrentMoveCount}");
        }

        // Delay loading next
        Invoke("LoadNewPuzzle", 1.5f);
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
