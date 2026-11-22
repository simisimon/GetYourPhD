using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GetYourPhD.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/StartScreen.unity (title, New Game, how-to-play, Johannes and the path up to the PhD cap)
    /// and makes it the first scene in the build settings. Re-run after changing the art; hand edits get overwritten.
    /// </summary>
    public static class StartScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/StartScreen.unity";
        const string Level1Path = "Assets/Scenes/Level1_MeasurementLab.unity";
        const string FontPath = "Assets/Fonts/PixelifySans.ttf";

        [MenuItem("GetYourPhD/Build Start Screen")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            // Crisp pixel font: no anti-aliasing.
            if (AssetImporter.GetAtPath(FontPath) is TrueTypeFontImporter fi && fi.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                fi.fontRenderingMode = FontRenderingMode.HintedRaster;
                fi.SaveAndReimport();
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: 7.5 world units tall, so one art pixel = 3 screen pixels at 720p.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.08f, 0.22f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Background: 320x180 campus at dusk, scaled to fill the view (with a little margin for wider screens).
            var bgSprite = Load("Assets/Art/StartScreen/campus_bg.png");
            if (bgSprite)
            {
                var bg = NewSprite("Background", bgSprite, null, new Vector3(0, 0, 10), -100);
                float s = 7.5f / (bgSprite.rect.height / 32f);
                bg.transform.localScale = new Vector3(s * 1.15f, s, 1);
                bg.color = new Color(0.62f, 0.6f, 0.75f); // dim the Paulinum skyline so the title, path and cap stand out
            }

            // Johannes on the ground, a path of lab platforms with data points leading up to the PhD cap (the goal).
            const float groundY = -3.0f;
            // Front (south) view breathing idle; falls back to the static south rotation.
            const string idleDir = "Assets/Art/Johannes/idle_south";
            var idle = Directory.Exists(idleDir)
                ? Directory.GetFiles(idleDir, "*.png").OrderBy(f => f)
                    .Select(f => Load(f.Replace('\\', '/'))).Where(x => x).ToArray()
                : new Sprite[0];
            if (idle.Length == 0) { var still = Load("Assets/Art/Johannes/south.png"); if (still) idle = new[] { still }; }
            if (idle.Length > 0)
            {
                var j = NewSprite("Johannes", idle[0], null, new Vector3(-1.2f, groundY, 0), 10);
                var anim = j.gameObject.AddComponent<SpriteAnimator>();
                anim.clips = new List<SpriteClip> { new SpriteClip { name = "idle", frames = idle, fps = 6, loop = true } };
                anim.defaultClip = "idle";
            }

            var platform = Load("Assets/Art/StartScreen/platform.png");
            var dataPoint = Load("Assets/Art/Sprites/datapoint.png");
            var steps = new[] { new Vector2(0.0f, -2.35f), new Vector2(1.2f, -1.55f), new Vector2(2.4f, -0.75f) };
            var path = new GameObject("PathToPhD").transform;
            for (int i = 0; i < steps.Length; i++)
            {
                if (platform) NewSprite("Platform" + i, platform, path, steps[i], 2);
                if (dataPoint && i < steps.Length - 1) // the last step leads straight to the cap
                {
                    var d = NewSprite("DataPoint" + i, dataPoint, path, steps[i] + new Vector2(0, 0.85f), 5);
                    d.transform.localScale = Vector3.one * 0.8f;
                    var b = d.gameObject.AddComponent<StartScreenBob>();
                    b.amplitude = 0.08f; b.phase = i * 0.9f;
                }
            }

            var cap = Load("Assets/Art/StartScreen/phd_cap.png");
            if (cap)
            {
                var c = NewSprite("PhDCap", cap, null, new Vector3(3.45f, 0.55f, 0), 8);
                c.transform.localScale = Vector3.one * 1.5f;
                var b = c.gameObject.AddComponent<StartScreenBob>();
                b.amplitude = 0.15f; b.speed = 1.6f; b.glow = true;
            }

            var ui = new GameObject("StartScreen").AddComponent<StartScreen>();
            ui.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath); // pixel game font (OFL)

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            SetBuildOrder();
            Debug.Log("[GetYourPhD] Start screen built: " + ScenePath);
        }

        /// <summary>StartScreen first, then Level 1, then every other scene already listed.</summary>
        static void SetBuildOrder()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(Level1Path, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath && s.path != Level1Path) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static SpriteRenderer NewSprite(string name, Sprite s, Transform parent, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
