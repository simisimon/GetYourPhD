using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Playtest autopilot for Level 4 (add the component in Play mode). Steers to the next reachable platform,
    /// flashes at nearby drones / shadows, and in the boss fight waits on a tier platform under the professor.
    /// <see cref="skill"/> below 1 adds sloppy steering, like a non-gamer would.
    /// </summary>
    public class ClimbBot : MonoBehaviour
    {
        [Range(0f, 1f)] public float skill = 0.7f;
        public float startTime;
        ClimbPlatform target;
        float wobble, wobbleTimer, throwTimer;
        float lastVy;

        void OnEnable()
        {
            ClimbPlayer.InputOverride = Steer;
            startTime = Time.time;
        }

        void OnDisable() { if (ClimbPlayer.InputOverride == (System.Func<float>)Steer) ClimbPlayer.InputOverride = null; }

        static float WrapDx(float from, float to, float hw)
        {
            float dx = to - from;
            if (dx > hw) dx -= 2f * hw; else if (dx < -hw) dx += 2f * hw;
            return dx;
        }

        public bool autoDismiss;

        /// <summary>Playtests: set PlayerPrefs "GYP_L4Bot" = 1 before entering Play mode (skill in "GYP_L4BotSkill", timescale in "GYP_L4BotSpeed").</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoAttach()
        {
            if (PlayerPrefs.GetInt("GYP_L4Bot", 0) == 0) return;
            var g = FindAnyObjectByType<ClimbGame>();
            if (!g) return;
            Application.runInBackground = true;
            var b = g.gameObject.AddComponent<ClimbBot>();
            b.autoDismiss = true;
            b.skill = PlayerPrefs.GetFloat("GYP_L4BotSkill", 0.7f);
        }

        float nextLog;
        void Update()
        {
            if (autoDismiss && !(ClimbGame.Instance && ClimbGame.Instance.Won))
            {
                HowToPlayOverlay.DismissRequested = true;
                float speed = PlayerPrefs.GetFloat("GYP_L4BotSpeed", 1f);
                if (Time.timeScale > 0f && ClimbGame.Instance && ClimbGame.Instance.Running) Time.timeScale = speed;
            }
            var g = ClimbGame.Instance; var p = ClimbPlayer.Instance; var c = DeadlineClock.Instance;
            if (!g || !p || Time.time < nextLog) return;
            nextLog = Time.time + 10f;
            var b = SiegmundBoss.Instance;
            Debug.Log($"[ClimbBot] t={Time.time - startTime:0} feet={p.Feet:0.0} floor={g.CurrentFloor} falls={g.Falls} clock={(c ? c.Remaining : 0):0} paper={(PaperMeter.Instance ? PaperMeter.Instance.Points : 0)}" +
                      (g.BossStarted && b ? $" boss phase={b.Phase} hp={b.Health} shield={g.Shield}/{g.MaxShield}" : ""));
        }

        float Steer()
        {
            var p = ClimbPlayer.Instance;
            var g = ClimbGame.Instance;
            if (!p || !g) return 0f;
            float hw = p.halfWidth;
            Vector2 pos = p.transform.position;

            wobbleTimer -= Time.deltaTime;
            if (wobbleTimer <= 0f) { wobbleTimer = Random.Range(0.3f, 0.8f); wobble = Random.value < (1f - skill) * 0.6f ? Random.Range(-1f, 1f) : 0f; }

            // flash at threats
            if (p.FlashCooldownRemaining <= 0f)
            {
                foreach (var d in FindObjectsByType<ClimbDrone>(FindObjectsSortMode.None))
                    if (!d.Stunned && Vector2.Distance(d.transform.position, pos) < 4f) { ClimbPlayer.FlashOverride = true; break; }
                foreach (var s in FindObjectsByType<ImposterShadow>(FindObjectsSortMode.None))
                    if (!s.Banished && Vector2.Distance(s.transform.position, pos) < 7f) { ClimbPlayer.FlashOverride = true; break; }
            }

            float wantX;
            var boss = SiegmundBoss.Instance;
            if (g.BossStarted && boss)
            {
                // on foot: fetch Mate when low, otherwise follow the professor and throw at him
                wantX = boss.transform.position.x;
                if (p.mateAmmo < 2)
                {
                    float best = float.MaxValue;
                    foreach (var pk in FindObjectsByType<ClimbPickup>(FindObjectsSortMode.None))
                        if (pk.kind == ClimbPickupKind.MateAmmo && Mathf.Abs(pk.transform.position.x - pos.x) < best) { best = Mathf.Abs(pk.transform.position.x - pos.x); wantX = pk.transform.position.x; }
                }
                throwTimer -= Time.deltaTime;
                if (p.mateAmmo > 0 && throwTimer <= 0f)
                {
                    throwTimer = Random.Range(0.6f, 1.4f) / Mathf.Max(0.3f, skill);
                    // lead the moving target a little; sloppy bots aim worse
                    Vector2 bp = boss.transform.position;
                    float flight = Vector2.Distance(p.ThrowOrigin, bp) / p.throwSpeed;
                    Vector2 aim = bp + boss.Velocity * flight * skill + Random.insideUnitCircle * (1f - skill) * 2.5f;
                    ClimbPlayer.ThrowOverride = aim;
                }
                // dodge things falling right above
                foreach (var f in FindObjectsByType<ClimbFaller>(FindObjectsSortMode.None))
                {
                    Vector2 fp = f.transform.position;
                    if (fp.y > pos.y && fp.y - pos.y < 5f && Mathf.Abs(fp.x - pos.x) < 1.3f && Random.value < skill + 0.3f) { float d = fp.x > pos.x ? -1f : 1f; if (Mathf.Abs(pos.x + d * 2.5f) > hw - 1.2f) d = -d; wantX = pos.x + d * 2.5f; }
                }
            }
            else
            {
                // commit to one target per bounce; pick again after landing or after missing it
                bool landed = p.Velocity.y > 0f && lastVy <= 0f; // a bounce just happened
                lastVy = p.Velocity.y;
                if (target == null || !target.Active || landed || (p.Velocity.y < 0f && pos.y < target.Top - 0.3f))
                    target = Pick(p);
                if (!target) return wobble;
                wantX = target.X;
            }
            float dx = p.BossMode ? wantX - pos.x : WrapDx(pos.x, wantX, hw);
            float steer = Mathf.Clamp(dx * 1.5f, -1f, 1f);
            if (Mathf.Abs(dx) < 0.2f) steer = 0f;
            return Mathf.Clamp(steer + wobble, -1f, 1f);
        }

        ClimbPlatform Pick(ClimbPlayer p)
        {
            var a = Pick(p, true);
            if (a && a.Top > p.Feet + 0.1f) return a;
            var b = Pick(p, false);
            return b ? b : a;
        }

        ClimbPlatform Pick(ClimbPlayer p, bool strict)
        {
            Vector2 pos = p.transform.position;
            float apexFeet = p.Velocity.y > 0f ? pos.y + p.Velocity.y * p.Velocity.y / (2f * p.gravity) : pos.y;
            ClimbPlatform best = null; float bestScore = float.MaxValue;
            foreach (var pl in ClimbPlatform.All)
            {
                if (!pl.Active) continue;
                float top = pl.Top;
                if (top > apexFeet - 0.4f) continue;           // can't reach
                if (p.Velocity.y <= 0f && top > pos.y) continue; // already above us while falling
                if (top < pos.y - 6f) continue;
                float dx = Mathf.Abs(WrapDx(pos.x, pl.X, p.halfWidth));
                // time until we pass the platform's height on the way down
                float vy = p.Velocity.y, h = top - pos.y;
                float disc = vy * vy - 2f * p.gravity * h;
                if (disc < 0f) continue;
                float tReach = (vy + Mathf.Sqrt(disc)) / p.gravity;
                if (strict && dx - pl.width * 0.4f > p.moveSpeed * tReach * 0.85f) continue;
                float score = -top * 3f + dx * 0.6f + (pl.kind == PlatKind.Crumble ? 1.5f : 0f) - (pl.kind == PlatKind.Spring ? 3f : 0f);
                if (score < bestScore) { bestScore = score; best = pl; }
            }
            return best;
        }
    }
}
