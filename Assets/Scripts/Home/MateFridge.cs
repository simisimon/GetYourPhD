using UnityEngine;

namespace GetYourPhD
{
    /// <summary>The kitchen fridge: press E for fresh Mate bottles, then it needs a while to "restock".</summary>
    public class MateFridge : MonoBehaviour
    {
        public int bottles = 2;
        public float cooldown = 18f;
        public TextMesh label;

        bool playerInZone;
        float timer;

        void Update()
        {
            timer -= Time.deltaTime;
            if (label) label.text = timer > 0f ? $"MATE in {Mathf.CeilToInt(timer)}s" : "MATE (E)";
            if (label) label.color = timer > 0f ? new Color(0.7f, 0.7f, 0.75f) : new Color(1f, 0.8f, 0.3f);
            if (!playerInZone || !GameInput.InteractPressed) return;
            var p = PlayerController.Instance;
            if (!p) return;
            if (timer > 0f) { HUD.Instance?.Toast($"Fridge is empty. Restocked in {Mathf.CeilToInt(timer)}s"); GameAudio.Play("mate_empty"); return; }
            if (p.mateCount >= p.mateMax) { HUD.Instance?.Toast("Your hands are full of Mate"); return; }
            p.AddMate(bottles);
            timer = cooldown;
            GameAudio.Play("fridge");
            GameAudio.Play("mate_pickup");
            HUD.Instance?.Toast($"+{bottles} Mate from the fridge");
            Effects.Burst(transform.position + Vector3.up, new Color(1f, 0.75f, 0.2f), 10);
        }

        void OnTriggerEnter2D(Collider2D c)
        {
            if (!c.GetComponentInParent<PlayerController>()) return;
            playerInZone = true;
            HUD.Instance?.Hint("Fridge: press E for Mate");
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (!c.GetComponentInParent<PlayerController>()) return;
            playerInZone = false;
            HUD.Instance?.Hint(null);
        }
    }
}
