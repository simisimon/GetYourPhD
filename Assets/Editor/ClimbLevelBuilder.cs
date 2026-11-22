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
    /// Builds Level 4 "Climb the Paulinum" (Assets/Scenes/Level4_PaulinumClimb.unity). The climb itself is generated
    /// at runtime by <see cref="ClimbGame"/> from a fixed seed; this builder wires the camera, Johannes, the boss,
    /// the HUD, the art references, the How to play screen and the credits photos, and puts the scene after Level 3.
    /// Art: Assets/Art/Climb (LevelTools/prep_climb_art.py), boss frames Assets/Art/Siegmund (PixelLab character 26d680b4-...).
    /// </summary>
    public static class ClimbLevelBuilder
    {
        const string ScenePath = "Assets/Scenes/Level4_PaulinumClimb.unity";
        const string Art = "Assets/Art/Climb";

        static Sprite S(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path.StartsWith("Assets/") ? path : $"{Art}/{path}.png");
            if (!s) Debug.LogWarning($"[GetYourPhD] Missing sprite {path}");
            return s;
        }

        static Sprite[] Frames(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").OrderBy(f => f).Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace('\\', '/'))).Where(s => s).ToArray() : new Sprite[0];

        [MenuItem("GetYourPhD/Build Level 4 (Climb the Paulinum)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- camera & light ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
            camGo.transform.position = new Vector3(0, 8, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // ---- art ----
            var art = new ClimbArt
            {
                pixel = S("Assets/Art/Road/pixel.png"), slab = S("slab"), sign = S("sign"), pillar = S("pillar"), lift = S("lift"),
                infoDesk = S("infodesk"), bench = S("bench"), organ = S("organ"), slide = S("slide"), paulinum = S("Assets/Art/Road/paulinum.png"),
                walls = new[] { S("wall_foyer"), S("wall_lecture"), S("wall_library"), S("wall_aula") },
                platforms = new[] { S("plat_foyer"), S("plat_lecture"), S("plat_library"), S("plat_aula") },
                reject = S("plat_reject"), beamer = S("beamer"), paperStack = S("paperstack"), pipe = S("pipe"),
                dataPoint = S("Assets/Art/Sprites/datapoint.png"), citation = S("Assets/Art/Road/citation.png"), paper = S("Assets/Art/Road/paper.png"),
                mate = S("Assets/Art/Sprites/mate.png"), photo = S("Assets/Art/Sprites/photo.png"),
                drone = S("drone"), coffee = S("coffee"), imposter = S("imposter"), warn = S("Assets/Art/Road/warn.png"), redPen = S("redpen"), configBox = S("configbox"),
            };

            // ---- Johannes ----
            var player = new GameObject("Johannes");
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(player.transform, false);
            spriteGo.transform.localPosition = new Vector3(0, -0.12f, 0); // the frames have a few empty pixels under the shoes
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            var anim = spriteGo.AddComponent<SpriteAnimator>();
            var clips = new List<SpriteClip>();
            foreach (var (name, fps, loop) in new[] { ("idle", 6f, true), ("run", 12f, true), ("throw", 20f, false), ("jump", 16f, false), ("doublejump", 18f, false), ("fall", 8f, true), ("drink", 14f, false), ("flash", 14f, false), ("hurt", 16f, false), ("victory", 8f, true) })
            {
                var fr = Frames($"Assets/Art/Johannes/{name}");
                if (fr.Length > 0) clips.Add(new SpriteClip { name = name, frames = fr, fps = fps, loop = loop });
            }
            anim.clips = clips;
            anim.defaultClip = "jump";
            sr.sprite = clips.Count > 0 ? clips[0].frames[0] : S("Assets/Art/Johannes/east.png");
            var pc = player.AddComponent<ClimbPlayer>();
            pc.body = sr;
            pc.anim = anim;
            pc.mateSprite = art.mate;

            // ---- Prof. Siegmund ----
            var bossGo = new GameObject("Boss_Siegmund");
            var bsr = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            bsr.transform.SetParent(bossGo.transform, false);
            bsr.transform.localScale = Vector3.one * 1.5f;
            bsr.sortingOrder = 9;
            var banim = bsr.gameObject.AddComponent<SpriteAnimator>();
            banim.clips = new List<SpriteClip>
            {
                new SpriteClip { name = "idle", frames = Frames("Assets/Art/Siegmund2/idle"), fps = 5, loop = true },
                new SpriteClip { name = "attack", frames = Frames("Assets/Art/Siegmund2/attack"), fps = 16, loop = false },
                new SpriteClip { name = "hit", frames = Frames("Assets/Art/Siegmund2/hit"), fps = 12, loop = false },
            };
            banim.defaultClip = "idle";
            bsr.sprite = S("Assets/Art/Siegmund2/south.png");
            var boss = bossGo.AddComponent<SiegmundBoss>();
            boss.view = bsr;
            boss.anim = banim;
            boss.penSprite = art.redPen;
            boss.boxSprite = art.configBox;
            boss.font = font;

            // ---- game systems ----
            var game = new GameObject("Game");
            var clock = game.AddComponent<DeadlineClock>();
            clock.levelDurationSeconds = 270f;
            clock.deadlineHour = 14;
            clock.deadlineMinute = 0;
            clock.timeZoneLabel = "";
            clock.inGameMinutesSpan = 30f; // 13:30 -> 14:00
            clock.slowdownStartsAt = 45f;
            clock.slowdownFullAt = 20f;
            clock.minSpeedFactor = 1f; // no slowdown in the climb, only the warning colours
            game.AddComponent<PaperMeter>().maxPoints = 100;
            var climb = game.AddComponent<ClimbGame>();
            climb.art = art;
            climb.font = font;
            climb.cam = cam;
            climb.player = pc;
            climb.boss = boss;
            climb.creditPhotos = new List<CreditPhoto>
            {
                new CreditPhoto { id = "L1_photo1", caption = "The lab from above", image = S("Assets/Art/Sprites/lab_bg.png") },
                new CreditPhoto { id = "L2_photo1", caption = "Wedding day", image = S("Assets/Art/Home/wedding.png") },
                new CreditPhoto { id = "L3_pc_IT", caption = "Saluti dall'Italia", image = S("Assets/Art/Road/pc_it.png") },
                new CreditPhoto { id = "L3_pc_AT", caption = "Greetings from the Alps", image = S("Assets/Art/Road/pc_at.png") },
                new CreditPhoto { id = "L3_pc_DE", caption = "Greetings from Leipzig", image = S("Assets/Art/Road/pc_leipzig.png") },
                new CreditPhoto { id = "L4_photo1", caption = "Felix-Klein-Hörsaal organ", image = art.organ },
            };
            climb.creditsText = "Thesis successfully defended.\nCongratulations Dr. Dorn from the whole SWS Group!";

            var hud = new GameObject("HUD").AddComponent<HUD>();
            hud.font = font;
            hud.mateIcon = art.mate;
            var chud = hud.gameObject.AddComponent<ClimbHUD>();
            chud.font = font;
            chud.paulinum = art.paulinum;
            chud.mateIcon = art.mate;
            chud.paperIcon = art.paper;
            chud.phdCap = S("Assets/Art/StartScreen/phd_cap.png");
            chud.johannesFront = S("Assets/Art/Johannes/south.png");
            chud.bossPortrait = S("Assets/Art/Siegmund2/south.png");
            // SWS Group lineup for the final screen (PixelLab characters made from Sebastian's group photo)
            // Paul, Henning, Tristan and Grit joined after Johannes left (PixelLab characters from their own photos, PixelLabRaw/group_refs);
            // Rebecca was drawn in the same 48x48 style from her photo. Johannes stays near the middle.
            chud.groupNames = new[] { "Paul", "Henning", "Sebastian", "Stefan", "Max", "Johannes", "Norbert", "Alina", "Annemarie", "Tristan", "Grit", "Rebecca" };
            chud.finaleBackground = S("Assets/Art/Group/lab_finale_bg.png"); // pixel-art panorama of the SWS lab from Sebastian's video
            chud.groupSprites = new[]
            {
                S("Assets/Art/Group/paul.png"), S("Assets/Art/Group/henning.png"),
                S("Assets/Art/Group/sebastian.png"), S("Assets/Art/Group/stefan.png"), S("Assets/Art/Group/max.png"),
                S("Assets/Art/Johannes/south.png"), S("Assets/Art/Siegmund2/south.png"),
                S("Assets/Art/Group/alina.png"), S("Assets/Art/Group/annemarie.png"),
                S("Assets/Art/Group/tristan.png"), S("Assets/Art/Group/grit.png"), S("Assets/Art/Group/rebecca.png"),
            };

            // ---- How to play ----
            var how = game.AddComponent<HowToPlayOverlay>();
            climb.howToPlay = how;
            how.levelId = "L4";
            how.font = font;
            how.title = "LEVEL 4: CLIMB THE PAULINUM";
            how.intro = "The defense starts at <b>14:00</b> in the <b>Felix-Klein-Hörsaal</b> on the top floor, and the lift is <b>\"AUSSER BETRIEB\"</b>. Johannes <b>bounces by himself</b>: " +
                        "just steer him from platform to platform through the Foyer, Lecture Halls, Library and Felix-Klein-Hörsaal. Falling off the screen drops him back to the last landing (-10 s). " +
                        "At the top you face Prof. Siegmund on foot: run left/right, grab the <b>Mate</b> bottles on the floor and <b>click to throw them at him</b> (aim with the mouse). Your <b>Paper</b> from all levels is your shield.";
            how.rowsCaption = "ON THE WAY UP";
            how.rows = new List<HowToRow>
            {
                new HowToRow { icon = art.dataPoint, name = "Research", text = "Data +1, citation +2, paper +3. Paper = shield in the defense" },
                new HowToRow { icon = art.mate, name = "Mate", text = "Climb: jetpack boost up. Defense: ammo to throw" },
                new HowToRow { icon = art.paperStack, name = "Springs", text = "Paper stacks and organ pipes bounce you extra high" },
                new HowToRow { icon = art.reject, name = "Desk Reject", text = "Library book stacks crumble after one bounce" },
                new HowToRow { icon = art.drone, name = "Reviewer 2", text = "Bounce on it from above. A hit costs 6 s and 2 paper" },
                new HowToRow { icon = art.coffee, name = "Coffee cups", text = "Fall from above: watch for the ! at the top" },
                new HowToRow { icon = art.imposter, name = "Imposter syndrome", text = "Makes platforms vanish. Flash it away!" },
                new HowToRow { icon = S("Assets/Art/Siegmund2/south.png"), name = "Prof. Siegmund", text = "Dodge red pens and boxes, hit him with thrown Mate" },
            };
            how.keys = new List<HowToKey>
            {
                new HowToKey { key = "← / →  or  A / D", action = "Steer (leave one side, come back on the other)" },
                new HowToKey { key = "nothing", action = "Jumping is automatic: Johannes bounces on every landing" },
                new HowToKey { key = "Q", action = "Camera flash: stuns drones, banishes shadows, takes photos" },
                new HowToKey { key = "Mouse click", action = "Defense: throw a Mate where the mouse points" },
                new HowToKey { key = "Ctrl+Shift+R", action = "Restart the level" },
            };
            how.gamepadNote = "Gamepad: stick or d-pad steer, B flash, X throws Mate at the professor";

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildAfterLevel3();
            Debug.Log($"[GetYourPhD] Built {ScenePath}");
        }

        static void AddToBuildAfterLevel3()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int i = scenes.FindIndex(s => s.path.Contains("Level3_"));
            scenes.Insert(i >= 0 ? i + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
