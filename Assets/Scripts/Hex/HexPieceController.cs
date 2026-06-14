using System;

using UnityEngine;
using UnityEngine.EventSystems;

namespace Gridlocked
{
    /// <summary>
    /// Visual + input for one hex puzzle piece.
    /// Drag is projected onto the piece's axis direction; the piece slides
    /// continuously during drag and snaps on release — same feel as CarController.
    ///
    /// Axis canvas step vectors (flat-top layout, per CellSize unit):
    ///   Axis 0: ( 1.5,  √3/2 )   30° from horizontal
    ///   Axis 1: ( 0,    √3   )   straight up
    ///   Axis 2: (-1.5,  √3/2 )   150° from horizontal
    /// All three have the same magnitude: √3 * CellSize.
    /// </summary>
    public class HexPieceController : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public int PieceIndex;
        public Piece Piece;
        public int Anchor;
        public HexBoardRenderer BoardRenderer;

        /// <summary>Called on release with (pieceIndex, newAnchor).</summary>
        public Action<int, int> OnMoveCommitted;

        // Drag state
        private Vector2 _dragStartScreen;   // screen pos at pointer-down
        private Vector2 _dragStartCanvas;   // canvas pos at pointer-down
        private int     _dragStartAnchor;
        private int     _dragMinDelta;      // how many steps negative are available
        private int     _dragMaxDelta;      // how many steps positive are available

        // Axis step vectors in canvas space (set once from CellSize)
        private Vector2 _axisStep;      // full canvas vector for one cell step
        private float   _stepMagnitude; // |_axisStep|

        /// <summary>
        /// Piece 0 (target) is always Coral. Every other piece gets a unique,
        /// evenly-spread hue via the golden-angle sequence at muted S/V — no two
        /// pieces ever share a color, regardless of piece count.
        /// </summary>
        private static Color BaseColor(int index)
        {
            if (index == 0) return new Color32(0xFF, 0x5E, 0x3A, 255); // Coral
            float hue = Mathf.Repeat(0.07f + index * 0.61803398875f, 1f);
            return Color.HSVToRGB(hue, 0.40f, 0.74f);
        }

        // Flat-top hex axis directions (unit CellSize).
        // Multiply by CellSize to get the canvas-space step vector per cell.
        private static readonly Vector2[] AxisUnitStep =
        {
            new Vector2( 1.5f,       Mathf.Sqrt(3f) / 2f),  // axis 0
            new Vector2( 0f,         Mathf.Sqrt(3f)),         // axis 1
            new Vector2(-1.5f,       Mathf.Sqrt(3f) / 2f),   // axis 2
        };

        // -----------------------------------------------------------------------

        public void Initialize(int pieceIndex, Piece piece, int startAnchor, HexBoardRenderer renderer)
        {
            PieceIndex = pieceIndex;
            Piece = piece;
            Anchor = startAnchor;
            BoardRenderer = renderer;

            float cs = BoardRenderer.CellSize;
            _axisStep      = AxisUnitStep[piece.Axis] * cs;
            _stepMagnitude = _axisStep.magnitude; // = √3 * CellSize for all axes

            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.color = BaseColor(pieceIndex);

            UpdateVisualPosition(startAnchor);
        }

        // -----------------------------------------------------------------------
        // Visual positioning
        // -----------------------------------------------------------------------

        public void UpdateVisualPosition(int anchor)
        {
            Anchor = anchor;
            var board = BoardRenderer.Board;
            var (q0, r0) = BoardRenderer.AnchorToAxial(anchor);
            var (dq, dr) = board.Deltas[Piece.Axis];

            float sumX = 0f, sumY = 0f;
            for (int k = 0; k < Piece.Length; k++)
            {
                var pos = BoardRenderer.HexToLocal(q0 + k * dq, r0 + k * dr);
                sumX += pos.x;
                sumY += pos.y;
            }

            var canvasPos = new Vector2(sumX / Piece.Length, sumY / Piece.Length)
                            - BoardRenderer.BoardCentre
                            + BoardRenderer.CentreShift; // shift up to clear HUD

            SetCanvasPosition(canvasPos);

            transform.localRotation = Quaternion.Euler(0f, 0f,
                Piece.Axis == 0 ? 30f : Piece.Axis == 1 ? 90f : 150f);

            var rt = GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(
                    BoardRenderer.CellSize * Piece.Length * 0.85f,
                    BoardRenderer.CellSize * 0.68f);
        }

