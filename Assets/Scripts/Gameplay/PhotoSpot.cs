using UnityEngine;

namespace GetYourPhD
{
    /// <summary>A hidden photo spot. Use the camera flash next to it to take the photo (100% collectible, shown in the credits).</summary>
    public class PhotoSpot : MonoBehaviour
    {
        public string spotId = "L1_photo1";
        public string caption = "The lab at night";
        bool taken;

        void Start()
        {
            if (SaveSystem.HasPhoto(spotId)) { taken = true; var sr = GetComponent<SpriteRenderer>(); if (sr) sr.color = new Color(1, 1, 1, 0.3f); }
        }

        public void Photograph()
        {
            if (taken) return;
            taken = true;
            SaveSystem.AddPhoto(spotId);
            HUD.Instance?.Toast($"Photo taken: {caption}");
            GameAudio.Play("toast_good", 0.8f);
            Effects.Burst(transform.position, Color.white, 16);
            var sr = GetComponent<SpriteRenderer>();
            if (sr) sr.color = new Color(1, 1, 1, 0.3f);
        }

        void OnTriggerEnter2D(Collider2D c)
        {
            if (!taken && c.GetComponentInParent<PlayerController>()) HUD.Instance?.Hint("Photo spot! Q: camera flash");
        }
        void OnTriggerExit2D(Collider2D c)
        {
            if (c.GetComponentInParent<PlayerController>()) HUD.Instance?.Hint(null);
        }
    }
}
