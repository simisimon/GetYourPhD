using System.Collections.Generic;
using UnityEngine;

namespace GetYourPhD
{
    [System.Serializable]
    public class SpriteClip
    {
        public string name;
        public Sprite[] frames;
        public float fps = 10f;
        public bool loop = true;
    }

    /// <summary>Minimal frame animator: plays named sprite clips on a SpriteRenderer. No Animator controller needed.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public List<SpriteClip> clips = new List<SpriteClip>();
        public string defaultClip = "idle";

        SpriteRenderer sr;
        SpriteClip current;
        float t;
        readonly Dictionary<string, SpriteClip> map = new Dictionary<string, SpriteClip>();

        public string CurrentName => current?.name;
        public bool Finished => current != null && !current.loop && t * current.fps >= current.frames.Length;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            foreach (var c in clips)
                if (c != null && !string.IsNullOrEmpty(c.name) && c.frames != null && c.frames.Length > 0)
                    map[c.name] = c;
            Play(defaultClip);
        }

        public bool Has(string clip) => map.ContainsKey(clip);

        public void Play(string clip, bool restart = false)
        {
            if (!map.TryGetValue(clip, out var c)) return;
            if (c == current && !restart) return;
            current = c;
            t = 0f;
            Apply();
        }

        void Update()
        {
            if (current == null) return;
            t += Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            int n = current.frames.Length;
            int i = Mathf.FloorToInt(t * current.fps);
            i = current.loop ? i % n : Mathf.Min(i, n - 1);
            sr.sprite = current.frames[i];
        }
    }
}
