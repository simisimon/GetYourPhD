using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes' wife cooks at the stove in the kitchen. Every few seconds she either makes a joke in a speech bubble
    /// or gives Johannes an order (a small <see cref="HomeTask"/>: walk to her and hold E). At the Night Shift she says
    /// good night and goes to bed.
    /// </summary>
    public class HomeWife : MonoBehaviour
    {
        public string displayName = "Your wife";
        public HomeTask orderTask;
        public SpriteRenderer body;
        public SpriteAnimator anim;
        public TextMesh speech;
        public SpriteRenderer speechBg;
        public float firstLineAfter = 6f;
        public Vector2 lineInterval = new Vector2(7f, 11f);
        [Range(0f, 1f)] public float orderChance = 0.55f;
        public float orderStartsAfter = 10f;

        static readonly string[] Jokes =
        {
            "Is the paper done yet?\n...And now?",
            "Dinner in 5 minutes.\nPhD minutes or real ones?",
            "My Bayesian model says\nthe pasta is 95% done.",
            "Did you measure the\nenergy use of this stove?",
            "Reviewer 2 called.\nHe wants more salt.",
            "I configured the oven:\n-O3, extra crispy.",
            "Another Mate?\nThat's your fifth today!",
            "Dr. Dorn, can you\nfix the dishwasher?",
            "The camper is not\na valid excuse.",
            "Deadline, then dinner,\nthen sleep. Deal?",
            "Who hung a saw\nabove your laptop?!",
            "Uncertainty: will you\neat warm food today?",
        };

        static readonly (string order, string verb)[] Orders =
        {
            ("Taste the sauce!", "Taste the sauce"),
            ("Set the table!", "Set the table"),
            ("Trash, please!", "Take out the trash"),
            ("Get the plates!", "Get the plates"),
            ("Open this jar!", "Open the jar"),
            ("Stir for me!", "Stir the pot"),
        };

        float timer, speechTimer, elapsed, fade = 1f;
        bool asleep;
        int lastJoke = -1;

        void Start()
        {
            timer = firstLineAfter;
            ShowSpeech(null);
            cooking = GameAudio.MakeLoop(this, "cooking", 0.1f);
            if (orderTask) orderTask.OnResolved += _ => Say("Thanks, love!", 2.5f);
        }

        public void GoToBed()
        {
            if (asleep) return;
            asleep = true;
            if (orderTask && orderTask.Active) orderTask.Resolve("she did it herself");
            Say("Good night!\nDon't stay up too late.", 4f);
            HUD.Instance?.Toast($"{displayName}: \"Good night! Don't stay up too late.\"");
        }

        void Say(string s, float seconds)
        {
            ShowSpeech(s);
            speechTimer = seconds;
        }

        void ShowSpeech(string s)
        {
            bool on = !string.IsNullOrEmpty(s);
            if (speech) speech.text = on ? s : "";
            if (speechBg)
            {
                speechBg.gameObject.SetActive(on);
                if (on)
                {
                    int lines = s.Split('\n').Length;
                    speechBg.transform.localScale = new Vector3(16f * 4.6f, 16f * (0.4f + 0.4f * lines), 1);
                }
            }
        }

        AudioSource cooking;

        void Update()
        {
            var lm = LevelManager.Instance;
            GameAudio.SetLoop(cooking, !asleep && !(lm && lm.Submitted));
            if (lm && lm.Submitted) { ShowSpeech(null); return; }

            if (speechTimer > 0f)
            {
                speechTimer -= Time.deltaTime;
                if (speechTimer <= 0f) ShowSpeech(null);
            }

            if (asleep)
            {
                if (speechTimer <= 0f && fade > 0f)
                {
                    fade -= Time.deltaTime * 1.5f; // walks off to bed
                    if (body) body.color = new Color(1, 1, 1, Mathf.Clamp01(fade));
                }
                return;
            }

            if (anim) anim.Play(anim.Has("cook") ? "cook" : "idle");
            elapsed += Time.deltaTime;
            bool orderOpen = orderTask && orderTask.Active;
            if (orderOpen) { if (speechTimer > 0f) ShowSpeech(null); speechTimer = 0f; }

            timer -= Time.deltaTime;
            if (timer > 0f || orderOpen) return;
            timer = Random.Range(lineInterval.x, lineInterval.y);

            if (orderTask && elapsed > orderStartsAfter && Random.value < orderChance)
            {
                var o = Orders[Random.Range(0, Orders.Length)];
                orderTask.bubbleText = o.order;
                orderTask.fixVerb = o.verb;
                orderTask.RefreshBubble();
                orderTask.Activate();
                HUD.Instance?.Toast($"{displayName}: \"{o.order}\"");
            }
            else
            {
                int j;
                do j = Random.Range(0, Jokes.Length); while (j == lastJoke && Jokes.Length > 1);
                lastJoke = j;
                Say(Jokes[j], 4.5f);
                GameAudio.Play("wife_joke", 0.8f);
            }
        }
    }
}
