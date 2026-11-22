using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GetYourPhD
{
    /// <summary>
    /// Pause menu for every level scene (scene name starts with "Level"). Created automatically and kept across scenes.
    /// ESC or gamepad Start opens/closes it. While open: Time.timeScale = 0 (deadline clocks, bosses and the road trip freeze),
    /// gameplay input is locked and sound effects are paused (music keeps playing so the volume can be heard).
    /// Not opened while a code keypad, the How to play screen or anything else that already froze time is showing.
    /// Rows: Resume, Music, Sound, Restart level, Quit to title, Exit game (the last two need a second press).
    /// Menu: Up/Down (or W/S, d-pad) select, Enter/A confirm, Left/Right change the music volume, ESC/B/Start resume.
    /// The music slider is the same saved setting as the start screen slider (GameAudio.SetMusicVolume).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsPaused { get; private set; }

        const string TitleScene = "StartScreen";

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.97f);
        static readonly Color Caption = new Color(0.6f, 0.8f, 1f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        static readonly Color Body = new Color(0.92f, 0.94f, 1f);
        static readonly Color RowIdle = new Color(0.2f, 0.25f, 0.38f, 0.95f);
        static readonly Color RowSelected = new Color(0.85f, 0.62f, 0.15f);

        enum Row { Resume, Music, Sound, Restart, Quit, Exit }
        static readonly Row[] Rows = { Row.Resume, Row.Music, Row.Sound, Row.Restart, Row.Quit, Row.Exit };

        Font font;
        GameObject root;
        Image[] rowBg;
        Text[] rowText;
        Slider musicSlider;
        Text musicValue, soundValue;
        int selected;
        float savedScale = 1f;
        int openedFrame;
        Row? confirming; // Quit / Exit need a second press

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenu>();
        }

        void Awake()
        {
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) { Instance = null; IsPaused = false; AudioListener.pause = false; }
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) { if (IsPaused) Close(false); }

        static bool InLevel => SceneManager.GetActiveScene().name.StartsWith("Level");

        bool CanOpen()
        {
            if (!InLevel || Time.timeScale <= 0f) return false;
            if (PeerReviewGate.KeypadOpen || Time.frameCount - PeerReviewGate.KeypadClosedFrame <= 1) return false; // ESC closes the keypad
            var how = FindAnyObjectByType<HowToPlayOverlay>();
            if (how && how.IsOpen) return false;
            return true;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool toggle = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);

            if (!IsPaused)
            {
                if (toggle && CanOpen()) Open();
                return;
            }
            if (Time.frameCount == openedFrame) return;

            bool back = toggle || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (back) { if (confirming != null) { confirming = null; Refresh(); } else Close(true); return; }

            int move = 0;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move--;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move++;
            }
            if (gp != null) { if (gp.dpad.up.wasPressedThisFrame) move--; if (gp.dpad.down.wasPressedThisFrame) move++; }
            if (move != 0) { Select((selected + move + Rows.Length) % Rows.Length); GameAudio.Play("ui_click", 0.5f); }

            int step = 0;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step++;
            }
            if (gp != null) { if (gp.dpad.left.wasPressedThisFrame) step--; if (gp.dpad.right.wasPressedThisFrame) step++; }
            if (step != 0 && Rows[selected] == Row.Music && musicSlider)
                musicSlider.value = Mathf.Clamp01(Mathf.Round((musicSlider.value + step * 0.1f) * 10f) / 10f);
            if (step != 0 && Rows[selected] == Row.Sound) Activate(Row.Sound);

            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                           (gp != null && gp.buttonSouth.wasPressedThisFrame);
            if (confirm) Activate(Rows[selected]);

            if (soundValue) soundValue.text = GameAudio.Muted ? "OFF" : "ON"; // M still works while paused
        }

        // ---------------- open / close ----------------
        void Open()
        {
            IsPaused = true;
            openedFrame = Time.frameCount;
            savedScale = Time.timeScale;
            Time.timeScale = 0f; // GameInput.Locked reads true while IsPaused
            AudioListener.pause = true; // sound effects and loops; music ignores this (GameAudio)
            confirming = null;
            EnsureEventSystem();
            font = SceneFont();
            Build();
            Select(0);
            GameAudio.Play("ui_click");
        }

        void Close(bool restore)
        {
            IsPaused = false;
            confirming = null;
            AudioListener.pause = false;
            if (root) Destroy(root);
            root = null;
            if (restore)
            {
                Time.timeScale = savedScale <= 0f ? 1f : savedScale;
                GameAudio.Play("ui_confirm");
            }
        }

        void Activate(Row r)
        {
            switch (r)
            {
                case Row.Resume: Close(true); break;
                case Row.Music: break;
                case Row.Sound: GameAudio.ToggleMute(); Refresh(); break;
                case Row.Restart: Load(SceneManager.GetActiveScene().buildIndex); break;
                case Row.Quit:
                case Row.Exit:
                    if (confirming != r) { confirming = r; Refresh(); GameAudio.Play("ui_click"); }
                    else if (r == Row.Quit) LoadTitle();
                    else ExitGame();
                    break;
            }
        }

        void Load(int buildIndex)
        {
            Close(false);
            SaveSystem.Checkpoint = "";
            GameInput.Locked = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
        }

        void LoadTitle()
        {
            Close(false);
            SaveSystem.Checkpoint = "";
            GameInput.Locked = false;
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(TitleScene)) SceneManager.LoadScene(TitleScene);
            else SceneManager.LoadScene(0);
        }

        void ExitGame()
        {
            Close(false);
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- UI ----------------
        void Select(int i)
        {
            selected = i;
            if (Rows[selected] != confirming) confirming = null;
            Refresh();
        }

        void Refresh()
        {
            if (rowBg == null) return;
            for (int i = 0; i < rowBg.Length; i++)
            {
                if (!rowBg[i]) continue;
                rowBg[i].color = i == selected ? RowSelected : RowIdle;
                rowText[i].color = i == selected ? Color.white : Body;
            }
            int q = System.Array.IndexOf(Rows, Row.Quit), x = System.Array.IndexOf(Rows, Row.Exit);
            if (rowText[q]) rowText[q].text = confirming == Row.Quit ? "PRESS AGAIN: QUIT TO TITLE" : "QUIT TO TITLE";
            if (rowText[x]) rowText[x].text = confirming == Row.Exit ? "PRESS AGAIN: EXIT THE GAME" : "EXIT GAME";
            if (soundValue) soundValue.text = GameAudio.Muted ? "OFF" : "ON";
        }

        /// <summary>Uses the level's own (pixel) font, like the HUD and How to play screen.</summary>
        static Font SceneFont()
        {
            if (HUD.Instance && HUD.Instance.font) return HUD.Instance.font;
            var how = FindAnyObjectByType<HowToPlayOverlay>();
            if (how && how.font) return how.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

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
            t.raycastTarget = false;
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
            if (root) Destroy(root);
            root = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above HUD, keypad and How to play
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var c = Vector2.one * 0.5f;
            Panel(root.transform, c, c, Vector2.zero, new Vector2(4000, 4000), new Color(0.02f, 0.03f, 0.08f, 0.72f));
            var panel = Panel(root.transform, c, c, Vector2.zero, new Vector2(560, 528), Ink).transform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.62f, 0.15f, 0.9f);
            outline.effectDistance = new Vector2(3, -3);

            var top = new Vector2(0.5f, 1);
            var title = Label(panel, top, top, new Vector2(0, -20), new Vector2(520, 56), 44, TextAnchor.UpperCenter, Gold, "PAUSED");
            title.fontStyle = FontStyle.Bold;
            Label(panel, top, top, new Vector2(0, -74), new Vector2(520, 24), 16, TextAnchor.UpperCenter, Caption, "The deadline clock is stopped. Take a Mate break.");

            rowBg = new Image[Rows.Length];
            rowText = new Text[Rows.Length];
            float y = -116;
            for (int i = 0; i < Rows.Length; i++)
            {
                int idx = i;
                var bg = Panel(panel, top, top, new Vector2(0, y), new Vector2(460, 50), RowIdle);
                rowBg[i] = bg;
                bool hasValue = Rows[i] == Row.Music || Rows[i] == Row.Sound;
                var a = hasValue ? new Vector2(0, 0.5f) : c;
                rowText[i] = Label(bg.transform, a, a, hasValue ? new Vector2(18, 0) : Vector2.zero, new Vector2(hasValue ? 160 : 450, 40), 22,
                    hasValue ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Body, RowLabel(Rows[i]));

                // Mouse: hover selects, click activates.
                var trig = bg.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trig, EventTriggerType.PointerEnter, () => { if (selected != idx) Select(idx); });
                if (Rows[i] != Row.Music) AddTrigger(trig, EventTriggerType.PointerClick, () => { Select(idx); Activate(Rows[idx]); });

                if (Rows[i] == Row.Music) BuildSlider(bg.transform);
                if (Rows[i] == Row.Sound)
                    soundValue = Label(bg.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(200, 40), 22, TextAnchor.MiddleRight, Gold, "");
                y -= 58;
            }

            var bottom = new Vector2(0.5f, 0);
            Label(panel, bottom, bottom, new Vector2(0, 10), new Vector2(540, 40), 14, TextAnchor.LowerCenter, new Color(0.7f, 0.75f, 0.85f),
                "Up/Down select   Enter confirm   Left/Right volume   ESC resume\nGamepad: d-pad, A confirm, B or Start resume", false);
            Refresh();
        }

        static string RowLabel(Row r) => r switch
        {
            Row.Resume => "RESUME",
            Row.Music => "MUSIC",
            Row.Sound => "SOUND (M)",
            Row.Restart => "RESTART LEVEL",
            Row.Quit => "QUIT TO TITLE",
            _ => "EXIT GAME",
        };

        static void AddTrigger(EventTrigger trig, EventTriggerType type, System.Action action)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => action());
            trig.triggers.Add(e);
        }

        void BuildSlider(Transform row)
        {
            musicValue = Label(row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(60, 40), 20, TextAnchor.MiddleRight, Gold, "");
            var srt = Rect("Slider", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170, 0), new Vector2(190, 22));
            var bg = srt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.2f);
            var fillArea = Rect("Fill Area", srt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0, 0.25f); fillArea.anchorMax = new Vector2(1, 0.75f);
            fillArea.offsetMin = new Vector2(4, 0); fillArea.offsetMax = new Vector2(-4, 0);
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Gold;
            var handleArea = Rect("Handle Slide Area", srt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
            var handle = Rect("Handle", handleArea, Vector2.zero, c5, Vector2.zero, new Vector2(16, 0));
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            var himg = handle.gameObject.AddComponent<Image>();
            himg.color = Color.white;

            musicSlider = srt.gameObject.AddComponent<Slider>();
            musicSlider.fillRect = fill;
            musicSlider.handleRect = handle;
            musicSlider.targetGraphic = himg;
            musicSlider.direction = Slider.Direction.LeftToRight;
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.navigation = new Navigation { mode = Navigation.Mode.None }; // keys are handled in Update
            musicSlider.value = GameAudio.MusicVolume;
            musicSlider.onValueChanged.AddListener(OnMusicVolume);
            OnMusicVolume(musicSlider.value);
        }

        static readonly Vector2 c5 = new Vector2(0.5f, 0.5f);

        void OnMusicVolume(float v)
        {
            GameAudio.SetMusicVolume(v);
            if (musicValue) musicValue.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }
}
