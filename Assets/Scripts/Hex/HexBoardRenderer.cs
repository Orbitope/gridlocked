using System.Collections.Generic;

using UnityEngine;

namespace Gridlocked
{
    /// <summary>
    /// Spawns the hex cell background using flat-top axial layout.
    /// Colors follow ContentKit Palette B (CKColor).
    /// CellSize is auto-computed from screen dimensions at Render() time.
    /// </summary>
    public class HexBoardRenderer : MonoBehaviour
    {
        [Header("Board")]
        public int Radius = 3;

        [Header("Layout — leave 0 to auto-fit")]
        public float CellSizeOverride = 0f;

        [Header("Prefabs")]
        public GameObject HexCellPrefab;

        public HexBoard Board       { get; private set; }
        public Vector2  BoardCentre { get; private set; }
        public float    CellSize    { get; private set; }
        public (int q, int r) ExitAxial { get; private set; }

        private readonly List<GameObject> _cells = new();
        private Sprite _hexSprite;

        // -----------------------------------------------------------------------

        public void Render(ulong exitMask = 0)
        {
            // Auto-fit: target ~80% of the shorter screen dimension across the board diameter.
            // Board width (flat-top) ≈ CellSize * 1.5 * (2R) = 3R * CellSize
            // So CellSize ≈ 0.8 * min(w,h) / (3 * R)
            CellSize = CellSizeOverride > 0f
                ? CellSizeOverride
                : Mathf.Min(Screen.width, Screen.height) * 0.80f / (3f * Radius);

            Board       = new HexBoard(Radius);
            BoardCentre = HexToLocal(Board.R, Board.R);

            // Set camera/scene background to Void.
            if (Camera.main != null)
                Camera.main.backgroundColor = new Color32(0x11,0x10,0x09,255);

            // Exit cell axial position.
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
                cell.GetComponent<RectTransform>().anchoredPosition = pos;
                _cells.Add(cell);
            }

            SpawnExitArrow();
        }

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

        private GameObject MakeCell(bool isExit)
        {
            var go = new GameObject("cell", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(transform, false);

            var img          = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite       = GetHexSprite();
            img.color        = isExit ? new Color32(0x9A,0xAA,0xBB,255) : new Color32(0x1E,0x1C,0x16,255);
            img.raycastTarget = false;

            var rt       = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CellSize, CellSize);
            return go;
        }

        private void SpawnExitArrow()
        {
            var go = new GameObject("ExitArrow", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(transform, false);

            var txt           = go.GetComponent<UnityEngine.UI.Text>();
            txt.text          = "EXIT →";
            txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize      = Mathf.RoundToInt(CellSize * 0.28f);
            txt.color         = new Color32(0xE8,0xC0,0x68,255);
            txt.fontStyle     = FontStyle.Bold;
            txt.alignment     = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            var rt              = go.GetComponent<RectTransform>();
            rt.anchoredPosition = HexToLocal(ExitAxial.q, ExitAxial.r) - BoardCentre
                                  + new Vector2(CellSize * 0.75f, 0f);
            rt.sizeDelta        = new Vector2(CellSize * 1.6f, CellSize * 0.5f);
        }

        // -----------------------------------------------------------------------

        private Sprite GetHexSprite()
        {
            if (_hexSprite != null) return _hexSprite;

            int res  = 128;
            var tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float cx = res / 2f, cy = res / 2f;
            float r  = res / 2f * 0.88f;
            var corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a  = Mathf.Deg2Rad * (60f * i);
                corners[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
            }

            for (int py = 0; py < res; py++)
            for (int px = 0; px < res; px++)
                tex.SetPixel(px, py, InsideHex(new Vector2(px, py), corners)
                    ? Color.white : Color.clear);

            tex.Apply();
            _hexSprite = Sprite.Create(tex, new Rect(0, 0, res, res),
                                       new Vector2(0.5f, 0.5f), res);
            return _hexSprite;
        }

        private static bool InsideHex(Vector2 p, Vector2[] v)
        {
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % v.Length];
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
