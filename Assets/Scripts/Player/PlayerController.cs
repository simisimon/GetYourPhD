using System.Linq;
using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes: run, jump, double jump, drink/throw Mate, camera flash.
    /// Speed and jump height sink as the deadline approaches (DeadlineClock.SpeedFactor); drinking Mate offsets that.
    /// Getting hit costs time on the clock instead of health.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        public float moveSpeed = 7f;
        public float groundAccel = 90f;
        public float airAccel = 55f;
        public float jumpVelocity = 14f;
        public float doubleJumpVelocity = 12.5f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;
        [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;
        public float maxFallSpeed = 20f;
        [Tooltip("How strongly the deadline slowdown also lowers jumps (0 = not at all, 1 = as much as speed).")]
        [Range(0f, 1f)] public float jumpSlowdownShare = 0.5f;

        [Header("Mate")]
        public int mateCount = 2;
        public int mateMax = 9;
        public float boostMultiplier = 1.6f;
        public float boostDuration = 5f;
        public Sprite mateSprite;
        public float throwSpeed = 13f;

        [Header("Camera flash")]
        public float flashCooldown = 4f;
        public float flashRadius = 7f;
        public float flashStun = 2.5f;
        public float flashReveal = 5f;

        [Header("Getting hit")]
        public float hitPenaltySeconds = 10f;
        [Tooltip("Paper points lost per hit (a data point is worth 1, a draft 5).")]
        public int hitPaperLoss = 2;
        public float invulnerableTime = 1.2f;
        public float fallPenaltySeconds = 10f;
        public float killY = -12f;

        [Header("Refs")]
        public SpriteRenderer body;
        public SpriteAnimator anim;

        Rigidbody2D rb;
        bool grounded;
        bool doubleJumpUsed;
        float coyoteTimer, bufferTimer;
        float boostTimer, flashTimer, invulnTimer, actionTimer;
        string actionClip;
        bool facingRight = true;
        Vector2 lastSafePos;
        float safeTimer;
        ConfigPlatform groundPlatform;
        bool victory;

        public bool Grounded => grounded;
        public bool Boosted => boostTimer > 0f;
        public float BoostRemaining => boostTimer;
        public float FlashCooldownRemaining => Mathf.Max(0f, flashTimer);
        public bool FacingRight => facingRight;

        public event System.Action OnMateChanged;
        public event System.Action OnFlash;

        public float EffectiveSpeedFactor
        {
            get
            {
                float f = DeadlineClock.Instance ? DeadlineClock.Instance.SpeedFactor : 1f;
                if (Boosted) f *= boostMultiplier;
                return Mathf.Min(f, 1.15f);
            }
        }

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            if (!body) body = GetComponentInChildren<SpriteRenderer>();
            if (!anim) anim = GetComponentInChildren<SpriteAnimator>();
            lastSafePos = transform.position;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            boostTimer -= dt; flashTimer -= dt; invulnTimer -= dt; actionTimer -= dt;
            coyoteTimer -= dt; bufferTimer -= dt;

            if (victory) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }

            if (GameInput.JumpPressed) bufferTimer = jumpBuffer;
            if (grounded) { coyoteTimer = coyoteTime; doubleJumpUsed = false; }

            float jumpScale = 1f - (1f - EffectiveSpeedFactorForJump) * jumpSlowdownShare;
            if (bufferTimer > 0f)
            {
                if (coyoteTimer > 0f)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity * jumpScale);
                    bufferTimer = 0f; coyoteTimer = 0f; grounded = false;
                    anim?.Play("jump", true);
                    GameAudio.Play("jump", 0.8f);
                }
                else if (!doubleJumpUsed)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpVelocity * jumpScale);
                    bufferTimer = 0f; doubleJumpUsed = true;
                    anim?.Play(anim.Has("doublejump") ? "doublejump" : "jump", true);
                    GameAudio.Play("jump2", 0.8f);
                }
            }
            if (!GameInput.JumpHeld && rb.linearVelocity.y > 0f && !grounded)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * Mathf.Pow(jumpCutMultiplier, dt * 20f));

            if (GameInput.DrinkPressed) Drink();
            if (GameInput.ThrowPressed) Throw();
            if (GameInput.FlashPressed) Flash();

            // Fell into a pit
            if (transform.position.y < killY)
            {
                transform.position = lastSafePos + Vector2.up * 0.5f;
                rb.linearVelocity = Vector2.zero;
                DeadlineClock.Instance?.AddPenalty(fallPenaltySeconds);
                HUD.Instance?.Toast($"Fell into the backlog! -{fallPenaltySeconds:0}s");
                GameAudio.Play("fall");
                invulnTimer = invulnerableTime;
            }

            if (grounded && groundPlatform == null)
            {
                safeTimer += dt;
                if (safeTimer > 0.3f) lastSafePos = transform.position;
            }
            else safeTimer = 0f;

            UpdateVisuals();
        }

        float EffectiveSpeedFactorForJump => Mathf.Min(1f, EffectiveSpeedFactor);

        void FixedUpdate()
        {
            CheckGround();
            if (victory) return;

            float input = GameInput.Horizontal;
            if (input > 0.01f) facingRight = true;
            else if (input < -0.01f) facingRight = false;

            float target = input * moveSpeed * EffectiveSpeedFactor;
            float carry = groundPlatform ? groundPlatform.Velocity.x : 0f;
            float accel = grounded ? groundAccel : airAccel;
            float vx = Mathf.MoveTowards(rb.linearVelocity.x - carry, target, accel * Time.fixedDeltaTime) + carry;
            float vy = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);
            rb.linearVelocity = new Vector2(vx, vy);
        }

        readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        void CheckGround()
        {
            grounded = false;
            groundPlatform = null;
            int n = rb.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                if (contacts[i].collider.isTrigger) continue;
                if (contacts[i].normal.y > 0.6f)
                {
                    var p = contacts[i].collider.GetComponentInParent<ConfigPlatform>();
                    float relVy = rb.linearVelocity.y - (p ? p.Velocity.y : 0f);
                    if (relVy > 1f) continue; // just jumped off
                    grounded = true;
                    if (p) groundPlatform = p;
                }
            }
        }

        void UpdateVisuals()
        {
            if (body)
            {
                body.flipX = !facingRight;
                body.enabled = invulnTimer <= 0f || Mathf.Repeat(Time.time * 12f, 1f) > 0.35f;
                body.color = Boosted ? Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f)) : Color.white;
            }
            if (!anim) return;
            if (actionTimer > 0f && actionClip != null) { anim.Play(actionClip); return; }
            if (typing && grounded) { anim.Play(anim.Has("type") ? "type" : "idle"); return; }
            if (!grounded)
            {
                string cur = anim.CurrentName;
                bool inJumpAnim = (cur == "jump" || cur == "doublejump") && !anim.Finished;
                if (!inJumpAnim) anim.Play(rb.linearVelocity.y > 0.5f ? "jump" : (anim.Has("fall") ? "fall" : "jump"));
            }
            else if (Mathf.Abs(rb.linearVelocity.x - (groundPlatform ? groundPlatform.Velocity.x : 0f)) > 0.3f) anim.Play("run");
            else anim.Play("idle");
        }

        void PlayAction(string clip, float duration)
        {
            if (anim == null || !anim.Has(clip)) return;
            actionClip = clip;
            actionTimer = duration;
            anim.Play(clip, true);
        }

        public void AddMate(int n = 1)
        {
            mateCount = Mathf.Clamp(mateCount + n, 0, mateMax);
            OnMateChanged?.Invoke();
        }

        void Drink()
        {
            if (mateCount <= 0) { HUD.Instance?.Toast("No Mate left!"); GameAudio.Play("mate_empty"); return; }
            mateCount--;
            GameAudio.Play("mate_drink");
            boostTimer = boostDuration;
            OnMateChanged?.Invoke();
            PlayAction("drink", 0.5f);
            HUD.Instance?.Toast("Mate boost!");
        }

        void Throw()
        {
            if (mateCount <= 0) { HUD.Instance?.Toast("No Mate left!"); GameAudio.Play("mate_empty"); return; }
            mateCount--;
            OnMateChanged?.Invoke();
            PlayAction("throw", 0.35f);
            GameAudio.Play("mate_throw");
            float dir = facingRight ? 1f : -1f;
            MateProjectile.Spawn(mateSprite, (Vector2)transform.position + new Vector2(0.5f * dir, 0.4f), new Vector2(throwSpeed * dir, 6f));
        }

        void Flash()
        {
            if (flashTimer > 0f) return;
            flashTimer = flashCooldown;
            PlayAction("flash", 0.4f);
            GameAudio.Play("flash");
            OnFlash?.Invoke();
            Vector2 p = transform.position;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (Vector2.Distance(e.transform.position, p) < flashRadius) e.Stun(flashStun);
            foreach (var h in FindObjectsByType<HiddenPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Vector2.Distance(h.Center, p) < flashRadius * 1.5f) h.Reveal(flashReveal);
            foreach (var t in HomeTask.All.ToArray())
                if (t.Active && t.fixableByFlash && Vector2.Distance(t.transform.position, p) < flashRadius) t.Resolve(t.flashFixText);
            foreach (var s in FindObjectsByType<PhotoSpot>(FindObjectsSortMode.None))
                if (Vector2.Distance(s.transform.position, p) < 3f) s.Photograph();
        }

        public void Hurt(Vector2 from, string reason = null)
        {
            if (invulnTimer > 0f || victory) return;
            invulnTimer = invulnerableTime;
            DeadlineClock.Instance?.AddPenalty(hitPenaltySeconds);
            PaperMeter.Instance?.Remove(hitPaperLoss);
            HUD.Instance?.Toast($"{reason ?? "Distracted"}! -{hitPenaltySeconds:0}s, -{PaperMeter.Pieces(hitPaperLoss)}");
            float dir = Mathf.Sign(transform.position.x - from.x);
            if (dir == 0f) dir = facingRight ? -1f : 1f;
            rb.linearVelocity = new Vector2(dir * 7f, 8f);
            PlayAction("hurt", 0.4f);
            GameAudio.Play("hurt");
        }

        public void Bounce(float vy = 11f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);
            doubleJumpUsed = false;
        }

        bool typing;
        /// <summary>Level 2: typing at the desk (plays the "type" clip if there is one).</summary>
        public void SetTyping(bool on)
        {
            if (on == typing) return;
            typing = on;
            if (on) facingRight = true;
        }

        public void SetVictory()
        {
            victory = true;
            if (anim && anim.Has("victory")) anim.Play("victory", true);
            else anim?.Play("idle");
        }

        public void ClearVictory() { victory = false; }

        public void Teleport(Vector2 pos)
        {
            transform.position = pos;
            rb.linearVelocity = Vector2.zero;
            lastSafePos = pos;
        }
    }
}
