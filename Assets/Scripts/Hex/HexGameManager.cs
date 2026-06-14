using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Gridlocked
{
    /// <summary>
    /// Orchestrates the hex playtest scene.
    /// Pieces are dragged along their axis — same feel as the square game.
    /// Ctrl+Z undoes the last committed move.
    /// </summary>
    public class HexGameManager : MonoBehaviour
    {
        [Header("Board")]
        public HexBoardRenderer BoardRenderer;

        [Header("Pieces")]
        public Transform PiecesContainer;
        public GameObject HexPiecePrefab;

        [Header("UI")]
        public Text SolvedText;
        public Text MoveCountText;
        [HideInInspector] public Text InstructionsText; // hidden — button bar replaces keyboard hints
        public UIDocument HudDocument;

        [Header("Generation")]
        public int PieceCount = 8;
        public int GeneratorSeed = 42;

        // Exposed for HexPieceController.ComputeDragBounds
        public Puzzle  CurrentPuzzle  { get; private set; }
        public int[]   CurrentAnchors => _anchors;

        // Runtime state
        private HexPuzzleDefinition _def;
        private int[] _anchors;
        private HexPieceController[] _controllers;
        private int _movesMade = 0;
        private int _optimalMoves = -1;
        private readonly Stack<int[]> _undoStack = new();

        // -----------------------------------------------------------------------

        private void Start()
        {
            if (BoardRenderer == null) BoardRenderer = GetComponent<HexBoardRenderer>();
            SetupUI();
            BuildPuzzle();
        }

        private void Update()
        {
            // Back to main menu.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SceneManager.LoadScene("MainMenu");
                return;
            }

            // New puzzle (R) — randomize seed and rebuild.
            if (Input.GetKeyDown(KeyCode.R))
            {
                GeneratorSeed = Random.Range(1, int.MaxValue);
                BuildPuzzle();
                return;
            }

            if (_def == null) return;

            // Undo.
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.Z))
                Undo();
        }

        /// <summary>Generates a puzzle from the current seed, (re)rendering board + pieces.</summary>
        private void BuildPuzzle()
        {
            // Clear any previous board + pieces.
            BoardRenderer.Clear();
            if (_controllers != null)
                foreach (var c in _controllers)
                    if (c != null) Destroy(c.gameObject);
            _undoStack.Clear();
            _movesMade = 0;

            var board = new HexBoard(BoardRenderer.Radius);
            var puzzle = GeneratePuzzle(board);
            if (puzzle == null)
            {
                Debug.LogError("[HexGameManager] Failed to generate a solvable puzzle.");
                return;
            }

            CurrentPuzzle = puzzle;
            _def     = new HexPuzzleDefinition { Board = board, Puzzle = puzzle };
            _anchors = (int[])puzzle.StartAnchors.Clone();

            BoardRenderer.Render(puzzle.ExitMask);

            var solveResult = Solver.Solve(puzzle);
            _optimalMoves = solveResult.Solved ? solveResult.MoveCount : -1;
            if (solveResult.Solved)
                Debug.Log($"[HexGameManager] New puzzle (seed {GeneratorSeed}) — solvable in {solveResult.MoveCount} moves.");

            var metrics = Analysis.Analyze(puzzle);
            Debug.Log($"[HexGameManager] States={metrics.TotalStates:N0}  " +
                      $"SolLen={metrics.SolutionLength}  Paths={metrics.ShortestPathCount:N0}  " +
                      $"Branch={metrics.BranchingAvg:F1}");

            var parent = PiecesContainer != null ? PiecesContainer : transform;
            _controllers = new HexPieceController[puzzle.Pieces.Length];
            for (int i = 0; i < puzzle.Pieces.Length; i++)
            {
                GameObject go;
                if (HexPiecePrefab != null)
                    go = Instantiate(HexPiecePrefab, parent);
                else
                {
                    go = new GameObject($"Piece_{i}",
                        typeof(RectTransform),
                        typeof(UnityEngine.UI.Image),
                        typeof(HexPieceController));
                    go.transform.SetParent(parent, false);
                }
                go.name = $"Piece_{i}";

                var ctrl = go.GetComponent<HexPieceController>();
                if (ctrl == null) ctrl = go.AddComponent<HexPieceController>();
                ctrl.Initialize(i, puzzle.Pieces[i], _anchors[i], BoardRenderer);
                ctrl.OnMoveCommitted = CommitMove;
                _controllers[i] = ctrl;
            }

            if (SolvedText != null) SolvedText.gameObject.SetActive(false);
            RefreshUI();
        }

        /// <summary>One-time UI text styling + positioning.</summary>
        private void SetupUI()
        {
            if (SolvedText != null)
            {
                SolvedText.gameObject.SetActive(false);
                SolvedText.color = new Color32(0xE8,0xC0,0x68,255);
                SolvedText.fontSize = 48;
                PositionRect(SolvedText.GetComponent<RectTransform>(), 0f, 0f, 500f, 80f);
            }
            if (MoveCountText != null)
            {
                // Centered at top in TextBright — clean, readable, not crowding the board.
                MoveCountText.color = new Color32(0xC8,0xC0,0xAE,255); // TextPrimary
                MoveCountText.fontSize = 28;
                MoveCountText.alignment = TextAnchor.MiddleCenter;
                PositionRect(MoveCountText.GetComponent<RectTransform>(),
                    0f, Screen.height * 0.5f - 30f, 300f, 44f,
                    new Vector2(0.5f, 1f));
            }
            // InstructionsText and SelectedText removed — the button bar covers
            // Menu/Undo/Hint/Restart/New; keyboard shortcuts are secondary/discoverable.
            if (InstructionsText != null) InstructionsText.gameObject.SetActive(false);

            WireHud();
        }

        /// <summary>Wire the UIDocument button bar (Menu / Undo / Hint / Restart / New).</summary>
        private void WireHud()
        {
            if (HudDocument == null) HudDocument = FindAnyObjectByType<UIDocument>();
            if (HudDocument == null) return;
            var root = HudDocument.rootVisualElement;
            if (root == null) return;

            void Wire(string name, System.Action cb)
            {
                var btn = root.Q<UnityEngine.UIElements.Button>(name);
                if (btn != null) btn.clicked += cb;
            }

            Wire("hex-menu-btn",    () => SceneManager.LoadScene("MainMenu"));
            Wire("hex-undo-btn",    Undo);
            Wire("hex-hint-btn",    ShowHint);
            Wire("hex-restart-btn", RestartPuzzle);
            Wire("hex-new-btn",     () => { GeneratorSeed = Random.Range(1, int.MaxValue); BuildPuzzle(); });
        }

        // -----------------------------------------------------------------------

        /// <summary>Called by HexPieceController when a drag is released.</summary>
        public void CommitMove(int pieceIdx, int newAnchor)
        {
            if (newAnchor == _anchors[pieceIdx]) return; // no-op

            _undoStack.Push((int[])_anchors.Clone());
            _anchors[pieceIdx] = newAnchor;
            _movesMade++;
            RefreshUI();

            if (CurrentPuzzle.IsGoal(_anchors)) OnSolved();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            _anchors = _undoStack.Pop();
            for (int i = 0; i < _controllers.Length; i++)
                _controllers[i].UpdateVisualPosition(_anchors[i]);
            _movesMade = Mathf.Max(0, _movesMade - 1);
            if (SolvedText != null) SolvedText.gameObject.SetActive(false);
            RefreshUI();
        }

        private void OnSolved()
        {
            Debug.Log($"[HexGameManager] Solved in {_movesMade} moves!");
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySuccess();
            if (SolvedText != null)
            {
                SolvedText.text = _optimalMoves > 0
                    ? $"Solved in {_movesMade} moves  (optimal: {_optimalMoves})"
                    : $"Solved in {_movesMade} moves!";
                SolvedText.gameObject.SetActive(true);
            }
        }

        /// <summary>Solve from the current state and flash the piece that should move next.</summary>
        public void ShowHint()
        {
            if (CurrentPuzzle == null || _controllers == null) return;

            var probe = new Puzzle(_def.Board, CurrentPuzzle.Pieces, _anchors,
                                   CurrentPuzzle.TargetIndex, CurrentPuzzle.ExitMask);
            var res = Solver.Solve(probe);
            if (!res.Solved || res.Path == null || res.Path.Count < 2) return;

            int[] next = res.Path[1].Unpack(_anchors.Length);
            for (int i = 0; i < _anchors.Length; i++)
            {
                if (_anchors[i] != next[i])
                {
                    _controllers[i].FlashHint();
                    break;
                }
            }
        }

        /// <summary>Reset the current puzzle to its starting position (no regeneration).</summary>
        public void RestartPuzzle()
        {
            if (CurrentPuzzle == null) return;
            _anchors = (int[])CurrentPuzzle.StartAnchors.Clone();
            _undoStack.Clear();
            _movesMade = 0;
            for (int i = 0; i < _controllers.Length; i++)
                _controllers[i].UpdateVisualPosition(_anchors[i]);
            if (SolvedText != null) SolvedText.gameObject.SetActive(false);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (MoveCountText != null) MoveCountText.text = $"Moves: {_movesMade}";
        }

        /// <summary>Sets anchoredPosition, sizeDelta, and pivot on a RectTransform.</summary>
        private static void PositionRect(RectTransform rt, float x, float y,
                                         float w, float h, Vector2? pivot = null)
        {
            if (rt == null) return;
            var p = pivot ?? new Vector2(0.5f, 0.5f);
            rt.pivot           = p;
            rt.anchorMin       = new Vector2(0.5f, 0.5f);
            rt.anchorMax       = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta       = new Vector2(w, h);
        }

        // -----------------------------------------------------------------------
        // Puzzle generation
        // -----------------------------------------------------------------------

        private Puzzle GeneratePuzzle(HexBoard board)
        {
            var rng = new System.Random(GeneratorSeed);
            int midRow = board.R;

            for (int attempt = 0; attempt < 500; attempt++)
            {
                if (!Placement.TryPlace(board, 0, 2, 0, midRow, out int targetAnchor, out ulong targetMask))
                    continue;

                var pieces  = new List<Piece>  { new(board, 0, 2) };
                var anchors = new List<int>    { targetAnchor };
                ulong occ = targetMask;

                while (pieces.Count < PieceCount)
                {
                    bool placed = false;
                    for (int a = 0; a < 120 && !placed; a++)
                    {
                        int axis = rng.Next(0, 3);
                        int len  = rng.Next(2, 4);
                        int q    = rng.Next(0, board.W);
                        int r    = rng.Next(0, board.W);
                        if (Placement.TryPlace(board, axis, len, q, r, out int an, out ulong mm)
                            && (mm & occ) == 0)
                        {
                            pieces.Add(new Piece(board, axis, len));
                            anchors.Add(an);
                            occ |= mm;
                            placed = true;
                        }
                    }
                    if (!placed) break;
                }
                if (pieces.Count < PieceCount) continue;

                int exitQ = board.W - 1;
                while (!board.InHex(exitQ, midRow)) exitQ--;
                ulong exitMask = board.Bit(exitQ, midRow);

                var puzzle = new Puzzle(board, pieces.ToArray(), anchors.ToArray(), 0, exitMask);
                var result = Solver.Solve(puzzle);
                if (result.Solved && result.MoveCount >= 3) return puzzle;
            }
            return null;
        }
    }
}
