using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Level 1 signature mechanic: a config option. Toggling it (interact key, or hitting it with a thrown Mate)
    /// moves or shows/hides every ConfigPlatform that listens to the same option name.
    /// </summary>
    public class ConfigSwitch : MonoBehaviour
    {
        public string optionName = "--cache";
        public bool isOn;
        public SpriteRenderer indicator;
        public TextMesh label;

        bool playerNear;
        float cooldown;

        void Start() { Refresh(); }

        void Update()
        {
            cooldown -= Time.deltaTime;
            if (playerNear && GameInput.InteractPressed) Toggle();
        }

        public void Toggle()
        {
            if (cooldown > 0f) return;
            cooldown = 0.25f;
            isOn = !isOn;
            foreach (var p in FindObjectsByType<ConfigPlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.optionName == optionName) p.SetOn(isOn);
            Refresh();
            HUD.Instance?.Toast($"{optionName} = {(isOn ? "true" : "false")}");
            Effects.Burst(transform.position, isOn ? Color.green : Color.red, 8);
            GameAudio.Play("switch");
        }

        void Refresh()
        {
            if (indicator) indicator.color = isOn ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            if (label) label.text = $"{optionName}={(isOn ? "on" : "off")}";
            var sr = GetComponent<SpriteRenderer>();
            if (sr) sr.flipX = isOn;
        }

        void OnTriggerEnter2D(Collider2D c) { if (c.GetComponentInParent<PlayerController>()) { playerNear = true; HUD.Instance?.Hint("E / S / Down: toggle option"); } }
        void OnTriggerExit2D(Collider2D c) { if (c.GetComponentInParent<PlayerController>()) { playerNear = false; HUD.Instance?.Hint(null); } }
    }
}
