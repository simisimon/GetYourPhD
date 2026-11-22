using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Counts down a level's real-time budget and shows it as an in-game clock that ends at the deadline (default 13:59 AOE).
    /// The closer the deadline, the lower <see cref="SpeedFactor"/> (down to <see cref="minSpeedFactor"/> in the last minute).
    /// When time runs out, the paper is submitted automatically.
    /// </summary>
    public class DeadlineClock : MonoBehaviour
    {
        public static DeadlineClock Instance { get; private set; }

        [Header("Time budget (real seconds)")]
        public float levelDurationSeconds = 240f;

        [Header("Deadline")]
        [Range(0, 23)] public int deadlineHour = 13;
        [Range(0, 59)] public int deadlineMinute = 59;
        public string timeZoneLabel = "AOE";

        [Header("In-game clock display")]
        [Tooltip("In-game minutes that pass over the whole level (240 = four in-game hours before the deadline).")]
        public float inGameMinutesSpan = 240f;

        [Header("Slowdown")]
        [Tooltip("Remaining seconds at which the slowdown starts.")]
        public float slowdownStartsAt = 120f;
        [Tooltip("Remaining seconds at which the minimum speed is reached (the last minute).")]
        public float slowdownFullAt = 60f;
        [Range(0.3f, 1f)] public float minSpeedFactor = 0.6f;

        public float Remaining { get; private set; }
        public bool Running { get; set; } = true;
        public bool Expired { get; private set; }

        public event System.Action OnExpired;
        public event System.Action<float> OnPenalty;

        void Awake()
        {
            Instance = this;
            Remaining = levelDurationSeconds;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Running || Expired) return;
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                Expired = true;
                OnExpired?.Invoke();
            }
        }

        /// <summary>1 when far from the deadline, sinking to minSpeedFactor in the last minute.</summary>
        public float SpeedFactor
        {
            get
            {
                if (Remaining >= slowdownStartsAt) return 1f;
                if (Remaining <= slowdownFullAt) return minSpeedFactor;
                float t = (Remaining - slowdownFullAt) / Mathf.Max(0.01f, slowdownStartsAt - slowdownFullAt);
                return Mathf.Lerp(minSpeedFactor, 1f, t);
            }
        }

        public void AddPenalty(float seconds)
        {
            if (Expired) return;
            Remaining = Mathf.Max(0.01f, Remaining - seconds);
            OnPenalty?.Invoke(seconds);
        }

        /// <summary>Gives extra real seconds and un-expires the clock (Level 4: the academic quarter "c.t.").</summary>
        public void Extend(float seconds)
        {
            Remaining += seconds;
            Expired = false;
        }

        /// <summary>In-game clock text such as "23:41 AOE".</summary>
        public string ClockText
        {
            get
            {
                float frac = Remaining / Mathf.Max(1f, levelDurationSeconds);
                int minutesBefore = Mathf.CeilToInt(frac * inGameMinutesSpan);
                int total = deadlineHour * 60 + deadlineMinute - minutesBefore;
                if (total < 0) total += 24 * 60;
                return $"{total / 60:00}:{total % 60:00} {timeZoneLabel}";
            }
        }

        /// <summary>The deadline itself, such as "13:59 AOE".</summary>
        public string DeadlineText => $"{deadlineHour:00}:{deadlineMinute:00} {timeZoneLabel}";

        /// <summary>Real time left, as m:ss.</summary>
        public string RemainingText
        {
            get
            {
                int s = Mathf.CeilToInt(Remaining);
                return $"{s / 60}:{s % 60:00}";
            }
        }
    }
}
