using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes in the Paulinum climb (Doodle Jump style): bounces automatically on every landing,
    /// left/right steers, the sides wrap around. Mate = short jetpack boost upward, Q = camera flash.
    /// Uses its own simple physics (no Rigidbody): one-way platforms from <see cref="ClimbPlatform.All"/>.
    /// The transform sits at his feet.
    /// </summary>
    public class ClimbPlayer : MonoBehaviour
    {
        public static ClimbPlayer Instance { get; private set; }
        /// <summary>Playtest autopilot: when set, replaces the horizontal input (-1..1).</summary>
        public static System.Func<float> InputOverride;
        public static bool FlashOverride;
        /// <summary>Playtest autopilot: a world point to throw a Mate at this frame (boss fight).</summary>
        public static Vector2? ThrowOverride;

        [Header("Movement")]
        public float gravity = 30f;
        public float bounceSpeed = 16f;
        public float moveSpeed = 9f;
        public float accel = 60f;
        public float maxFall = 22f;
        public float halfWidth = 11f;

        [Header("Mate jetpack")]
        public float jetpackSpeed = 20f;
        public float jetpackDuration = 1.2f;

        [Header("Boss fight: run and throw Mate")]
        public int mateAmmo = 3;
        public int mateAmmoMax = 6;
        public float throwSpeed = 16f;
        public Sprite mateSprite;

        [Header("Camera flash")]
        public float flashCooldown = 4f;
        public float flashRadius = 7f;
        public float flashStun = 3f;

        [Header("Getting hit")]
        public float hitPenaltySeconds = 6f;
        public int hitPaperLoss = 2;
        public float invulnerableTime = 1.5f;

        [Header("Refs")]
        public SpriteRenderer body;
        public SpriteAnimator anim;

        public Vector2 Velocity;
        public bool Frozen;      // cutscenes, end screens
        public bool BossMode;    // boss fight: runs on the floor (no jumping), throws Mate; hits use the paper shield
        public float GroundY;    // the Felix-Klein-Hörsaal floor in boss mode
        float jetTimer, flashTimer, invulnTimer, actionTimer;
        string actionClip;
        bool facingRight = true;

        public float Feet => transform.position.y;
        public bool Falling => Velocity.y < 0f;
        public bool Jetpacking => jetTimer > 0f;
        public float FlashCooldownRemaining => Mathf.Max(0f, flashTimer);
        public float JetpackRemaining => Mathf.Max(0f, jetTimer);
        public bool Invulnerable => invulnTimer > 0f;
        public ClimbPlatform LastPlatform { get; private set; }

        public event System.Action OnFlash;
        public event System.Action<string> OnShieldHit;

        /// <summary>Apex height of a normal bounce.</summary>
        public float BounceHeight => bounceSpeed * bounceSpeed / (2f * gravity);

        void Awake() { Instance = this; if (!body) body = GetComponentInChildren<SpriteRenderer>(); if (!anim) anim = GetComponentInChildren<SpriteAnimator>(); }
        void OnDestroy() { if (Instance == this) { Instance = null; InputOverride = null; FlashOverride = false; ThrowOverride = null; } }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            jetTimer -= dt; flashTimer -= dt; invulnTimer -= dt; actionTimer -= dt;
            if (Frozen) { UpdateVisuals(); return; }
            if (BossMode) { UpdateBossMode(dt); return; }

            float input = InputOverride != null ? InputOverride() : GameInput.Horizontal;
            if (input > 0.01f) facingRight = true; else if (input < -0.01f) facingRight = false;
            Velocity.x = Mathf.MoveTowards(Velocity.x, input * moveSpeed, accel * dt);

            if (jetTimer > 0f) Velocity.y = jetpackSpeed;
            else Velocity.y = Mathf.Max(Velocity.y - gravity * dt, -maxFall);

            // move in a few substeps so fast falls never skip a platform
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(Velocity.y) * dt / 0.15f));
            float sdt = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = transform.position;
                float prevFeet = p.y;
                p.x += Velocity.x * sdt;
                p.y += Velocity.y * sdt;
                if (p.x > halfWidth) p.x -= 2f * halfWidth;
                else if (p.x < -halfWidth) p.x += 2f * halfWidth;
                if (Velocity.y <= 0f)
                {
                    foreach (var pl in ClimbPlatform.All)
                    {
                        if (!pl.Active) continue;
                        float top = pl.Top;
                        if (prevFeet >= top - 0.02f && p.y <= top && p.x > pl.Left - 0.3f && p.x < pl.Right + 0.3f)
                        {
                            p.y = top;
                            transform.position = p;
                            Land(pl);
                            break;
                        }
                    }
                }
                transform.position = p;
            }

            if (GameInput.FlashPressed || FlashOverride) { FlashOverride = false; Flash(); }
            UpdateVisuals();
        }

        // ---------------- boss fight: run on the floor, throw Mate at the mouse ----------------
        void UpdateBossMode(float dt)
        {
            float input = InputOverride != null ? InputOverride() : GameInput.Horizontal;
            if (input > 0.01f) facingRight = true; else if (input < -0.01f) facingRight = false;
            Velocity.x = Mathf.MoveTowards(Velocity.x, input * moveSpeed, accel * 1.5f * dt);
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x + Velocity.x * dt, -halfWidth + 0.7f, halfWidth - 0.7f);
            if (Mathf.Abs(p.x) >= halfWidth - 0.71f) Velocity.x = 0f;
            // no jumping: if he arrives in the air he simply lands on the Felix-Klein-Hörsaal floor
            if (p.y > GroundY + 0.001f || Velocity.y > 0f)
            {
                Velocity.y = Mathf.Max(Velocity.y - gravity * dt, -maxFall);
                if (Velocity.y > 0f) Velocity.y = Mathf.Min(Velocity.y, 4f);
                p.y += Velocity.y * dt;
            }
            if (p.y <= GroundY) { p.y = GroundY; Velocity.y = 0f; }
            transform.position = p;

            if (ThrowOverride.HasValue) { ThrowMate(ThrowOverride.Value); ThrowOverride = null; }
            else if (ThrowInput(out var target)) ThrowMate(target);
            if (GameInput.FlashPressed || FlashOverride) { FlashOverride = false; Flash(); }
            UpdateVisuals();
        }

        /// <summary>Left or right click / Space throws toward the mouse; gamepad X throws at the professor.</summary>
        bool ThrowInput(out Vector2 target)
        {
            target = Vector2.zero;
            if (GameInput.Locked) return false;
            var ms = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            bool mouseThrow = (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame)) || (kb != null && kb.spaceKey.wasPressedThisFrame);
            if (mouseThrow && ms != null) { target = MouseWorld(); return true; }
            if (gp != null && gp.buttonWest.wasPressedThisFrame && SiegmundBoss.Instance) { target = SiegmundBoss.Instance.transform.position; return true; }
            return false;
        }

        public static Vector2 MouseWorld()
        {
            var ms = UnityEngine.InputSystem.Mouse.current;
            var cam = Camera.main;
            if (ms == null || !cam) return Vector2.zero;
            Vector3 sp = ms.position.ReadValue();
            sp.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(sp);
        }

        public Vector2 ThrowOrigin => (Vector2)transform.position + Vector2.up * 1.0f;

        void ThrowMate(Vector2 target)
        {
            if (mateAmmo <= 0) { HUD.Instance?.Toast("No Mate left! Grab a bottle from the floor"); GameAudio.Play("mate_empty", 1f, 0.06f, 0.4f); return; }
            mateAmmo--;
            GameAudio.Play("mate_throw");
            Vector2 from = ThrowOrigin;
            Vector2 dir = target - from;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();
            if (dir.y < 0.1f) { dir.y = 0.1f; dir.Normalize(); } // never into the floor
            facingRight = dir.x >= 0f;
            var go = new GameObject("ThrownMate");
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = mateSprite;
            sr.sortingOrder = 13;
            go.AddComponent<ClimbMateShot>().velocity = dir * throwSpeed;
            PlayAction("throw", 0.3f);
        }

        public void AddMateAmmo(int n)
        {
            mateAmmo = Mathf.Min(mateAmmoMax, mateAmmo + n);
            HUD.Instance?.Toast($"Mate +{n}  (click to throw at the professor)");
        }

        void Land(ClimbPlatform pl)
        {
            LastPlatform = pl;
            float k = pl.kind == PlatKind.Spring ? pl.springBoost : 1f;
            Velocity.y = bounceSpeed * k;
            pl.OnBounce();
            if (k > 1f) GameAudio.Play("spring", 0.8f);
            else if (pl.kind != PlatKind.Crumble) GameAudio.Play("bounce", 0.6f, 0.1f);
            if (k > 1f) { anim?.Play(anim.Has("doublejump") ? "doublejump" : "jump", true); Effects.Burst(transform.position, Color.white, 6); }
            else anim?.Play("jump", true);
        }

        /// <summary>Bounce off something (drone, boss head). k scales the normal bounce.</summary>
        public void Bounce(float k = 1f)
        {
            Velocity.y = bounceSpeed * k;
            anim?.Play("jump", true);
            GameAudio.Play("stomp");
        }

        public void Jetpack()
        {
            jetTimer = jetpackDuration;
            PlayAction("drink", 0.4f);
            GameAudio.Play("mate_drink");
            GameAudio.Play("boss_throw", 1.4f, 0.05f);
            HUD.Instance?.Toast("Mate! Whoosh!");
        }

        /// <summary>Box of 0.5 x 1.3 at the feet against a circle.</summary>
        public bool Overlaps(Vector2 c, float r)
        {
            Vector2 p = transform.position;
            float cx = Mathf.Clamp(c.x, p.x - 0.28f, p.x + 0.28f);
            float cy = Mathf.Clamp(c.y, p.y, p.y + 1.3f);
            float dx = c.x - cx, dy = c.y - cy;
            return dx * dx + dy * dy < r * r;
        }

        void Flash()
        {
            if (flashTimer > 0f) return;
            flashTimer = flashCooldown;
            PlayAction("flash", 0.4f);
            GameAudio.Play("flash");
            OnFlash?.Invoke();
            Vector2 p = (Vector2)transform.position + Vector2.up * 0.7f;
            foreach (var d in FindObjectsByType<ClimbDrone>(FindObjectsSortMode.None))
                if (Vector2.Distance(d.transform.position, p) < flashRadius) d.Stun(flashStun);
            foreach (var s in FindObjectsByType<ImposterShadow>(FindObjectsSortMode.None))
                if (Vector2.Distance(s.transform.position, p) < flashRadius * 1.2f) s.Banish(7f);
            foreach (var f in FindObjectsByType<ClimbFaller>(FindObjectsSortMode.None))
                if (Vector2.Distance(f.transform.position, p) < flashRadius * 0.7f) f.Pop();
            foreach (var s in FindObjectsByType<ClimbPhotoSpot>(FindObjectsSortMode.None))
                if (Vector2.Distance(s.transform.position, p) < 4f) s.Photograph();
            var boss = SiegmundBoss.Instance;
            if (boss && Vector2.Distance(boss.transform.position, p) < flashRadius * 1.5f) boss.Dazzle();
        }

        /// <summary>Climbing hit: costs time and a little paper.</summary>
        public void Hurt(string reason)
        {
            if (invulnTimer > 0f || Frozen) return;
            if (BossMode) { HitShield(reason); return; }
            invulnTimer = invulnerableTime;
            DeadlineClock.Instance?.AddPenalty(hitPenaltySeconds);
            PaperMeter.Instance?.Remove(hitPaperLoss);
            HUD.Instance?.Toast($"{reason}! -{hitPenaltySeconds:0}s, -{PaperMeter.Pieces(hitPaperLoss)}");
            PlayAction("hurt", 0.4f);
            GameAudio.Play("hurt");
        }

        /// <summary>Boss fight hit: the paper works as a shield (handled by the boss / game).</summary>
        public void HitShield(string reason)
        {
            if (invulnTimer > 0f || Frozen) return;
            invulnTimer = invulnerableTime;
            PlayAction("hurt", 0.4f);
            GameAudio.Play("paper_hit");
            OnShieldHit?.Invoke(reason);
        }

        public void Teleport(Vector2 pos, float vy)
        {
            transform.position = pos;
            Velocity = new Vector2(0f, vy);
            jetTimer = 0f;
            invulnTimer = invulnerableTime;
        }

        public void SetVictory()
        {
            Frozen = true;
            Velocity = Vector2.zero;
            if (anim && anim.Has("victory")) anim.Play("victory", true);
        }

        void PlayAction(string clip, float duration)
        {
            if (anim == null || !anim.Has(clip)) return;
            actionClip = clip;
            actionTimer = duration;
            anim.Play(clip, true);
        }

        void UpdateVisuals()
        {
            if (body)
            {
                body.flipX = !facingRight;
                body.enabled = invulnTimer <= 0f || Frozen || Mathf.Repeat(Time.time * 12f, 1f) > 0.35f;
                body.color = Jetpacking ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f)) : Color.white;
            }
            if (!anim || Frozen) return;
            if (actionTimer > 0f && actionClip != null) { anim.Play(actionClip); return; }
            if (BossMode && Feet <= GroundY + 0.01f)
            {
                anim.Play(Mathf.Abs(Velocity.x) > 0.3f && anim.Has("run") ? "run" : "idle");
                return;
            }
            string cur = anim.CurrentName;
            bool inJump = (cur == "jump" || cur == "doublejump") && !anim.Finished;
            if (Velocity.y < -2f && !inJump) anim.Play(anim.Has("fall") ? "fall" : "jump");
            if (Jetpacking && Mathf.Repeat(Time.time * 20f, 1f) > 0.5f)
                Effects.Burst((Vector2)transform.position + new Vector2(facingRight ? -0.25f : 0.25f, 0.4f), new Color(1f, 0.7f, 0.2f), 1);
        }
    }
}
