using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GetYourPhD
{
    /// <summary>
    /// Level 3 "The Camper Road Trip": drives the whole level. The road scrolls upward through three country legs
    /// (Germany, Alps, Italy) to the defense venue. Fails: empty tank or third hit = back to the last border.
    /// The deadline clock is the real fail condition. The Paper meter is the score; one postcard per country.
    /// </summary>
    public class RoadTrip : MonoBehaviour
    {
        public static RoadTrip Instance { get; private set; }

        [Header("Layout (filled by RoadLevelBuilder)")]
        public RoadLeg[] legs;
        public List<RoadEvent> events = new List<RoadEvent>();
        public float finishY = 2000f;
        public float laneWidth = 2.5f;
        public float startY = 4f;

        [Header("Refs")]
        public CamperController camper;
        public RoadArt art;
        public Font font;
        public Camera cam;
        public float cameraLead = 6f;

        [Header("Tuning")]
        public float spawnAhead = 34f;
        public float despawnBehind = 16f;
        public float jerryFuel = 15f;
        public float gasPenalty = 6f;
        public float workshopPenalty = 16f;
        public float tollSpeed = 6.5f;
        public float mateBoost = 6f;
        public int hitPaperLoss = 2;
        public int rejectPaperLoss = 4;
        public float curveSlideSpeed = 12.5f;
        public string nextScene = "StartScreen";
        public string levelTitle = "Level 3: The Camper Road Trip";

        public int LegIndex { get; private set; }
        public int CheckpointLeg { get; private set; }
        public bool[] Postcards { get; private set; }
        public bool Finished { get; private set; }
        public bool Lost { get; private set; }
        public bool Busy { get; private set; }   // fail / station sequence
        public RoadLeg Leg => legs[Mathf.Clamp(LegIndex, 0, legs.Length - 1)];
        public float Progress => Mathf.Clamp01((camper ? camper.Y : 0f) / finishY);

        readonly List<RoadThing> things = new List<RoadThing>();
        readonly List<RoadEvent> armed = new List<RoadEvent>();   // chasers waiting for the camper to reach their row
        readonly List<Station> stations = new List<Station>();
        int spawnIdx;
        float smokeT, curveT;
        bool started, lowFuelWarned;
        readonly HashSet<char> tipsShown = new HashSet<char>();

        class Station { public float y0, y1; public bool workshop; public bool used; }

        float venueY = -1f;
        bool brokeDown;

        public static readonly Vector2 CamperHalf = new Vector2(0.55f, 1.45f);

        void Awake()
        {
            Instance = this;
            GameInput.Locked = false;
            Time.timeScale = 1f;
            Postcards = new bool[legs != null ? legs.Length : 3];
            foreach (var e in events)
                if (e.kind == "G" || e.kind == "S")
                    stations.Add(new Station { y0 = e.y, y1 = e.y + 18f, workshop = e.kind == "S" });
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            camper.Init(this, startY, 2);
            if (DeadlineClock.Instance) DeadlineClock.Instance.OnExpired += OnDeadline;
            var hud = HUD.Instance;
            if (hud)
            {
                hud.HideMatePanel();
                hud.clockSubProvider = () => $"{Mathf.RoundToInt(camper.Kmh)} km/h{(camper.Speeding ? "  SPEEDING" : "")}";
            }
            UpdateCamera(true);
            yield return null;
            var how = FindAnyObjectByType<HowToPlayOverlay>();
            if (how) yield return how.ShowIfNew();
            started = true;
            GameAudio.Play("engine_start");
            HUD.Instance?.Banner(levelTitle, $"Reach the Paulinum in Leipzig before {(DeadlineClock.Instance ? DeadlineClock.Instance.DeadlineText : "the deadline")}!");
        }

        // ---------------- road geometry ----------------
        public int LegIndexAt(float y)
        {
            for (int i = legs.Length - 1; i >= 0; i--) if (y >= legs[i].startY) return i;
            return 0;
        }

        public RoadLeg LegAt(float y) => legs[LegIndexAt(y)];

        /// <summary>Sideways offset of the road centre at y (bends in the Alps).</summary>
        public float RoadOffset(float y)
        {
            var l = LegAt(y);
            if (l.curveAmp <= 0f) return 0f;
            float t = y - l.startY;
            float fade = Mathf.Clamp01(t / 25f) * Mathf.Clamp01((l.endY - y) / 25f);
            // straight road around gas stations / workshops so the station lane lines up
            foreach (var s in stations)
            {
                float d = y < s.y0 - 8f ? (s.y0 - 8f - y) : y > s.y1 + 8f ? (y - s.y1 - 8f) : 0f;
                fade *= Mathf.Clamp01(d / 20f);
            }
            return Mathf.Sin(t / l.curveLen * Mathf.PI * 2f) * l.curveAmp * fade;
        }

        public float RoadHalfWidth(float y) => LegAt(y).Lanes * laneWidth * 0.5f;

        /// <summary>Lane centre relative to the road centre. Column 4 is the station lane on the right shoulder.</summary>
        public float LaneX(float y, int col)
        {
            var l = LegAt(y);
            if (col >= 4) return RoadHalfWidth(y) + laneWidth * 0.5f + 0.3f;
            col = Mathf.Max(col, l.firstCol);
            return (col - l.firstCol - (l.Lanes - 1) * 0.5f) * laneWidth;
        }

        public bool LaneAllowed(float y, int col)
        {
            var l = LegAt(y);
            if (col >= l.firstCol && col <= 3) return true;
            if (col == 4) return StationAt(y) != null;
            return false;
        }

        Station StationAt(float y)
        {
            foreach (var s in stations) if (y >= s.y0 - 3f && y <= s.y1 - 5f) return s;
            return null;
        }

        // ---------------- main loop ----------------
        AudioSource engineLoop;

        /// <summary>Engine hum: pitch follows the speed, silent once the trip is over.</summary>
        void EngineSound()
        {
            if (!engineLoop) engineLoop = GameAudio.MakeLoop(this, "engine", 0.22f);
            if (!engineLoop) return;
            bool on = started && !Lost && !(Finished && camper.Speed < 0.2f) && Time.timeScale > 0f;
            GameAudio.SetLoop(engineLoop, on);
            float k = Mathf.Clamp01(camper.Speed / Mathf.Max(1f, camper.maxSpeed));
            engineLoop.pitch = Mathf.Lerp(0.55f, 1.15f, k) + (camper.Boosted ? 0.12f : 0f);
            engineLoop.volume = GameAudio.SfxVolume * Mathf.Lerp(0.14f, 0.26f, k);
        }

        void Update()
        {
            EngineSound();
            if (!started) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!Lost) camper.Tick(dt);

            // leg / border
            int li = LegIndexAt(camper.Y);
            if (li != LegIndex)
            {
                LegIndex = li;
                if (li > CheckpointLeg)
                {
                    CheckpointLeg = li;
                    HUD.Instance?.Banner(Leg.welcome, "Border crossed - checkpoint saved");
                    GameAudio.Play("bell");
                    Effects.Burst(camper.transform.position, Leg.flagB, 16);
                }
                // fewer lanes after the border: squeeze into the road
                var l = Leg;
                if (camper.Col < l.firstCol) camper.ForceLane(l.firstCol);
            }

            SpawnAhead();
            TickThings(dt);
            if (!Busy && !Finished && !Lost)
            {
                Collide();
                Stations();
                Curves(dt);
                FuelCheck();
                if (camper.Y >= finishY - 2f) StartCoroutine(Finish());
            }
            if (camper.Hp <= 1 && (smokeT -= dt) <= 0f)
            {
                smokeT = 0.18f;
                Puff(camper.transform.position + camper.transform.up * -1.3f);
            }
        }

        void LateUpdate()
        {
            UpdateCamera(false);
            // organizer keys: Ctrl+Shift+R restarts the level, Ctrl+Shift+P drives straight to the venue
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                if (kb.rKey.wasPressedThisFrame) Replay();
                if (started && !Finished && !Lost && kb.pKey.wasPressedThisFrame) { HUD.Instance?.Toast("Organizer override"); DebugJumpTo(finishY - 25f); }
            }
        }

        /// <summary>Grey smoke puff behind a badly damaged camper.</summary>
        void Puff(Vector3 pos)
        {
            var go = new GameObject("Smoke");
            go.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.2f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.shadow;
            sr.sortingOrder = 13;
            sr.color = new Color(0.45f, 0.45f, 0.48f, 0.7f);
            go.AddComponent<SmokePuff>();
        }

        void UpdateCamera(bool snap)
        {
            if (!cam || !camper) return;
            float cy = camper.Y + cameraLead;
            float cx = RoadOffset(cy) * 0.6f + (camper.transform.position.x) * 0.25f;
            bool arrived = Finished && !brokeDown && venueY > 0f;
            if (arrived) { cy = venueY - 2f; cx = 0f; } // pan up to show the Paulinum
            var p = new Vector3(cx, cy, -10);
            if (!snap) p.x = Mathf.Lerp(cam.transform.position.x, cx, 1f - Mathf.Exp(-Time.deltaTime * 4f));
            if (!snap && arrived) p.y = Mathf.MoveTowards(cam.transform.position.y, cy, Mathf.Max(3f, Mathf.Abs(cy - cam.transform.position.y) * 2f) * Time.deltaTime);
            // pixel snap keeps the 32 PPU art crisp
            p.x = Mathf.Round(p.x * 32f) / 32f;
            if (!arrived) p.y = Mathf.Round(p.y * 32f) / 32f;
            cam.transform.position = p;
        }

        // ---------------- spawning ----------------
        static bool IsChaser(string k) => k == "f" || k == "T" || k == "R";

        void SpawnAhead()
        {
            while (spawnIdx < events.Count && events[spawnIdx].y < camper.Y + spawnAhead)
            {
                var e = events[spawnIdx++];
                if (e.y < camper.Y - despawnBehind) continue;
                if (IsChaser(e.kind)) armed.Add(e);
                else Spawn(e);
            }
            for (int i = armed.Count - 1; i >= 0; i--)
            {
                if (camper.Y >= armed[i].y) { Spawn(armed[i]); armed.RemoveAt(i); }
            }
        }

        RoadThing NewThing(RoadEvent e, char k, Sprite s, float scale, int order, float hitScale = 0.8f)
        {
            var go = new GameObject("Road_" + k);
            go.transform.SetParent(transform);
            var t = go.AddComponent<RoadThing>();
            t.Setup(e, k);
            var sgo = new GameObject("Sprite");
            sgo.transform.SetParent(go.transform, false);
            t.sr = sgo.AddComponent<SpriteRenderer>();
            t.sr.sprite = s;
            t.sr.sortingOrder = order;
            sgo.transform.localScale = Vector3.one * scale;
            go.transform.localScale = Vector3.one;
            if (s) t.half = (Vector2)s.bounds.extents * scale * hitScale;
            t.y = e != null ? e.y : camper.Y;
            t.lateral = LaneX(t.y, t.col);
            things.Add(t);
            return t;
        }

        // Builds a child sprite that scales with the parent sprite holder (used for the "!" warning)
        SpriteRenderer Child(RoadThing t, Sprite s, int order)
        {
            var g = new GameObject("Warn");
            g.transform.SetParent(t.transform, false);
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = s; r.sortingOrder = order;
            g.SetActive(false);
            return r;
        }

        void Spawn(RoadEvent e)
        {
            char k = e.kind[0];
            RoadThing t;
            var r = new System.Random(e.seed);
            switch (k)
            {
                case 'c': // slower traffic
                {
                    var spr = Leg.theme == 2 && r.NextDouble() < 0.5 ? art.smallCar : art.traffic[r.Next(art.traffic.Length)];
                    t = NewThing(e, k, spr, 0.85f, 10, 0.78f);
                    t.solid = true;
                    t.baseSpeed = t.speed = 5.5f + (float)r.NextDouble() * 2.5f;
                    break;
                }
                case 'f': // fast car from behind (left lanes)
                    t = NewThing(e, k, art.traffic[r.Next(art.traffic.Length)], 0.85f, 10, 0.78f);
                    t.solid = true;
                    t.y = camper.Y - 14f;
                    t.baseSpeed = t.speed = 17f;
                    t.warn = Child(t, art.warn, 60);
                    break;
                case 'T': // tailgating sports car: gets into your lane
                    e.col = camper.Col;
                    t = NewThing(e, k, art.sportsCar, 0.85f, 11, 0.78f);
                    t.col = t.targetCol = Mathf.Clamp(camper.Col, Leg.firstCol, 3);
                    t.lateral = LaneX(camper.Y, t.col);
                    t.solid = true;
                    t.y = camper.Y - 13f;
                    t.warn = Child(t, art.warn, 60);
                    Tip('T', "A sports car is tailgating you! Change lanes to let it pass.");
                    GameAudio.Play("horn", 0.8f);
                    break;
                case 'R': // Reviewer 2 chases you
                    t = NewThing(e, k, art.reviewer2, 0.85f, 11, 0.78f);
                    t.col = t.targetCol = Mathf.Clamp(camper.Col, Leg.firstCol, 3);
                    t.lateral = LaneX(camper.Y, t.col);
                    t.solid = true;
                    t.y = camper.Y - 14f;
                    t.timer = 10f; // gives up after this
                    t.warn = Child(t, art.warn, 60);
                    Tip('R', "REVIEWER 2 is chasing you! Dodge late or brake.");
                    GameAudio.Play("horn", 0.9f, 0.02f);
                    break;
                case 'v': // Vespa weaving between lanes
                    t = NewThing(e, k, art.vespa, 1.1f, 10, 0.7f);
                    t.solid = true;
                    t.baseSpeed = t.speed = 6.5f + (float)r.NextDouble();
                    t.timer = 0.8f + (float)r.NextDouble();
                    t.latSpeed = 2.6f;
                    break;
                case 'p': // parked car
                    t = NewThing(e, k, Leg.theme == 2 ? art.smallCar : art.traffic[r.Next(art.traffic.Length)], 0.85f, 9, 0.78f);
                    t.solid = true;
                    break;
                case '=': // roadworks
                    t = NewThing(e, k, art.barrier, 1f, 9, 0.8f);
                    t.half = new Vector2(laneWidth * 0.42f, 0.45f);
                    t.solid = true;
                    Tip('=', "Roadworks ahead! Change lanes early.");
                    break;
                case 'o': // Desk Reject pothole
                    t = NewThing(e, k, art.pothole, 1.1f, 3, 0.6f);
                    t.solid = true;
                    Label(t, "DESK REJECT", new Color(1f, 0.85f, 0.4f), 0.07f, 0.9f);
                    break;
                case 'k': // cow crossing
                    t = NewThing(e, k, art.cow, 1.35f, 10, 0.7f);
                    t.solid = true;
                    t.timer2 = r.NextDouble() < 0.5 ? -1f : 1f;
                    Tip('k', "Muh! A cow on the road. Slow down or steer around it.");
                    GameAudio.Play("moo", 0.7f);
                    break;
                case 'r': // falling rock: shadow first, then the rock lands
                    t = NewThing(e, k, art.rock, 1.25f, 9, 0.7f);
                    t.solid = false;
                    t.state = 0;
                    t.sr.enabled = false;
                    t.warn = Child(t, art.shadow, 2);
                    t.warn.transform.localPosition = Vector3.zero;
                    break;
                case 'B': // toll booth (one barrier per lane, sign is in the map)
                    t = NewThing(e, k, art.barrier, 1f, 9, 0.8f);
                    t.half = new Vector2(laneWidth * 0.5f, 0.35f);
                    t.solid = true;
                    if (art.tollBooth && t.col == Leg.firstCol)
                    {
                        // booths on both sides of the road
                        for (int s = -1; s <= 1; s += 2)
                        {
                            var b = new GameObject("Booth").AddComponent<SpriteRenderer>();
                            b.sprite = art.tollBooth; b.sortingOrder = 12;
                            b.transform.SetParent(t.transform, false);
                            float side = s < 0 ? -RoadHalfWidth(t.y) - 0.9f - t.lateral : RoadHalfWidth(t.y) + 0.9f - t.lateral;
                            b.transform.localPosition = new Vector3(side, 0.3f, 0);
                        }
                    }
                    break;
                case 'j': t = NewThing(e, k, art.jerry, 1.1f, 8, 1.3f); t.pickup = true; break;
                case 'a': t = NewThing(e, k, art.paper, 1.1f, 8, 1.3f); t.pickup = true; break;
                case 'i': t = NewThing(e, k, art.citation, 1f, 8, 1.3f); t.pickup = true; break;
                case 's': t = NewThing(e, k, art.stamp, 1.2f, 8, 1.3f); t.pickup = true; break;
                case 'm': t = NewThing(e, k, art.mate, 1f, 8, 1.4f); t.pickup = true; break;
                case 'P':
                {
                    int leg = LegIndexAt(e.y);
                    if (Postcards[leg]) return;
                    var icon = art.postcardIcons != null && leg < art.postcardIcons.Length && art.postcardIcons[leg] ? art.postcardIcons[leg] : art.postcardIcon;
                    t = NewThing(e, k, icon, 1.2f, 8, 1.3f); t.pickup = true;
                    break;
                }
                case 'G':
                case 'S':
                    t = NewThing(e, k, k == 'G' ? art.gasStation : art.workshop, 1f, 4, 1f);
                    t.passive = true;
                    t.y = e.y + 9f;
                    t.lateral = LaneX(e.y, 4) + laneWidth * 0.5f + 2.4f;
                    // station lane asphalt on the shoulder
                    var lane = new GameObject("StationLane").AddComponent<SpriteRenderer>();
                    lane.sprite = art.asphalt[Leg.theme]; lane.drawMode = SpriteDrawMode.Tiled;
                    lane.size = new Vector2(laneWidth, 22f); lane.sortingOrder = -5;
                    lane.transform.SetParent(t.transform, false);
                    lane.transform.localPosition = new Vector3(-(laneWidth * 0.5f + 2.4f), -1.5f, 0);
                    Label(t, k == 'G' ? "GAS" : "WORKSHOP", Color.white, 0.07f, 3.2f);
                    break;
                case '!': // roadside sign
                    t = NewThing(e, k, art.signBoard, 2f, 20, 1f);
                    t.passive = true;
                    t.lateral = (e.col <= 0 ? -1f : 1f) * (RoadHalfWidth(e.y) + 1.8f) + (e.col >= 4 ? laneWidth : 0f);
                    if (e.text == "LEIPZIG")
                    {
                        // yellow German city sign (Ortsschild) with a black border
                        t.sr.sprite = art.pixel; t.sr.color = Color.black;
                        t.sr.transform.localScale = new Vector3(3.4f * 16f, 1.5f * 16f, 1f);
                        t.sr.transform.localPosition = new Vector3(0, 0.6f, 0);
                        var face = new GameObject("Face").AddComponent<SpriteRenderer>();
                        face.sprite = art.pixel; face.color = new Color(1f, 0.82f, 0.1f); face.sortingOrder = 21;
                        face.transform.SetParent(t.transform, false);
                        face.transform.localPosition = new Vector3(0, 0.6f, 0);
                        face.transform.localScale = new Vector3(3.2f * 16f, 1.3f * 16f, 1f);
                        var post = new GameObject("Post").AddComponent<SpriteRenderer>();
                        post.sprite = art.pixel; post.color = new Color(0.45f, 0.45f, 0.5f); post.sortingOrder = 19;
                        post.transform.SetParent(t.transform, false);
                        post.transform.localPosition = new Vector3(0, -0.4f, 0);
                        post.transform.localScale = new Vector3(0.15f * 16f, 1.2f * 16f, 1f);
                        Label(t, "Leipzig", Color.black, 0.1f, 0.55f).anchor = TextAnchor.MiddleCenter;
                        Label(t, "Stadt", Color.black, 0.04f, 1.05f).anchor = TextAnchor.MiddleCenter;
                        t.lateral += 0.8f;
                    }
                    else Label(t, e.text, Color.white, 0.055f, 0.38f).anchor = TextAnchor.MiddleCenter;
                    break;
                case '|': // border gantry
                    t = NewThing(e, k, art.gantry, 1f, 25, 1f);
                    t.passive = true;
                    t.lateral = 0f;
                    t.sr.drawMode = SpriteDrawMode.Sliced;
                    t.sr.size = new Vector2(RoadHalfWidth(e.y + 1f) * 2f + 2f, 1.6f);
                    Label(t, e.text, new Color(1f, 0.9f, 0.3f), 0.11f, 0f).anchor = TextAnchor.MiddleCenter;
                    break;
                case 'F': // the defense venue
                    t = NewThing(e, k, art.venue, art.venueScale, 6, 1f);
                    t.passive = true;
                    t.lateral = 0f;
                    // the building's front edge sits 8 units past the finish line
                    float vh = art.venue ? art.venue.bounds.extents.y * art.venueScale : 3f;
                    t.y = e.y + 8f + vh;
                    venueY = t.y;
                    Label(t, art.venueName, new Color(1f, 0.9f, 0.4f), 0.11f, -vh - 1.6f);
                    var line = new GameObject("FinishLine").AddComponent<SpriteRenderer>();
                    line.sprite = art.finishLine; line.drawMode = SpriteDrawMode.Tiled;
                    line.size = new Vector2(RoadHalfWidth(e.y) * 2f, 1f); line.sortingOrder = 2;
                    line.transform.SetParent(t.transform, false);
                    line.transform.localPosition = new Vector3(0, -(8f + vh), 0);
                    break;
                default:
                    return;
            }
            t.Place(this);
        }

        TextMesh Label(RoadThing t, string text, Color c, float size, float dy)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(t.transform, false);
            go.transform.localPosition = new Vector3(0, dy, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 48; tm.characterSize = size;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            if (font) mr.sharedMaterial = font.material;
            mr.sortingOrder = 30;
            return tm;
        }

        void Tip(char k, string msg)
        {
            if (tipsShown.Add(k)) HUD.Instance?.Toast(msg);
        }

        void Remove(RoadThing t)
        {
            things.Remove(t);
            if (t) Destroy(t.gameObject);
        }

        // ---------------- behaviour ----------------
        RoadThing Ahead(RoadThing me, int col, float range)
        {
            RoadThing best = null;
            foreach (var o in things)
            {
                if (o == me || o.passive || o.pickup || o.col != col) continue;
                float d = o.y - me.y;
                if (d > 0f && d < range && (best == null || d < best.y - me.y)) best = o;
            }
            return best;
        }

        void TickThings(float dt)
        {
            float camBottom = cam ? cam.transform.position.y - cam.orthographicSize : camper.Y - 5f;
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (!t) { things.RemoveAt(i); continue; }
                bool sameLane = Mathf.Abs(t.lateral - camper.Lateral) < laneWidth * 0.6f;
                float gap = camper.Y - CamperHalf.y - (t.y + t.half.y); // > 0: t is behind the camper
                switch (t.kind)
                {
                    case 'c':
                    case 'f':
                    case 'v':
                    {
                        float sp = t.baseSpeed;
                        var a = Ahead(t, t.col, 4.5f);
                        if (a) sp = Mathf.Min(sp, a.speed);
                        if (a && a.speed < 1f && t.targetCol == t.col)
                        {
                            // blocked by something standing: change lanes
                            int to = t.col - 1;
                            if (!LaneAllowed(t.y, to)) to = t.col + 1;
                            if (LaneAllowed(t.y, to) && to != 4) t.targetCol = to;
                        }
                        // fast cars from behind never rear-end the camper
                        if (t.kind == 'f' && sameLane && gap > 0f && gap < 5f) sp = Mathf.Min(sp, camper.Speed - 0.3f);
                        if (t.state == 9) sp = t.baseSpeed + 6f; // bumped: speed away
                        t.speed = Mathf.MoveTowards(t.speed, sp, 12f * dt);
                        // ...even when the camper brakes hard: match its speed instantly
                        if (t.kind == 'f' && sameLane && gap > -0.5f && gap < 5f) t.speed = Mathf.Min(t.speed, Mathf.Max(0f, camper.Speed - 0.3f));
                        if (t.kind == 'v')
                        {
                            // Vespas weave, but blink first and never cut right into the camper
                            t.timer -= dt;
                            if (t.state == 1)
                            {
                                t.sr.color = Mathf.Repeat(Time.time * 8f, 1f) > 0.5f ? new Color(1f, 0.75f, 0.3f) : Color.white;
                                if (t.timer <= 0f)
                                {
                                    t.sr.color = Color.white;
                                    t.state = 0;
                                    t.timer = 1.6f + (float)t.rng.NextDouble() * 1.6f;
                                    float camDy = t.y - camper.Y;
                                    bool camperThere = Mathf.Abs(LaneX(t.y, t.timer2 > 0 ? t.col + 1 : t.col - 1) - camper.Lateral) < laneWidth * 0.8f && camDy > -4f && camDy < 9f;
                                    int to = t.col + (t.timer2 > 0 ? 1 : -1);
                                    if (!camperThere && LaneAllowed(t.y, to) && to != 4) t.targetCol = to;
                                }
                            }
                            else if (t.state == 0 && t.timer <= 0f && t.col == t.targetCol)
                            {
                                int dir = t.rng.NextDouble() < 0.5 ? -1 : 1;
                                int to = t.col + dir;
                                if (!LaneAllowed(t.y, to) || to == 4) dir = -dir;
                                t.timer2 = dir;
                                t.state = 1;
                                t.timer = 0.8f;
                            }
                        }
                        t.SteerToTarget(this, dt);
                        break;
                    }
                    case 'T':
                    {
                        if (t.state == 0)
                        {
                            // close in, then flash the lights
                            float want = gap > 4f ? camper.Speed + 7f : camper.Speed;
                            t.speed = Mathf.MoveTowards(t.speed, want, 14f * dt);
                            if (!sameLane && gap > 0f) t.state = 2; // you moved over: it overtakes
                            else if (gap <= 4.2f)
                            {
                                t.timer += dt;
                                t.sr.color = Mathf.Repeat(Time.time * 6f, 1f) > 0.5f ? new Color(1f, 1f, 0.6f) : Color.white;
                                if (t.timer > 3.2f && sameLane)
                                {
                                    t.speed = camper.Speed + 3f; // bump
                                    t.state = 1;
                                }
                            }
                        }
                        else if (t.state == 1)
                        {
                            if (gap < -0.2f || !sameLane) t.state = 2;
                        }
                        else
                        {
                            t.sr.color = Color.white;
                            // overtake in a free lane
                            if (sameLane && gap > -2f)
                            {
                                int to = t.col + (t.col > Leg.firstCol ? -1 : 1);
                                if (LaneAllowed(t.y, to) && to != 4) t.targetCol = to;
                            }
                            t.speed = Mathf.MoveTowards(t.speed, 19f, 8f * dt);
                        }
                        t.SteerToTarget(this, dt);
                        break;
                    }
                    case 'R':
                    {
                        t.timer -= dt;
                        if (t.state == 0)
                        {
                            // follow the camper's lane, but only while fully behind it and after a short delay
                            if (camper.Col != t.targetCol && gap > 0.4f && camper.Col != 4)
                            {
                                t.timer2 += dt;
                                if (t.timer2 > 1.2f) { t.targetCol = Mathf.Clamp(camper.Col, Leg.firstCol, 3); t.timer2 = 0f; }
                            }
                            else t.timer2 = 0f;
                            float want = Mathf.Max(8f, camper.Speed + (gap < 3f ? 3f : 2.4f));
                            t.speed = Mathf.MoveTowards(t.speed, want, 10f * dt);
                            if (gap < -CamperHalf.y) { t.state = 2; HUD.Instance?.Toast("Reviewer 2 overtook you. Phew!"); }
                            else if (t.timer <= 0f) { t.state = 3; HUD.Instance?.Toast("Reviewer 2 lost interest."); }
                        }
                        else if (t.state == 2) t.speed = Mathf.MoveTowards(t.speed, 19f, 6f * dt);
                        else t.speed = Mathf.MoveTowards(t.speed, camper.Speed - 5f, 6f * dt); // falls back
                        t.SteerToTarget(this, dt);
                        break;
                    }
                    case 'k':
                    {
                        // the cow wanders across the road
                        float edge = RoadHalfWidth(t.y) - 0.6f;
                        t.lateral += t.timer2 * 0.9f * dt;
                        if (t.lateral > edge) { t.lateral = edge; t.timer2 = -1f; }
                        if (t.lateral < -edge) { t.lateral = -edge; t.timer2 = 1f; }
                        t.sr.flipX = t.timer2 > 0f;
                        if (t.state == 9) { t.lateral += t.timer2 * 4f * dt; }
                        break;
                    }
                    case 'r':
                    {
                        if (t.state == 0 && camper.Y > t.y - 24f) { t.state = 1; t.timer = 0f; t.warn.gameObject.SetActive(true); Tip('r', "Falling rocks! Watch the shadows."); }
                        if (t.state == 1)
                        {
                            t.timer += dt;
                            float f = Mathf.Clamp01(t.timer / 1.4f);
                            t.warn.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.3f, f);
                            t.warn.color = new Color(0, 0, 0, 0.25f + 0.3f * f);
                            if (f > 0.6f)
                            {
                                t.sr.enabled = true;
                                float drop = (1f - f) * 10f;
                                t.sr.transform.localPosition = new Vector3(0, drop, 0);
                            }
                            if (f >= 1f)
                            {
                                t.state = 2; t.solid = true;
                                t.sr.transform.localPosition = Vector3.zero;
                                Effects.Burst(t.transform.position, new Color(0.6f, 0.6f, 0.62f), 10);
                            }
                        }
                        break;
                    }
                    case 'B':
                    {
                        // the barrier opens when you are slow enough
                        if (t.state == 0 && camper.Y > t.y - 6f && camper.Y < t.y)
                        {
                            if (camper.Speed <= tollSpeed)
                            {
                                OpenToll(t.y);
                            }
                            else if (camper.Y > t.y - 5.5f && t.col == Mathf.Clamp(camper.Col, 0, 3)) Tip('B', $"TOLL! Brake below {Mathf.RoundToInt(tollSpeed * camper.kmhPerUnit)} km/h!");
                        }
                        break;
                    }
                }

                t.y += t.speed * dt;

                // warning "!" at the bottom edge while a chaser is still below the screen
                if (t.warn && t.kind != 'r')
                {
                    bool below = t.y + t.half.y < camBottom;
                    t.warn.gameObject.SetActive(below && t.y > camBottom - 16f);
                    if (below)
                    {
                        t.warn.transform.position = new Vector3(t.WorldX(this), camBottom + 0.7f, 0);
                        t.warn.color = Mathf.Repeat(Time.time * 4f, 1f) > 0.4f ? Color.white : new Color(1, 1, 1, 0.3f);
                    }
                }

                t.Place(this);

                bool gone = t.y < camper.Y - despawnBehind - (IsChaser(t.kind.ToString()) && t.state == 0 ? 10f : 0f) || t.y > camper.Y + 70f;
                if (gone) Remove(t);
            }
        }

        void OpenToll(float y)
        {
            bool opened = false;
            foreach (var o in things)
            {
                if (o.kind != 'B' || Mathf.Abs(o.y - y) > 0.1f || o.state != 0) continue;
                o.state = 1;
                o.solid = false;
                o.sr.transform.localRotation = Quaternion.Euler(0, 0, 80f);
                o.sr.transform.localPosition = new Vector3(-laneWidth * 0.45f, 0.8f, 0);
                opened = true;
            }
            if (opened) { HUD.Instance?.Toast("Toll paid. Gute Fahrt!"); GameAudio.Play("barrier"); }
        }

        // ---------------- collisions ----------------
        void Collide()
        {
            Vector2 c = camper.transform.position;
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (!t || (!t.solid && !t.pickup)) continue;
                if (!t.Overlaps(c, CamperHalf, this)) continue;
                if (t.pickup) { Collect(t); continue; }
                if (t.hitDone) continue;
                if (!camper.Hit()) continue;
                OnHit(t);
                if (camper.Hp <= 0) { StartCoroutine(BrokeDown()); return; }
            }
        }

        void OnHit(RoadThing t)
        {
            var meter = PaperMeter.Instance;
            string msg;
            int loss = hitPaperLoss;
            switch (t.kind)
            {
                case 'o': msg = "DESK REJECT!"; loss = rejectPaperLoss; break;
                case 'R': msg = "Reviewer 2 rammed you! Major revisions."; loss = rejectPaperLoss; t.state = 3; break;
                case 'T': msg = "The tailgater bumped you!"; t.state = 2; break;
                case '=': msg = "Crashed into the roadworks!"; break;
                case 'k': msg = "Sorry, cow!"; t.state = 9; t.timer2 = t.lateral > camper.Lateral ? 1f : -1f; t.solid = false; break;
                case 'r': msg = "Hit a rock!"; break;
                case 'B': msg = "Crashed through the toll barrier!"; break;
                case 'v': msg = "Scusi! Hit a Vespa."; t.state = 9; break;
                case 'p': msg = "Scratched a parked car!"; break;
                default: msg = "Crash!"; t.state = 9; break;
            }
            t.hitDone = true;
            GameAudio.Play("crash");
            if (t.kind == 'k') GameAudio.Play("moo");
            if (t.kind == 'o') GameAudio.Play("paper_hit");
            if (t.kind == '=' || t.kind == 'r' || t.kind == 'B')
            {
                t.solid = false;
                t.sr.color = new Color(1, 1, 1, 0.4f);
                t.sr.transform.localRotation = Quaternion.Euler(0, 0, 35f);
            }
            meter?.Remove(loss);
            int left = camper.Hp;
            HUD.Instance?.Toast($"{msg}  -{PaperMeter.Pieces(loss)}   ({left} of {camper.maxHp} left)");
            Effects.Burst(camper.transform.position + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.3f), 14);
            if (left == 1) HUD.Instance?.Hint("The camper is smoking! One more crash ends the trip. Workshop: steer right.");
        }

        void Collect(RoadThing t)
        {
            var meter = PaperMeter.Instance;
            Color fx = new Color(0.5f, 0.9f, 1f);
            switch (t.kind)
            {
                case 'j': GameAudio.Play("fuel"); camper.AddFuel(jerryFuel); HUD.Instance?.Toast($"Jerry can! +{Mathf.RoundToInt(jerryFuel)} fuel"); fx = new Color(1f, 0.4f, 0.3f); lowFuelWarned = false; break;
                case 'a': GameAudio.Play("pickup_big"); meter?.Add(3); HUD.Instance?.Toast($"+{PaperMeter.Pieces(3)}"); break;
                case 'i': GameAudio.Play("pickup", 0.7f, 0.12f); meter?.Add(1); break;
                case 's': GameAudio.Play("toast_good"); meter?.Add(6); HUD.Instance?.Toast($"\"Reviewer 2 approved\" +{PaperMeter.Pieces(6)}"); fx = new Color(0.4f, 1f, 0.5f); break;
                case 'm': GameAudio.Play("mate_drink"); meter?.Add(1); camper.Boost(mateBoost); HUD.Instance?.Toast("Mate! Turbo for a few seconds"); fx = new Color(1f, 0.75f, 0.2f); break;
                case 'P':
                {
                    int leg = LegIndexAt(t.y);
                    Postcards[leg] = true;
                    SaveSystem.AddPhoto("L3_pc_" + legs[leg].code); // shown in the Level 4 credits
                    int n = 0; foreach (var b in Postcards) if (b) n++;
                    HUD.Instance?.Banner(legs[leg].postcardTitle, $"Postcard collected ({n}/{Postcards.Length})");
                    GameAudio.Play("postcard");
                    GameAudio.Play("flash", 0.6f);
                    fx = new Color(1f, 0.9f, 0.4f);
                    Effects.Burst(t.transform.position, Color.white, 20);
                    break;
                }
            }
            Effects.Burst(t.transform.position, fx, 10);
            Remove(t);
        }

        // ---------------- stations ----------------
        void Stations()
        {
            var s = StationAt(camper.Y);
            if (s != null && !s.used && camper.Col == 3 && Mathf.Abs(camper.Y - s.y0) < 6f)
                HUD.Instance?.Hint(s.workshop ? "WORKSHOP on the right: steer right to repair (-" + workshopPenalty + " s)" : "GAS STATION on the right: steer right to fill up (-" + gasPenalty + " s)");
            else if (!Busy) HUD.Instance?.Hint(camper.Hp <= 1 ? "The camper is smoking! One more crash ends the trip. Workshop: steer right." : null);

            if (camper.Col == 4)
            {
                foreach (var st in stations)
                    if (camper.Y >= st.y0 - 3f && camper.Y <= st.y1 && !st.used) { StartCoroutine(Service(st)); return; }
                // past the station lane: merge back
                if (StationAt(camper.Y) == null) camper.ForceLane(3);
            }
        }

        IEnumerator Service(Station st)
        {
            st.used = true;
            Busy = true;
            camper.SteeringLocked = true;
            HUD.Instance?.Hint(null);
            // roll up to the building, then stop
            camper.Autopilot = Mathf.Min(camper.Speed, 6f);
            while (camper.Y < st.y0 + 6.5f) yield return null;
            camper.Autopilot = 0f;
            while (camper.Speed > 0.2f) yield return null;
            var clock = DeadlineClock.Instance;
            if (st.workshop)
            {
                if (camper.Hp < camper.maxHp)
                {
                    HUD.Instance?.Toast($"Workshop: fixing the dents...  -{workshopPenalty:0} s");
                    GameAudio.Play("repair");
                    yield return new WaitForSeconds(2.2f);
                    clock?.AddPenalty(workshopPenalty);
                    camper.Repair();
                    camper.AddFuel(15f);
                    HUD.Instance?.Toast("Good as new!");
                    GameAudio.Play("toast_good");
                }
                else
                {
                    HUD.Instance?.Toast("Mechanic: \"Nothing to fix, drive on!\"");
                    yield return new WaitForSeconds(0.8f);
                }
            }
            else
            {
                HUD.Instance?.Toast($"Filling up the tank...  -{gasPenalty:0} s");
                GameAudio.Play("fuel");
                yield return new WaitForSeconds(1.4f);
                clock?.AddPenalty(gasPenalty);
                camper.AddFuel(camper.fuelMax);
                lowFuelWarned = false;
                HUD.Instance?.Toast("Full tank!");
            }
            camper.Autopilot = 7f;
            yield return new WaitForSeconds(0.5f);
            camper.ForceLane(3);
            camper.SteeringLocked = false;
            yield return new WaitForSeconds(0.4f);
            camper.Autopilot = null;
            Busy = false;
            // stations are usable again after a restart at the border
        }

        // ---------------- curves (Alps) ----------------
        void Curves(float dt)
        {
            if (Leg.curveAmp <= 0f) return;
            float k = Mathf.Abs(RoadOffset(camper.Y + 2f) - 2f * RoadOffset(camper.Y) + RoadOffset(camper.Y - 2f));
            if (camper.Speed > curveSlideSpeed && k > 0.05f)
            {
                curveT += dt;
                if (curveT > 0.9f)
                {
                    curveT = 0f;
                    float bend = RoadOffset(camper.Y + 3f) - RoadOffset(camper.Y);
                    int outward = bend > 0 ? -1 : 1;
                    if (camper.ChangeLane(outward)) HUD.Instance?.Toast("Too fast for the curve!");
                }
            }
            else curveT = 0f;
        }

        // ---------------- fuel ----------------
        void FuelCheck()
        {
            if (!lowFuelWarned && camper.Fuel < camper.fuelMax * 0.25f)
            {
                lowFuelWarned = true;
                HUD.Instance?.Toast("Fuel is low! Grab jerry cans or stop at the gas station.");
                GameAudio.Play("toast_bad");
            }
            if (camper.Fuel <= 0f) StartCoroutine(Fail("Out of fuel!"));
        }

        // ---------------- fail / restart ----------------
        IEnumerator Fail(string reason)
        {
            if (Busy) yield break;
            Busy = true;
            camper.Autopilot = 0f;
            camper.SteeringLocked = true;
            var leg = legs[CheckpointLeg];
            GameAudio.Play("jingle_fail", 0.7f);
            GameAudio.DuckMusic(4f);
            RoadHUD.Instance?.ShowFail(reason, CheckpointLeg == 0 ? "Back to the start..." : $"Back to the border: {leg.name}");
            yield return new WaitForSeconds(2.2f);
            if (Lost) yield break;
            RestartAtCheckpoint();
            RoadHUD.Instance?.HideFail();
            Busy = false;
        }

        void RestartAtCheckpoint()
        {
            foreach (var t in things) if (t) Destroy(t.gameObject);
            things.Clear();
            armed.Clear();
            var leg = legs[CheckpointLeg];
            float y = CheckpointLeg == 0 ? startY : leg.startY + 3f;
            foreach (var s in stations) if (s.y0 >= y) s.used = false;
            int col = Mathf.Clamp(2, leg.firstCol, 3);
            camper.Place(y, col, 5f);
            LegIndex = CheckpointLeg;
            spawnIdx = 0;
            while (spawnIdx < events.Count && events[spawnIdx].y < y - 1f) spawnIdx++;
            lowFuelWarned = false;
            UpdateCamera(true);
        }

        // ---------------- end ----------------
        void OnDeadline()
        {
            if (Finished) return;
            Lost = true;
            StopAllCoroutines();
            camper.Autopilot = 0f;
            GameInput.Locked = true;
            RoadHUD.Instance?.HideFail();
            RoadHUD.Instance?.ShowLost();
            GameAudio.Play("deadline");
            GameAudio.Play("jingle_fail", 0.8f);
            GameAudio.DuckMusic(6f);
            StartCoroutine(WaitRetry());
        }

        IEnumerator WaitRetry()
        {
            while (true)
            {
                if (GameInput.RetryPressed || GameInput.ConfirmPressed) { Replay(); yield break; }
                yield return null;
            }
        }

        IEnumerator Finish()
        {
            if (Finished) yield break;
            Finished = true;
            Busy = true;
            var clock = DeadlineClock.Instance;
            if (clock) clock.Running = false;
            camper.Autopilot = 0f;
            camper.SteeringLocked = true;
            HUD.Instance?.Banner("You made it to the Paulinum!", "The defense can begin");
            GameAudio.Play("jingle_win");
            GameAudio.Play("applause", 0.8f);
            GameAudio.PlayMusic("victory", 2f);
            Effects.Burst(camper.transform.position, new Color(1f, 0.85f, 0.3f), 30);
            yield return new WaitForSeconds(3.5f);
            float fill = PaperMeter.Instance ? PaperMeter.Instance.Fill : 0f;
            SaveSystem.SaveBestCompletion("L3", fill);
            SaveSystem.SaveLastCompletion("L3", fill);
            SaveSystem.SaveOutcome("L3", "arrived");
            GameInput.Locked = true;
            RoadHUD.Instance?.ShowArrival(this, fill, clock ? clock.RemainingText : "");
            yield return EndScreenInput();
        }

        /// <summary>Third crash: the trip is over and the paper is submitted in its current state.</summary>
        IEnumerator BrokeDown()
        {
            if (Finished) yield break;
            Finished = true;
            brokeDown = true;
            Busy = true;
            var clock = DeadlineClock.Instance;
            if (clock) clock.Running = false;
            camper.Autopilot = 0f;
            camper.SteeringLocked = true;
            HUD.Instance?.Hint(null);
            HUD.Instance?.Banner("The camper broke down!", "Submitting the paper as is...");
            GameAudio.Play("jingle_fail");
            GameAudio.DuckMusic(6f);
            for (int i = 0; i < 12; i++) { Puff(camper.transform.position + Vector3.up * 0.5f); yield return new WaitForSeconds(0.15f); }
            yield return new WaitForSeconds(0.6f);
            float fill = PaperMeter.Instance ? PaperMeter.Instance.Fill : 0f;
            SaveSystem.SaveBestCompletion("L3", fill);
            SaveSystem.SaveLastCompletion("L3", fill);
            SaveSystem.SaveOutcome("L3", "broken");
            GameInput.Locked = true;
            RoadHUD.Instance?.ShowSubmitted(this, fill);
            yield return EndScreenInput();
        }

        /// <summary>Enter / A continues (next scene), R / X drives again.</summary>
        IEnumerator EndScreenInput()
        {
            yield return null;
            while (true)
            {
                if (GameInput.ConfirmPressed)
                {
                    // the Paulinum door: the code comes from the real-life mini game (gate 3 in peer_review_codes.json)
                    bool accepted = false, closed = false;
                    RoadHUD.Instance?.SetEndVisible(false);
                    var gate = PaulinumGate();
                    gate.OnAccepted = () => accepted = true;
                    gate.OnClosed = () => closed = true;
                    gate.OpenFromCode();
                    while (!accepted && !closed) yield return null;
                    if (accepted)
                    {
                        GameInput.Locked = false;
                        string next = NextLevelScene();
                        if (!string.IsNullOrEmpty(next) && Application.CanStreamedLevelBeLoaded(next)) SceneManager.LoadScene(next);
                        yield break;
                    }
                    // stepped away with Esc: back to the travel memories
                    GameInput.Locked = true;
                    RoadHUD.Instance?.SetEndVisible(true);
                    yield return null;
                    continue;
                }
                if (GameInput.RetryPressed) { Replay(); yield break; }
                yield return null;
            }
        }

        PeerReviewGate paulinumGate;

        /// <summary>The keypad at the Paulinum entrance, using the Rebuttal Room keypad of Levels 1 and 2 (gate 3).</summary>
        PeerReviewGate PaulinumGate()
        {
            if (paulinumGate) return paulinumGate;
            var go = new GameObject("PaulinumGate_3");
            go.transform.SetParent(transform);
            paulinumGate = go.AddComponent<PeerReviewGate>();
            paulinumGate.gateNumber = 3;
            paulinumGate.customTitle = "PAULINUM ENTRANCE  (Gate 3)\nWin the real-life challenge, then enter its code.";
            return paulinumGate;
        }

        /// <summary>The next "Level..." scene in build order (Level 4 once it exists), otherwise <see cref="nextScene"/> (Start Screen).</summary>
        string NextLevelScene()
        {
            for (int i = SceneManager.GetActiveScene().buildIndex + 1; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (name.StartsWith("Level")) return name;
            }
            return nextScene;
        }

        public void Replay()
        {
            GameInput.Locked = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------- test helpers (used from the editor while tuning) ----------------
        public void DebugJumpTo(float y)
        {
            foreach (var t in things) if (t) Destroy(t.gameObject);
            things.Clear(); armed.Clear();
            camper.Place(y, Mathf.Clamp(2, LegAt(y).firstCol, 3), 9f);
            spawnIdx = 0;
            while (spawnIdx < events.Count && events[spawnIdx].y < y - 1f) spawnIdx++;
            UpdateCamera(true);
        }

        public int ThingCount => things.Count;
    }

    /// <summary>Grows, drifts and fades, then disappears.</summary>
    public class SmokePuff : MonoBehaviour
    {
        float t;
        SpriteRenderer sr;
        void Awake() { sr = GetComponent<SpriteRenderer>(); transform.localScale = Vector3.one * 0.25f; }
        void Update()
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.one * (0.25f + t * 0.6f);
            transform.position += new Vector3(0.3f, -2.5f, 0) * Time.deltaTime;
            var c = sr.color; c.a = Mathf.Max(0f, 0.7f - t * 0.8f); sr.color = c;
            if (t > 0.9f) Destroy(gameObject);
        }
    }
}
