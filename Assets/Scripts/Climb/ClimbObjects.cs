using System.Collections.Generic;
using UnityEngine;

namespace GetYourPhD
{
    public enum PlatKind { Normal, Moving, Crumble, Spring, Solid }

    /// <summary>
    /// A one-way platform in the Paulinum climb. Its transform sits on the top surface; Johannes lands on it
    /// only while falling. Moving = side-moving beamer, Crumble = "Desk Reject" book stack (one bounce),
    /// Spring = paper stack / organ pipe (extra high bounce), Solid = full-width floor landing (checkpoint).
    /// </summary>
    public class ClimbPlatform : MonoBehaviour
    {
        public static readonly List<ClimbPlatform> All = new List<ClimbPlatform>();

        public PlatKind kind = PlatKind.Normal;
        public float width = 3f;
        public float moveRange = 3f;
        public float moveSpeed = 2f;
        public float springBoost = 1.65f;
        public SpriteRenderer view;
        [Tooltip("Imposter syndrome can make this platform vanish (landings never do).")]
        public bool canVanish = true;

        float baseX, t, vanish, crumbleTimer, respawnTimer;
        bool crumbled;
        Vector3 viewBase;
        float squash;

        public bool Active => !crumbled && vanish <= 0.35f;
        public float Top => transform.position.y;
        public float X => transform.position.x;
        public float Left => X - width * 0.5f;
        public float Right => X + width * 0.5f;
        public float VelocityX { get; private set; }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            baseX = transform.position.x;
            t = Random.value * 10f;
            if (view) viewBase = view.transform.localPosition;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (kind == PlatKind.Moving)
            {
                t += dt;
                float old = transform.position.x;
                float x = baseX + Mathf.Sin(t * moveSpeed / Mathf.Max(0.5f, moveRange)) * moveRange;
                transform.position = new Vector3(x, transform.position.y, 0);
                VelocityX = (x - old) / Mathf.Max(0.0001f, dt);
            }
            // imposter syndrome: fade while a shadow is near
            vanish = Mathf.Max(0f, vanish - dt * 1.2f);
            if (crumbled)
            {
                crumbleTimer += dt;
                respawnTimer -= dt;
                if (view)
                {
                    view.transform.localPosition = viewBase + Vector3.down * crumbleTimer * crumbleTimer * 6f;
                    view.transform.localRotation = Quaternion.Euler(0, 0, crumbleTimer * 40f);
                    var c = view.color; c.a = Mathf.Max(0f, 1f - crumbleTimer * 1.5f); view.color = c;
                }
                if (respawnTimer <= 0f) Restore();
            }
            else if (view)
            {
                float a = Mathf.Clamp01(1f - vanish);
                view.color = new Color(1f, 1f, 1f, vanish > 0.01f ? Mathf.Lerp(0.15f, 1f, a) : 1f);
                if (vanish > 0.35f) view.color = new Color(0.6f, 0.45f, 0.9f, 0.18f + 0.1f * Mathf.Sin(Time.time * 10f));
                squash = Mathf.MoveTowards(squash, 0f, dt * 4f);
                view.transform.localScale = new Vector3(1f, 1f - squash * 0.35f, 1f);
            }
        }

        public void OnBounce()
        {
            if (kind == PlatKind.Crumble && !crumbled)
            {
                crumbled = true;
                crumbleTimer = 0f;
                GameAudio.Play("crumble");
                respawnTimer = 6f;
                Effects.Burst(transform.position, new Color(0.8f, 0.3f, 0.3f), 10);
                HUD.Instance?.Toast("Desk Reject!");
            }
            else if (kind == PlatKind.Spring) squash = 1f;
        }

        public void Vanish(float amount) { if (canVanish && kind != PlatKind.Solid) vanish = Mathf.Min(1.5f, Mathf.Max(vanish, amount)); }

