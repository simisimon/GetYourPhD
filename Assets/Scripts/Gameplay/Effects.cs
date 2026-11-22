using UnityEngine;

namespace GetYourPhD
{
    /// <summary>Simple pixel burst for pickups and hits.</summary>
    public static class Effects
    {
        static Sprite pixel;
        public static Sprite Pixel
        {
            get
            {
                if (pixel == null)
                {
                    var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.Apply();
                    pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
                }
                return pixel;
            }
        }

        public static void Burst(Vector2 pos, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("fx");
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Pixel;
                sr.color = color;
                sr.sortingOrder = 30;
                var p = go.AddComponent<FxParticle>();
                float a = Random.value * Mathf.PI * 2f;
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 6f);
            }
        }
    }
}
