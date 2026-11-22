using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Level 1 boss: the overheating Benchmark Cluster.
    /// Cycle: heat up (glows red) → fires a volley of crashed jobs → overheats and opens its vents (vulnerable) → repeat.
    /// Only Mate thrown into open vents does damage. Defeating it enables the Submit button.
    /// </summary>
    public class BenchmarkClusterBoss : MonoBehaviour
    {
        public string bossName = "The Overheating Benchmark Cluster";
        public int maxHealth = 5;
        public float idleTime = 1.6f;
        public float heatTime = 1.4f;
        public float ventTime = 3.2f;
        public int volleySize = 4;
        public Sprite projectileSprite;
        public GameObject[] enableOnDefeat;
        public GameObject[] disableOnDefeat;
        public GameObject[] enableOnStart;
        [Tooltip("Platform spots where Mate bottles randomly appear during the fight.")]
        public Transform[] mateSpawnPoints;
        [Tooltip("Seconds between spawns (random between x and y).")]
        public Vector2 mateSpawnInterval = new Vector2(3f, 6f);
        public int maxMateOnField = 2;
        public Sprite mateSprite;

        public int Health { get; private set; }
        public bool Active { get; private set; }
        public bool Vulnerable { get; private set; }
        public bool Defeated { get; private set; }

        SpriteRenderer sr;
        Vector3 basePos;
        GameObject spawnedMate;
        float mateTimer;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            Health = maxHealth;
            basePos = transform.position;
        }

        public void Activate()
        {
            if (Active || Defeated) return;
            Active = true;
            foreach (var g in enableOnStart) if (g) g.SetActive(true);
            HUD.Instance?.ShowBoss(this);
            HUD.Instance?.Toast("Boss: " + bossName);
            GameAudio.PlayMusic("boss", 0.8f);
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(1f);
            while (!Defeated)
            {
                float speedUp = Mathf.Lerp(0.65f, 1f, (float)Health / maxHealth);
                yield return Tint(Color.white, idleTime * speedUp);
                // Heat up
                float t = 0f;
                while (t < heatTime * speedUp)
                {
                    t += Time.deltaTime;
                    float k = t / (heatTime * speedUp);
                    sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.25f), k);
                    transform.position = basePos + (Vector3)Random.insideUnitCircle * 0.06f * k;
                    yield return null;
                }
                transform.position = basePos;
                // Volley of crashed jobs
                int n = volleySize + (maxHealth - Health) / 2;
                for (int i = 0; i < n; i++)
                {
                    FireAtPlayer(i, n);
                    yield return new WaitForSeconds(0.28f * speedUp);
                }
                // Overheated: vents open
                Vulnerable = true;
                HUD.Instance?.Toast("Overheated! Throw Mate into the vents!");
                t = 0f;
                while (t < ventTime && !Defeated)
                {
                    t += Time.deltaTime;
                    sr.color = Color.Lerp(new Color(0.65f, 0.85f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
                    if (Random.value < 0.25f) Effects.Burst(transform.position + new Vector3(Random.Range(-1f, 1f), 1.2f, 0f), new Color(0.85f, 0.9f, 1f, 0.8f), 1);
                    yield return null;
                }
                Vulnerable = false;
            }
        }

        IEnumerator Tint(Color c, float dur)
        {
            sr.color = c;
            yield return new WaitForSeconds(dur);
        }

        void FireAtPlayer(int i, int n)
        {
            var player = PlayerController.Instance;
            Vector2 from = (Vector2)transform.position + new Vector2(-1.4f, 2.4f);
            Vector2 target = player ? (Vector2)player.transform.position : from + Vector2.left * 5f;
            float spread = (i - (n - 1) * 0.5f) * 1.3f;
            target.x += spread;
            float flight = 1.1f;
            Vector2 g = Physics2D.gravity * 1.5f;
            Vector2 v = (target - from - 0.5f * g * flight * flight) / flight;
            BossProjectile.Spawn(projectileSprite, from, v, 1.5f);
        }

        void Update()
        {
            if (!Active || Defeated) return;

            mateTimer -= Time.deltaTime;
            // Mate bottles pop up at random on the arena platforms; the player has to go and get them.
            if (mateSpawnPoints == null || mateSpawnPoints.Length == 0) return;
            if (!spawnedMate) spawnedMate = new GameObject("BossFightMate");
            if (mateTimer > 0f) return;
            mateTimer = Random.Range(mateSpawnInterval.x, mateSpawnInterval.y);
            if (spawnedMate.transform.childCount >= maxMateOnField) return;
            for (int tries = 0; tries < 6; tries++)
            {
                var pt = mateSpawnPoints[Random.Range(0, mateSpawnPoints.Length)];
                if (!pt) continue;
                bool taken = false;
                foreach (Transform b in spawnedMate.transform)
                    if (Vector2.Distance(b.position, pt.position) < 0.5f) taken = true;
                if (taken) continue;
                SpawnBottle(pt.position, spawnedMate.transform);
                break;
            }
        }

        void SpawnBottle(Vector3 pos, Transform parent)
        {
            var go = new GameObject("Mate");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = mateSprite;
            s.sortingOrder = 10;
            var c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true; c.radius = 0.4f;
            var col = go.AddComponent<Collectible>();
            col.kind = CollectibleKind.Mate;
            col.paperPoints = 0;
            Effects.Burst(pos, new Color(1f, 0.75f, 0.2f), 8);
        }

        public void Hit()
        {
            if (!Active || Defeated) return;
            if (!Vulnerable)
            {
                HUD.Instance?.Toast("Throttled. Wait for the vents to open!");
                GameAudio.Play("boss_block");
                Effects.Burst(transform.position, Color.gray, 6);
                return;
            }
            Health--;
            GameAudio.Play("boss_hit");
            Effects.Burst(transform.position + Vector3.up, new Color(1f, 0.8f, 0.3f), 18);
            StartCoroutine(HitFlash());
            if (Health <= 0) StartCoroutine(Die());
        }

        IEnumerator HitFlash()
        {
            var old = sr.color;
            sr.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            if (!Defeated) sr.color = old;
        }

        IEnumerator Die()
        {
            Defeated = true;
            Vulnerable = false;
            HUD.Instance?.Toast("Benchmark Cluster: all jobs finished!");
            GameAudio.Play("boss_die");
            GameAudio.PlayMusic("level1", 2f);
            for (int i = 0; i < 10; i++)
            {
                Effects.Burst(transform.position + (Vector3)Random.insideUnitCircle * 1.5f, new Color(1f, Random.value, 0.2f), 14);
                yield return new WaitForSeconds(0.12f);
            }
            foreach (var g in enableOnDefeat) if (g) g.SetActive(true);
            foreach (var g in disableOnDefeat) if (g) g.SetActive(false);
            HUD.Instance?.ShowBoss(null);
            gameObject.SetActive(false);
        }

        void OnCollisionStay2D(Collision2D c)
        {
            var p = c.collider.GetComponentInParent<PlayerController>();
            if (p && !Defeated) p.Hurt(transform.position, "Burned by the cluster");
        }
    }

    public class BossProjectile : MonoBehaviour
    {
        float life = 4f;

        public static void Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float gravityScale)
        {
            var go = new GameObject("CrashedJob");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 15;
            sr.color = new Color(1f, 0.6f, 0.5f);
            go.transform.localScale = Vector3.one * 0.6f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            rb.linearVelocity = vel;
            rb.angularVelocity = 360f;
            var c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.4f;
            go.AddComponent<BossProjectile>();
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var p = other.GetComponentInParent<PlayerController>();
            if (p) { p.Hurt(transform.position, "Hit by a crashed job"); Destroy(gameObject); return; }
            if (!other.isTrigger && !other.GetComponentInParent<BenchmarkClusterBoss>())
            {
                Effects.Burst(transform.position, new Color(1f, 0.5f, 0.4f), 6);
                Destroy(gameObject);
            }
        }
    }
}
