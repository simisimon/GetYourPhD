using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GetYourPhD
{
    /// <summary>
    /// Builds the whole UI at runtime: deadline clock, Paper meter, Mate counter, toasts, hints, boss bar,
    /// grade screen and the Peer Review keypad.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        public Font font;
        public Sprite mateIcon;
        public Sprite paperIcon;

        Canvas canvas;
        Text clockText, clockSub, mateText, flashText, toastText, hintText, bannerTitle, bannerSub;
        Image paperFill, paperBack, flashOverlay, bossFill;
        Text paperGrade;
        GameObject bossBar; Text bossName;
        GameObject gradePanel; Text gradeTitle, gradeBig, gradeInfo;
        GameObject keypadPanel; Text keypadTitle, keypadDigits, keypadStatus, keypadHelp; Image keypadBg, stamp; Text stampText;
        GameObject clockGroup, matePanel;
        /// <summary>Optional: replaces the line under the deadline clock (Level 3 shows the speed in km/h).</summary>
        public System.Func<string> clockSubProvider;
        /// <summary>Optional: replaces the deadline clock text (Level 4 shows the academic quarter after 14:00).</summary>
        public System.Func<string> clockTextProvider;
        Image tipPanel; Text tipText; TipSign tipOwner;
        BenchmarkClusterBoss boss;
        float toastTimer, bannerTimer;
        bool endCard;

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.82f);

        void Awake()
        {
            Instance = this;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var p = PlayerController.Instance;
            if (p) p.OnFlash += () => StartCoroutine(FlashRoutine());
        }

        // ---------- building helpers ----------
        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            // The pixel font reads smaller than a regular font, so small labels get a little extra size.
            t.fontSize = fontSize <= 16 ? fontSize + 2 : fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        void Build()
        {
            var cgo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = cgo.transform;

            flashOverlay = Panel("Flash", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(1, 1, 1, 0));
            flashOverlay.raycastTarget = false;

            // Deadline clock (top-left)
            var clockBg = Panel("ClockPanel", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(270, 84), Ink);
            clockGroup = clockBg.gameObject;
            Label("ClockCaption", clockBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(240, 20), 14, TextAnchor.UpperLeft, new Color(0.7f, 0.8f, 1f)).text = "DEADLINE";
            clockText = Label("Clock", clockBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -22), new Vector2(240, 36), 32, TextAnchor.UpperLeft, Color.white);
            clockSub = Label("ClockSub", clockBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -58), new Vector2(240, 20), 14, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));

            // Paper meter (top-center)
            var paperBg = Panel("PaperPanel", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(420, 64), Ink);
            Label("PaperCaption", paperBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(200, 20), 14, TextAnchor.UpperLeft, new Color(0.7f, 0.8f, 1f)).text = "PAPER";
            paperGrade = Label("PaperGrade", paperBg.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -6), new Vector2(240, 20), 14, TextAnchor.UpperRight, Color.white);
            paperBack = Panel("PaperBack", paperBg.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 12), new Vector2(392, 20), new Color(0.15f, 0.18f, 0.25f));
            paperFill = Panel("PaperFill", paperBack.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 20), new Color(0.5f, 0.9f, 1f));

            // Mate + flash (top-right)
            var mateBg = Panel("MatePanel", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(200, 84), Ink);
            matePanel = mateBg.gameObject;
            var icon = Panel("MateIcon", mateBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(40, 40), Color.white);
            if (mateIcon) { icon.sprite = mateIcon; icon.preserveAspect = true; } else icon.color = new Color(1f, 0.7f, 0.2f);
            mateText = Label("MateCount", mateBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -12), new Vector2(130, 36), 30, TextAnchor.UpperLeft, Color.white);
            flashText = Label("Flash", mateBg.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -58), new Vector2(180, 20), 14, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));

            toastText = Label("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(900, 30), 22, TextAnchor.MiddleCenter, Color.white);

            // Tip box (top of the screen, under the Paper meter), filled by TipSign objects in the level
            tipPanel = Panel("TipPanel", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(820, 70), Ink);
            Label("TipCaption", tipPanel.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(100, 18), 13, TextAnchor.UpperLeft, new Color(0.6f, 0.8f, 1f)).text = "TIP";
            tipText = Label("TipText", tipPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(780, 52), 18, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.8f));
            tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tipPanel.gameObject.SetActive(false);
            hintText = Label("Hint", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(900, 30), 20, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.7f));
            bannerTitle = Label("BannerTitle", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1200, 60), 48, TextAnchor.MiddleCenter, Color.white);
            bannerSub = Label("BannerSub", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1200, 40), 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));

            // Boss bar
            var bb = Panel("BossBar", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(560, 46), Ink);
            bossBar = bb.gameObject;
            bossName = Label("BossName", bb.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(540, 18), 15, TextAnchor.UpperCenter, new Color(1f, 0.7f, 0.6f));
            var bback = Panel("BossBack", bb.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 8), new Vector2(540, 14), new Color(0.2f, 0.1f, 0.1f));
            bossFill = Panel("BossFill", bback.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(540, 14), new Color(1f, 0.35f, 0.25f));
            bossBar.SetActive(false);

            BuildGradePanel(root);
            BuildKeypad(root);
        }

        void BuildGradePanel(Transform root)
        {
            var bg = Panel("GradePanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.02f, 0.03f, 0.08f, 0.82f));
            gradePanel = bg.gameObject;
            var box = Panel("Box", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 400), new Color(0.95f, 0.94f, 0.9f));
            gradeTitle = Label("Title", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(680, 30), 22, TextAnchor.UpperCenter, new Color(0.2f, 0.2f, 0.25f));
            gradeBig = Label("Grade", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(680, 80), 64, TextAnchor.MiddleCenter, Color.red);
            gradeInfo = Label("Info", box.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(680, 120), 20, TextAnchor.LowerCenter, new Color(0.2f, 0.2f, 0.25f));
            foreach (var s in box.GetComponentsInChildren<Shadow>()) s.effectColor = new Color(0, 0, 0, 0.15f);
            gradePanel.SetActive(false);
        }

        void BuildKeypad(Transform root)
        {
            var bg = Panel("KeypadPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.02f, 0.03f, 0.08f, 0.7f));
            keypadPanel = bg.gameObject;
            keypadBg = Panel("Box", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 380), new Color(0.12f, 0.14f, 0.2f));
            keypadTitle = Label("Title", keypadBg.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(600, 60), 22, TextAnchor.UpperCenter, Color.white);
            keypadDigits = Label("Digits", keypadBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(600, 90), 72, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.7f));
            keypadStatus = Label("Status", keypadBg.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(600, 30), 24, TextAnchor.MiddleCenter, Color.white);
            keypadHelp = Label("Help", keypadBg.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(600, 40), 14, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));
            keypadHelp.text = "Keys: number keys / numpad, Backspace, Enter, Esc: step away\nGamepad: D-pad up/down pick, right/A add, left/B delete";
            stamp = Panel("Stamp", keypadBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(420, 110), new Color(0, 0, 0, 0));
            stampText = Label("StampText", stamp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 110), 72, TextAnchor.MiddleCenter, new Color(0.2f, 0.85f, 0.3f));
            stamp.gameObject.SetActive(false);
            keypadPanel.SetActive(false);
        }

        // ---------- per-frame ----------
        void Update()
        {
            var clock = DeadlineClock.Instance;
            if (clock)
            {
                clockText.text = clockTextProvider != null ? clockTextProvider() : clock.ClockText;
                float sf = PlayerController.Instance ? PlayerController.Instance.EffectiveSpeedFactor : clock.SpeedFactor;
                clockSub.text = clockSubProvider != null ? clockSubProvider() : $"left {clock.RemainingText}   speed {Mathf.RoundToInt(sf * 100)}%";
                bool urgent = clock.Remaining < clock.slowdownFullAt && clock.Running;
                clockText.color = urgent ? (Mathf.Repeat(Time.time * 2f, 1f) > 0.5f ? new Color(1f, 0.35f, 0.3f) : Color.white)
                                         : (clock.Remaining < clock.slowdownStartsAt ? new Color(1f, 0.8f, 0.4f) : Color.white);
            }
            var meter = PaperMeter.Instance;
            if (meter)
            {
                paperFill.rectTransform.sizeDelta = new Vector2(392f * meter.Fill, 20f);
                var c = PaperMeter.FillColor(meter.Fill);
                paperFill.color = c;
                paperGrade.text = Mathf.RoundToInt(meter.Fill * 100) + "% complete";
                paperGrade.color = c;
            }
            var p = PlayerController.Instance;
            if (p)
            {
                mateText.text = "x " + p.mateCount + (p.Boosted ? "  BOOST" : "");
                flashText.text = p.FlashCooldownRemaining > 0f ? $"camera recharging {p.FlashCooldownRemaining:0.0}s" : "camera ready (Q)";
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                var c = toastText.color; c.a = Mathf.Clamp01(toastTimer / 0.4f); toastText.color = c;
            }
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                float a = Mathf.Clamp01(bannerTimer / 0.6f);
                bannerTitle.color = new Color(1, 1, 1, a);
                bannerSub.color = new Color(1f, 0.9f, 0.6f, a);
            }
            if (boss && bossBar.activeSelf)
                bossFill.rectTransform.sizeDelta = new Vector2(540f * boss.Health / boss.maxHealth, 14f);

            if (endCard && GameInput.RetryPressed) LevelManager.Instance?.Retry();
        }

        // ---------- API ----------
        public void Toast(string msg)
        {
            toastText.text = msg;
            toastText.color = Color.white;
            toastTimer = 2.2f;
        }

        /// <summary>Levels without Johannes on foot (Level 3) hide the Mate/camera panel and draw their own.</summary>
        public void HideMatePanel() { if (matePanel) matePanel.SetActive(false); }

        public Canvas Canvas => canvas;

        public void Hint(string msg) { hintText.text = msg ?? ""; }

        public void ShowTip(string tip, TipSign owner)
        {
            tipOwner = owner;
            tipText.text = tip;
            tipPanel.gameObject.SetActive(!string.IsNullOrEmpty(tip));
        }

        public void HideTip(TipSign owner)
        {
            if (owner != tipOwner) return;
            tipOwner = null;
            tipPanel.gameObject.SetActive(false);
        }

        public void Banner(string title, string sub)
        {
            bannerTitle.text = title;
            bannerSub.text = sub;
            bannerTimer = 3.5f;
        }

        public void ShowBoss(BenchmarkClusterBoss b)
        {
            boss = b;
            bossBar.SetActive(b != null);
            if (b) bossName.text = b.bossName.ToUpperInvariant();
        }

        IEnumerator FlashRoutine()
        {
            float t = 0.35f;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                flashOverlay.color = new Color(1, 1, 1, Mathf.Clamp01(t / 0.35f) * 0.85f);
                yield return null;
            }
            flashOverlay.color = new Color(1, 1, 1, 0);
        }

        /// <summary>After submitting, the reviewers always ask for a rebuttal (the real-life challenge in the Rebuttal Room).</summary>
        public void ShowRebuttal(bool forced, float fill)
        {
            gradePanel.SetActive(true);
            gradeTitle.text = forced ? $"{(DeadlineClock.Instance ? DeadlineClock.Instance.DeadlineText : "Deadline")}: submitted as is. The reviewers say..." : "Paper submitted! The reviewers say...";
            gradeBig.text = "REBUTTAL REQUIRED";
            gradeBig.color = new Color(0.75f, 0.45f, 0.05f);
            string time = DeadlineClock.Instance && !forced ? $"Submitted at {DeadlineClock.Instance.ClockText}, {DeadlineClock.Instance.RemainingText} to spare.   " : "";
            gradeInfo.text = $"{time}Paper {Mathf.RoundToInt(fill * 100)}% complete.\nWin the rebuttal to get the acceptance code.\n\nEnter / A: go to the rebuttal      R / X: replay the level";
        }

        public void HideGrade() { gradePanel.SetActive(false); }

        public void SetReviewMode()
        {
            clockGroup.SetActive(false);
            Hint("Rebuttal pending. Walk to the Rebuttal Room.");
        }

        public void ShowEndCard(string title, string body)
        {
            endCard = true;
            gradePanel.SetActive(true);
            gradeTitle.text = "";
            gradeBig.text = title;
            gradeBig.color = new Color(0.15f, 0.6f, 0.25f);
            gradeInfo.text = body;
        }

        // Keypad
        public void ShowKeypad(PeerReviewGate gate)
        {
            keypadPanel.SetActive(true);
            stamp.gameObject.SetActive(false);
            keypadTitle.text = !string.IsNullOrEmpty(gate.customTitle) ? gate.customTitle
                : $"REBUTTAL ROOM  (Gate {gate.gateNumber})\nWin the rebuttal challenge, then enter the acceptance code.";
            keypadStatus.text = "";
            keypadBg.color = new Color(0.12f, 0.14f, 0.2f);
            Hint(null);
            tipPanel.gameObject.SetActive(false);
            RefreshKeypad(gate);
        }

        public void RefreshKeypad(PeerReviewGate gate)
        {
            string s = "";
            for (int i = 0; i < 4; i++)
            {
                if (i < gate.Entry.Length) s += gate.Entry[i];
                else if (i == gate.Entry.Length && UnityEngine.InputSystem.Gamepad.current != null) s += $"<color=#7f8fb0>{gate.PadDigit}</color>";
                else s += "_";
                if (i < 3) s += " ";
            }
            keypadDigits.supportRichText = true;
            keypadDigits.text = s;
        }

        public void KeypadRejected() { StartCoroutine(RejectRoutine()); }

        IEnumerator RejectRoutine()
        {
            keypadStatus.text = "Rejected, please revise.";
            keypadStatus.color = new Color(1f, 0.45f, 0.45f);
            for (int i = 0; i < 4; i++)
            {
                keypadBg.color = new Color(0.6f, 0.1f, 0.1f);
                yield return new WaitForSeconds(0.09f);
                keypadBg.color = new Color(0.12f, 0.14f, 0.2f);
                yield return new WaitForSeconds(0.09f);
            }
        }

        public void KeypadAccepted()
        {
            keypadStatus.text = "";
            keypadDigits.text = "";
            stamp.gameObject.SetActive(true);
            stampText.text = "ACCEPTED!";
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
            keypadBg.color = new Color(0.1f, 0.3f, 0.15f);
        }

        public void HideKeypad() { keypadPanel.SetActive(false); }
    }
}
