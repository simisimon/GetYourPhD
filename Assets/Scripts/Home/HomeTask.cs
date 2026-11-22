using System.Collections.Generic;
using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// One problem spot in the flat (crying daughter, flooding washing machine, boiling pot, doorbell, cat on the keyboard).
    /// The <see cref="HomeOfficeDirector"/> activates it; a speech bubble with a patience bar shows above it.
    /// Fix it by standing next to it and holding E, by throwing a Mate into it, or (for some) with the camera flash.
    /// If it waits too long it gets out of hand: time and paper are lost, and it keeps going until fixed.
    /// </summary>
    public class HomeTask : MonoBehaviour
    {
        public static readonly List<HomeTask> All = new List<HomeTask>();

        [Header("Text")]
        public string taskName = "Daughter is crying";
        public string bubbleText = "WAAAH!";
        public string fixVerb = "Rock the crib";
        public string fixedText = "Back asleep";

        [Header("Timing")]
        public float patience = 12f;
        public float holdToFix = 1.2f;
        public float escalationPenaltySeconds = 10f;
        public int escalationPaperLoss = 3;

        [Header("Fixes")]
        public bool fixableByMate = true;
        public bool fixableByFlash;
        public string mateFixText = "Mate";
        public string flashFixText = "camera flash";
        [Tooltip("Activated by something else (the cat walking to the desk), not picked by the director.")]
        public bool externalActivation;

        [Header("Visuals")]
        public SpriteRenderer target;     // shaken while active
        public Sprite calmSprite, activeSprite;
        public Color bubbleColor = new Color(1f, 0.95f, 0.85f);
        public Font font;
        public float bubbleHeight = 2.2f;
        public float bubbleOffsetX = 0f;

        public bool Active { get; private set; }
        public float Waited { get; private set; }
        public float PatienceScale { get; set; } = 1f;
        public float Urgency => Active ? Mathf.Clamp01(Waited / Mathf.Max(0.1f, patience * PatienceScale)) : 0f;
        public string Room => roomName;
        public string roomName = "Nursery";

        public event System.Action<HomeTask> OnResolved;

        bool playerInZone;
        float hold;
        Vector3 targetBase;
        Transform bubble;
        TextMesh bubbleLabel, promptLabel;
        SpriteRenderer bubbleBg, barBack, barFill, holdFill;
        int escalations;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            if (target) targetBase = target.transform.localPosition;
            BuildBubble();
            SetVisual(false);
        }

        void BuildBubble()
        {
            bubble = new GameObject("Bubble").transform;
            bubble.SetParent(transform, false);
            bubble.localPosition = new Vector3(bubbleOffsetX, bubbleHeight, 0);
            bubbleBg = Quad("Bg", bubble, new Vector3(0, 0.12f, 0), new Vector2(3.2f, 1.1f), bubbleColor, 40);
            Quad("Tail", bubble, new Vector3(0, -0.45f, 0), new Vector2(0.3f, 0.3f), bubbleColor, 40).transform.localRotation = Quaternion.Euler(0, 0, 45);
            bubbleLabel = Text("Label", bubble, new Vector3(0, 0.22f, 0), 0.085f, new Color(0.75f, 0.15f, 0.1f));
            bubbleLabel.text = bubbleText;
            barBack = Quad("BarBack", bubble, new Vector3(0, -0.22f, 0), new Vector2(2.8f, 0.16f), new Color(0.25f, 0.2f, 0.2f), 41);
            barFill = Quad("BarFill", bubble, new Vector3(-1.4f, -0.22f, 0), new Vector2(0f, 0.16f), Color.green, 42);
            holdFill = Quad("Hold", bubble, new Vector3(-1.4f, -0.62f, 0), new Vector2(0f, 0.14f), new Color(0.4f, 0.8f, 1f), 42);
            promptLabel = Text("Prompt", bubble, new Vector3(0, -0.95f, 0), 0.06f, new Color(1f, 1f, 0.8f));
            bubble.gameObject.SetActive(false);
            RefreshBubble();
        }

        static SpriteRenderer Quad(string name, Transform parent, Vector3 pos, Vector2 size, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x * 16f, size.y * 16f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Effects.Pixel; // 2px at 32 PPU = 1/16 unit
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh Text(string name, Transform parent, Vector3 pos, float size, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 45;
            return tm;
        }

        static void SetBar(SpriteRenderer bar, float frac, float width)
        {
            frac = Mathf.Clamp01(frac);
            bar.transform.localScale = new Vector3(width * frac * 16f, bar.transform.localScale.y, 1);
            var p = bar.transform.localPosition;
            bar.transform.localPosition = new Vector3(-width * 0.5f + width * frac * 0.5f, p.y, 0);
        }

        /// <summary>Updates the bubble text after bubbleText changed.</summary>
        public void RefreshBubble()
        {
            if (bubbleLabel) bubbleLabel.text = bubbleText;
            if (bubbleBg)
            {
                float w = Mathf.Max(3.2f, bubbleText.Length * 0.19f + 0.5f);
                bubbleBg.transform.localScale = new Vector3(w * 16f, bubbleBg.transform.localScale.y, 1);
            }
        }

        public void Activate()
        {
            if (Active) return;
            Active = true;
            Waited = 0f;
            hold = 0f;
            escalations = 0;
            SetVisual(true);
            SetupSound();
            GameAudio.Play(sfxStart);
            sfxRepeatTimer = sfxRepeatEvery;
        }

        // ---- sound: picked from the task name, so the level builder needs no extra fields ----
        string sfxStart, sfxLoop, sfxRepeat;
        float sfxRepeatEvery, sfxRepeatTimer;
        AudioSource loopSource;
        bool soundSetUp;

        void SetupSound()
        {
            if (soundSetUp) return;
            soundSetUp = true;
            string n = taskName.ToLowerInvariant();
            if (n.Contains("cry")) sfxLoop = "baby_cry";
            else if (n.Contains("wash")) { sfxStart = "splash"; sfxLoop = "washer"; }
            else if (n.Contains("parcel") || n.Contains("door")) { sfxStart = sfxRepeat = "doorbell"; sfxRepeatEvery = 4f; }
            else if (n.Contains("wife")) sfxStart = "wife_order";
            if (sfxLoop != null) loopSource = GameAudio.MakeLoop(this, sfxLoop, 0.4f);
        }

        void UpdateSound(float urgency)
        {
            if (loopSource) GameAudio.SetLoop(loopSource, Active, 0.3f + 0.45f * urgency);
            if (!Active || sfxRepeat == null) return;
            sfxRepeatTimer -= Time.deltaTime;
            if (sfxRepeatTimer <= 0f) { sfxRepeatTimer = sfxRepeatEvery * (1f - 0.4f * urgency); GameAudio.Play(sfxRepeat, 0.6f + 0.4f * urgency); }
        }

        void SetVisual(bool on)
        {
            if (bubble) bubble.gameObject.SetActive(on);
            if (target)
            {
                if (on && activeSprite) target.sprite = activeSprite;
                if (!on && calmSprite) target.sprite = calmSprite;
                if (!on) target.transform.localPosition = targetBase;
            }
        }

        public void Resolve(string how)
        {
            if (!Active) return;
            Active = false;
            hold = 0f;
            SetVisual(false);
            UpdateSound(0f);
            GameAudio.Play("toast_good", 0.7f);
            Effects.Burst(transform.position + Vector3.up * 1f, new Color(0.5f, 1f, 0.6f), 12);
            HUD.Instance?.Toast($"{fixedText}! ({how})");
            if (playerInZone) HUD.Instance?.Hint(null);
            OnResolved?.Invoke(this);
        }

        void Update()
        {
            if (!Active) return;
            bool locked = LevelManager.Instance && LevelManager.Instance.Submitted;
            if (locked) { SetVisual(false); Active = false; UpdateSound(0f); return; }

            Waited += Time.deltaTime;
            float limit = patience * PatienceScale;
            if (Waited >= limit)
            {
                escalations++;
                DeadlineClock.Instance?.AddPenalty(escalationPenaltySeconds);
                PaperMeter.Instance?.Remove(escalationPaperLoss);
                HUD.Instance?.Toast($"{taskName}: out of hand! -{escalationPenaltySeconds:0}s, -{PaperMeter.Pieces(escalationPaperLoss)}");
                Effects.Burst(transform.position + Vector3.up, new Color(1f, 0.35f, 0.3f), 14);
                GameAudio.Play("toast_bad");
                Waited = limit * 0.4f; // keeps escalating until fixed
            }

            // Hold E next to it.
            if (playerInZone && GameInput.InteractHeld && PlayerController.Instance && PlayerController.Instance.Grounded)
            {
                hold += Time.deltaTime;
                if (hold >= holdToFix) { Resolve("fixed"); return; }
            }
            else hold = Mathf.Max(0f, hold - Time.deltaTime * 2f);

            // Visuals
            float u = Urgency;
            UpdateSound(u);
            SetBar(barFill, 1f - u, 2.8f);
            barFill.color = PaperMeter.FillColor(1f - u);
            SetBar(holdFill, hold / Mathf.Max(0.05f, holdToFix), 2.8f);
            holdFill.gameObject.SetActive(hold > 0.01f);
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * (8f + 10f * u));
            bubble.localScale = new Vector3(pulse, pulse, 1f);
            bubbleLabel.color = escalations > 0 && Mathf.Repeat(Time.time * 4f, 1f) > 0.5f ? new Color(1f, 0.1f, 0.1f) : new Color(0.75f, 0.15f, 0.1f);
            promptLabel.text = playerInZone ? $"hold E: {fixVerb}" : "";
            if (target)
            {
                float amp = 0.03f + 0.06f * u;
                target.transform.localPosition = targetBase + new Vector3(Mathf.Sin(Time.time * 40f) * amp, 0, 0);
            }
        }

        void OnTriggerEnter2D(Collider2D c)
        {
            if (!c.GetComponentInParent<PlayerController>()) return;
            playerInZone = true;
            if (Active) HUD.Instance?.Hint($"{taskName}. Hold E: {fixVerb.ToLowerInvariant()}");
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (!c.GetComponentInParent<PlayerController>()) return;
            playerInZone = false;
            if (Active) HUD.Instance?.Hint(null);
        }

        public bool PlayerNear => playerInZone;
    }
}
