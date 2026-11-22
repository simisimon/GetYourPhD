using UnityEngine;

namespace GetYourPhD
{
    /// <summary>The level exit. Walking into it submits the paper.</summary>
    public class SubmitButton : MonoBehaviour
    {
        bool used;
        void OnTriggerEnter2D(Collider2D c)
        {
            if (used || !c.GetComponentInParent<PlayerController>()) return;
            used = true;
            LevelManager.Instance?.Submit(false);
        }
    }
}