        public void Restore()
        {
            crumbled = false;
            vanish = 0f;
            if (view)
            {
                view.transform.localPosition = viewBase;
                view.transform.localRotation = Quaternion.identity;
                view.color = Color.white;
            }
        }
    }

    public enum ClimbPickupKind { DataPoint, Citation, Paper, Mate, MateAmmo }

    /// <summary>Data points, citations and papers fill the Paper meter; Mate is drunk on the spot (jetpack boost).</summary>
    public class ClimbPickup : MonoBehaviour
    {
        public ClimbPickupKind kind;
        public int points = 1;
        Vector3 basePos;
        float phase;

        void Start() { basePos = transform.position; phase = Random.value * 6f; }

        void Update()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 3f + phase) * 0.1f;
            var p = ClimbPlayer.Instance;
            if (!p || !p.Overlaps(transform.position, 0.55f)) return;
            if (kind == ClimbPickupKind.MateAmmo)
            {
                if (p.mateAmmo >= p.mateAmmoMax) return;
                p.AddMateAmmo(1);
                GameAudio.Play("mate_pickup");
                Effects.Burst(transform.position, new Color(1f, 0.75f, 0.2f), 10);
            }
            else if (kind == ClimbPickupKind.Mate)
            {
                p.Jetpack();
                Effects.Burst(transform.position, new Color(1f, 0.75f, 0.2f), 12);
            }
            else
            {
                PaperMeter.Instance?.Add(points);
                GameAudio.Play(points >= 3 ? "pickup_big" : "pickup", points >= 3 ? 1f : 0.7f, 0.12f);
                if (points >= 3) HUD.Instance?.Toast($"{(kind == ClimbPickupKind.Paper ? "Fresh draft" : "Citation")}: +{PaperMeter.Pieces(points)}");
                Effects.Burst(transform.position, new Color(0.5f, 0.9f, 1f), 8);
            }
            ClimbGame.Instance?.OnPickup(kind);
            Destroy(gameObject);
        }
    }

    /// <summary>Reviewer 2 drone: flies side to side. Bounce on it from above to send it away; touching it otherwise costs time.</summary>
    public class ClimbDrone : MonoBehaviour
    {
        public float range = 5f;
        public float speed = 2.5f;
        public SpriteRenderer view;
        float baseX, baseY, t, stun;

        public bool Stunned => stun > 0f;

        void Start() { baseX = transform.position.x; baseY = transform.position.y; t = Random.value * 10f; }

        public void Stun(float s) { stun = Mathf.Max(stun, s); }

        void Update()
        {
            float dt = Time.deltaTime;
            if (stun > 0f)
            {
                stun -= dt;
                if (view) view.color = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 20f));
            }
            else
            {
                if (view) view.color = Color.white;
                t += dt;
                float old = transform.position.x;
                float x = baseX + Mathf.Sin(t * speed / Mathf.Max(0.5f, range)) * range;
                transform.position = new Vector3(x, baseY + Mathf.Sin(t * 4f) * 0.25f, 0);
                if (view) view.flipX = x > old;
            }
            var p = ClimbPlayer.Instance;
            if (!p || Stunned || !p.Overlaps(transform.position, 0.7f)) return;
            if (p.Falling && p.Feet > transform.position.y - 0.1f)
            {
                p.Bounce(1.1f);
                Effects.Burst(transform.position, new Color(1f, 0.4f, 0.4f), 14);
                HUD.Instance?.Toast("Reviewer 2 dismissed!");
                Destroy(gameObject);
                return;
            }
            p.Hurt("Reviewer 2");
        }
    }

    /// <summary>Something falling from above: coffee cups on the way up, red-pen comments and config boxes in the boss fight.</summary>
    public class ClimbFaller : MonoBehaviour
    {
        public float speed = 6f;
        public float spin = 0f;
        public float radius = 0.5f;
        public string reason = "Coffee spill";
        public bool bossProjectile;
        public float killBelow = -9999f;

        void Update()
        {
            transform.position += Vector3.down * speed * Time.deltaTime;
            if (spin != 0f) transform.Rotate(0, 0, spin * Time.deltaTime);
            if (transform.position.y < killBelow) { Destroy(gameObject); return; }
            var p = ClimbPlayer.Instance;
            if (!p || !p.Overlaps(transform.position, radius)) return;
            if (bossProjectile) p.HitShield(reason);
            else { p.Hurt(reason); GameAudio.Play("coffee", 0.8f); }
            Effects.Burst(transform.position, bossProjectile ? new Color(1f, 0.3f, 0.3f) : new Color(0.6f, 0.4f, 0.2f), 10);
            Destroy(gameObject);
        }

        /// <summary>Camera flash: blown away.</summary>
        public void Pop()
        {
            GameAudio.Play("stomp", 0.7f);
            Effects.Burst(transform.position, Color.white, 8);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Imposter syndrome: Johannes' own dark shadow drifting through the stairwell. Platforms near it vanish;
    /// touching it costs time. The camera flash drives it away for a while.
    /// </summary>
    public class ImposterShadow : MonoBehaviour
    {
        public float range = 7.5f;
        public float speed = 3.2f;
        public float vanishRadius = 2.0f;
        public SpriteRenderer view;
        float baseX, baseY, t, banished;
        static bool hinted;
        public bool Banished => banished > 0f;

        void Start() { baseX = transform.position.x; baseY = transform.position.y; t = Random.value * 10f; hinted = false; }

        public void Banish(float s, bool quiet = false)
        {
            if (banished <= 0f && !quiet) { GameAudio.Play("reveal"); HUD.Instance?.Toast("You belong here! The imposter shadow fades."); Effects.Burst(transform.position, new Color(0.7f, 0.5f, 1f), 16); }
            banished = Mathf.Max(banished, s);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float x = baseX + Mathf.Sin(t * speed / Mathf.Max(0.5f, range)) * range;
            float old = transform.position.x;
            transform.position = new Vector3(x, baseY + Mathf.Sin(t * 1.3f) * 0.3f, 0);
            if (view) view.flipX = x < old;
            if (banished > 0f)
            {
                banished -= dt;
                if (view) view.color = new Color(1, 1, 1, 0.12f);
                return;
            }
            if (view) view.color = new Color(1, 1, 1, 0.75f + 0.2f * Mathf.Sin(Time.time * 5f));
            Vector2 c = transform.position + Vector3.up * 0.7f;
            var pp = ClimbPlayer.Instance;
            if (!hinted && pp && Vector2.Distance(pp.transform.position, c) < 8f)
            {
                hinted = true;
                HUD.Instance?.Banner("Imposter syndrome!", "It makes platforms vanish. Press Q to flash it away!");
            }
            foreach (var pl in ClimbPlatform.All)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(pl.X - c.x) - pl.width * 0.5f);
                float dy = pl.Top - c.y;
                if (dx * dx + dy * dy < vanishRadius * vanishRadius) pl.Vanish(1f);
            }
            var p = ClimbPlayer.Instance;
            if (p && p.Overlaps(c, 0.6f)) p.Hurt("Imposter syndrome");
        }
    }

    /// <summary>Level 4 photo spot (flash next to it). Same save id scheme as <see cref="PhotoSpot"/>.</summary>
    public class ClimbPhotoSpot : MonoBehaviour
    {
        public string spotId = "L4_photo1";
        public string caption = "Felix-Klein-Hörsaal organ";
        public SpriteRenderer view;
        bool taken;

        void Start()
        {
            taken = SaveSystem.HasPhoto(spotId);
            if (taken && view) view.color = new Color(1, 1, 1, 0.3f);
        }

        public void Photograph()
        {
            if (taken) return;
            taken = true;
            SaveSystem.AddPhoto(spotId);
            HUD.Instance?.Toast($"Photo taken: {caption}");
            GameAudio.Play("toast_good", 0.8f);
            Effects.Burst(transform.position, Color.white, 16);
            if (view) view.color = new Color(1, 1, 1, 0.3f);
        }

        void Update()
        {
            var p = ClimbPlayer.Instance;
            bool isNear = !taken && p && p.Overlaps(transform.position, 3f);
            if (isNear != near) HUD.Instance?.Hint(isNear ? "Photo spot! Q: camera flash" : null);
            near = isNear;
        }
        bool near;
    }
}
