using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GetYourPhD
{
    /// <summary>
    /// Level 2 (Home Sweet Home Office): decides when problems pop up in the flat. It starts calm and gets busier;
    /// in the last minute the Night Shift begins (lights down, everything goes off more often, less patience).
    /// Also drops occasional ideas (experiment results) in random rooms.
    /// </summary>
    public class HomeOfficeDirector : MonoBehaviour
    {
        public HomeTask[] tasks;
        public HomeTask toolTask;
        public DeskToolFall tools;
        public HomeWife wife;

        [Header("Pacing (real seconds)")]
        public float calmStart = 5f;
        public float intervalStart = 7f;
        public float intervalBeforeNight = 4f;
        public float intervalNight = 2.8f;
        public float nightShiftAt = 65f;  // remaining seconds
        public float nightPatience = 0.75f;
        public int maxActiveEarly = 3;

        [Header("Ideas")]
        public Transform[] ideaSpots;
        public Sprite ideaSprite;
        public float ideaInterval = 22f;

        [Header("Lights")]
        public Light2D globalLight;
        public float dayIntensity = 0.8f, nightIntensity = 0.38f;
        public Light2D[] lamps;
        public SpriteRenderer[] windows;
        public SpriteRenderer skyline;
        [Tooltip("Black overlay between the house and Johannes; its alpha makes the flat darker towards 3 a.m.")]
        public SpriteRenderer nightOverlay;
        public float overlayBeforeNight = 0.22f, overlayNight = 0.45f;

        float timer, ideaTimer;
        bool night;
        GameObject currentIdea;
        readonly string[] intro =
        {
            "Stand still at the desk to write the paper.",
            "When something goes off in the flat, run there and hold E.",
            "Thrown Mate (right click) fixes problems from afar. Fridge = more Mate.",
        };
        int introStep;
        float introTimer = 4f;

        void Start()
        {
            timer = calmStart;
            ideaTimer = ideaInterval * 0.6f;
        }

        void Update()
        {
            var clock = DeadlineClock.Instance;
            var lm = LevelManager.Instance;
            if (!clock || (lm && lm.Submitted)) { if (lm && lm.Submitted) { SetLamps(1f); if (nightOverlay) nightOverlay.color = new Color(0, 0, 0, 0.15f); if (globalLight) globalLight.intensity = dayIntensity; } return; }

            if (introStep < intro.Length)
            {
                introTimer -= Time.deltaTime;
                if (introTimer <= 0f) { HUD.Instance?.Toast(intro[introStep++]); introTimer = 4.5f; }
            }

            float elapsed = clock.levelDurationSeconds - clock.Remaining;
            float span = Mathf.Max(1f, clock.levelDurationSeconds - nightShiftAt);
            float k = Mathf.Clamp01(elapsed / span);

            if (!night && clock.Remaining <= nightShiftAt)
            {
                night = true;
                HUD.Instance?.Banner("NIGHT SHIFT", "Everybody is awake. Of course.");
                GameAudio.Play("deadline");
                GameAudio.PlayMusic("night", 3f);
                wife?.GoToBed();
                foreach (var t in tasks) if (t) t.PatienceScale = nightPatience;
                if (toolTask) toolTask.PatienceScale = nightPatience;
                timer = 1f;
                // everything goes off at once
                foreach (var t in tasks) if (t && !t.Active && Random.value < 0.5f) t.Activate();
            }

            // Lights: getting darker towards 3 a.m.
            float dark = night ? 1f : k * 0.6f;
            if (globalLight) globalLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, dark);
            SetLamps(night ? 1f + 0.08f * Mathf.Sin(Time.time * 7f) : 0.6f + 0.4f * k);
            var sky = Color.Lerp(new Color(0.35f, 0.4f, 0.65f), new Color(0.12f, 0.12f, 0.28f), dark);
            foreach (var w in windows) if (w) w.color = sky;
            if (nightOverlay) nightOverlay.color = new Color(0.02f, 0.02f, 0.08f, night ? overlayNight : overlayBeforeNight * dark / 0.6f);
            if (skyline) skyline.color = Color.Lerp(new Color(0.45f, 0.45f, 0.62f), new Color(0.22f, 0.22f, 0.38f), dark);

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = night ? intervalNight * Random.Range(0.8f, 1.2f) : Mathf.Lerp(intervalStart, intervalBeforeNight, k) * Random.Range(0.85f, 1.15f);
                Spawn(k);
            }

            ideaTimer -= Time.deltaTime;
            if (ideaTimer <= 0f)
            {
                ideaTimer = ideaInterval * Random.Range(0.8f, 1.2f);
                if (!currentIdea && ideaSpots != null && ideaSpots.Length > 0) SpawnIdea();
            }
        }

        void SetLamps(float intensity)
        {
            if (lamps == null) return;
            foreach (var l in lamps) if (l) l.intensity = intensity;
        }

        void Spawn(float k)
        {
            int active = 0;
            foreach (var t in tasks) if (t && t.Active) active++;
            if (toolTask && toolTask.Active) active++;
            int cap = night ? 99 : (k < 0.5f ? maxActiveEarly : maxActiveEarly + 1);
            if (active >= cap) return;

            var pool = new List<System.Action>();
            foreach (var t in tasks) if (t && !t.Active) { var tt = t; pool.Add(() => tt.Activate()); }
            if (tools && !tools.Full && elapsedOk(k)) pool.Add(() => tools.Drop());
            if (pool.Count == 0) return;
            pool[Random.Range(0, pool.Count)]();
        }

        static bool elapsedOk(float k) => k > 0.05f;

        void SpawnIdea()
        {
            var spot = ideaSpots[Random.Range(0, ideaSpots.Length)];
            var go = new GameObject("Idea");
            go.transform.position = spot.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ideaSprite;
            sr.sortingOrder = 9;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;
            var c = go.AddComponent<Collectible>();
            c.kind = CollectibleKind.ExperimentResult;
            c.paperPoints = 3;
            currentIdea = go;
            HUD.Instance?.Toast("An idea popped up somewhere in the flat!");
        }
    }
}
