using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GetYourPhD
{
    /// <summary>
    /// Title screen. Builds its UI at runtime (like the HUD): title and "New Game" button (How to play is shown before each level)
    /// Johannes, the path and the PhD cap are sprites in the scene.
    /// New Game: button click, Enter or gamepad South.
    /// </summary>
    public class StartScreen : MonoBehaviour
    {
        public string firstLevelScene = "Level1_MeasurementLab";
        public Font font;

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.86f);
        static readonly Color Caption = new Color(0.6f, 0.8f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        static readonly Color Body = new Color(0.92f, 0.94f, 1f);

        bool loading;

        void Awake()
        {
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameInput.Locked = false;
            EnsureEventSystem();
            Build();
        }

        Slider musicSlider;
        Text musicValue;

        void Update()
        {
            if (GameInput.ConfirmPressed) NewGame();

            // Music volume: slider (mouse), Left/Right arrows or the gamepad d-pad in 10% steps.
            int step = 0;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) { if (kb.leftArrowKey.wasPressedThisFrame) step--; if (kb.rightArrowKey.wasPressedThisFrame) step++; }
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null) { if (gp.dpad.left.wasPressedThisFrame) step--; if (gp.dpad.right.wasPressedThisFrame) step++; }
            if (step != 0 && musicSlider) musicSlider.value = Mathf.Clamp01(Mathf.Round((musicSlider.value + step * 0.1f) * 10f) / 10f);
        }

        void OnMusicVolume(float v)
        {
            GameAudio.SetMusicVolume(v);
            if (musicValue) musicValue.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        /// <summary>Bottom-right panel: "Music" label, slider and percentage.</summary>
        void BuildVolume(Transform root)
        {
            // Two rows: "MUSIC", slider and percentage on top, the key hint on its own line below
            // (the hint used to wrap into a second line and grow upwards into the slider).
            var panel = Panel(root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(360, 82), Ink);
            panel.gameObject.name = "MusicVolume";
            Label(panel.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(90, 30), 20, TextAnchor.MiddleLeft, Gold, "MUSIC");
            var hint = Label(panel.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 8), new Vector2(332, 18), 12, TextAnchor.LowerLeft, Caption, "drag, or Left/Right keys   (M = mute)", false);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            musicValue = Label(panel.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -10), new Vector2(60, 30), 20, TextAnchor.MiddleRight, Body, "");

            var srt = Rect("Slider", panel.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, -14), new Vector2(180, 22));
            var bg = srt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.28f, 0.36f);
            var fillArea = Rect("Fill Area", srt, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0, 0.25f); fillArea.anchorMax = new Vector2(1, 0.75f);
            fillArea.offsetMin = new Vector2(4, 0); fillArea.offsetMax = new Vector2(-4, 0);
            var fill = Rect("Fill", fillArea, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.62f, 0.15f);
            var handleArea = Rect("Handle Slide Area", srt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 0));
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            var himg = handle.gameObject.AddComponent<Image>();
            himg.color = Color.white;

            musicSlider = srt.gameObject.AddComponent<Slider>();
            musicSlider.fillRect = fill;
            musicSlider.handleRect = handle;
            musicSlider.targetGraphic = himg;
            musicSlider.direction = Slider.Direction.LeftToRight;
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.navigation = new Navigation { mode = Navigation.Mode.None }; // arrows are handled in Update
            musicSlider.value = GameAudio.MusicVolume;
            musicSlider.onValueChanged.AddListener(OnMusicVolume);
            OnMusicVolume(musicSlider.value);
        }

        public void NewGame()
        {
            if (loading) return;
            loading = true;
            SaveSystem.Checkpoint = ""; // a new game always starts at the beginning of Level 1
            HowToPlayOverlay.ResetSeen(); // every new player sees the How to play screens
            GameAudio.Play("ui_confirm");
            SceneManager.LoadScene(firstLevelScene);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(null);
        }

        // ---------- UI helpers ----------
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

        Text Label(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color, string text, bool shadow = true)
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
            var cgo = new GameObject("StartCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = cgo.transform;

            // ----- Title across the full width, New Game centered -----
            var title = Label(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(1250, 130), 116, TextAnchor.UpperCenter, Gold,
                "GET YOUR PhD, <color=#ffffff>MR. DORN!</color>");
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 40;
            title.resizeTextMaxSize = 116;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            Label(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -142), new Vector2(1240, 30), 22, TextAnchor.UpperCenter, Body,
                "Help Johannes reach his PhD: finish the paper before the deadline and survive the Rebuttal Room.");

            var btnImg = Panel(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(280, 66), new Color(0.85f, 0.62f, 0.15f));
            btnImg.gameObject.name = "NewGameButton";
            var outline = btnImg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.06f, 0.02f);
            outline.effectDistance = new Vector2(3, -3);
            var btn = btnImg.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(NewGame);
            Label(btnImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 66), 32, TextAnchor.MiddleCenter, Color.white, "NEW GAME");
            Label(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -268), new Vector2(300, 26), 15, TextAnchor.MiddleCenter, Caption, "or press Enter");

            // How to play is shown before each level (HowToPlayOverlay), since every level plays differently.
            BuildVolume(root);
        }
    }
}
