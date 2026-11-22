using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Final boss: Prof. Dr. Norbert Siegmund floats above the Felix-Klein-Hörsaal and drops red-pen comments and
    /// "just one more configuration" boxes. Johannes runs left and right on the Felix-Klein-Hörsaal floor (no jumping),
    /// collects Mate bottles that appear on the floor and throws them at the professor (aimed with the mouse).
    /// 3 phases, each faster; <see cref="hitsPerPhase"/> Mate hits each. Only the falling things hurt.
    /// </summary>
    public class SiegmundBoss : MonoBehaviour
    {
        public static SiegmundBoss Instance { get; private set; }

        public string bossName = "Prof. Dr. Norbert Siegmund";
        public SpriteRenderer view;
        public SpriteAnimator anim;
        public Sprite penSprite, boxSprite;
        public Font font;

        [Header("Arena (world units)")]
        public float arenaFloorY;
        public float halfWidth = 9.5f;
        [Tooltip("Height above the Felix-Klein-Hörsaal floor he floats at (he bobs between low and high).")]
        public float hoverLow = 8f;
        public float hoverHigh = 11.5f;
        public float hitRadius = 1.1f;

        [Header("Difficulty per phase (1..3)")]
        public int hitsPerPhase = 4;
        public float[] moveSpeed = { 2.5f, 3.5f, 4.5f };
        public float[] throwInterval = { 2.2f, 1.8f, 1.5f };
        public float[] penSpeed = { 5f, 6f, 7f };
        public float[] boxSpeed = { 3f, 3.6f, 4.2f };

        public int Phase { get; private set; } = 1;
        public int HitsTaken { get; private set; }
        public int MaxHealth => hitsPerPhase * 3;
        public int Health => MaxHealth - HitsTaken;
        public bool Defeated { get; private set; }
        public bool Active { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float HoverY => arenaFloorY + hoverHigh;

        public event System.Action<int> OnPhaseChanged;
        public event System.Action OnDefeated;

        float t, throwTimer, hitTimer, dazzle, dir = 1f;
        bool penNext = true;
        TextMesh bubble;
        float bubbleTimer;

        static readonly string[] PenComments = { "unclear!", "citation\nneeded", "why?", "rephrase", "typo", "more\nexperiments", "see\nmy notes", "too long", "RQ?" };
        static readonly string[] HitLines = { "Very good! But...", "Interesting point!", "Hm, convincing.", "OK, I'll accept that.", "Nice argument!", "Fair enough!", "Mate? Thanks!" };

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        int P => Mathf.Clamp(Phase - 1, 0, 2);

        public void Begin()
        {
            Active = true;
            throwTimer = 1.5f;
            Say("Let's start with your\nfirst research question!", 3f);
        }

        /// <summary>Shield empty: the phase starts over (no game over).</summary>
        public void RestartPhase()
        {
            HitsTaken = (Phase - 1) * hitsPerPhase;
            Say("Let's go over this\nchapter once more.", 3f);
        }

        public void Dazzle() { if (Active && !Defeated) { dazzle = 1.5f; Say("Ah, a photo for\nthe group website!", 2f); } }

        public void Say(string text, float seconds)
        {
            if (!bubble)
            {
                var go = new GameObject("Speech");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 1.3f, 0);
                bubble = go.AddComponent<TextMesh>();
                bubble.font = font;
                bubble.fontSize = 48;
                bubble.characterSize = 0.06f;
                bubble.anchor = TextAnchor.LowerCenter;
                bubble.alignment = TextAlignment.Center;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingOrder = 60;
            }
            bubble.text = text;
            bubble.color = new Color(1f, 0.95f, 0.75f);
            bubbleTimer = seconds;
        }

        /// <summary>A thrown Mate reaches him.</summary>
        public bool HitByMate(Vector2 at)
        {
            if (!Active || Defeated || hitTimer > 0f) return false;
            if (Vector2.Distance(at, (Vector2)transform.position + Vector2.up * 0.2f) > hitRadius) return false;
            HitsTaken++;
            hitTimer = 0.8f;
            GameAudio.Play("mate_shatter");
            GameAudio.Play("boss_hit", 0.7f);
            Effects.Burst(transform.position, new Color(1f, 0.75f, 0.2f), 18);
            anim?.Play("hit", true);
            HUD.Instance?.Toast(Health > 0 ? $"Direct hit! {Health} to go" : "Direct hit!");
            if (HitsTaken >= MaxHealth) { Defeat(); return true; }
            Say(HitLines[Random.Range(0, HitLines.Length)], 2.2f);
            int newPhase = 1 + HitsTaken / hitsPerPhase;
            if (newPhase != Phase)
            {
                Phase = newPhase;
                OnPhaseChanged?.Invoke(Phase);
            }
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (bubble)
            {
                bubbleTimer -= dt;
                bubble.gameObject.SetActive(bubbleTimer > 0f);
                float x = transform.position.x;
                bubble.transform.localPosition = new Vector3(Mathf.Clamp(x, -halfWidth + 2.5f, halfWidth - 2.5f) - x, 1.3f, 0);
            }
            Vector3 pos = transform.position;
            Vector3 old = pos;
            if (Defeated)
            {
                pos.y = Mathf.MoveTowards(pos.y, arenaFloorY + 1.1f, 4f * dt);
                pos.x = Mathf.MoveTowards(pos.x, 2.5f, 3f * dt);
                transform.position = pos;
                return;
            }
            float midY = arenaFloorY + (hoverLow + hoverHigh) * 0.5f;
            float amp = (hoverHigh - hoverLow) * 0.5f;
            if (!Active)
            {
                pos.y = midY + Mathf.Sin(t * 2f) * 0.3f;
                transform.position = pos;
                return;
            }
            dazzle -= dt;
            hitTimer -= dt;
            // side to side across the Felix-Klein-Hörsaal, gently bobbing up and down
            float speed = dazzle > 0f ? 0f : moveSpeed[P] * (hitTimer > 0f ? 0.3f : 1f);
            pos.x += dir * speed * dt;
            if (pos.x > halfWidth - 1.2f) dir = -1f; else if (pos.x < -halfWidth + 1.2f) dir = 1f;
            pos.y = Mathf.MoveTowards(pos.y, midY + Mathf.Sin(t * (0.9f + 0.2f * P)) * amp, 5f * dt);
            transform.position = pos;
            Velocity = (pos - old) / Mathf.Max(0.0001f, dt);
            if (view) view.color = dazzle > 0f ? Color.Lerp(Color.white, new Color(1f, 1f, 0.6f), 0.5f + 0.5f * Mathf.Sin(t * 25f))
                                 : hitTimer > 0f ? Color.Lerp(Color.white, new Color(1f, 0.6f, 0.5f), 0.5f + 0.5f * Mathf.Sin(t * 30f)) : Color.white;

            if (dazzle <= 0f)
            {
                throwTimer -= dt;
                if (throwTimer <= 0f)
                {
                    Throw(ClimbPlayer.Instance);
                    throwTimer = throwInterval[P];
                }
            }
            if (anim && anim.CurrentName != "idle" && anim.Finished) anim.Play("idle");
        }

        void Defeat()
        {
            Defeated = true;
            GameAudio.Play("applause");
            GameAudio.Play("jingle_win");
            GameAudio.PlayMusic("victory", 2.5f);
            Say("Congratulations,\nDr. Dorn!", 6f);
            foreach (var f in FindObjectsByType<ClimbFaller>(FindObjectsSortMode.None)) f.Pop();
            OnDefeated?.Invoke();
        }

        void Throw(ClimbPlayer p)
        {
            anim?.Play("attack", true);
            GameAudio.Play("boss_throw", 0.8f);
            float targetX = p ? Mathf.Lerp(transform.position.x, p.transform.position.x, 0.6f) : transform.position.x;
            var go = new GameObject(penNext ? "RedPenComment" : "ConfigBox");
            go.transform.position = new Vector3(targetX, transform.position.y - 0.8f, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 12;
            var f = go.AddComponent<ClimbFaller>();
            f.bossProjectile = true;
            f.killBelow = arenaFloorY - 1f;
            if (penNext)
            {
                sr.sprite = penSprite;
                go.transform.rotation = Quaternion.Euler(0, 0, -70f);
                f.speed = penSpeed[P];
                f.radius = 0.4f;
                f.reason = "Red-pen comment";
                var label = new GameObject("Comment");
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = new Vector3(0.6f, 0.2f, 0);
                label.transform.rotation = Quaternion.identity;
                var tm = label.AddComponent<TextMesh>();
                tm.font = font; tm.fontSize = 48; tm.characterSize = 0.05f;
                tm.anchor = TextAnchor.MiddleLeft; tm.color = new Color(1f, 0.25f, 0.25f);
                tm.text = PenComments[Random.Range(0, PenComments.Length)];
                var mr = label.GetComponent<MeshRenderer>(); mr.sharedMaterial = font.material; mr.sortingOrder = 61;
                label.AddComponent<KeepUpright>();
            }
            else
            {
                sr.sprite = boxSprite;
                f.speed = boxSpeed[P];
                f.spin = 40f;
                f.radius = 0.55f;
                f.reason = "Just one more configuration";
                go.transform.localScale = Vector3.one * 1.2f;
                if (Random.value < 0.4f) Say("Just one more\nconfiguration!", 1.6f);
            }
            penNext = !penNext;
            if (Phase >= 3 && Random.value < 0.35f) penNext = true; // phase 3: more pens
        }
    }

    /// <summary>Keeps a child (e.g. a label) unrotated while its parent spins.</summary>
    public class KeepUpright : MonoBehaviour
    {
        void LateUpdate() { transform.rotation = Quaternion.identity; }
    }

    /// <summary>A Mate bottle thrown at the professor: flies straight, spinning. Also knocks falling comments and boxes out of the air.</summary>
    public class ClimbMateShot : MonoBehaviour
    {
        public Vector2 velocity;
        float life = 2.5f;

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += (Vector3)(velocity * dt);
            transform.Rotate(0, 0, 720f * dt);
            life -= dt;
            Vector2 p = transform.position;
            var boss = SiegmundBoss.Instance;
            if (boss && boss.HitByMate(p)) { Destroy(gameObject); return; }
            foreach (var f in FindObjectsByType<ClimbFaller>(FindObjectsSortMode.None))
                if (f.bossProjectile && Vector2.Distance(f.transform.position, p) < 0.7f) f.Pop();
            if (life <= 0f) { Effects.Burst(p, new Color(1f, 0.75f, 0.2f), 4); Destroy(gameObject); }
        }
    }
}
