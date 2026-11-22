using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace GetYourPhD.EditorTools
{
    /// <summary>
    /// Builds a level scene from an ASCII map (Assets/Levels/*.txt). Rows are top to bottom, one char per 32x32 tile.
    ///
    /// Legend
    ///   #  solid lab floor/wall            P  Johannes start           H  hallway spawn (after submitting)
    ///   d  data point (1)   r  experiment result (3)   f  feedback (2)   p  paper draft (5)   M  Mate bottle
    ///   w  walking distraction (crashing benchmark job)     n  flying distraction (noisy measurement)
    ///   1 2 3  config switches (--cache, -O3, --threads=8)
    ///   a / b  shown / hidden when --cache is on
    ///   c      elevator that runs while -O3 is on (rises 6 tiles); e / g shown / hidden when -O3 is on
    ///   k / q  shown / hidden when --threads=8 is on
    ///   h  hidden fog platform (camera flash reveals)       o  photo spot
    ///   T  boss arena trigger column    W  arena wall that closes when the fight starts
    ///   X  boss (bottom-left tile)      m  boss-fight Mate refill point     S  Submit button (appears after the boss)
    ///   G  Peer Review Gate door        ?  sign (texts in order from left to right)
    /// </summary>
    public static class LevelBuilder
    {
        const string ArtRoot = "Assets/Art";
        const string SpriteDir = "Assets/Art/Sprites";
        const string TileDir = "Assets/Art/Tiles/Lab";
        const string GenDir = "Assets/Art/Generated";

        static readonly string[] Level1Signs =
        {
            "A/D or arrows: run     Space: jump, again in the air: double jump",
            "Stomp distractions or throw Mate (right click).\nDrink Mate (left click) to outrun the deadline slowdown.",
            "Config switch ahead: stand next to it and press E,\nor hit it with a thrown Mate.",
            "Measurement fog! Camera flash (Q) reveals hidden platforms.\nFlash next to a photo spot to take a photo.",
            "The Benchmark Cluster guards the Submit button.\nWait until it overheats, then throw Mate into its vents!",
            "Rebuttal time! Win the real-life challenge, then enter the acceptance code at the door of the Rebuttal Room.",
        };

        [MenuItem("GetYourPhD/Build Level 1 (Measurement Lab)")]
        public static void BuildLevel1()
        {
            Build("Assets/Levels/Level1_MeasurementLab.txt", "Assets/Scenes/Level1_MeasurementLab.unity", Level1Signs);
        }

        class Ctx
        {
            public string[] rows;
            public int W, H;
            public char At(int x, int y)
            {
                if (y < 0 || y >= H || x < 0) return '.';
                string r = rows[H - 1 - y];
                return x < r.Length ? r[x] : '.';
            }
            public Dictionary<int, Tile> wang;
            public Font font;
        }

        public static void Build(string mapPath, string scenePath, string[] signs)
        {
            AssetDatabase.Refresh();
            var ctx = new Ctx();
            ctx.rows = File.ReadAllLines(mapPath).Where(l => l.Length > 0).ToArray();
            ctx.H = ctx.rows.Length;
            ctx.W = ctx.rows.Max(r => r.Length);
            Directory.CreateDirectory(GenDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets only after NewScene: creating a scene unloads unreferenced assets.
            ctx.wang = LoadWangTiles();
            // Pixel game font (same as the start screen); falls back to the built-in font.
            ctx.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            if (!ctx.font) ctx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // --- Camera, light, background ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.625f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.1f, 0.18f);
            camGo.transform.position = new Vector3(10, 6, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 32;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;
            var follow = camGo.AddComponent<CameraFollow>();
            follow.minBounds = new Vector2(0, 0);
            follow.maxBounds = new Vector2(ctx.W, ctx.H);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var bgSprite = LoadSprite("lab_bg", new Color(0.12f, 0.17f, 0.3f), 320, 180);
            var bg = NewSprite("Background", bgSprite, camGo.transform, new Vector3(0, 0, 20), -100);
            float bgScale = 20f / (bgSprite.rect.width / 32f) + 0.05f;
            bg.transform.localScale = new Vector3(bgScale, bgScale, 1);
            bg.color = new Color(0.45f, 0.5f, 0.62f);

            // --- Terrain ---
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var collisionTile = GetCollisionTile();

            var colGo = new GameObject("Collision");
            colGo.transform.SetParent(gridGo.transform, false);
            var colMap = colGo.AddComponent<Tilemap>();
            var colRb = colGo.AddComponent<Rigidbody2D>();
            colRb.bodyType = RigidbodyType2D.Static;
            var tmc = colGo.AddComponent<TilemapCollider2D>();
            tmc.compositeOperation = Collider2D.CompositeOperation.Merge;
            var comp = colGo.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var visGo = new GameObject("Terrain");
            visGo.transform.SetParent(gridGo.transform, false);
            visGo.transform.localPosition = new Vector3(-0.5f, -0.5f, 0);
            var visMap = visGo.AddComponent<Tilemap>();
            visGo.AddComponent<TilemapRenderer>().sortingOrder = 0;

            var solid = new HashSet<Vector2Int>();
            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                    if (ctx.At(x, y) == '#') { solid.Add(new Vector2Int(x, y)); colMap.SetTile(new Vector3Int(x, y, 0), collisionTile); }
            PaintDualGrid(visMap, solid, ctx.wang, Vector2Int.zero);
            tmc.ProcessTilemapChanges();
            comp.GenerateGeometry();

            // --- Groups (config / hidden / arena wall) ---
            var groupsRoot = new GameObject("Platforms").transform;
            groupsRoot.SetParent(gridGo.transform, false);
            var arenaWalls = new List<GameObject>();
            var visited = new HashSet<Vector2Int>();
            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                {
                    char c = ctx.At(x, y);
                    if ("abcegkqhW".IndexOf(c) < 0 || visited.Contains(new Vector2Int(x, y))) continue;
                    var cells = Flood(ctx, x, y, c, visited);
                    var g = MakeGroup(ctx, groupsRoot, c, cells);
                    if (c == 'W') arenaWalls.Add(g);
                }

            // --- Objects ---
            var objects = new GameObject("Objects").transform;
            var sprites = new Dictionary<string, Sprite>
            {
                ["mate"] = LoadSprite("mate", new Color(1f, 0.6f, 0.1f)),
                ["datapoint"] = LoadSprite("datapoint", new Color(0.3f, 0.9f, 1f)),
                ["result"] = LoadSprite("result", new Color(0.3f, 1f, 0.4f)),
                ["feedback"] = LoadSprite("feedback", new Color(1f, 0.9f, 0.3f)),
                ["draft"] = LoadSprite("draft", Color.white),
                ["submit"] = LoadSprite("submit", Color.red),
                ["walker"] = LoadSprite("walker", new Color(0.9f, 0.8f, 0.6f)),
                ["flyer"] = LoadSprite("flyer", new Color(1f, 0.3f, 0.3f)),
                ["switch"] = LoadSprite("switch", Color.gray),
                ["photo"] = LoadSprite("photo", new Color(0.9f, 0.9f, 1f)),
                ["door"] = LoadSprite("door", new Color(0.5f, 0.3f, 0.15f), 32, 64),
                ["boss"] = LoadSprite("Bottom/boss", new Color(0.2f, 0.2f, 0.25f), 96, 96),
            };

            Vector2 start = new Vector2(2.5f, 2f), hallway = Vector2.zero;
            BenchmarkClusterBoss boss = null;
            GameObject submitGo = null;
            Transform mateSpawn = null;
            var triggerCells = new List<Vector2Int>();
            var signPositions = new List<Vector2Int>();
            int photoIndex = 0;

            for (int y = 0; y < ctx.H; y++)
                for (int x = 0; x < ctx.W; x++)
                {
                    char c = ctx.At(x, y);
                    Vector2 cell = new Vector2(x + 0.5f, y + 0.5f);
                    switch (c)
                    {
                        case 'P': start = new Vector2(x + 0.5f, y); break;
                        case 'H': hallway = new Vector2(x + 0.5f, y + 0.1f); break;
                        case 'd': MakeCollectible(objects, sprites["datapoint"], cell, CollectibleKind.DataPoint); break;
                        case 'r': MakeCollectible(objects, sprites["result"], cell, CollectibleKind.ExperimentResult); break;
                        case 'f': MakeCollectible(objects, sprites["feedback"], cell, CollectibleKind.Feedback); break;
                        case 'p': MakeCollectible(objects, sprites["draft"], cell, CollectibleKind.PaperDraft); break;
                        case 'M': MakeCollectible(objects, sprites["mate"], cell, CollectibleKind.Mate); break;
                        case 'w': MakeWalker(objects, sprites["walker"], new Vector2(x + 0.5f, y + 0.45f)); break;
                        case 'n': MakeFlyer(objects, sprites["flyer"], cell); break;
                        case '1': MakeSwitch(ctx, objects, sprites["switch"], cell, "--cache"); break;
                        case '2': MakeSwitch(ctx, objects, sprites["switch"], cell, "-O3"); break;
                        case '3': MakeSwitch(ctx, objects, sprites["switch"], cell, "--threads=8"); break;
                        case 'o': MakePhoto(objects, sprites["photo"], cell, ++photoIndex); break;
                        case 'T': triggerCells.Add(new Vector2Int(x, y)); break;
                        case 'X': boss = MakeBoss(objects, sprites["boss"], new Vector2(x + 1.5f, y), sprites["walker"], sprites["mate"]); break;
                        case 'm': mateSpawn = new GameObject("MateRefillPoint").transform; mateSpawn.SetParent(objects); mateSpawn.position = cell; break;
                        case 'S':
                            submitGo = MakeSubmit(objects, sprites["submit"], cell);
                            MakeLabel(ctx, submitGo.transform, "SUBMIT", new Vector2(cell.x, cell.y + 1.1f), 0.06f, new Color(1f, 0.5f, 0.45f));
                            break;
                        case 'G': MakeGate(ctx, objects, sprites["door"], new Vector2(x + 0.5f, y)); break;
                        case '?': signPositions.Add(new Vector2Int(x, y)); break;
                    }
                }

            var signSprite = LoadSprite("Bottom/infosign", new Color(0.3f, 0.5f, 0.9f));
            for (int i = 0; i < signPositions.Count && i < signs.Length; i++)
            {
                var sr = NewSprite("TipSign", signSprite, objects, new Vector3(signPositions[i].x + 0.5f, signPositions[i].y, 0), 3);
                var box = sr.gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(3f, 3f);
                box.offset = new Vector2(0, 1.5f);
                sr.gameObject.AddComponent<TipSign>().tip = signs[i].Replace("\n", " ");
            }

            if (boss)
            {
                // Mate spawn spots: the top of every floating platform between the arena entrance and the boss.
                int arenaStart = triggerCells.Count > 0 ? triggerCells.Min(v => v.x) : Mathf.FloorToInt(boss.transform.position.x) - 25;
                int arenaEnd = Mathf.FloorToInt(boss.transform.position.x) - 2;
                var spots = new List<Transform>();
                var spotRoot = new GameObject("BossMateSpots").transform;
                spotRoot.SetParent(objects);
                for (int x = arenaStart + 1; x < arenaEnd; x++)
                    for (int y = 3; y < ctx.H - 1; y++)
                        if (ctx.At(x, y) == '#' && ctx.At(x, y + 1) != '#')
                        {
                            var sp = new GameObject("MateSpot").transform;
                            sp.SetParent(spotRoot);
                            sp.position = new Vector3(x + 0.5f, y + 1.5f, 0);
                            spots.Add(sp);
                        }
                if (spots.Count == 0 && mateSpawn) spots.Add(mateSpawn);
                boss.mateSpawnPoints = spots.ToArray();
                boss.enableOnStart = arenaWalls.ToArray();
                foreach (var wgo in arenaWalls) wgo.SetActive(false);
                if (submitGo) { boss.enableOnDefeat = new[] { submitGo }; submitGo.SetActive(false); }
                if (triggerCells.Count > 0)
                {
                    int tx = triggerCells.Min(v => v.x), ty0 = triggerCells.Min(v => v.y), ty1 = triggerCells.Max(v => v.y);
                    var tgo = new GameObject("BossArenaTrigger");
                    tgo.transform.SetParent(objects);
                    tgo.transform.position = new Vector3(tx + 0.5f, (ty0 + ty1 + 1) * 0.5f, 0);
                    var bc = tgo.AddComponent<BoxCollider2D>();
                    bc.isTrigger = true;
                    bc.size = new Vector2(1f, ty1 - ty0 + 1);
                    var pt = tgo.AddComponent<PlayerTrigger>();
                    UnityEventTools.AddVoidPersistentListener(pt.onEnter, boss.Activate);
                }
            }

            // --- Player ---
            var player = MakePlayer(start, sprites["mate"]);
            follow.target = player.transform;
            camGo.transform.position = new Vector3(start.x + 6, start.y + 4, -10);

            // --- Game systems ---
            var game = new GameObject("Game");
            var clock = game.AddComponent<DeadlineClock>();
            clock.levelDurationSeconds = 240f;
            game.AddComponent<PaperMeter>();
            var lm = game.AddComponent<LevelManager>();
            lm.levelId = "L1";
            lm.levelTitle = "Level 1: The Measurement Lab";
            lm.nextScene = "Level2_HomeOffice";
            var hs = new GameObject("HallwaySpawn").transform;
            hs.SetParent(game.transform);
            hs.position = hallway;
            lm.hallwaySpawn = hs;
            var hud = new GameObject("HUD").AddComponent<HUD>();
            hud.font = ctx.font;
            hud.mateIcon = sprites["mate"];

            // How to play (shown before the level starts)
            var how = game.AddComponent<HowToPlayOverlay>();
            how.levelId = "L1";
            how.font = ctx.font;
            how.title = "LEVEL 1: THE MEASUREMENT LAB";
            how.intro = "Run and jump through the lab and collect research to fill the <b>Paper</b> meter (0-100%). Beat the Benchmark Cluster, " +
                        "then reach the <b>Submit</b> button before the deadline (<b>13:59 AOE</b>). When time runs out, the paper is submitted " +
                        "automatically. The reviewers always want a rebuttal: head to the <b>Rebuttal Room</b> to win it.";
            how.rowsCaption = "COLLECTIBLES";
            how.rows = new List<HowToRow>
            {
                new HowToRow { icon = sprites["datapoint"], name = "Data point", text = "+1 paper" },
                new HowToRow { icon = sprites["feedback"], name = "Feedback", text = "+2 paper" },
                new HowToRow { icon = sprites["result"], name = "Experiment result", text = "+3 paper" },
                new HowToRow { icon = sprites["draft"], name = "Paper draft", text = "+5 paper" },
                new HowToRow { icon = sprites["mate"], name = "Mate", text = "Drink: 5 s speed boost. Throw: hits distractions, switches, boss" },
                new HowToRow { icon = sprites["photo"], name = "Photo spot", text = "Hidden. Use the camera flash right next to it" },
                new HowToRow { icon = sprites["walker"], name = "Distractions", text = "A hit costs 10 s and 2 paper. Stomp them or throw Mate" },
            };
            how.keys = new List<HowToKey>
            {
                new HowToKey { key = "A / D  or  ← / →", action = "Move" },
                new HowToKey { key = "Space / W / ↑", action = "Jump (again in the air: double jump)" },
                new HowToKey { key = "Left click", action = "Drink Mate (speed boost)" },
                new HowToKey { key = "Right click", action = "Throw Mate" },
                new HowToKey { key = "Q", action = "Camera flash: stun, reveal platforms, take photos" },
                new HowToKey { key = "E / S / ↓", action = "Interact (config switches, doors)" },
            };

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuild(scenePath);
            Debug.Log($"[GetYourPhD] Built {scenePath} ({ctx.W}x{ctx.H} tiles)");
        }

        // ---------------- helpers ----------------

        static Dictionary<int, Tile> LoadWangTiles()
        {
            var map = new Dictionary<int, Tile>();
            string metaPath = "Assets/Art/Tiles/lab_tileset_meta.json";
            if (!File.Exists(metaPath)) return map;
            var meta = JsonUtility.FromJson<TilesetMeta>(File.ReadAllText(metaPath));
            foreach (var t in meta.tileset_data.tiles)
            {
                int mask = (t.corners.NW == "lower" ? 8 : 0) | (t.corners.NE == "lower" ? 4 : 0) |
                           (t.corners.SW == "lower" ? 2 : 0) | (t.corners.SE == "lower" ? 1 : 0);
                if (mask == 0) continue;
                string tilePath = $"{TileDir}/{t.name}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TileDir}/{t.name}.png");
                if (!tile)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                map[mask] = tile;
            }
            AssetDatabase.SaveAssets();
            return map;
        }

        [System.Serializable] class TilesetMeta { public TilesetData tileset_data; }
        [System.Serializable] class TilesetData { public TileMeta[] tiles; }
        [System.Serializable] class TileMeta { public string name; public Corners corners; }
        [System.Serializable] class Corners { public string NE, NW, SE, SW; }

        internal static Tile GetCollisionTile()
        {
            string path = $"{GenDir}/CollisionTile.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.colliderType = Tile.ColliderType.Grid;
            t.sprite = MakePlaceholder("collision_px", Color.white, 32, 32);
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        /// <summary>Dual-grid painting: a visual tile sits on each cell corner and picks its art from the 4 surrounding cells.</summary>
        static void PaintDualGrid(Tilemap map, HashSet<Vector2Int> cells, Dictionary<int, Tile> wang, Vector2Int origin)
        {
            if (cells.Count == 0) return;
            int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x) + 1;
            int minY = cells.Min(c => c.y), maxY = cells.Max(c => c.y) + 1;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int mask = (cells.Contains(new Vector2Int(x - 1, y)) ? 8 : 0) | (cells.Contains(new Vector2Int(x, y)) ? 4 : 0) |
                               (cells.Contains(new Vector2Int(x - 1, y - 1)) ? 2 : 0) | (cells.Contains(new Vector2Int(x, y - 1)) ? 1 : 0);
                    if (mask == 0) continue;
                    if (wang.TryGetValue(mask, out var tile)) map.SetTile(new Vector3Int(x - origin.x, y - origin.y, 0), tile);
                }
        }

        static List<Vector2Int> Flood(Ctx ctx, int x0, int y0, char c, HashSet<Vector2Int> visited)
        {
            var list = new List<Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(x0, y0));
            visited.Add(new Vector2Int(x0, y0));
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                list.Add(p);
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = p + d;
                    if (!visited.Contains(n) && ctx.At(n.x, n.y) == c) { visited.Add(n); q.Enqueue(n); }
                }
            }
            return list;
        }

        static GameObject MakeGroup(Ctx ctx, Transform parent, char c, List<Vector2Int> cells)
        {
            int minX = cells.Min(v => v.x), maxX = cells.Max(v => v.x), minY = cells.Min(v => v.y), maxY = cells.Max(v => v.y);
            string name = c == 'h' ? "HiddenPlatform" : c == 'W' ? "ArenaWall" : $"ConfigPlatform_{c}";
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            box.size = new Vector2(maxX - minX + 1, maxY - minY + 1);

            var vis = new GameObject("Tiles");
            vis.transform.SetParent(go.transform, false);
            vis.transform.localPosition = new Vector3(-0.5f, -0.5f, 0);
            var tm = vis.AddComponent<Tilemap>();
            vis.AddComponent<TilemapRenderer>().sortingOrder = 1;
            PaintDualGrid(tm, new HashSet<Vector2Int>(cells), ctx.wang, Vector2Int.zero);

            switch (c)
            {
                case 'h': go.AddComponent<HiddenPlatform>(); break;
                case 'W': tm.color = new Color(1f, 0.75f, 0.7f); break;
                default:
                    var cp = go.AddComponent<ConfigPlatform>();
                    cp.optionName = "abc".IndexOf(c) >= 0 && c != 'c' ? "--cache" : "ceg".IndexOf(c) >= 0 ? "-O3" : "--threads=8";
                    if (c == 'a' || c == 'b') cp.optionName = "--cache";
                    cp.mode = (c == 'c') ? ConfigPlatform.Mode.Move : ("aek".IndexOf(c) >= 0 ? ConfigPlatform.Mode.ShowWhenOn : ConfigPlatform.Mode.HideWhenOn);
                    if (cp.mode == ConfigPlatform.Mode.Move)
                    {
                        cp.offWorldPos = Vector2.zero;
                        cp.onWorldPos = new Vector2(0, 6);
                        tm.color = new Color(0.8f, 1f, 0.85f);
                    }
                    else if (cp.mode == ConfigPlatform.Mode.ShowWhenOn) tm.color = new Color(0.85f, 1f, 0.85f);
                    else tm.color = new Color(1f, 0.85f, 0.85f);
                    // Option label floating over the platform
                    MakeLabel(ctx, go.transform, cp.optionName, new Vector2((minX + maxX + 1) * 0.5f, maxY + 1.4f), 0.05f, new Color(0.7f, 1f, 0.8f, 0.8f));
                    break;
            }
            return go;
        }

        internal static SpriteRenderer NewSprite(string name, Sprite s, Transform parent, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        static void MakeCollectible(Transform parent, Sprite s, Vector2 pos, CollectibleKind kind)
        {
            var sr = NewSprite(kind.ToString(), s, parent, pos, 5);
            var col = sr.gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;
            var c = sr.gameObject.AddComponent<Collectible>();
            c.kind = kind;
            c.paperPoints = Collectible.DefaultPoints(kind);
        }

        static void MakeWalker(Transform parent, Sprite s, Vector2 pos)
        {
            var go = new GameObject("Walker_CrashingJob");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 0.8f);
            NewSprite("Sprite", s, go.transform, Vector3.zero, 6);
            var e = go.AddComponent<Enemy>();
            e.motion = Enemy.Motion.Walker;
            e.displayName = "Crashing benchmark job";
            e.speed = 2f;
        }

        static void MakeFlyer(Transform parent, Sprite s, Vector2 pos)
        {
            var go = new GameObject("Flyer_NoisyMeasurement");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.4f;
            NewSprite("Sprite", s, go.transform, Vector3.zero, 6);
            var e = go.AddComponent<Enemy>();
            e.motion = Enemy.Motion.Flyer;
            e.displayName = "Noisy measurement";
            e.speed = 2.5f;
            e.flyRange = 2.5f;
        }

        static void MakeSwitch(Ctx ctx, Transform parent, Sprite s, Vector2 pos, string option)
        {
            var sr = NewSprite("ConfigSwitch " + option, s, parent, pos, 4);
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.2f, 1.6f);
            var sw = sr.gameObject.AddComponent<ConfigSwitch>();
            sw.optionName = option;
            sw.indicator = NewSprite("Indicator", Effects_Pixel(), sr.transform, new Vector3(0, 0.75f, 0), 5);
            sw.indicator.transform.localScale = new Vector3(4, 4, 1);
            sw.label = MakeLabel(ctx, sr.transform, option + "=off", new Vector2(pos.x, pos.y + 1.3f), 0.05f, Color.white);
        }

        static Sprite Effects_Pixel() => MakePlaceholder("pixel", Color.white, 4, 4);

        static void MakePhoto(Transform parent, Sprite s, Vector2 pos, int index)
        {
            var sr = NewSprite("PhotoSpot", s, parent, pos, 5);
            var c = sr.gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 1.5f;
            var p = sr.gameObject.AddComponent<PhotoSpot>();
            p.spotId = "L1_photo" + index;
            p.caption = index == 1 ? "Measurement lab from above" : "Lab photo " + index;
        }

        static BenchmarkClusterBoss MakeBoss(Transform parent, Sprite s, Vector2 pos, Sprite projectile, Sprite mate)
        {
            var go = new GameObject("Boss_BenchmarkCluster");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            float h = s.rect.height / 32f;
            box.size = new Vector2(Mathf.Min(2.4f, s.rect.width / 32f * 0.8f), h * 0.9f);
            box.offset = new Vector2(0, h * 0.45f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = 6;
            var b = go.AddComponent<BenchmarkClusterBoss>();
            b.projectileSprite = projectile;
            b.mateSprite = mate;
            return b;
        }

        static GameObject MakeSubmit(Transform parent, Sprite s, Vector2 pos)
        {
            var sr = NewSprite("SubmitButton", s, parent, pos, 5);
            sr.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            sr.transform.position = new Vector3(pos.x, pos.y + 0.25f, 0);
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.9f, 1f);
            sr.gameObject.AddComponent<SubmitButton>();
            return sr.gameObject;
        }

        static void MakeGate(Ctx ctx, Transform parent, Sprite door, Vector2 pos)
        {
            var go = new GameObject("PeerReviewGate_1");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var zone = go.AddComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = new Vector2(2.5f, 2f);
            zone.offset = new Vector2(-1.2f, 1f);
            var gate = go.AddComponent<PeerReviewGate>();
            gate.gateNumber = 1;
            var d = NewSprite("Door", door, go.transform, new Vector3(0, door.rect.height / 64f, 0), 4);
            var dc = d.gameObject.AddComponent<BoxCollider2D>();
            dc.size = new Vector2(1f, door.rect.height / 32f);
            gate.doorCollider = dc;
            gate.doorSprite = d;
            MakeLabel(ctx, go.transform, "REBUTTAL ROOM", new Vector2(pos.x, pos.y + 2.6f), 0.06f, new Color(1f, 0.85f, 0.4f));
        }

        static TextMesh MakeLabel(Ctx ctx, Transform parent, string text, Vector2 worldPos, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent);
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = ctx.font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = ctx.font.material;
            mr.sortingOrder = 50;
            return tm;
        }

        internal static GameObject MakePlayer(Vector2 pos, Sprite mate)
        {
            var go = new GameObject("Johannes");
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.5f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var cap = go.AddComponent<CapsuleCollider2D>();
            cap.size = new Vector2(0.45f, 1.3f);
            cap.offset = new Vector2(0, 0.66f);
            cap.sharedMaterial = GetFrictionless();

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            var anim = spriteGo.AddComponent<SpriteAnimator>();
            anim.clips = LoadClips();
            if (anim.clips.Count > 0 && anim.clips[0].frames.Length > 0) sr.sprite = anim.clips[0].frames[0];
            else sr.sprite = MakePlaceholder("johannes_placeholder", new Color(0.15f, 0.15f, 0.2f), 16, 44, bottom: true);

            var pc = go.AddComponent<PlayerController>();
            pc.body = sr;
            pc.anim = anim;
            pc.mateSprite = mate;
            pc.killY = -3f;
            return go;
        }

        static readonly (string name, float fps, bool loop)[] ClipDefs =
        {
            ("idle", 6, true), ("run", 12, true), ("jump", 16, false), ("doublejump", 18, false), ("fall", 8, true),
            ("throw", 20, false), ("drink", 12, false), ("flash", 14, false), ("hurt", 16, false), ("victory", 8, true), ("type", 10, true),
        };

        static List<SpriteClip> LoadClips()
        {
            var list = new List<SpriteClip>();
            string root = $"{ArtRoot}/Johannes";
            foreach (var def in ClipDefs)
            {
                string dir = $"{root}/{def.name}";
                if (!Directory.Exists(dir)) continue;
                var frames = Directory.GetFiles(dir, "*.png").OrderBy(f => f)
                    .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace('\\', '/'))).Where(s => s).ToArray();
                if (frames.Length == 0) continue;
                list.Add(new SpriteClip { name = def.name, frames = frames, fps = def.fps, loop = def.loop });
            }
            // idle must exist: fall back to the static rotation
            if (!list.Any(c => c.name == "idle"))
            {
                var still = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/east.png");
                if (still) list.Insert(0, new SpriteClip { name = "idle", frames = new[] { still }, fps = 1, loop = true });
            }
            return list;
        }

        static PhysicsMaterial2D GetFrictionless()
        {
            string path = $"{GenDir}/Frictionless.physicsMaterial2D";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (m) return m;
            m = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        internal static Sprite LoadSprite(string name, Color fallback, int w = 32, int h = 32)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
            return s ? s : MakePlaceholder("ph_" + name.Replace('/', '_'), fallback, w, h, name.StartsWith("Bottom/"));
        }

        internal static Sprite MakePlaceholder(string name, Color color, int w, int h, bool bottom = false)
        {
            string dir = bottom ? $"{GenDir}/Bottom" : GenDir;
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                        px[y * w + x] = edge ? Color.Lerp(color, Color.black, 0.6f) : color;
                    }
                tex.SetPixels(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static void AddToBuild(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
