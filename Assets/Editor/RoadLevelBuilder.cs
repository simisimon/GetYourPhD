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
    /// Builds Level 3 "The Camper Road Trip" from Assets/Levels/Level3_RoadTrip.txt (legend in the file header).
    /// The map is read top to bottom in driving order; each row is <see cref="RowHeight"/> units of road.
    /// Art lives in Assets/Art/Road (PixelLab sprites + generated road tiles).
    /// </summary>
    public static class RoadLevelBuilder
    {
        const string MapPath = "Assets/Levels/Level3_RoadTrip.txt";
        const string ScenePath = "Assets/Scenes/Level3_CamperRoadTrip.unity";
        const string Art = "Assets/Art/Road";
        public const float RowHeight = 5f;

        static readonly Dictionary<char, int> Points = new Dictionary<char, int> { { 'a', 3 }, { 'i', 1 }, { 's', 6 }, { 'm', 1 } };

        static RoadLeg MakeLeg(string code)
        {
            switch (code)
            {
                case "AT":
                    return new RoadLeg
                    {
                        code = "AT", name = "The Alps", welcome = "Grüezi! Welcome to the Alps",
                        firstCol = 1, theme = 1, curveAmp = 3.2f, curveLen = 70f,
                        flagStyle = 2, flagA = new Color(0.85f, 0.1f, 0.12f), flagB = Color.white, flagC = Color.white,
                        hudName = "Switzerland", altName = "Austria", altFlagStyle = 1, altFlagA = new Color(0.8f, 0.1f, 0.15f), altFlagB = Color.white, altFlagC = new Color(0.8f, 0.1f, 0.15f),
                        postcard = S("pc_at"), postcardTitle = "Greetings from the Alps",
                    };
                case "IT":
                    return new RoadLeg
                    {
                        code = "IT", name = "Italy", welcome = "Benvenuti in Italia!",
                        firstCol = 1, theme = 2, curveAmp = 1.0f, curveLen = 110f,
                        flagStyle = 0, flagA = new Color(0.1f, 0.55f, 0.25f), flagB = Color.white, flagC = new Color(0.8f, 0.15f, 0.15f),
                        postcard = S("pc_it"), postcardTitle = "Saluti dall'Italia",
                    };
                default:
                    return new RoadLeg
                    {
                        code = "DE", name = "Germany", welcome = "Willkommen in Deutschland!",
                        firstCol = 0, theme = 0,
                        flagStyle = 1, flagA = new Color(0.1f, 0.1f, 0.1f), flagB = new Color(0.85f, 0.1f, 0.1f), flagC = new Color(1f, 0.8f, 0.1f),
                        postcard = S("pc_leipzig"), postcardTitle = "Greetings from Leipzig",
                    };
            }
        }

        static Sprite S(string n)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{n}.png");
            if (!s) Debug.LogWarning($"[GetYourPhD] Missing road sprite {n}");
            return s;
        }

        [MenuItem("GetYourPhD/Build Level 3 (Camper Road Trip)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var lines = File.ReadAllLines(MapPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- parse the map ----
            var legs = new List<RoadLeg>();
            var events = new List<RoadEvent>();
            int row = 0;
            float finishY = -1f;
            int paper = 0;
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                float y = row * RowHeight;
                if (line.StartsWith("@"))
                {
                    var parts = line.Split(new[] { ' ' }, 3);
                    switch (parts[0])
                    {
                        case "@leg":
                            if (legs.Count > 0) legs[legs.Count - 1].endY = y;
                            var leg = MakeLeg(parts.Length > 1 ? parts[1] : "DE");
                            leg.startY = y;
                            if (legs.Count > 0)
                                events.Add(new RoadEvent { y = y + 1f, kind = "|", col = 0, text = leg.welcome.ToUpperInvariant(), seed = row });
                            legs.Add(leg);
                            break;
                        case "@sign":
                            if (legs.Count > 0 && parts.Length > 2 && parts[2].Trim() == "ÖSTERREICH") legs[legs.Count - 1].altFromY = y;
                            events.Add(new RoadEvent { y = y, kind = "!", col = parts.Length > 1 && parts[1] == "L" ? 0 : 3, text = parts.Length > 2 ? parts[2].Replace(" - ", "\n") : "", seed = row });
                            break;
                        case "@finish":
                            finishY = y;
                            events.Add(new RoadEvent { y = y, kind = "F", col = 0, seed = row });
                            break;
                    }
                    continue;
                }
                for (int c = 0; c < line.Length && c < 5; c++)
                {
                    char k = line[c];
                    if (k == '.' || k == ' ') continue;
                    events.Add(new RoadEvent { y = y + RowHeight * 0.5f, kind = k.ToString(), col = c, seed = row * 31 + c * 7 + 1 });
                    if (Points.TryGetValue(k, out int p)) paper += p;
                }
                row++;
            }
            if (finishY < 0) finishY = row * RowHeight;
            if (legs.Count == 0) legs.Add(MakeLeg("DE"));
            legs[legs.Count - 1].endY = finishY + 60f;
            events = events.OrderBy(e => e.y).ToList();

            // ---- camera & light ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.4375f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.33f, 0.55f, 0.28f);
            camGo.transform.position = new Vector3(0, 8, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // ---- art ----
            var art = new RoadArt
            {
                pixel = S("pixel"),
                camperDamage = new[] { S("camper"), S("camper_dent1"), S("camper_dent2") },
                traffic = new[] { S("wagon"), S("car_red"), S("car_green"), S("car_silver") },
                sportsCar = S("sportscar"), reviewer2 = S("reviewer2"), vespa = S("vespa"), cow = S("cow"), smallCar = S("smallcar"),
                rock = S("rock"), pothole = S("pothole"), barrier = S("barrier"),
                jerry = S("jerry"), paper = S("paper"), citation = S("citation"), stamp = S("stamp"),
                mate = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/mate.png"), postcardIcon = S("postcard_icon"), postcardIcons = new[] { S("postcard_icon_it"), S("postcard_icon_at"), S("postcard_icon_leipzig") },
                gasStation = S("gas"), workshop = S("workshop"), tollBooth = S("booth"), venue = S("paulinum"), venueScale = 1.6f, venueName = "PAULINUM LEIPZIG", shadow = S("shadow"), warn = S("warn"),
                asphalt = new[] { S("asphalt_de"), S("asphalt_at"), S("asphalt_it") },
                grass = new[] { S("grass_de"), S("grass_at"), S("grass_it") },
                decorDE = new[] { S("tree"), S("tree"), S("house_de"), S("haybale"), S("haybale") },
                decorAT = new[] { S("pine"), S("pine"), S("pine"), S("chalet"), S("alprocks"), S("alprocks") },
                decorIT = new[] { S("cypress"), S("cypress"), S("olive"), S("olive"), S("house_it") },
                signBoard = S("sign_board"), gantry = S("gantry"), finishLine = S("finish_line"),
            };

            // ---- camper ----
            var camperGo = new GameObject("Camper");
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(camperGo.transform, false);
            body.transform.localScale = Vector3.one * 0.85f;
            body.sprite = art.camperDamage[0];
            body.sortingOrder = 12;
            var camper = camperGo.AddComponent<CamperController>();
            camper.body = body;
            camper.damageSprites = art.camperDamage;

            // ---- game systems ----
            var game = new GameObject("Game");
            var clock = game.AddComponent<DeadlineClock>();
            clock.levelDurationSeconds = 315f;
            clock.slowdownStartsAt = 60f;
            clock.slowdownFullAt = 30f;
            clock.minSpeedFactor = 1f;
            clock.inGameMinutesSpan = 600f;
            var meter = game.AddComponent<PaperMeter>();
            meter.maxPoints = Mathf.Max(10, Mathf.RoundToInt(paper * 0.8f));
            var trip = game.AddComponent<RoadTrip>();
            trip.legs = legs.ToArray();
            trip.events = events;
            trip.finishY = finishY;
            trip.camper = camper;
            trip.art = art;
            trip.font = font;
            trip.cam = cam;
            trip.nextScene = "StartScreen";

            var scenery = new GameObject("Scenery").AddComponent<RoadScenery>();
            scenery.trip = trip;
            scenery.cam = cam;

            var hud = new GameObject("HUD").AddComponent<HUD>();
            hud.font = font;
            var rhud = hud.gameObject.AddComponent<RoadHUD>();
            rhud.font = font;
            rhud.camperIcon = art.camperDamage[0];
            rhud.jerryIcon = art.jerry;
            rhud.postcardBlank = S("postcard_blank");

            // ---- How to play ----
            var how = game.AddComponent<HowToPlayOverlay>();
            how.levelId = "L3";
            how.font = font;
            how.title = "LEVEL 3: THE CAMPER ROAD TRIP";
            how.intro = "The defense is far away: drive the <b>camper</b> from <b>Italy</b> over the <b>Alps</b> home to the <b>Paulinum in Leipzig</b> before " +
                        "<b>13:59 AOE</b>. Watch the <b>fuel</b> and the <b>dents</b>: an empty tank sends you back to the last border, and a <b>third crash</b> ends the trip: the paper is <b>submitted as is</b>. " +
                        "Gas stations and workshops on the right help, but cost time.";
            how.rowsCaption = "ON THE ROAD";
            how.rows = new List<HowToRow>
            {
                new HowToRow { icon = art.jerry, name = "Jerry can", text = "+15 fuel. Speeding burns fuel much faster" },
                new HowToRow { icon = art.gasStation, name = "Gas / Workshop", text = "Steer right into it: full tank (-6 s) or repair (-16 s)" },
                new HowToRow { icon = art.paper, name = "Paper, citation", text = "+3 / +1 paper" },
                new HowToRow { icon = art.stamp, name = "Approved stamp", text = "\"Reviewer 2 approved\": +6 paper" },
                new HowToRow { icon = art.mate, name = "Mate", text = "Turbo for a few seconds" },
                new HowToRow { icon = art.reviewer2, name = "Reviewer 2", text = "Chases you. Swerve at the last moment or brake" },
                new HowToRow { icon = art.pothole, name = "Desk Reject", text = "Pothole: a dent and -4 paper" },
                new HowToRow { icon = art.postcardIcon, name = "Postcard", text = "One hidden in every country. Collect them all!" },
            };
            how.keys = new List<HowToKey>
            {
                new HowToKey { key = "← / →  or  A / D", action = "Change lanes (and turn into gas stations / workshops)" },
                new HowToKey { key = "↑ / W", action = "Accelerate (the Ducato is slow to speed up)" },
                new HowToKey { key = "↓ / S", action = "Brake (slow down for tolls, cows and curves)" },
                new HowToKey { key = "nothing", action = "The camper cruises at a comfy 90 km/h" },
            };
            how.gamepadNote = "Gamepad: stick or d-pad steer, A / RT accelerate, X / LT brake";

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildAfterLevel2();
            Debug.Log($"[GetYourPhD] Built {ScenePath}: {legs.Count} legs, {events.Count} events, finish at {finishY}, paper max {meter.maxPoints}");
        }

        static void AddToBuildAfterLevel2()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int i = scenes.FindIndex(s => s.path.Contains("Level2_"));
            scenes.Insert(i >= 0 ? i + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
