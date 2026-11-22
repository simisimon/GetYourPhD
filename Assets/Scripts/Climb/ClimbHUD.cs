using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GetYourPhD
{
    /// <summary>
    /// Level 4 extras on top of the shared <see cref="HUD"/> (clock, Paper meter, toasts, banners):
    /// camera / Mate panel, altitude bar with the four floors, boss bar with the paper shield,
    /// the intro card, the "Dr. Dorn" screen and the credits with the collected photos and postcards.
    /// </summary>
    public class ClimbHUD : MonoBehaviour
    {
        public static ClimbHUD Instance { get; private set; }

        public Font font;
        public Sprite paulinum, mateIcon, paperIcon, phdCap, johannesFront, bossPortrait;
        [Tooltip("The SWS Group on the Dr. Dorn screen, left to right (front-facing sprites).")]
        public Sprite[] groupSprites;
        public string[] groupNames;
        [Tooltip("Pixel-art photo of the SWS lab (from Sebastian's video) behind the group on the Dr. Dorn screen.")]
        public Sprite finaleBackground;

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.82f);
        static readonly Color Caption = new Color(0.6f, 0.8f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        static readonly Color Body = new Color(0.92f, 0.94f, 1f);

        Transform root;
        Text camText, mateText, floorText;
        Image mateBar, altMarker, flashOverlay;
        GameObject bossPanel; Image bossFill; Text bossName, shieldText; readonly List<Image> shieldIcons = new List<Image>();
        GameObject overlay;
        SiegmundBoss boss;

        const float AltBarH = 300f;

        void Awake()
        {
            Instance = this;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var p = ClimbPlayer.Instance;
            if (p) p.OnFlash += () => StartCoroutine(FlashRoutine());
        }

        // ---------- helpers ----------
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
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

        Text Label(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color, string text = "")
        {
            var rt = Rect("Text", parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static Image Panel(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Sprite s = null)
        {
            var rt = Rect("Panel", parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (s) { img.sprite = s; img.preserveAspect = true; }
            img.raycastTarget = false;
            return img;
        }

        void Build()
        {
            var cgo = new GameObject("ClimbCanvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            root = cgo.transform;

            flashOverlay = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(1, 1, 1, 0));

            // camera + Mate (top right, where the Mate panel sits in the other levels)
            var right = Panel(root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(220, 84), Ink);
            var icon = Panel(right.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(36, 36), Color.white, mateIcon);
            if (!mateIcon) icon.color = new Color(1f, 0.7f, 0.2f);
            mateText = Label(right.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -10), new Vector2(160, 20), 16, TextAnchor.UpperLeft, Body, "Mate: grab it to fly");
            var back = Panel(right.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -34), new Vector2(150, 10), new Color(0.15f, 0.18f, 0.25f));
            mateBar = Panel(back.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 10), new Color(1f, 0.75f, 0.25f));
            camText = Label(right.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -58), new Vector2(200, 20), 16, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));

            // altitude bar (left)
            var alt = Panel(root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -40), new Vector2(190, AltBarH + 40), Ink);
            Label(alt.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -4), new Vector2(140, 18), 14, TextAnchor.UpperLeft, Caption, "PAULINUM");
            var track = Panel(alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 12), new Vector2(10, AltBarH), new Color(0.2f, 0.24f, 0.33f));
            for (int i = 0; i < 4; i++)
            {
                float y = 12 + AltBarH * i / 4f;
                Panel(alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, y), new Vector2(18, 2), Caption);
                Label(alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, y - 2), new Vector2(150, 18), 13, TextAnchor.LowerLeft, Body, ClimbGame.FloorNames[i]);
            }
            Panel(alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 12 + AltBarH - 2), new Vector2(22, 4), Gold);
            Label(alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 12 + AltBarH - 4), new Vector2(110, 18), 13, TextAnchor.LowerLeft, Gold, "DEFENSE");
            altMarker = Panel(track.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18), Color.white, johannesFront);
            floorText = Label(root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 12), new Vector2(400, 20), 14, TextAnchor.LowerLeft, new Color(0.8f, 0.85f, 0.95f), "Ctrl+Shift+R: restart level");

            // boss bar + shield
            var bb = Panel(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(600, 74), Ink);
            bossPanel = bb.gameObject;
            bossName = Label(bb.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(580, 20), 16, TextAnchor.UpperCenter, new Color(1f, 0.7f, 0.6f));
            var bback = Panel(bb.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -26), new Vector2(580, 14), new Color(0.2f, 0.1f, 0.1f));
            bossFill = Panel(bback.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(580, 14), new Color(1f, 0.35f, 0.25f));
            shieldText = Label(bb.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 6), new Vector2(160, 22), 15, TextAnchor.LowerLeft, Caption, "PAPER SHIELD");
            for (int i = 0; i < 8; i++)
            {
                var si = Panel(bb.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(160 + i * 30, 5), new Vector2(26, 22), Color.white, paperIcon);
                shieldIcons.Add(si);
            }
            bossPanel.SetActive(false);
        }

        void Update()
        {
            var p = ClimbPlayer.Instance;
            var g = ClimbGame.Instance;
            if (p)
            {
                camText.text = p.FlashCooldownRemaining > 0f ? $"camera recharging {p.FlashCooldownRemaining:0.0}s" : "camera ready (Q)";
                if (p.BossMode)
                {
                    mateText.text = p.mateAmmo > 0 ? $"Mate: {p.mateAmmo} (click)" : "Grab a Mate!";
                    mateBar.rectTransform.sizeDelta = new Vector2(150f * p.mateAmmo / Mathf.Max(1, p.mateAmmoMax), 10f);
                }
                else
                {
                    mateText.text = p.Jetpacking ? "MATE JETPACK!" : "Mate: grab it to fly";
                    mateBar.rectTransform.sizeDelta = new Vector2(150f * p.JetpackRemaining / Mathf.Max(0.01f, p.jetpackDuration), 10f);
                }
            }
            if (g) altMarker.rectTransform.anchoredPosition = new Vector2(0, AltBarH * g.Altitude01);
            if (boss && bossPanel.activeSelf && g)
            {
                bossFill.rectTransform.sizeDelta = new Vector2(580f * boss.Health / Mathf.Max(1, boss.MaxHealth), 14f);
                bossName.text = $"{boss.bossName.ToUpperInvariant()}   ·   PHASE {boss.Phase}/3";
                for (int i = 0; i < shieldIcons.Count; i++)
                {
                    shieldIcons[i].gameObject.SetActive(i < g.MaxShield);
                    shieldIcons[i].color = i < g.Shield ? Color.white : new Color(1, 1, 1, 0.18f);
                }
            }
        }

        public void ShowBoss(SiegmundBoss b) { boss = b; bossPanel.SetActive(b != null); }

        IEnumerator FlashRoutine()
        {
            for (float t = 0.35f; t > 0f; t -= Time.deltaTime)
            {
                flashOverlay.color = new Color(1, 1, 1, Mathf.Clamp01(t / 0.35f) * 0.85f);
                yield return null;
            }
            flashOverlay.color = new Color(1, 1, 1, 0);
        }

        // ---------- full screen cards ----------
        Transform NewOverlay(Color bg)
        {
            if (overlay) Destroy(overlay);
            var img = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), bg);
            overlay = img.gameObject;
            var stage = Rect("Stage", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280, 720));
            return stage;
        }

        void Prompt(Transform stage, string text)
        {
            var btn = Panel(stage, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(600, 46), new Color(0.85f, 0.62f, 0.15f));
            var t = Label(btn.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 46), 22, TextAnchor.MiddleCenter, Color.white, text);
            btn.gameObject.AddComponent<UnscaledPulse>().target = t;
        }

        public void Hide() { if (overlay) Destroy(overlay); }

        static bool Continue() => GameInput.ConfirmPressed || HowToPlayOverlay.DismissRequested ||
                                  (!PauseMenu.IsPaused && UnityEngine.InputSystem.Keyboard.current != null &&UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame);

        IEnumerator WaitContinue(float minTime, float autoAfter = -1f)
        {
            float t = 0f;
            yield return null;
            HowToPlayOverlay.DismissRequested = false;
            while (true)
            {
                if (PauseMenu.IsPaused) { yield return null; continue; } // story cards wait while paused
                t += Time.unscaledDeltaTime;
                if (t > minTime && Continue()) break;
                if (autoAfter > 0f && t > autoAfter) break;
                yield return null;
            }
            HowToPlayOverlay.DismissRequested = false;
        }

        /// <summary>Story card: the Paulinum from outside, the lift is out of order.</summary>
        public IEnumerator Intro(string text)
        {
            var s = NewOverlay(new Color(0.02f, 0.03f, 0.08f, 0.97f));
            Panel(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, 20), new Vector2(460, 430), Color.white, paulinum);
            Label(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 50), 40, TextAnchor.UpperCenter, Gold, "LEVEL 4: CLIMB THE PAULINUM");
            Label(s, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-40, 20), new Vector2(560, 380), 24, TextAnchor.MiddleLeft, Body, text);
            Prompt(s, "Press Enter or Space to climb");
            yield return WaitContinue(0.4f, 12f);
            Hide();
        }

        public void ShowMessage(string title, string body, string prompt)
        {
            var s = NewOverlay(new Color(0.02f, 0.03f, 0.08f, 0.85f));
            Label(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1200, 70), 54, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.4f), title);
            Label(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1000, 120), 24, TextAnchor.MiddleCenter, Body, body);
            Prompt(s, prompt);
        }

        /// <summary>"Dr. Dorn" screen after the defense: the SWS Group standing in their lab.</summary>
        public IEnumerator DoctorScreen(float fill, int falls)
        {
            var s = NewOverlay(new Color(0.03f, 0.04f, 0.1f, 1f));
            if (finaleBackground)
            {
                // the lab photo covers the whole screen (any aspect ratio), slightly dimmed, with dark fades
                // at the top and bottom so the title, names and button stay readable
                var canvasSize = ((RectTransform)root).rect.size;
                float k = Mathf.Max(canvasSize.x / 1280f, canvasSize.y / 720f, 1f);
                var bg = Panel(overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f * k, 720f * k), new Color(0.8f, 0.82f, 0.9f), finaleBackground);
                bg.preserveAspect = false;
                bg.transform.SetSiblingIndex(0);
                var fade = FadeSprite();
                var top = Panel(overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 360f * k), new Vector2(1280f * k, 320f * k), new Color(0.02f, 0.03f, 0.09f, 0.88f), fade);
                top.preserveAspect = false;
                top.transform.SetSiblingIndex(1);
                var bottom = Panel(overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -360f * k), new Vector2(1280f * k, 220f * k), new Color(0.02f, 0.03f, 0.09f, 0.92f), fade);
                bottom.preserveAspect = false;
                bottom.rectTransform.localScale = new Vector3(1, -1, 1); // same fade, upside down
                bottom.transform.SetSiblingIndex(2);
            }

            var cap = Panel(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(130, 95), Color.white, phdCap);
            cap.gameObject.AddComponent<UIBob>();
            var title = Label(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(1260, 120), 104, TextAnchor.UpperCenter, Gold, "DR. DORN");
            title.fontStyle = FontStyle.Bold;
            Label(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(1200, 70), 26, TextAnchor.UpperCenter, Color.white,
                "Thesis successfully defended.\nCongratulations Dr. Dorn from the whole <color=#ffd659>SWS Group</color>!");

            // the SWS Group lineup standing on the lab floor, Johannes in the middle
            int n = groupSprites != null ? groupSprites.Length : 0;
            float cell = Mathf.Min(160f, 1240f / Mathf.Max(1, n)), size = cell * 0.94f;
            const float feetY = -205f; // from the stage centre
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * cell;
                bool hero = groupNames != null && i < groupNames.Length && groupNames[i] == "Johannes";
                float sz = hero ? size * 1.15f : size;
                Panel(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, feetY + 3f), new Vector2(sz * 0.55f, 7f), new Color(0, 0, 0, 0.35f)); // shadow
                var img = Panel(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(x, feetY), new Vector2(sz, sz), Color.white, groupSprites[i]);
                if (hero) img.gameObject.AddComponent<UIBob>();
                if (groupNames != null && i < groupNames.Length)
                    Label(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(x, feetY - 6f), new Vector2(cell, 26), hero ? 20 : (n > 8 ? 15 : 17), TextAnchor.UpperCenter, hero ? Gold : Body, groupNames[i]);
            }

            var conf = s.gameObject.AddComponent<UIConfetti>();
            conf.parent = s;
            Prompt(s, "Press for Overview");
            yield return WaitContinue(1.2f);
        }

        static Sprite fadeSprite;
        /// <summary>Vertical fade: opaque at the top, transparent at the bottom (tinted by the Image colour).</summary>
        static Sprite FadeSprite()
        {
            if (fadeSprite) return fadeSprite;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f; // 0 bottom, 1 top
                tex.SetPixel(0, y, new Color(1, 1, 1, Mathf.Pow(t, 1.4f)));
            }
            tex.Apply();
            fadeSprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
            return fadeSprite;
        }

        /// <summary>Credits: every photo and postcard found on the way (missing ones as question marks).</summary>
        public IEnumerator Credits(List<CreditPhoto> photos, string text)
        {
            var s = NewOverlay(new Color(0.03f, 0.04f, 0.1f, 1f));
            int found = 0;
            foreach (var ph in photos) if (SaveSystem.HasPhoto(ph.id)) found++;
            Label(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(1200, 44), 38, TextAnchor.UpperCenter, Gold, "MEMORIES OF THE PhD");
            Label(s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1200, 26), 18, TextAnchor.UpperCenter, Caption, $"Photos and postcards collected: {found} / {photos.Count}");
            int cols = Mathf.Min(4, Mathf.Max(1, photos.Count));
            float cw = 280f, ch = 210f;
            for (int i = 0; i < photos.Count; i++)
            {
                var ph = photos[i];
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, photos.Count - r * cols);
                float x = (c - (inRow - 1) / 2f) * cw;
                float y = 130 - r * ch;
                bool has = SaveSystem.HasPhoto(ph.id);
                var frame = Panel(s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(250, 180), has ? new Color(0.96f, 0.95f, 0.9f) : new Color(0.2f, 0.22f, 0.3f));
                frame.rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? -2f : 2f));
                var img = Panel(frame.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(230, 130), Color.white, ph.image);
                if (!has) img.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
                var cap = Label(frame.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(240, 30), 16, TextAnchor.MiddleCenter, has ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.6f, 0.65f, 0.75f), has ? ph.caption : "? not found ?");
                cap.GetComponent<Shadow>().enabled = !has;
                if (!has) Label(frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 15), new Vector2(240, 80), 64, TextAnchor.MiddleCenter, new Color(0.5f, 0.55f, 0.7f), "?");
            }
            Label(s, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 86), new Vector2(1200, 80), 22, TextAnchor.MiddleCenter, Body, text);
            Prompt(s, "Press Enter to return to the start screen");
            yield return WaitContinue(1.2f);
            Hide();
        }
    }

    /// <summary>Gentle up and down bob for UI images (unscaled time).</summary>
    public class UIBob : MonoBehaviour
    {
        Vector2 basePos; RectTransform rt;
        void Start() { rt = (RectTransform)transform; basePos = rt.anchoredPosition; }
        void Update() { rt.anchoredPosition = basePos + Vector2.up * Mathf.Sin(Time.unscaledTime * 2.5f) * 8f; }
    }

    /// <summary>Falling confetti squares on a UI stage.</summary>
    public class UIConfetti : MonoBehaviour
    {
        public Transform parent;
        readonly List<RectTransform> bits = new List<RectTransform>();
        readonly List<Vector2> vel = new List<Vector2>();
        static readonly Color[] Colors = { new Color(1f, 0.84f, 0.35f), new Color(0.5f, 0.9f, 1f), new Color(1f, 0.45f, 0.45f), new Color(0.5f, 1f, 0.6f), Color.white };

        void Start()
        {
            for (int i = 0; i < 70; i++)
            {
                var go = new GameObject("confetti", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(8, 8);
                rt.anchoredPosition = new Vector2(Random.Range(-660f, 660f), Random.Range(380f, 1200f));
                var img = go.AddComponent<Image>();
                img.color = Colors[i % Colors.Length];
                img.raycastTarget = false;
                bits.Add(rt);
                vel.Add(new Vector2(Random.Range(-30f, 30f), -Random.Range(90f, 200f)));
            }
        }

        void Update()
        {
            for (int i = 0; i < bits.Count; i++)
            {
                var p = bits[i].anchoredPosition + vel[i] * Time.unscaledDeltaTime;
                p.x += Mathf.Sin(Time.unscaledTime * 3f + i) * 0.6f;
                if (p.y < -400f) p.y = 400f;
                bits[i].anchoredPosition = p;
                bits[i].localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 90f + i * 20f);
            }
        }
    }
}
