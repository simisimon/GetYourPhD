using UnityEngine;

namespace GetYourPhD
{
    /// <summary>Start screen decoration: gentle floating, optionally with a golden glow pulse (the PhD cap).</summary>
    public class StartScreenBob : MonoBehaviour
    {
        public float amplitude = 0.1f;
        public float speed = 2.5f;
        public float phase;
        public bool glow;

        Vector3 basePos;
        SpriteRenderer sr;

        void Start()
        {
            basePos = transform.localPosition;
            sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(t) * amplitude;
            if (glow && sr)
            {
                float g = 0.5f + 0.5f * Mathf.Sin(t * 1.5f);
                sr.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.6f), g * 0.6f);
            }
        }
    }
}
