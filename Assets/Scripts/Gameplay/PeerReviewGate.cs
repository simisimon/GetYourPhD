using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GetYourPhD
{
    /// <summary>
    /// The locked "Peer Review" door between levels. Walking up opens a keypad; the 4-digit acceptance code
    /// comes from StreamingAssets/peer_review_codes.json. Wrong code: "Rejected, please revise", unlimited tries.
    /// Organizer skip: Ctrl+Shift+P.
    /// Input: number keys / numpad, Backspace, Enter. Gamepad: d-pad up/down picks a digit, left/right moves, A confirms, B deletes.
    /// </summary>
    public class PeerReviewGate : MonoBehaviour
    {
        public int gateNumber = 1;
        public Collider2D doorCollider;
        public SpriteRenderer doorSprite;

        [Tooltip("Optional keypad heading (Level 3: the Paulinum door). Empty = Rebuttal Room text.")]
        public string customTitle;
        /// <summary>When set, called instead of LevelManager.GateOpened (Level 3 has no LevelManager).</summary>
        public System.Action OnAccepted;
        /// <summary>Called when the player steps away from the keypad with Esc.</summary>
        public System.Action OnClosed;

        string code;
        string entry = "";
        int padDigit;          // gamepad: digit currently selected
        bool open;
        bool keypadActive;
        bool playerInZone;
        float reopenDelay;

        public bool KeypadActive => keypadActive;
        public string Entry => entry;
        public int PadDigit => padDigit;

        void Start()
        {
            if (code == null) code = GateCodes.Load().For(gateNumber);
        }

        /// <summary>Opens the keypad directly (no door zone), e.g. after arriving at the Paulinum.</summary>
        public void OpenFromCode()
        {
            if (code == null) code = GateCodes.Load().For(gateNumber);
            if (!open && !keypadActive) OpenKeypad();
        }

        void Update()
        {
            reopenDelay -= Time.deltaTime;
            if (open) return;

            if (GameInput.OrganizerSkipPressed && (LevelManager.Instance ? LevelManager.Instance.InHallway : keypadActive))
            {
                HUD.Instance?.Toast("Organizer override");
                Accept();
                return;
            }

            if (!keypadActive)
            {
                if (LevelManager.Instance && !LevelManager.Instance.InHallway) return; // only after submitting
                if (playerInZone && reopenDelay <= 0f) OpenKeypad();
                return;
            }

            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null)
            {
                for (int d = 0; d <= 9; d++)
                {
                    if (kb[d == 0 ? Key.Digit0 : Key.Digit1 + (d - 1)].wasPressedThisFrame || kb[Key.Numpad0 + d].wasPressedThisFrame) Type(d);
                }
                if (kb.backspaceKey.wasPressedThisFrame) Backspace();
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Submit();
                if (kb.escapeKey.wasPressedThisFrame) CloseKeypad();
            }
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) { padDigit = (padDigit + 1) % 10; HUD.Instance?.RefreshKeypad(this); }
                if (gp.dpad.down.wasPressedThisFrame) { padDigit = (padDigit + 9) % 10; HUD.Instance?.RefreshKeypad(this); }
                if (gp.dpad.right.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame)
                {
                    if (entry.Length < 4) Type(padDigit); else Submit();
                }
                if (gp.dpad.left.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame) Backspace();
                if (gp.startButton.wasPressedThisFrame) Submit();
            }
        }

        void Type(int d)
        {
            if (entry.Length >= 4) return;
            entry += d.ToString();
            GameAudio.Play("key_beep", 1f, 0.02f, 0f);
            HUD.Instance?.RefreshKeypad(this);
            if (entry.Length == 4) Submit();
        }

        void Backspace()
        {
            if (entry.Length > 0) entry = entry.Substring(0, entry.Length - 1);
            GameAudio.Play("ui_click", 0.6f);
            HUD.Instance?.RefreshKeypad(this);
        }

        void Submit()
        {
            if (entry.Length < 4) return;
            if (entry == code) Accept();
            else
            {
                HUD.Instance?.KeypadRejected();
                GameAudio.Play("key_wrong");
                entry = "";
                HUD.Instance?.RefreshKeypad(this);
            }
        }

        /// <summary>True while any gate's keypad is showing (the pause menu stays closed then, ESC belongs to the keypad).</summary>
        public static bool KeypadOpen;
        /// <summary>Frame the last keypad closed, so the same ESC press doesn't also open the pause menu.</summary>
        public static int KeypadClosedFrame = -10;

        void OnDisable() { if (keypadActive) { KeypadOpen = false; KeypadClosedFrame = Time.frameCount; } }

        void OpenKeypad()
        {
            keypadActive = true;
            KeypadOpen = true;
            entry = "";
            GameAudio.Play("ui_click");
            GameInput.Locked = true;
            HUD.Instance?.ShowKeypad(this);
        }

        void CloseKeypad()
        {
            keypadActive = false;
            KeypadOpen = false;
            KeypadClosedFrame = Time.frameCount;
            GameInput.Locked = false;
            reopenDelay = 1.5f;
            HUD.Instance?.HideKeypad();
            OnClosed?.Invoke();
        }

        void Accept()
        {
            GameAudio.Play("gate_open");
            GameAudio.Play("jingle_win", 0.8f);
            GameAudio.DuckMusic(5f);
            open = true;
            keypadActive = false;
            KeypadOpen = false;
            KeypadClosedFrame = Time.frameCount;
            StartCoroutine(OpenSequence());
        }

        IEnumerator OpenSequence()
        {
            GameInput.Locked = true;
            HUD.Instance?.KeypadAccepted();
            yield return new WaitForSeconds(1.6f);
            HUD.Instance?.HideKeypad();
            if (doorCollider) doorCollider.enabled = false;
            if (doorSprite)
            {
                Vector3 start = doorSprite.transform.position;
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime;
                    doorSprite.transform.position = start + Vector3.up * 2f * t;
                    doorSprite.color = new Color(1f, 1f, 1f, 1f - t);
                    yield return null;
                }
            }
            GameInput.Locked = false;
            yield return new WaitForSeconds(0.4f);
            if (OnAccepted != null) OnAccepted();
            else LevelManager.Instance?.GateOpened();
        }

        void OnTriggerEnter2D(Collider2D c) { if (c.GetComponentInParent<PlayerController>()) playerInZone = true; }
        void OnTriggerExit2D(Collider2D c) { if (c.GetComponentInParent<PlayerController>()) playerInZone = false; }
    }
}
