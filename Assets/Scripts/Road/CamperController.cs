using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes' Fiat Ducato camper in Level 3. Left/right switch lanes (smoothly, with a heavy sway),
    /// up accelerates slowly, down brakes. Fuel drains all the time and faster when speeding.
    /// Driven by <see cref="RoadTrip"/> (Tick) so the update order is fixed.
    /// </summary>
    public class CamperController : MonoBehaviour
    {
        public static CamperController Instance { get; private set; }

        public SpriteRenderer body;
        public Sprite[] damageSprites;
        public ParticleSystem smoke;

        [Header("Speed (units per second, 1 unit ≈ 9 km/h on the speedometer)")]
        public float maxSpeed = 15.5f;
        public float cruiseSpeed = 10f;
        public float minSpeed = 3f;
        public float accel = 2.4f;
        public float brake = 9f;
        public float coast = 1.0f;
        public float speedingAt = 12.5f;
        public float kmhPerUnit = 9f;

        [Header("Steering")]
        public float laneSmoothTime = 0.26f;
        public float repeatDelay = 0.3f;

        [Header("Fuel")]
        public float fuelMax = 100f;
        public float fuelBase = 0.7f;
        public float fuelPerSpeed = 0.06f;
        public float speedingMultiplier = 1.8f;

        [Header("Damage")]
        public int maxHp = 3;
        public float invulnerableTime = 1.6f;

        public float Y { get; private set; }
        public float Speed { get; private set; }
        public float Lateral { get; private set; }
        public int Col { get; private set; } = 2;
        public float Fuel { get; set; }
        public int Hp { get; private set; }
        public bool Speeding => Speed > speedingAt + 0.05f;
        public float Kmh => Speed * kmhPerUnit;
        public bool Boosted => boost > 0f;
        public bool Invulnerable => invuln > 0f;

        /// <summary>When set, the camper ignores up/down and eases to this speed (stations, finish).</summary>
        public float? Autopilot;
        public bool SteeringLocked;

        /// <summary>Automated playtests drive the camper through this (x = steer, y = gas/brake).</summary>
        public static System.Func<Vector2> InputOverride;

        float latVel, repeat, prevH, invuln, boost, swayT;
        RoadTrip trip;

        void Awake()
        {
            Instance = this;
            Fuel = fuelMax;
            Hp = maxHp;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Init(RoadTrip t, float y, int col)
        {
            trip = t;
            Place(y, col, 6f);
        }

        public void Place(float y, int col, float speed)
        {
            Y = y;
            Col = col;
            Lateral = trip.LaneX(y, col);
            latVel = 0f;
            Speed = speed;
            Fuel = fuelMax;
            Hp = maxHp;
            invuln = 2.5f;
            boost = 0f;
            Autopilot = null;
            SteeringLocked = false;
            RefreshSprite();
            Apply(0f);
        }

        public void Tick(float dt)
        {
            // --- speed ---
            float top = maxSpeed + (Boosted ? 2.5f : 0f);
            if (Autopilot.HasValue)
            {
                Speed = Mathf.MoveTowards(Speed, Autopilot.Value, (Autopilot.Value < Speed ? brake : accel * 2f) * dt);
            }
            else
            {
                float v = InputOverride != null ? InputOverride().y : GameInput.Vertical;
                if (v > 0f)
                {
                    // heavy: acceleration fades towards top speed
                    float a = accel * (Boosted ? 1.6f : 1f) * Mathf.Lerp(1.3f, 0.35f, Speed / top);
                    Speed += a * dt;
                }
                else if (v < 0f) Speed -= brake * dt;
                else Speed = Mathf.MoveTowards(Speed, cruiseSpeed, coast * dt);
                Speed = Mathf.Clamp(Speed, minSpeed, top);
            }

            // --- steering (lane based, with key repeat) ---
            float h = SteeringLocked ? 0f : (InputOverride != null ? InputOverride().x : GameInput.Horizontal);
            repeat -= dt;
            if (Mathf.Abs(h) > 0.5f)
            {
                if (Mathf.Abs(prevH) < 0.5f || repeat <= 0f)
                {
                    ChangeLane(h > 0 ? 1 : -1);
                    repeat = Mathf.Abs(prevH) < 0.5f ? repeatDelay : repeatDelay * 0.7f;
                }
            }
            prevH = h;

            Y += Speed * dt;
            float target = trip.LaneX(Y, Col);
            Lateral = Mathf.SmoothDamp(Lateral, target, ref latVel, laneSmoothTime, 10f, dt);

            // --- fuel ---
            float drain = (fuelBase + fuelPerSpeed * Speed) * (Speeding ? speedingMultiplier : 1f);
            if (Speed < 0.5f) drain = 0.1f;
            Fuel = Mathf.Max(0f, Fuel - drain * dt);

            invuln -= dt;
            boost -= dt;
            Apply(dt);
        }

        /// <summary>Returns false if there is no lane on that side.</summary>
        public bool ChangeLane(int dir)
        {
            int to = Col + dir;
            if (!trip.LaneAllowed(Y, to)) { latVel += dir * 2.5f; return false; } // bump the curb
            Col = to;
            return true;
        }

        public void ForceLane(int col) { Col = col; }

        void Apply(float dt)
        {
            swayT += dt * (0.8f + Speed * 0.12f);
            float x = trip.RoadOffset(Y) + Lateral;
            transform.position = new Vector3(x, Y, 0);
            // lean into lane changes and road bends; slight body sway (the Ducato is tall and heavy)
            float slope = (trip.RoadOffset(Y + 1f) - trip.RoadOffset(Y - 1f)) * 0.5f;
            float lean = Mathf.Clamp(-latVel * 3f, -12f, 12f);
            float angle = -Mathf.Atan2(slope, 1f) * Mathf.Rad2Deg + lean + Mathf.Sin(swayT * 2.2f) * Mathf.Clamp01(Speed / 10f) * 1.4f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            if (body)
            {
                bool blink = invuln > 0f && Hp < maxHp && Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
                body.color = blink ? new Color(1f, 0.55f, 0.5f) : (Boosted ? new Color(1f, 1f, 0.8f) : Color.white);
            }
        }

        /// <summary>Takes one hit. Returns false while still blinking from the last one.</summary>
        public bool Hit()
        {
            if (invuln > 0f) return false;
            Hp = Mathf.Max(0, Hp - 1);
            invuln = invulnerableTime;
            Speed *= 0.45f;
            latVel += Random.Range(-3f, 3f);
            RefreshSprite();
            return true;
        }

        public void Repair() { Hp = maxHp; RefreshSprite(); }

        public void AddFuel(float f) { Fuel = Mathf.Min(fuelMax, Fuel + f); }

        public void Boost(float seconds) { boost = Mathf.Max(boost, seconds); }

        public void SetSpeed(float s) { Speed = s; }

        void RefreshSprite()
        {
            if (body && damageSprites != null && damageSprites.Length > 0)
            {
                int i = Mathf.Clamp(maxHp - Hp, 0, damageSprites.Length - 1);
                body.sprite = damageSprites[i];
            }
            if (smoke)
            {
                var em = smoke.emission;
                em.enabled = Hp <= 1;
            }
        }
    }
}
