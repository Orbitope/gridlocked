using System.Collections.Generic;
using UnityEngine;

namespace Gridlocked
{
    /// <summary>
    /// Spawns the hex cell background using flat-top axial layout.
    /// Cells have raycastTarget=false so pieces beneath receive pointer events.
    /// The exit cell is highlighted so the player knows where to send the red piece.
    /// </summary>
    public class HexBoardRenderer : MonoBehaviour
    {
        [Header("Board")]
        public int Radius = 3;

        [Header("Layout")]
        public float CellSize = 110f;

        [Header("Prefabs")]
        public GameObject HexCellPrefab;

        public HexBoard Board       { get; private set; }
        public Vector2  BoardCentre { get; private set; }

        // Exit cell position (rightmost in-hex cell on the middle row).
        public (int q, int r) ExitAxial { get; private set; }

        private readonly List<GameObject> _cells = new();
        private Sprite _hexSprite;
        private Sprite _exitSprite;  // brighter version for the exit cell

        // -----------------------------------------------------------------------

        public void Render(ulong exitMask = 0)
        {
            Board       = new HexBoard(Radius);
            BoardCentre = HexToLocal(Board.R, Board.R);

            // Find exit axial coords from the bitmask.
            int exitQ = Board.W - 1;
            int exitR = Board.R;
            while (!Board.InHex(exitQ, exitR)) exitQ--;
            ExitAxial = (exitQ, exitR);

            for (int r = 0; r < Board.W; r++)
            for (int q = 0; q < Board.W; q++)
            {
                if (!Board.InHex(q, r)) continue;

                bool isExit = (q == ExitAxial.q && r == ExitAxial.r);
                Vector2 pos = HexToLocal(q, r) - BoardCentre;

                var cell = MakeCell(isExit);
                cell.name = $"Cell_{q}_{r}";
                var rt = cell.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = pos;
                _cells.Add(cell);
            }

            // Add a small arrow label at the exit pointing right.
            SpawnExitArrow();
        }

        // -----------------------------------------------------------------------
        // Coordinate helpers
        // -----------------------------------------------------------------------

        public Vector2 HexToLocal(int q, int r)
        {
            float x = CellSize * 1.5f * q;
            float y = CellSize * Mathf.Sqrt(3f) * (r + q * 0.5f);
            return new Vector2(x, y);
        }

        public (int q, int r) AnchorToAxial(int anchor) =>
            (anchor % Board.W, anchor / Board.W);

        // -----------------------------------------------------------------------
        // Cell spawning
        // -----------------------------------------------------------------------

        private GameObject MakeCell(bool isExit)
        {
            var go = new GameObject("cell", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite       = GetHexSprite(isExit);
            img.color        = isExit
                ? new Color(0.35f, 0.78f, 0.45f, 1f)   // bright green exit
                : new Color(0.72f, 0.76f, 0.83f, 1f);   // blue-grey normal
            img.raycastTarget = false;  // never block piece input
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CellSize, CellSize);
            return go;
        }

        private void SpawnExitArrow()
        {
            // A simple "→" text label just outside the exit cell.
            var go = new GameObject("ExitArrow", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(transform, false);
            var txt = go.GetComponent<UnityEngine.UI.Text>();
            txt.text      = "EXIT →";
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = Mathf.RoundToInt(CellSize * 0.28f);
            txt.color     = new Color(0.2f, 0.75f, 0.35f, 1f);
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            Vector2 exitPos = HexToLocal(ExitAxial.q, ExitAxial.r) - BoardCentre;
            rt.anchoredPosition = exitPos + new Vector2(CellSize * 0.75f, 0f);
            rt.sizeDelta = new Vector2(CellSize * 1.4f, CellSize * 0.5f);
        }

        // -----------------------------------------------------------------------
        // Sprite generation
        // -----------------------------------------------------------------------

        private Sprite GetHexSprite(bool _) => GetHexSprite();

        private Sprite GetHexSprite()
        {
            if (_hexSprite != null) return _hexSprite;
            _hexSprite = BuildHexSprite(0.90f);
            return _hexSprite;
        }

        private Sprite BuildHexSprite(float inset)
        {
            int res = 128;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float cx = res / 2f, cy = res / 2f;
            float r = res / 2f * inset;

            var corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i);
                corners[i]  = new Vector2(cx + r * Mathf.Cos(angle),
                                          cy + r * Mathf.Sin(angle));
            }

            for (int py = 0; py < res; py++)
            for (int px = 0; px < res; px++)
                tex.SetPixel(px, py, InsideHex(new Vector2(px, py), corners)
                    ? Color.white : Color.clear);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res),
                                 new Vector2(0.5f, 0.5f), res);
        }

        private static bool InsideHex(Vector2 p, Vector2[] verts)
        {
            int n = verts.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = verts[i], b = verts[(i + 1) % n];
                if ((b - a).x * (p - a).y - (b - a).y * (p - a).x < 0) return false;
            }
            return true;
        }

        public void Clear()
        {
            foreach (var c in _cells) Destroy(c);
            _cells.Clear();
        }
    }
}
