using UnityEngine;

namespace Gridlocked
{
    /// <summary>
    /// Visual representation of one hex puzzle piece.
    /// Positioned by anchor (bit index) via the board renderer's coordinate math.
    /// Rotation matches the piece axis:
    ///   Axis 0 (q)  →   0°
    ///   Axis 1 (r)  →  60°
    ///   Axis 2 (s)  → 120°
    /// </summary>
    public class HexPieceController : MonoBehaviour
    {
        public int PieceIndex;
        public Piece Piece;
        public int Anchor;                      // current anchor (bit index)
        public bool IsSelected;

        [Header("References")]
        public HexBoardRenderer BoardRenderer;

        [Header("Visuals")]
        public UnityEngine.UI.Image Image;      // assign in prefab

        // Colours for up to 12 pieces.
        private static readonly Color[] PieceColors =
        {
            new Color(0.93f, 0.27f, 0.27f), // 0 target — red
            new Color(0.27f, 0.53f, 0.93f),
            new Color(0.27f, 0.83f, 0.47f),
            new Color(0.93f, 0.73f, 0.27f),
            new Color(0.67f, 0.27f, 0.93f),
            new Color(0.27f, 0.83f, 0.93f),
            new Color(0.93f, 0.47f, 0.73f),
            new Color(0.57f, 0.83f, 0.27f),
            new Color(0.93f, 0.57f, 0.27f),
            new Color(0.47f, 0.27f, 0.67f),
            new Color(0.73f, 0.93f, 0.47f),
            new Color(0.27f, 0.47f, 0.67f),
        };

        private static readonly float[] AxisAngles = { 0f, 60f, 120f };

        public void Initialize(int pieceIndex, Piece piece, int startAnchor, HexBoardRenderer renderer)
        {
            PieceIndex = pieceIndex;
            Piece = piece;
            Anchor = startAnchor;
            BoardRenderer = renderer;

            if (Image != null)
                Image.color = PieceColors[pieceIndex % PieceColors.Length];

            UpdateVisualPosition(startAnchor);
        }

        public void UpdateVisualPosition(int anchor)
        {
            Anchor = anchor;
            var board = BoardRenderer.Board;
            var (q0, r0) = BoardRenderer.AnchorToAxial(anchor);
            var (dq, dr) = board.Deltas[Piece.Axis];

            // Average position of all cells this piece occupies.
            float sumX = 0f, sumY = 0f;
            for (int k = 0; k < Piece.Length; k++)
            {
                var pos = BoardRenderer.HexToLocal(q0 + k * dq, r0 + k * dr);
                sumX += pos.x;
                sumY += pos.y;
            }
            var centre = new Vector2(sumX / Piece.Length, sumY / Piece.Length);

            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = centre;
            else transform.localPosition = new Vector3(centre.x, centre.y, 0f);

            // Rotate to match axis.
            transform.localRotation = Quaternion.Euler(0f, 0f, AxisAngles[Piece.Axis]);

            // Width scales with piece length; height = one cell.
            if (rt != null)
                rt.sizeDelta = new Vector2(
                    BoardRenderer.CellSize * Piece.Length * 0.9f,
                    BoardRenderer.CellSize * 0.72f);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (Image != null)
                Image.color = selected
                    ? Color.Lerp(PieceColors[PieceIndex % PieceColors.Length], Color.white, 0.4f)
                    : PieceColors[PieceIndex % PieceColors.Length];
        }
    }
}
