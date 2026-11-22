using UnityEngine;
using UnityEngine.Tilemaps;

namespace GetYourPhD
{
    /// <summary>A platform hidden in fog. The camera flash reveals it (solid) for a few seconds.</summary>
    public class HiddenPlatform : MonoBehaviour
    {
        float timer;
        Collider2D[] cols;
        SpriteRenderer[] rends;
        Tilemap[] maps;

        /// <summary>World-space center of the platform (the object itself sits at the origin; its collider carries the offset).</summary>
        public Vector2 Center
        {
            get
            {
                var box = GetComponent<BoxCollider2D>();
                return box ? (Vector2)transform.TransformPoint(box.offset) : (Vector2)transform.position;
            }
        }

        void Awake()
        {
            cols = GetComponentsInChildren<Collider2D>(true);
            rends = GetComponentsInChildren<SpriteRenderer>(true);
            maps = GetComponentsInChildren<Tilemap>(true);
            Set(false, 0.1f);
        }

        public void Reveal(float seconds) { if (timer <= 0f) GameAudio.Play("reveal", 0.7f, 0.06f, 0.3f); timer = Mathf.Max(timer, seconds); Set(true, 1f); }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            float a = timer < 1.2f ? (Mathf.Repeat(timer * 6f, 1f) > 0.5f ? 1f : 0.4f) : 1f;
            Tint(new Color(1f, 1f, 1f, a));
            if (timer <= 0f) Set(false, 0.1f);
        }

        void Set(bool solid, float alpha)
        {
            foreach (var c in cols) c.enabled = solid;
            Tint(new Color(0.8f, 0.9f, 1f, alpha));
        }

        void Tint(Color c)
        {
            foreach (var r in rends) r.color = c;
            foreach (var m in maps) m.color = c;
        }
    }
}
