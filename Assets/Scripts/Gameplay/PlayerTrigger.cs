using UnityEngine;
using UnityEngine.Events;

namespace GetYourPhD
{
    /// <summary>Fires an event once when the player enters (used to start the boss fight).</summary>
    public class PlayerTrigger : MonoBehaviour
    {
        public UnityEvent onEnter = new UnityEvent();
        bool fired;

        void OnTriggerEnter2D(Collider2D c)
        {
            if (fired || !c.GetComponentInParent<PlayerController>()) return;
            fired = true;
            onEnter?.Invoke();
        }
    }
}
