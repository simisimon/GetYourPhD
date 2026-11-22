using UnityEngine;
using UnityEngine.Tilemaps;

namespace GetYourPhD
{
    /// <summary>
    /// A platform controlled by a ConfigSwitch option.
    /// Move: rides between its off and on positions (optionally ping-pongs while the option is on, like an elevator).
    /// ShowWhenOn / HideWhenOn: appears or disappears with the option.
    /// </summary>
    public class ConfigPlatform : MonoBehaviour
    {
        public enum Mode { Move, ShowWhenOn, HideWhenOn }
        public string optionName = "--cache";
        public Mode mode = Mode.Move;
        public Vector2 offWorldPos;
        public Vector2 onWorldPos;
        public float moveSpeed = 3f;
        [Tooltip("Move mode: while on, keep riding back and forth between both positions.")]
        public bool pingPongWhenOn = true;
        public float pauseAtEnds = 0.6f;

        Rigidbody2D rb;
        bool on;
        bool goingToOn;
        float pause;
        public Vector2 Velocity { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void Start() { ApplyVisibility(); }

        public void SetOn(bool v)
        {
            on = v;
            goingToOn = v;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            if (mode == Mode.Move) return;
            bool visible = mode == Mode.ShowWhenOn ? on : !on;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = visible;
            Color col = visible ? Color.white : new Color(0.6f, 0.8f, 1f, 0.15f);
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.color = col;
            foreach (var t in GetComponentsInChildren<Tilemap>(true)) t.color = col;
        }

        void FixedUpdate()
        {
            if (mode != Mode.Move || rb == null) { Velocity = Vector2.zero; return; }
            if (pause > 0f) { pause -= Time.fixedDeltaTime; Velocity = Vector2.zero; return; }
            Vector2 target = goingToOn ? onWorldPos : offWorldPos;
            Vector2 next = Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime);
            Velocity = (next - rb.position) / Time.fixedDeltaTime;
            rb.MovePosition(next);
            if ((next - target).sqrMagnitude < 0.0001f && on && pingPongWhenOn)
            {
                goingToOn = !goingToOn;
                pause = pauseAtEnds;
            }
        }
    }
}
