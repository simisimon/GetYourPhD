using UnityEngine;

namespace GetYourPhD
{
    /// <summary>An info sign in the level. Walking past it shows its tip in the box at the bottom of the screen.</summary>
    public class TipSign : MonoBehaviour
    {
        [TextArea] public string tip;
        SpriteRenderer sr;
        Vector3 baseScale;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        void Update()
        {
            // Gentle bob so it reads as interactive.
            float s = 1f + 0.04f * Mathf.Sin(Time.time * 4f);
            transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
        }

        void OnTriggerEnter2D(Collider2D c)
        {
            if (c.GetComponentInParent<PlayerController>()) HUD.Instance?.ShowTip(tip, this);
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (c.GetComponentInParent<PlayerController>()) HUD.Instance?.HideTip(this);
        }
    }
}
