using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// A distraction. Walkers patrol a platform and turn at walls and edges; flyers drift on a sine path.
    /// Touching one costs time. A thrown Mate (or a stomp) takes it out; the camera flash stuns it.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public enum Motion { Walker, Flyer }
        public Motion motion = Motion.Walker;
        public string displayName = "Crashing benchmark job";
        public float speed = 2f;
        public float flyRange = 3f;
        public float flyAmplitude = 0.6f;
        public bool stompable = true;

        SpriteRenderer sr;
        Rigidbody2D rb;
        float dir = -1f;
        float stunTimer;
        Vector3 origin;
        float t;

        void Start()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            origin = transform.position;
            t = Random.value * 10f;
        }

        public void Stun(float seconds)
        {
            stunTimer = Mathf.Max(stunTimer, seconds);
        }

        void FixedUpdate()
        {
            if (stunTimer > 0f)
            {
                stunTimer -= Time.fixedDeltaTime;
                if (rb && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (sr) sr.color = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 20f));
                return;
            }
            if (sr) sr.color = Color.white;

            if (motion == Motion.Walker && rb)
            {
                Vector2 pos = rb.position;
                // Turn at walls and edges.
                Vector2 ahead = pos + new Vector2(dir * 0.55f, 0f);
                bool wall = Physics2D.Raycast(pos, new Vector2(dir, 0f), 0.6f, ~0, -1f, 1f).collider is Collider2D c && !c.isTrigger && !c.GetComponentInParent<PlayerController>() && c.attachedRigidbody != rb;
                var groundHit = Physics2D.Raycast(ahead, Vector2.down, 1.0f);
                bool edge = groundHit.collider == null || groundHit.collider.isTrigger;
                if (wall || edge) dir = -dir;
                rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
            }
            else if (motion == Motion.Flyer)
            {
                t += Time.fixedDeltaTime;
                float x = origin.x + Mathf.Sin(t * speed / Mathf.Max(0.5f, flyRange)) * flyRange;
                float y = origin.y + Mathf.Sin(t * 3f) * flyAmplitude;
                float prevX = transform.position.x;
                transform.position = new Vector3(x, y, 0f);
                dir = Mathf.Sign(x - prevX);
            }
            if (sr) sr.flipX = dir > 0f; // art faces left
        }

        void OnCollisionEnter2D(Collision2D c) => Touch(c.collider);
        void OnTriggerEnter2D(Collider2D c) => Touch(c);
        void OnCollisionStay2D(Collision2D c) => Touch(c.collider);

        void Touch(Collider2D c)
        {
            var player = c.GetComponentInParent<PlayerController>();
            if (!player) return;
            if (stunTimer > 0f) return;
            var prb = player.GetComponent<Rigidbody2D>();
            bool fromAbove = player.transform.position.y > transform.position.y + 0.35f && prb.linearVelocity.y <= 0.5f;
            if (stompable && fromAbove)
            {
                player.Bounce();
                Defeat();
                return;
            }
            player.Hurt(transform.position, displayName);
        }

        public void Defeat()
        {
            GameAudio.Play("stomp");
            Effects.Burst(transform.position, new Color(1f, 0.4f, 0.4f), 14);
            Destroy(gameObject);
        }
    }
}
