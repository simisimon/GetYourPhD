using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace GetYourPhD.EditorTools
{
    /// <summary>
    /// Builds Level 2 "Home Sweet Home Office" from Assets/Levels/Level2_HomeOffice.txt: the whole flat on one screen,
    /// seen from the side like a dollhouse. Rows are top to bottom, one char per 32x32 tile.
    ///
    /// Legend
    ///   #  wall / floor            P  Johannes start          H  spawn in the yard after submitting
    ///   D  desk (typing + tool wall)  K  crib (daughter)      L  washing machine      O  stove (wife cooks left of it)
    ///   R  front door (parcel)     F  fridge (Mate refill)
    ///   b  bookshelf  B  bed  y  toys  w  window  o  photo spot (wedding photo)  i  idea spawn spot
    ///   G  Rebuttal Room door      ?  tip sign
    /// Rooms (wallpaper, labels, lamps) are defined in <see cref="Rooms"/>.
    /// </summary>
    public static class HomeLevelBuilder
    {
        const string MapPath = "Assets/Levels/Level2_HomeOffice.txt";
        const string ScenePath = "Assets/Scenes/Level2_HomeOffice.unity";
        const string HomeArt = "Assets/Art/Home";
        const string GenDir = "Assets/Art/Home/Gen";

        static readonly string[] Signs =
        {
            "Rebuttal time! Win the real-life challenge, then enter the acceptance code at the door of the Rebuttal Room.",
        };

        struct Room
        {
            public string name; public int x0, x1, y0, y1; public Color a, b; public int pattern; public Vector2 lamp; public Color lampColor;
            public Room(string n, int x0, int x1, int y0, int y1, Color a, Color b, int pattern, Vector2 lamp, Color lampColor)
            { name = n; this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; this.a = a; this.b = b; this.pattern = pattern; this.lamp = lamp; this.lampColor = lampColor; }
        }

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        static readonly Room[] Rooms =
        {
            new Room("KITCHEN", 1, 7, 1, 5, C(236, 214, 150), C(222, 196, 128), 1, new Vector2(4, 5.2f), C(255, 220, 160)),
            new Room("OFFICE", 8, 15, 1, 5, C(150, 178, 150), C(136, 164, 136), 0, new Vector2(10.6f, 3.4f), C(255, 230, 170)),
            new Room("HALL", 16, 22, 1, 6, C(206, 186, 160), C(192, 172, 146), 0, new Vector2(20.5f, 5.2f), C(255, 214, 150)),
            new Room("NURSERY", 1, 7, 7, 11, C(240, 186, 196), C(250, 214, 220), 2, new Vector2(2.5f, 9.5f), C(255, 170, 200)),
            new Room("BATHROOM", 8, 16, 7, 11, C(170, 206, 226), C(150, 190, 214), 3, new Vector2(12, 11.2f), C(220, 240, 255)),
            new Room("BEDROOM", 17, 22, 7, 11, C(180, 168, 214), C(166, 154, 200), 0, new Vector2(20.5f, 11.2f), C(255, 210, 170)),
        };

        class Ctx
        {
            public string[] rows; public int W, H; public Font font;
            public char At(int x, int y)
            {
                if (y < 0 || y >= H || x < 0) return '.';
                string r = rows[H - 1 - y];
                return x < r.Length ? r[x] : '.';
            }
        }

        [MenuItem("GetYourPhD/Build Level 2 (Home Office)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var ctx = new Ctx();
            ctx.rows = File.ReadAllLines(MapPath).Where(l => l.Length > 0).ToArray();
            ctx.H = ctx.rows.Length;
            ctx.W = ctx.rows.Max(r => r.Length);
            Directory.CreateDirectory(GenDir);
            GenerateTextures();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ctx.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            if (!ctx.font) ctx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // --- Camera: whole flat on one screen (30 x 16.9 tiles, 2x pixels at 1080p) ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.4375f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.16f);
            camGo.transform.position = new Vector3(ctx.W * 0.5f, 8.4375f, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var global = lightGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.8f;

            // Leipzig skyline behind the house
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/StartScreen/campus_bg.png");
            SpriteRenderer skyline = null;
            if (bgSprite)
            {
                skyline = LevelBuilder.NewSprite("Skyline", bgSprite, null, new Vector3(ctx.W * 0.5f, 8.4375f, 20), -100);
                float s = 17.5f / (bgSprite.rect.height / 32f);
                skyline.transform.localScale = new Vector3(s * 1.2f, s, 1);
                skyline.color = new Color(0.45f, 0.45f, 0.62f);
            }

            var house = new GameObject("House").transform;
            var interior = Load($"{GenDir}/interior.png");
            LevelBuilder.NewSprite("Interior", interior, house, new Vector3(12f, 6f, 5), -50);
            var roof = Load($"{GenDir}/roof.png");
            LevelBuilder.NewSprite("Roof", roof, house, new Vector3(12f, 13f + 1.5f, 5), -40);
            foreach (var r in Rooms)
                Label(ctx, house, r.name, new Vector2((r.x0 + r.x1 + 1) * 0.5f, Mathf.Min(r.y1, r.y0 + 4) + 0.3f), 0.06f, new Color(0.2f, 0.15f, 0.25f, 0.45f), -30);

            // --- Terrain ---
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var colGo = new GameObject("Collision");
            colGo.transform.SetParent(gridGo.transform, false);
            var colMap = colGo.AddComponent<Tilemap>();
            colGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var tmc = colGo.AddComponent<TilemapCollider2D>();
            tmc.compositeOperation = Collider2D.CompositeOperation.Merge;
            colGo.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
            var visGo = new GameObject("Terrain");
            visGo.transform.SetParent(gridGo.transform, false);
            var visMap = visGo.AddComponent<Tilemap>();
            visGo.AddComponent<TilemapRenderer>().sortingOrder = 0;

            var collisionTile = LevelBuilder.GetCollisionTile();
            var wood = TileFor("wood"); var brick = TileFor("brick"); var grass = TileFor("grass"); var stair = TileFor("stair");
            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                {
                    if (ctx.At(x, y) != '#') continue;
                    var cell = new Vector3Int(x, y, 0);
                    colMap.SetTile(cell, collisionTile);
                    Tile t = x > 23 ? grass : (x == 0 || x == 23) ? brick : (y == 0 ? wood : (y >= 1 && y <= 5 ? stair : wood));
                    if (y == 0 && x == 23) t = brick;
                    visMap.SetTile(cell, t);
                }
            tmc.ProcessTilemapChanges();
            colGo.GetComponent<CompositeCollider2D>().GenerateGeometry();
            // One-way stair steps ('='): jump up through them from below, walk underneath on the floor.
            var plank = Load($"{GenDir}/plank.png");
            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                {
                    if (ctx.At(x, y) != '=' || ctx.At(x - 1, y) == '=') continue;
                    int x1 = x; while (ctx.At(x1 + 1, y) == '=') x1++;
                    var step = new GameObject("Step");
                    step.transform.SetParent(gridGo.transform, false);
                    var bc = step.AddComponent<BoxCollider2D>();
                    bc.offset = new Vector2((x + x1 + 1) * 0.5f, y + 0.875f);
                    bc.size = new Vector2(x1 - x + 1, 0.25f);
                    bc.usedByEffector = true;
                    var eff = step.AddComponent<PlatformEffector2D>();
                    eff.useOneWay = true;
                    eff.surfaceArc = 160f;
                    for (int cx2 = x; cx2 <= x1; cx2++)
                        LevelBuilder.NewSprite("Plank", plank, step.transform, new Vector3(cx2 + 0.5f, y + 0.875f, 0), 1);
                }

            // Invisible edges left and right
            Edge("EdgeLeft", new Vector2(-0.5f, ctx.H * 0.5f), new Vector2(1, ctx.H * 2));
            Edge("EdgeRight", new Vector2(ctx.W + 0.5f, ctx.H * 0.5f), new Vector2(1, ctx.H * 2));

            // --- Objects ---
            var objects = new GameObject("Objects").transform;
            var mate = LevelBuilder.LoadSprite("mate", new Color(1f, 0.6f, 0.1f));
            var idea = LevelBuilder.LoadSprite("result", new Color(0.3f, 1f, 0.4f));
            var windowSprites = new List<SpriteRenderer>();
            var ideaSpots = new List<Transform>();
            var tasks = new List<HomeTask>();
            Vector2 start = new Vector2(13.5f, 1f), hallway = new Vector2(25.5f, 1.1f), stovePos = new Vector2(5.5f, 1f);
            DeskWorkstation desk = null; HomeTask toolTask = null; DeskToolFall tools = null;
            var signPositions = new List<Vector2Int>();

            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                {
                    char c = ctx.At(x, y);
                    float cx = x + 0.5f;
                    switch (c)
                    {
                        case 'P': start = new Vector2(cx, y); break;
                        case 'H': hallway = new Vector2(cx, y + 0.1f); break;
                        case 'i': { var t = new GameObject("IdeaSpot").transform; t.SetParent(objects); t.position = new Vector3(cx, y + 0.5f, 0); ideaSpots.Add(t); break; }
                        case '?': signPositions.Add(new Vector2Int(x, y)); break;
                        case 'b': Decor(objects, "bookshelf", new Vector2(cx, y), -10); break;
                        case 'B': Decor(objects, "bed", new Vector2(cx - 0.1f, y), -10); break;
                        case 'y': Decor(objects, "toys", new Vector2(cx, y), 2); break;
                        case 'w':
                        {
                            var glass = LevelBuilder.NewSprite("WindowGlass", Effects_Pixel(), objects, new Vector3(cx, y + 0.75f, 0), -20);
                            glass.transform.localScale = new Vector3(16f * 0.9f, 16f * 1.2f, 1);
                            windowSprites.Add(glass);
                            LevelBuilder.NewSprite("WindowFrame", Load($"{GenDir}/window.png"), objects, new Vector3(cx, y + 0.75f, 0), -19);
                            break;
                        }
                        case 'o':
                        {
                            LevelBuilder.NewSprite("WeddingPhoto", Load($"{HomeArt}/wedding.png"), objects, new Vector3(cx, y + 0.5f, 0), -15);
                            var ps = new GameObject("PhotoSpot_Wedding");
                            ps.transform.SetParent(objects);
                            ps.transform.position = new Vector3(cx, y - 0.5f, 0);
                            var cc = ps.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 1.5f;
                            var spot = ps.AddComponent<PhotoSpot>(); spot.spotId = "L2_photo1"; spot.caption = "Wedding day";
                            break;
                        }
                        case 'D':
                        {
                            var go = Station(objects, "Desk", "desk", new Vector2(cx + 0.5f, y), new Vector2(3f, 2f), -5);
                            desk = go.AddComponent<DeskWorkstation>();
                            desk.font = ctx.font;
                            var wall = Load($"{HomeArt}/toolwall.png");
                            if (wall) LevelBuilder.NewSprite("ToolWall", wall, go.transform, new Vector3(0, 3.85f, 0), -18).transform.localScale = new Vector3(1.3f, 1.3f, 1);
                            toolTask = Task(go, ctx, "Tools on the desk", "CLONK!", "Hang the tools back up", "Tools back on the wall", 20f, 0.45f, false, false, null, null, null, 3.0f);
                            toolTask.externalActivation = true;
                            toolTask.bubbleOffsetX = -2.9f; toolTask.bubbleHeight = 2.7f;
                            toolTask.escalationPenaltySeconds = 5f;
                            tools = go.AddComponent<DeskToolFall>();
                            tools.toolTask = toolTask;
                            tools.toolSprites = new[] { "hammer", "wrench", "drill", "saw" }.Select(n => Load($"{HomeArt}/tool_{n}.png")).Where(t => t).ToArray();
                            float dx = cx + 0.5f;
                            tools.wallMin = new Vector2(dx - 1.5f, y + 3.0f); tools.wallMax = new Vector2(dx + 1.5f, y + 4.6f);
                            tools.deskTopY = y + 1.25f; tools.deskMinX = dx - 1.1f; tools.deskMaxX = dx + 1.1f;
                            desk.toolTask = toolTask; desk.tools = tools;
                            break;
                        }
                        case 'K':
                        {
                            var go = Station(objects, "Crib", "crib_calm", new Vector2(cx + 0.5f, y), new Vector2(3f, 2f), -5);
                            var t = Task(go, ctx, "Daughter is crying", "WAAAH!", "Rock the crib", "Back asleep", 11f, 1.5f, true, true,
                                go.GetComponentInChildren<SpriteRenderer>(), Load($"{HomeArt}/Bottom/crib_calm.png"), Load($"{HomeArt}/Bottom/crib_cry.png"), 2.6f);
                            t.mateFixText = "pacifier delivered"; t.flashFixText = "she giggles at the flash";
                            tasks.Add(t);
                            break;
                        }
                        case 'L':
                        {
                            var go = Station(objects, "WashingMachine", "washer", new Vector2(cx, y), new Vector2(2.6f, 2f), -5);
                            tasks.Add(Task(go, ctx, "Washing machine is flooding", "SPLOSH!", "Turn it off", "Laundry saved", 10f, 1.2f, true, false, go.GetComponentInChildren<SpriteRenderer>(), null, null, 2.6f));
                            break;
                        }
                        case 'O':
                            Decor(objects, "stove", new Vector2(cx, y), -5);
                            stovePos = new Vector2(cx, y);
                            break;
                        case 'R':
                        {
                            var go = Station(objects, "FrontDoor", "frontdoor", new Vector2(cx + 0.2f, y), new Vector2(2.6f, 2f), -5);
                            tasks.Add(Task(go, ctx, "Parcel delivery", "DING DONG!", "Sign for the parcel", "Parcel received", 7f, 0.7f, false, false, go.GetComponentInChildren<SpriteRenderer>(), null, null, 3f));
                            break;
                        }
                        case 'F':
                        {
                            var go = Station(objects, "Fridge", "fridge", new Vector2(cx, y), new Vector2(2.2f, 2f), -5);
                            var f = go.AddComponent<MateFridge>();
                            f.label = Label(ctx, go.transform, "MATE (E)", new Vector2(cx, y + 2.2f), 0.06f, new Color(1f, 0.8f, 0.3f), 50);
                            break;
                        }
                        case 'G': MakeGate(ctx, objects, new Vector2(cx, y)); break;
                    }
                }

            var signSprite = LevelBuilder.LoadSprite("Bottom/infosign", new Color(0.3f, 0.5f, 0.9f));
            for (int i = 0; i < signPositions.Count && i < Signs.Length; i++)
            {
                var sr = LevelBuilder.NewSprite("TipSign", signSprite, objects, new Vector3(signPositions[i].x + 0.5f, signPositions[i].y, 0), 3);
                var box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true; box.size = new Vector2(3f, 3f); box.offset = new Vector2(0, 1.5f);
                sr.gameObject.AddComponent<TipSign>().tip = Signs[i];
            }

            // --- Cat ---
            var wife = MakeWife(ctx, objects, new Vector2(stovePos.x - 1.2f, stovePos.y));

            // --- Lamps ---
            var lamps = new List<Light2D>();
            var lampRoot = new GameObject("Lamps").transform;
            foreach (var r in Rooms)
            {
                var lg = new GameObject("Lamp_" + r.name);
                lg.transform.SetParent(lampRoot);
                lg.transform.position = new Vector3(r.lamp.x, r.lamp.y, 0);
                var l = lg.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = r.lampColor;
                l.intensity = 0.7f;
                l.pointLightInnerRadius = 0.5f;
                l.pointLightOuterRadius = 5.5f;
                lamps.Add(l);
            }

            // --- Player ---
            var player = LevelBuilder.MakePlayer(start, mate);
            player.GetComponent<PlayerController>().mateCount = 3;

            // --- Game systems ---
            var game = new GameObject("Game");
            var clock = game.AddComponent<DeadlineClock>();
            clock.levelDurationSeconds = 240f;
            var meter = game.AddComponent<PaperMeter>();
            meter.maxPoints = 100;
            var lm = game.AddComponent<LevelManager>();
            lm.levelId = "L2";
            lm.levelTitle = "Level 2: Home Sweet Home Office";
            lm.nextScene = "Level3_CamperRoadTrip";
            lm.nextLevelTitle = "Level 3: The Camper Road Trip";
            var hs = new GameObject("HallwaySpawn").transform;
            hs.SetParent(game.transform);
            hs.position = hallway;
            lm.hallwaySpawn = hs;
            var dir = game.AddComponent<HomeOfficeDirector>();
            dir.tasks = tasks.ToArray();
            dir.toolTask = toolTask;
            dir.tools = tools;
            dir.wife = wife;
            dir.ideaSpots = ideaSpots.ToArray();
            dir.ideaSprite = idea;
            dir.globalLight = global;
            dir.lamps = lamps.ToArray();
            dir.windows = windowSprites.ToArray();
            dir.skyline = skyline;
            var overlay = LevelBuilder.NewSprite("NightOverlay", Effects_Pixel(), null, new Vector3(ctx.W * 0.5f, 8.4375f, 0), 7);
            overlay.transform.localScale = new Vector3(16f * 40f, 16f * 20f, 1);
            overlay.color = new Color(0, 0, 0, 0);
            dir.nightOverlay = overlay;
            var hud = new GameObject("HUD").AddComponent<HUD>();
            hud.font = ctx.font;
            hud.mateIcon = mate;

            // How to play (shown before the level starts)
            var how = game.AddComponent<HowToPlayOverlay>();
            how.levelId = "L2";
            how.font = ctx.font;
            how.title = "LEVEL 2: HOME SWEET HOME OFFICE";
            how.intro = "No running and jumping this time: the paper is written at home. The <b>Paper</b> meter only grows while Johannes " +
                        "<b>stands still at his desk</b>. Meanwhile, life happens in the flat: fix problems before they get out of hand. " +
                        "At 100%, press E at the desk to <b>submit</b> before <b>13:59 AOE</b>, then head to the <b>Rebuttal Room</b> in the yard.";
            how.rowsCaption = "AT HOME";
            var hammer = Load($"{HomeArt}/tool_hammer.png");
            how.rows = new List<HowToRow>
            {
                new HowToRow { icon = Load($"{HomeArt}/Bottom/desk.png"), name = "Desk", text = "Stand still to type. Every open problem in the flat costs focus" },
                new HowToRow { icon = Load($"{HomeArt}/Bottom/crib_cry.png"), name = "Daughter", text = "Crying: hold E at the crib, throw Mate or use the flash" },
                new HowToRow { icon = Load($"{HomeArt}/Bottom/washer.png"), name = "Washing machine", text = "Flooding: hold E next to it or throw Mate" },
                new HowToRow { icon = Load($"{HomeArt}/Bottom/frontdoor.png"), name = "Doorbell", text = "Parcel: hold E at the front door" },
                new HowToRow { icon = hammer, name = "Tool wall", text = "Tools fall on the desk and slow typing. Hold E at the desk" },
                new HowToRow { icon = Load($"{HomeArt}/Bottom/wife/east.png"), name = "Wife", text = "Cooks and jokes. Orders: walk to her and hold E" },
                new HowToRow { icon = Load($"{HomeArt}/Bottom/fridge.png"), name = "Fridge", text = "Press E for 2 Mate (then it needs to restock)" },
                new HowToRow { icon = idea, name = "Idea", text = "Pops up somewhere in the flat: +3 paper" },
                new HowToRow { icon = null, name = "Ignored problem", text = "Gets out of hand: -10 s and -3 paper, again and again" },
            };
            how.keys = new List<HowToKey>
            {
                new HowToKey { key = "A / D  or  ← / →", action = "Move" },
                new HowToKey { key = "Space / W / ↑", action = "Jump (through the hall steps to go upstairs)" },
                new HowToKey { key = "Hold E", action = "Fix a problem, hang tools back up, help your wife" },
                new HowToKey { key = "E at the desk", action = "Submit when the paper is at 100%" },
                new HowToKey { key = "Right click", action = "Throw Mate (fixes problems from afar)" },
                new HowToKey { key = "Left click", action = "Drink Mate (run and type faster)" },
                new HowToKey { key = "Q", action = "Camera flash: calms the daughter, takes photos" },
            };

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildAfterLevel1();
            Debug.Log($"[GetYourPhD] Built {ScenePath}");
        }

        // ---------------- objects ----------------

        static GameObject Station(Transform parent, string name, string art, Vector2 pos, Vector2 zone, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var sprite = Load($"{HomeArt}/Bottom/{art}.png");
            if (!sprite) sprite = LevelBuilder.MakePlaceholder("ph_home_" + art, new Color(0.7f, 0.6f, 0.5f), 32, 32, true);
            LevelBuilder.NewSprite("Sprite", sprite, go.transform, Vector3.zero, order);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = zone;
            box.offset = new Vector2(0, zone.y * 0.5f);
            return go;
        }

        static void Decor(Transform parent, string art, Vector2 pos, int order)
        {
            var sprite = Load($"{HomeArt}/Bottom/{art}.png");
            if (sprite) LevelBuilder.NewSprite(art, sprite, parent, pos, order);
        }

        static HomeTask Task(GameObject go, Ctx ctx, string name, string bubble, string verb, string done, float patience, float hold,
            bool mate, bool flash, SpriteRenderer target, Sprite calm, Sprite active, float bubbleHeight)
        {
            var t = go.AddComponent<HomeTask>();
            t.taskName = name; t.bubbleText = bubble; t.fixVerb = verb; t.fixedText = done;
            t.patience = patience; t.holdToFix = hold; t.fixableByMate = mate; t.fixableByFlash = flash;
            t.target = target; t.calmSprite = calm; t.activeSprite = active; t.font = ctx.font; t.bubbleHeight = bubbleHeight;
            return t;
        }

        static HomeWife MakeWife(Ctx ctx, Transform parent, Vector2 pos)
        {
            var go = new GameObject("Wife");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var sprGo = new GameObject("Sprite");
            sprGo.transform.SetParent(go.transform, false);
            var sr = sprGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;
            var anim = sprGo.AddComponent<SpriteAnimator>();
            anim.clips = new List<SpriteClip>();
            foreach (var (name, fps) in new[] { ("cook", 8f), ("idle", 5f) })
            {
                string dir = $"{HomeArt}/Bottom/wife/{name}";
                if (!Directory.Exists(dir)) continue;
                var frames = Directory.GetFiles(dir, "*.png").OrderBy(f => f).Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace('\\', '/'))).Where(s => s).ToArray();
                if (frames.Length > 0) anim.clips.Add(new SpriteClip { name = name, frames = frames, fps = fps, loop = true });
            }
            var still = Load($"{HomeArt}/Bottom/wife/east.png");
            if (still && !anim.clips.Any(c => c.name == "idle")) anim.clips.Add(new SpriteClip { name = "idle", frames = new[] { still }, fps = 1, loop = true });
            sr.sprite = still ? still : LevelBuilder.MakePlaceholder("ph_wife", new Color(0.4f, 0.7f, 0.5f), 16, 44, true);
            anim.defaultClip = "idle";
            var w = go.AddComponent<HomeWife>();
            w.body = sr; w.anim = anim;
            var bg = LevelBuilder.NewSprite("SpeechBg", Effects_Pixel(), go.transform, new Vector3(1.0f, 2.35f, 0), 40);
            bg.color = new Color(1f, 0.97f, 0.9f);
            w.speechBg = bg;
            w.speech = Label(ctx, go.transform, "", new Vector2(pos.x + 1.0f, pos.y + 2.35f), 0.068f, new Color(0.25f, 0.15f, 0.3f), 45);
            w.speech.anchor = TextAnchor.MiddleCenter;
            // Orders: walk to her and hold E
            var zone = go.AddComponent<BoxCollider2D>();
            zone.isTrigger = true; zone.size = new Vector2(1.6f, 2f); zone.offset = new Vector2(0, 1f);
            var order = Task(go, ctx, "Your wife needs a hand", "Set the table!", "Help", "Order done", 10f, 1.0f, false, false, null, null, null, 2.6f);
            order.externalActivation = true;
            order.bubbleOffsetX = 0.8f;
            w.orderTask = order;
            return w;
        }

        static void MakeGate(Ctx ctx, Transform parent, Vector2 pos)
        {
            var go = new GameObject("PeerReviewGate_2");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var zone = go.AddComponent<BoxCollider2D>();
            zone.isTrigger = true; zone.size = new Vector2(2.5f, 2f); zone.offset = new Vector2(-1.2f, 1f);
            var gate = go.AddComponent<PeerReviewGate>();
            gate.gateNumber = 2;
            var door = LevelBuilder.LoadSprite("door", new Color(0.5f, 0.3f, 0.15f), 32, 64);
            var d = LevelBuilder.NewSprite("Door", door, go.transform, new Vector3(0, door.rect.height / 64f, 0), 4);
            var dc = d.gameObject.AddComponent<BoxCollider2D>();
            dc.size = new Vector2(1f, door.rect.height / 32f);
            gate.doorCollider = dc; gate.doorSprite = d;
            Label(ctx, go.transform, "REBUTTAL ROOM", new Vector2(pos.x - 0.3f, pos.y + 2.4f), 0.045f, new Color(1f, 0.85f, 0.4f), 50);
        }

        static void Edge(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        static TextMesh Label(Ctx ctx, Transform parent, string text, Vector2 worldPos, float size, Color color, int order)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent);
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = ctx.font; tm.fontSize = 48; tm.characterSize = size;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = ctx.font.material;
            mr.sortingOrder = order;
            return tm;
        }

        static Sprite Effects_Pixel() => Load($"{GenDir}/white.png");

        static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static Tile TileFor(string name)
        {
            string path = $"{GenDir}/tile_{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (!t) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = Load($"{GenDir}/{name}.png");
            t.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(t);
            return t;
        }

        static void AddToBuildAfterLevel1()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int i = scenes.FindIndex(s => s.path.Contains("Level1_"));
            scenes.Insert(i >= 0 ? i + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------- generated pixel textures ----------------

        static void GenerateTextures()
        {
            Save("wood", 32, 32, (x, y) =>
            {
                int plank = y / 8;
                Color b = plank % 2 == 0 ? C(150, 98, 58) : C(138, 88, 50);
                if (y % 8 == 0) return C(96, 60, 34);
                if ((x + plank * 11) % 32 == 0) return C(104, 66, 38);
                if (y >= 30) return C(178, 124, 76);
                return (x * 7 + y * 3) % 23 == 0 ? b * 0.92f + new Color(0, 0, 0, 0.08f) : b;
            });
            Save("stair", 32, 32, (x, y) =>
            {
                if (y >= 28) return C(178, 124, 76);
                if (y >= 26) return C(110, 70, 40);
                return (x + y) % 9 == 0 ? C(120, 80, 48) : C(132, 86, 52);
            });
            Save("brick", 32, 32, (x, y) =>
            {
                int row = y / 8;
                int off = row % 2 == 0 ? 0 : 8;
                if (y % 8 == 0 || (x + off) % 16 == 0) return C(196, 186, 170);
                return (x * 5 + y * 3) % 13 == 0 ? C(160, 72, 56) : C(176, 82, 62);
            });
            Save("grass", 32, 32, (x, y) =>
            {
                if (y >= 27) return (x * 3 + y) % 5 == 0 ? C(120, 190, 80) : C(92, 160, 64);
                if (y >= 25) return C(70, 128, 52);
                return (x * 7 + y * 5) % 11 == 0 ? C(110, 80, 52) : C(128, 94, 62);
            });
            Save("window", 32, 40, (x, y) =>
            {
                bool frame = x < 3 || x > 28 || y < 3 || y > 36 || (x >= 15 && x <= 16) || (y >= 19 && y <= 20);
                if (!frame) return new Color(0, 0, 0, 0);
                return (x < 1 || x > 30 || y < 1 || y > 38) ? C(70, 60, 60) : C(240, 236, 226);
            });
            // Interior: 24 x 12 tiles, rooms painted with their wallpaper
            Save("interior", 24 * 32, 12 * 32, (x, y) =>
            {
                int tx = x / 32, ty = y / 32;
                foreach (var r in Rooms)
                {
                    if (tx < r.x0 || tx > r.x1 || ty < r.y0 || ty > r.y1) continue;
                    int lx = x - r.x0 * 32, ly = y - r.y0 * 32;
                    // skirting board and a darker band near the floor
                    if (ly < 4) return C(240, 232, 214);
                    if (ly < 6) return C(120, 100, 90);
                    switch (r.pattern)
                    {
                        case 1: // kitchen: checker tiles below 40 px
                            if (ly < 40) return ((lx / 8 + ly / 8) % 2 == 0) ? C(240, 240, 232) : C(200, 214, 220);
                            return r.a;
                        case 2: // nursery: dots
                            return (lx % 16 == 4 && ly % 16 == 4) || (lx % 16 == 12 && ly % 16 == 12) ? r.b : r.a;
                        case 3: // bathroom tiles
                            return (lx % 12 == 0 || ly % 12 == 0) ? r.b : r.a;
                        default: // vertical stripes
                            return (lx / 6) % 3 == 0 ? r.b : r.a;
                    }
                }
                return C(60, 50, 50); // behind walls and slabs
            });
            // Roof: trapezoid 26 x 3 tiles
            Save("roof", 26 * 32, 3 * 32, (x, y) =>
            {
                int inset = (y * 26 * 32 / 3) / (96 * 3);
                int w = 26 * 32;
                if (x < inset || x >= w - inset) return new Color(0, 0, 0, 0);
                if (y < 4) return C(110, 50, 40);
                int row = y / 8;
                int off = row % 2 == 0 ? 0 : 6;
                if (y % 8 == 0 || (x + off) % 12 == 0) return C(120, 48, 40);
                return C(170, 66, 52);
            });
            Save("white", 2, 2, (x, y) => Color.white);
            Save("plank", 32, 8, (x, y) =>
            {
                if (y == 0 || x == 0 || x == 31) return C(90, 56, 32);
                if (y >= 6) return C(186, 132, 82);
                return (x % 16 == 0) ? C(110, 70, 40) : C(150, 98, 58);
            });
            AssetDatabase.Refresh();
        }

        static void Save(string name, int w, int h, System.Func<int, int, Color> f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            tex.SetPixels(px);
            File.WriteAllBytes($"{GenDir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
