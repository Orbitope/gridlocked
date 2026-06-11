using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Gridlocked
{
    /// <summary>
    /// Orchestrates the hex playtest scene.
    ///
    /// Controls:
    ///   Tab          — cycle selected piece
    ///   Left/Right   — slide selected piece along its axis (±1)
    ///   Ctrl+Z       — undo
    ///
    /// Assign all references in the Inspector, then hit Play.
    /// </summary>
    public class HexGameManager : MonoBehaviour
    {
        [Header("Board")]
        public HexBoardRenderer BoardRenderer;

        [Header("Pieces")]
        public Transform PiecesContainer;
        public GameObject HexPiecePrefab;

        [Header("UI")]
        public TextMeshProUGUI SolvedText;
        public TextMeshProUGUI MoveCountText;
        public TextMeshProUGUI SelectedText;

        [Header("Generation")]
        public int PieceCount = 8;
        public int GeneratorSeed = 42;

        // Runtime state
        private HexPuzzleDefinition _def;
        private int[] _anchors;
        private HexPieceController[] _controllers;
        private int _selectedIndex = 0;
        private int _movesMade = 0;
        private readonly Stack<int[]> _undoStack = new();

        // -----------------------------------------------------------------------

        private void Start()
        {
            BoardRenderer.Render();
            var board = BoardRenderer.Board;

            var puzzle = GeneratePuzzle(board);
            if (puzzle == null)
            {
                Debug.LogError("[HexGameManager] Failed to generate a solvable puzzle.");
                return;
            }

            _def = new HexPuzzleDefinition { Board = board, Puzzle = puzzle };
            _anchors = (int[])puzzle.StartAnchors.Clone();

            // Solve + log metrics.
            var solveResult = Solver.Solve(puzzle);
            if (solveResult.Solved)
                Debug.Log($"[HexGameManager] Puzzle solvable in {solveResult.MoveCount} moves.");
            else
                Debug.LogWarning("[HexGameManager] Generated puzzle has no solution — regenerating.");

            var metrics = Analysis.Analyze(puzzle);
            Debug.Log($"[HexGameManager] States={metrics.TotalStates:N0}  " +
                      $"SolLen={metrics.SolutionLength}  Paths={metrics.ShortestPathCount:N0}  " +
                      $"Branch={metrics.BranchingAvg:F1}");

            // Spawn piece GameObjects.
            _controllers = new HexPieceController[puzzle.Pieces.Length];
            for (int i = 0; i < puzzle.Pieces.Length; i++)
            {
                var go = Instantiate(HexPiecePrefab, PiecesContainer);
                go.name = $"Piece_{i}";
                var ctrl = go.GetComponent<HexPieceController>();
                ctrl.Initialize(i, puzzle.Pieces[i], _anchors[i], BoardRenderer);
                _controllers[i] = ctrl;
            }

            _controllers[0].SetSelected(true);
            RefreshUI();

            if (SolvedText != null) SolvedText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_def == null) return;

            // Cycle selection.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _controllers[_selectedIndex].SetSelected(false);
                _selectedIndex = (_selectedIndex + 1) % _controllers.Length;
                _controllers[_selectedIndex].SetSelected(true);
                RefreshUI();
            }

            // Move: Left = -1, Right = +1 along the piece's own axis.
            int dir = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = +1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) dir = -1;

            if (dir != 0) TryMove(_selectedIndex, dir);

            // Undo.
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.Z))
                Undo();
        }

        // -----------------------------------------------------------------------

        private void TryMove(int pieceIdx, int dir)
        {
            var board = _def.Board;
            var piece = _def.Puzzle.Pieces[pieceIdx];
            ulong mask = piece.MaskAt(_anchors[pieceIdx]);
            ulong occ = _def.Puzzle.Occupancy(_anchors);
            ulong others = occ ^ mask;

            if (!board.CanSlide(mask, others, piece.Axis, dir)) return;

            // Push undo snapshot.
            _undoStack.Push((int[])_anchors.Clone());

            _anchors[pieceIdx] += dir * board.Step[piece.Axis];
            _controllers[pieceIdx].UpdateVisualPosition(_anchors[pieceIdx]);
            _movesMade++;
            RefreshUI();

            if (_def.Puzzle.IsGoal(_anchors))
                OnSolved();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            _anchors = _undoStack.Pop();
            for (int i = 0; i < _controllers.Length; i++)
                _controllers[i].UpdateVisualPosition(_anchors[i]);
            _movesMade = Mathf.Max(0, _movesMade - 1);
            RefreshUI();
            if (SolvedText != null) SolvedText.gameObject.SetActive(false);
        }

        private void OnSolved()
        {
            Debug.Log($"[HexGameManager] Solved in {_movesMade} moves!");
            if (SolvedText != null)
            {
                SolvedText.text = $"Solved in {_movesMade} moves!";
                SolvedText.gameObject.SetActive(true);
            }
        }

        private void RefreshUI()
        {
            if (MoveCountText != null) MoveCountText.text = $"Moves: {_movesMade}";
            if (SelectedText  != null) SelectedText.text  = $"Piece: {_selectedIndex} (Tab to switch)";
        }

        // -----------------------------------------------------------------------
        // Puzzle generation (same logic as Gate3, fixed seed for playtest)
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
