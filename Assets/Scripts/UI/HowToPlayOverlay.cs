using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GetYourPhD
{
    [System.Serializable]
    public class HowToRow
    {
        public Sprite icon;
        public string name;
        public string text;
    }

    [System.Serializable]
    public class HowToKey
    {
        public string key;
        public string action;
    }

    /// <summary>
    /// The "How to play" screen shown before a level starts. The game is paused (and the deadline clock stops)
    /// until the player presses Enter / Space / A. Shown once per level per game; a retry skips it.
    /// The content is filled in by the level builders.
    /// </summary>
    public class HowToPlayOverlay : MonoBehaviour
    {
        static readonly HashSet<string> Seen = new HashSet<string>();

        /// <summary>Start screen calls this on "New Game" so every new player sees the screens again.</summary>
        public static void ResetSeen() => Seen.Clear();

        public string levelId = "L1";
        public string title = "LEVEL 1: THE MEASUREMENT LAB";
        [TextArea] public string intro;
        public string rowsCaption = "COLLECTIBLES";
        public List<HowToRow> rows = new List<HowToRow>();
        public List<HowToKey> keys = new List<HowToKey>();
        public string gamepadNote = "Gamepad: stick move, A jump, X throw, Y drink, B flash, RB interact";
        public Font font;

        public bool IsOpen { get; private set; }

        /// <summary>Closes an open screen from code (used by automated playtests).</summary>
        public static bool DismissRequested;

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.98f);
        static readonly Color Caption = new Color(0.6f, 0.8f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        static readonly Color Body = new Color(0.92f, 0.94f, 1f);

        GameObject root;

        /// <summary>Shows the screen if this level's has not been seen yet; yields until it is closed.</summary>
        public IEnumerator ShowIfNew()
        {
            if (Seen.Contains(levelId)) yield break;
            Seen.Add(levelId);
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            IsOpen = true;
            float oldScale = Time.timeScale;
            Time.timeScale = 0f;
            GameInput.Locked = true;
            yield return null;
            DismissRequested = false;
            while (!(GameInput.ConfirmPressed || StartPressed() || DismissRequested)) yield return null;
            DismissRequested = false;
            GameAudio.Play("ui_confirm");
            Destroy(root);
            Time.timeScale = oldScale <= 0f ? 1f : oldScale;
            yield return new WaitForSecondsRealtime(0.15f); // so the Space press doesn't also jump
            GameInput.Locked = false;
            IsOpen = false;
        }

        static bool StartPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        // ---------- UI ----------
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

        Text Label(Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color, string text, bool shadow = true)
        {
            var rt = Rect("Text", parent, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.85f);
                sh.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        static Image Panel(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect("Panel", parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        void Build()
        {
            root = new GameObject("HowToPlay", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            Panel(root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.02f, 0.03f, 0.08f, 0.8f));
            var panel = Panel(root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 660), Ink).transform;

            float y = -18;
            Label(panel, new Vector2(28, y), new Vector2(1120, 40), 34, TextAnchor.UpperLeft, Gold, title);
            Label(panel, new Vector2(900, y + 2), new Vector2(240, 30), 18, TextAnchor.UpperRight, Caption, "HOW TO PLAY");
            y -= 50;
            Label(panel, new Vector2(28, y), new Vector2(1120, 80), 18, TextAnchor.UpperLeft, Body, intro);
            y -= 92;

            // Left column: rows with icons. Right column: controls.
            float ly = y;
            Label(panel, new Vector2(28, ly), new Vector2(600, 22), 16, TextAnchor.UpperLeft, Caption, rowsCaption);
            ly -= 26;
            foreach (var r in rows)
            {
                var img = Panel(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, ly), new Vector2(36, 36), Color.white);
                if (r.icon) { img.sprite = r.icon; img.preserveAspect = true; } else img.color = new Color(0, 0, 0, 0);
                Label(panel, new Vector2(74, ly), new Vector2(190, 36), 18, TextAnchor.MiddleLeft, Color.white, r.name);
                Label(panel, new Vector2(266, ly), new Vector2(400, 40), 15, TextAnchor.MiddleLeft, Body, r.text);
                ly -= 42;
            }

            float ry = y;
            const float rx = 700;
            Label(panel, new Vector2(rx, ry), new Vector2(450, 22), 16, TextAnchor.UpperLeft, Caption, "CONTROLS");
            ry -= 26;
            foreach (var k in keys)
            {
                var bg = Panel(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(rx, ry), new Vector2(170, 24), new Color(0.2f, 0.25f, 0.38f, 0.95f));
                var kt = Label(bg.transform, Vector2.zero, new Vector2(170, 24), 16, TextAnchor.MiddleCenter, Gold, k.key, false);
                kt.rectTransform.anchorMin = kt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                kt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Label(panel, new Vector2(rx + 182, ry), new Vector2(270, 40), 15, TextAnchor.UpperLeft, Body, k.action);
                ry -= 42;
            }
            if (!string.IsNullOrEmpty(gamepadNote))
                Label(panel, new Vector2(rx, ry - 4), new Vector2(450, 40), 14, TextAnchor.UpperLeft, new Color(0.7f, 0.75f, 0.85f), gamepadNote);

            // Start prompt
            var btn = Panel(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(420, 50), new Color(0.85f, 0.62f, 0.15f));
            var bt = Label(btn.transform, Vector2.zero, new Vector2(420, 50), 24, TextAnchor.MiddleCenter, Color.white, "Press Enter or Space to start");
            bt.rectTransform.anchorMin = bt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var blink = btn.gameObject.AddComponent<UnscaledPulse>();
            blink.target = bt;
        }
    }

    /// <summary>Pulses a text while the game is paused (uses unscaled time).</summary>
    public class UnscaledPulse : MonoBehaviour
    {
        public Text target;
        void Update()
        {
            if (!target) return;
            var c = target.color;
            c.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            target.color = c;
        }
    }
}
