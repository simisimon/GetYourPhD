using UnityEngine;

namespace GetYourPhD
{
    /// <summary>A thrown Mate bottle. Takes out a distraction or damages a boss.</summary>
    public class MateProjectile : MonoBehaviour
    {
        public float life = 2.5f;
        Rigidbody2D rb;

        public static MateProjectile Spawn(Sprite sprite, Vector2 pos, Vector2 vel)
        {
            var go = new GameObject("MateProjectile");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 20;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.2f;
            rb.linearVelocity = vel;
            rb.angularVelocity = -Mathf.Sign(vel.x) * 720f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.25f;
            col.isTrigger = true;
            var p = go.AddComponent<MateProjectile>();
            p.rb = rb;
            return p;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>()) return;
            var enemy = other.GetComponentInParent<Enemy>();
            if (enemy) { enemy.Defeat(); Pop(); return; }
            var boss = other.GetComponentInParent<BenchmarkClusterBoss>();
            if (boss) { boss.Hit(); Pop(); return; }
            var sw = other.GetComponentInParent<ConfigSwitch>();
            if (sw) { sw.Toggle(); Pop(); return; }
            var task = other.GetComponentInParent<HomeTask>();
            if (task && task.Active && task.fixableByMate) { task.Resolve(task.mateFixText); Pop(); return; }
            if (!other.isTrigger) Pop();
        }

        void Pop()
        {
            GameAudio.Play("mate_shatter", 0.8f);
            Effects.Burst(transform.position, new Color(1f, 0.75f, 0.2f), 10);
            Destroy(gameObject);
        }
    }
}
