using UnityEngine;

namespace GetYourPhD
{
    public enum CollectibleKind { DataPoint, ExperimentResult, Feedback, PaperDraft, Mate }

    /// <summary>Data points, results, feedback and drafts fill the Paper meter; Mate bottles go to the inventory.</summary>
    public class Collectible : MonoBehaviour
    {
        public CollectibleKind kind = CollectibleKind.DataPoint;
        public int paperPoints = 1;
        public float bobAmplitude = 0.08f;
        public float bobSpeed = 3f;

        Vector3 basePos;
        float phase;

        public static int DefaultPoints(CollectibleKind k)
        {
            switch (k)
            {
                case CollectibleKind.DataPoint: return 1;
                case CollectibleKind.ExperimentResult: return 3;
                case CollectibleKind.Feedback: return 2;
                case CollectibleKind.PaperDraft: return 5;
                default: return 0;
            }
        }

        public static string Label(CollectibleKind k)
        {
            switch (k)
            {
                case CollectibleKind.DataPoint: return "Data point";
                case CollectibleKind.ExperimentResult: return "Experiment result";
                case CollectibleKind.Feedback: return "Feedback";
                case CollectibleKind.PaperDraft: return "Paper draft";
                default: return "Mate";
            }
        }

        void Start()
        {
            basePos = transform.position;
            phase = Random.value * 6f;
        }

        void Update()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (!player) return;
            if (kind == CollectibleKind.Mate)
            {
                if (player.mateCount >= player.mateMax) return;
                player.AddMate(1);
                GameAudio.Play("mate_pickup");
                Effects.Burst(transform.position, new Color(1f, 0.75f, 0.2f), 8);
            }
            else
            {
                PaperMeter.Instance?.Add(paperPoints);
                GameAudio.Play(paperPoints >= 3 ? "pickup_big" : "pickup", paperPoints >= 3 ? 1f : 0.7f, 0.12f);
                if (paperPoints >= 3) HUD.Instance?.Toast($"{Label(kind)}: +{PaperMeter.Pieces(paperPoints)}");
                Effects.Burst(transform.position, new Color(0.5f, 0.9f, 1f), 8);
            }
            Destroy(gameObject);
        }
    }
}
