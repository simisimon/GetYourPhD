using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Draws the endless road around the camera from a small pool of 1-unit rows (grass, asphalt, lane lines)
    /// plus roadside decoration (trees, pines, houses) picked deterministically from the row index.
    /// Each row is shifted by the road bend, which gives the Alps their curves.
    /// </summary>
    public class RoadScenery : MonoBehaviour
    {
        public RoadTrip trip;
        public Camera cam;
        public int rows = 24;
        public float decorSpacing = 2.5f;

        class Row
        {
            public int index = int.MinValue;
            public SpriteRenderer grass, shoulder, asphalt, edgeL, edgeR;
            public SpriteRenderer[] dividers;
        }

        class Decor { public int index = int.MinValue; public SpriteRenderer left, right; }

        Row[] pool;
        Decor[] decor;

        static readonly Color[] LineColor = { Color.white, Color.white, new Color(1f, 0.95f, 0.85f) };
        static readonly Color[] ShoulderColor = { new Color(0.55f, 0.55f, 0.55f), new Color(0.6f, 0.58f, 0.55f), new Color(0.72f, 0.62f, 0.48f) };

        void Start()
        {
            pool = new Row[rows];
            for (int i = 0; i < rows; i++)
            {
                var r = new Row();
                var parent = new GameObject("Row" + i).transform;
                parent.SetParent(transform);
                r.grass = Tiled(parent, "Grass", -20);
                r.shoulder = Plain(parent, "Shoulder", -12);
                r.asphalt = Tiled(parent, "Asphalt", -10);
                r.edgeL = Plain(parent, "EdgeL", -8);
                r.edgeR = Plain(parent, "EdgeR", -8);
                r.dividers = new SpriteRenderer[3];
                for (int d = 0; d < 3; d++) r.dividers[d] = Plain(parent, "Lane" + d, -8);
                pool[i] = r;
            }
            int dn = Mathf.CeilToInt(24f / decorSpacing) + 2;
            decor = new Decor[dn];
            for (int i = 0; i < dn; i++)
            {
                var parent = new GameObject("Decor" + i).transform;
                parent.SetParent(transform);
                decor[i] = new Decor { left = Plain(parent, "L", 15), right = Plain(parent, "R", 15) };
            }
        }

        SpriteRenderer Tiled(Transform parent, string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Plain(Transform parent, string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sortingOrder = order;
            return sr;
        }

        void LateUpdate()
        {
            if (!trip || !cam || pool == null) return;
            float cy = cam.transform.position.y;
            int baseRow = Mathf.FloorToInt(cy - rows * 0.5f);
            for (int i = 0; i < rows; i++)
            {
                int r = baseRow + Mod(i - baseRow, rows);
                LayoutRow(pool[i], r);
            }
            int baseDec = Mathf.FloorToInt((cy - 12f) / decorSpacing);
            for (int i = 0; i < decor.Length; i++)
            {
                int j = baseDec + Mod(i - baseDec, decor.Length);
                LayoutDecor(decor[i], j);
            }
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        void LayoutRow(Row row, int r)
        {
            float y = r + 0.5f;
            bool past = y > trip.finishY + 14f;
            var leg = trip.LegAt(y);
            int theme = leg.theme;
            float off = trip.RoadOffset(y);
            float hw = trip.RoadHalfWidth(y);
            var art = trip.art;
            if (row.index != r)
            {
                row.index = r;
                row.grass.sprite = art.grass[theme];
                row.asphalt.sprite = art.asphalt[theme];
                row.shoulder.sprite = art.pixel;
                row.shoulder.color = ShoulderColor[theme];
                foreach (var e in new[] { row.edgeL, row.edgeR }) { e.sprite = art.pixel; e.color = LineColor[theme]; }
                foreach (var d in row.dividers) { d.sprite = art.pixel; d.color = LineColor[theme]; }
            }
            row.grass.transform.position = new Vector3(Mathf.Round(off), y, 0);
            row.grass.size = new Vector2(64f, 1f);
            row.asphalt.gameObject.SetActive(!past);
            row.shoulder.gameObject.SetActive(!past);
            row.edgeL.gameObject.SetActive(!past);
            row.edgeR.gameObject.SetActive(!past);
            if (past) { foreach (var d in row.dividers) d.gameObject.SetActive(false); return; }

            row.asphalt.transform.position = new Vector3(off, y, 0);
            row.asphalt.size = new Vector2(hw * 2f, 1f);
            Scale(row.shoulder, off, y, hw * 2f + 0.7f);
            Scale(row.edgeL, off - hw + 0.18f, y, 0.12f);
            Scale(row.edgeR, off + hw - 0.18f, y, 0.12f);
            bool dash = Mod(r, 5) < 3;
            for (int d = 0; d < 3; d++)
            {
                bool on = d < leg.Lanes - 1 && dash && y < trip.finishY - 2f;
                row.dividers[d].gameObject.SetActive(on);
                if (on) Scale(row.dividers[d], off - hw + (d + 1) * trip.laneWidth, y, 0.12f);
            }
        }

        static void Scale(SpriteRenderer sr, float x, float y, float w)
        {
            sr.transform.position = new Vector3(x, y, 0);
            // the pixel sprite is 2x2 px = 1/16 unit
            sr.transform.localScale = new Vector3(w * 16f, 16f, 1f);
        }

        void LayoutDecor(Decor d, int j)
        {
            if (d.index == j) { Reposition(d, j); return; }
            d.index = j;
            float y = j * decorSpacing;
            var leg = trip.LegAt(y);
            var set = leg.theme == 0 ? trip.art.decorDE : leg.theme == 1 ? trip.art.decorAT : trip.art.decorIT;
            Pick(d.left, set, j * 2 + 0);
            Pick(d.right, set, j * 2 + 1);
            Reposition(d, j);
        }

        void Pick(SpriteRenderer sr, Sprite[] set, int seed)
        {
            uint h = Hash(seed);
            bool show = set != null && set.Length > 0 && (h % 100) < 70;
            sr.enabled = show;
            if (show) sr.sprite = set[(h / 100) % (uint)set.Length];
            sr.flipX = ((h >> 12) & 1) == 1;
        }

        void Reposition(Decor d, int j)
        {
            float y = j * decorSpacing;
            float off = trip.RoadOffset(y);
            float hw = trip.RoadHalfWidth(y);
            float l = 1.6f + (Hash(j * 7 + 3) % 1000) / 1000f * 8f;
            float r = 1.6f + (Hash(j * 13 + 5) % 1000) / 1000f * 8f;
            // keep the right side clear of gas stations / workshops
            if (NearStation(y)) r = Mathf.Max(r, 9.5f);
            d.left.transform.position = new Vector3(Mathf.Round((off - hw - l) * 32f) / 32f, y + (Hash(j) % 100) / 100f, 0);
            d.right.transform.position = new Vector3(Mathf.Round((off + hw + r) * 32f) / 32f, y + (Hash(j + 77) % 100) / 100f, 0);
            d.left.sortingOrder = 15 - Mathf.RoundToInt(y) % 5;
            d.right.sortingOrder = 15 - Mathf.RoundToInt(y) % 5;
            bool past = y > trip.finishY - 6f;
            if (past) { d.left.enabled = d.right.enabled = false; }
        }

        bool NearStation(float y)
        {
            foreach (var e in trip.events)
                if ((e.kind == "G" || e.kind == "S") && y > e.y - 6f && y < e.y + 24f) return true;
            return false;
        }

        static uint Hash(int x)
        {
            uint h = (uint)x * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return h;
        }
    }
}