        private void SetCanvasPosition(Vector2 pos)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
            else transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        }

        // -----------------------------------------------------------------------
        // Drag input
        // -----------------------------------------------------------------------

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragStartScreen = eventData.position;
            _dragStartAnchor = Anchor;
            _dragStartCanvas = GetComponent<RectTransform>()?.anchoredPosition ?? Vector2.zero;

            // Compute how many steps are free in each direction.
            ComputeDragBounds(out _dragMinDelta, out _dragMaxDelta);

            transform.localScale = Vector3.one * 1.05f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 screenDelta = eventData.position - _dragStartScreen;

            // Convert screen delta to canvas delta (account for canvas scale).
            // RectTransformUtility handles this correctly even at non-1:1 scale.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                GetComponentInParent<Canvas>().GetComponent<RectTransform>(),
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 currentCanvas);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                GetComponentInParent<Canvas>().GetComponent<RectTransform>(),
                _dragStartScreen,
                eventData.pressEventCamera,
                out Vector2 startCanvas);
            Vector2 canvasDelta = currentCanvas - startCanvas;

            // Project onto axis direction.
            float projection = Vector2.Dot(canvasDelta, _axisStep.normalized);
            float gridDelta  = projection / _stepMagnitude;

            // Clamp to available space.
            float clampedDelta = Mathf.Clamp(gridDelta, _dragMinDelta, _dragMaxDelta);

            // Move visually (smooth, not snapped yet).
            SetCanvasPosition(_dragStartCanvas + _axisStep * clampedDelta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;

            // Snap: project current canvas offset onto axis, round to nearest cell.
            var rt = GetComponent<RectTransform>();
            Vector2 currentCanvas = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 offset = currentCanvas - _dragStartCanvas;
            float projection = Vector2.Dot(offset, _axisStep.normalized);
            int snapDelta = Mathf.RoundToInt(projection / _stepMagnitude);
            snapDelta = Mathf.Clamp(snapDelta, _dragMinDelta, _dragMaxDelta);

            int newAnchor = _dragStartAnchor + snapDelta * BoardRenderer.Board.Step[Piece.Axis];

            // SFX: slide if the piece moved; thock if it was shoved against a
            // bound (player dragged past the available space).
            if (AudioManager.Instance != null)
            {
                float rawDelta = projection / _stepMagnitude;
                if (snapDelta != 0)
                    AudioManager.Instance.PlaySlide(
                        Mathf.Clamp(1.1f - Mathf.Abs(snapDelta) * 0.05f, 0.85f, 1.1f));
                else if (Mathf.Abs(rawDelta) > 0.5f)
                    AudioManager.Instance.PlayThock();
            }

            Anchor = newAnchor;
            UpdateVisualPosition(newAnchor); // snap visual to grid
            OnMoveCommitted?.Invoke(PieceIndex, newAnchor);
        }

        // -----------------------------------------------------------------------

        private void ComputeDragBounds(out int minDelta, out int maxDelta)
        {
            var board   = BoardRenderer.Board;
            var puzzle  = FindAnyObjectByType<HexGameManager>()?.CurrentPuzzle;
            var anchors = FindAnyObjectByType<HexGameManager>()?.CurrentAnchors;

            minDelta = 0;
            maxDelta = 0;

            if (puzzle == null || anchors == null) return;

            ulong occ    = puzzle.Occupancy(anchors);
            ulong myMask = Piece.MaskAt(Anchor);
            ulong others = occ ^ myMask;

            // Positive direction.
            ulong cur = myMask;
            int a = Anchor;
            for (int i = 1; i <= board.W * 2; i++)
            {
                if (!board.CanSlide(cur, others, Piece.Axis, +1)) break;
                cur = board.Shift(cur, Piece.Axis, +1);
                a  += board.Step[Piece.Axis];
                maxDelta = i;
            }

            // Negative direction.
            cur = myMask;
            a   = Anchor;
            for (int i = 1; i <= board.W * 2; i++)
            {
                if (!board.CanSlide(cur, others, Piece.Axis, -1)) break;
                cur = board.Shift(cur, Piece.Axis, -1);
                a  -= board.Step[Piece.Axis];
                minDelta = -i;
            }
        }

        public void SetSelected(bool selected)
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;
            var baseColor = BaseColor(PieceIndex);
            img.color = selected ? Color.Lerp(baseColor, new Color32(0xE8,0xC0,0x68,255), 0.45f) : baseColor;
        }

        /// <summary>Blink the piece amber twice to signal the suggested move.</summary>
        public void FlashHint() => StartCoroutine(FlashRoutine());

        private System.Collections.IEnumerator FlashRoutine()
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img == null) yield break;

            Color baseColor = BaseColor(PieceIndex);
            Color flash = new Color32(0xE8, 0xC0, 0x68, 255); // AmberBright

            for (int i = 0; i < 2; i++)
            {
                img.color = flash;
                yield return new UnityEngine.WaitForSeconds(0.18f);
                img.color = baseColor;
                yield return new UnityEngine.WaitForSeconds(0.12f);
            }
        }
    }
}
