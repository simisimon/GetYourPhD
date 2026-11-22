using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes' desk. Standing still at the desk types the paper: the Paper meter grows over time.
    /// Every other open problem in the flat costs focus (slower typing); a Mate boost types faster.
    /// Tools that fell off the wall onto the desk slow the typing down. At 100% press E here to submit.
    /// </summary>
    public class DeskWorkstation : MonoBehaviour
    {
        public static DeskWorkstation Instance { get; private set; }

        [Tooltip("Paper points per second at full focus.")]
        public float typingRate = 1.1f;
        [Tooltip("Focus lost per other open problem.")]
        public float focusLossPerProblem = 0.3f;
        public float boostMultiplier = 1.6f;
        [Tooltip("Typing speed per tool lying on the desk (multiplied for each tool).")]
        public float toolSlowdown = 0.6f;
        public HomeTask toolTask;
        public DeskToolFall tools;
        public Font font;

        public bool Typing { get; private set; }
        public float Focus { get; private set; } = 1f;

        bool playerInZone;
        AudioSource typingLoop;
        float acc, clackTimer;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var lm = LevelManager.Instance;
            var p = PlayerController.Instance;
            var meter = PaperMeter.Instance;
            bool submitted = lm && lm.Submitted;
            int toolCount = toolTask && toolTask.Active && tools ? Mathf.Max(1, tools.Count) : 0;
            float toolFactor = Mathf.Pow(toolSlowdown, toolCount);

            int others = 0;
            foreach (var t in HomeTask.All) if (t.Active && t != toolTask) others++;
            Focus = 1f / (1f + focusLossPerProblem * others);

            Typing = playerInZone && !submitted && p && p.Grounded && Mathf.Abs(GameInput.Horizontal) < 0.01f && !GameInput.Locked;
            if (!typingLoop) typingLoop = GameAudio.MakeLoop(this, "typing", 0.5f);
            GameAudio.SetLoop(typingLoop, Typing);
            if (typingLoop) typingLoop.pitch = Typing && p.Boosted ? 1.25f : 1f;
            p?.SetTyping(Typing);
            if (Typing && meter)
            {
                float rate = typingRate * Focus * toolFactor * (p.Boosted ? boostMultiplier : 1f);
                acc += rate * Time.deltaTime;
                while (acc >= 1f) { acc -= 1f; meter.Add(1); }
                clackTimer -= Time.deltaTime;
                if (clackTimer <= 0f)
                {
                    clackTimer = 0.45f / Mathf.Max(0.3f, Focus);
                    string[] words = { "clack", "tap", "Bayes", "p<0.05", "RQ1", "energy", "config", "\\cite{}", "LaTeX", "tap tap" };
                    Float(words[Random.Range(0, words.Length)], new Color(0.7f, 0.9f, 1f));
                }
            }

            if (playerInZone && !submitted)
            {
                bool full = meter && meter.Fill >= 0.999f;
                if (full)
                {
                    HUD.Instance?.Hint("Paper 100% complete! Press E to SUBMIT");
                    if (GameInput.InteractPressed) { HUD.Instance?.Hint(null); lm?.Submit(false); }
                }
                else if (toolCount > 0) HUD.Instance?.Hint($"{toolCount} tool{(toolCount > 1 ? "s" : "")} on the desk: typing at {Mathf.RoundToInt(toolFactor * 100)}%. Hold E to hang {(toolCount > 1 ? "them" : "it")} back up");
                else if (Typing) HUD.Instance?.Hint(others > 0 ? $"Typing... focus {Mathf.RoundToInt(Focus * 100)}% ({others} problem{(others > 1 ? "s" : "")} in the flat)" : "Typing... focus 100%");
                else HUD.Instance?.Hint("Stand still at the desk to write the paper");
            }
        }

        void Float(string text, Color c)
        {
            var go = new GameObject("TypeFx");
            go.transform.position = transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 1.6f, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 48;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 46;
            go.AddComponent<FloatingText>();
        }

        void OnTriggerEnter2D(Collider2D c) { if (c.GetComponentInParent<PlayerController>()) playerInZone = true; }
        void OnTriggerExit2D(Collider2D c)
        {
            if (!c.GetComponentInParent<PlayerController>()) return;
            playerInZone = false;
            HUD.Instance?.Hint(null);
            PlayerController.Instance?.SetTyping(false);
        }
    }

    /// <summary>Text that drifts up and fades out.</summary>
    public class FloatingText : MonoBehaviour
    {
        public float life = 1f;
        TextMesh tm;
        float t;
        void Start() { tm = GetComponent<TextMesh>(); }
        void Update()
        {
            t += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 0.9f;
            if (tm) { var c = tm.color; c.a = 1f - t / life; tm.color = c; }
            if (t >= life) Destroy(gameObject);
        }
    }
}
