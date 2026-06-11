using System.Collections.Generic;
using UnityEngine;

namespace Gridlocked
{
    /// <summary>
    /// Spawns the hex cell background using flat-top axial layout.
    /// Attach to BoardContainer in HexScene.
    ///
    /// Flat-top axial → canvas pixel:
    ///   x = cellSize * 1.5f * q
    ///   y = cellSize * sqrt(3) * (r + q * 0.5f)
    ///
    /// The origin is shifted so the hexagon is centred on this transform.
    /// </summary>
    public class HexBoardRenderer : MonoBehaviour
    {
        [Header("Board")]
        public int Radius = 3;

        [Header("Layout")]
        public float CellSize = 80f;

        [Header("Prefabs")]
        public GameObject HexCellPrefab;

        // Read-only after Render() — used by HexPieceController.
        public HexBoard Board { get; private set; }

        private readonly List<GameObject> _cells = new();

        public void Render()
        {
            Board = new HexBoard(Radius);

            // Pre-compute centre offset so the board sits at this transform's origin.
            Vector2 centre = HexToLocal(Board.R, Board.R);

            for (int r = 0; r < Board.W; r++)
            for (int q = 0; q < Board.W; q++)
            {
                if (!Board.InHex(q, r)) continue;

                Vector2 pos = HexToLocal(q, r) - centre;
                var cell = Instantiate(HexCellPrefab, transform);
                cell.name = $"Cell_{q}_{r}";
                var rt = cell.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = pos;
                else cell.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                _cells.Add(cell);
            }
        }

        /// <summary>
        /// Converts axial (q, r) to a local canvas position (flat-top layout).
        /// Call this from HexPieceController to position pieces.
        /// </summary>
        public Vector2 HexToLocal(int q, int r)
        {
            float x = CellSize * 1.5f * q;
            float y = CellSize * Mathf.Sqrt(3f) * (r + q * 0.5f);
            return new Vector2(x, y);
        }

        /// <summary>Converts a bit-index anchor to axial (q, r).</summary>
        public (int q, int r) AnchorToAxial(int anchor)
        {
            return (anchor % Board.W, anchor / Board.W);
        }

        public void Clear()
        {
            foreach (var c in _cells) Destroy(c);
            _cells.Clear();
        }
    }
}
