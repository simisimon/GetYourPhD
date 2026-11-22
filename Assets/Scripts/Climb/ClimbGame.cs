using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GetYourPhD
{
    [System.Serializable]
    public class ClimbArt
    {
        public Sprite pixel, slab, sign, pillar, lift, infoDesk, bench, organ, slide, paulinum;
        public Sprite[] walls = new Sprite[4];      // foyer, lecture, library, aula
        public Sprite[] platforms = new Sprite[4];
        public Sprite reject, beamer, paperStack, pipe;
        public Sprite dataPoint, citation, paper, mate, photo;
        public Sprite drone, coffee, imposter, warn, redPen, configBox;
    }

    /// <summary>One photo / postcard for the credits.</summary>
    [System.Serializable]
    public class CreditPhoto
    {
        public string id;
        public string caption;
        public Sprite image;
    }

    /// <summary>
    /// Level 4 "Climb the Paulinum": a vertical Doodle Jump style climb through four floors (Foyer, Lecture Halls,
    /// Library, Felix-Klein-Hörsaal) to the defense, then the boss fight against the supervisor on the top floor.
    /// The level is generated at runtime from a fixed seed (same layout every time), so tuning happens in the
    /// fields below rather than in a map file. Falling out of the screen drops Johannes back to the last landing (-10 s).
    /// </summary>
    public class ClimbGame : MonoBehaviour
    {
        public static ClimbGame Instance { get; private set; }

        [Header("Refs")]
        public ClimbArt art = new ClimbArt();
        public Font font;
        public Camera cam;
        public ClimbPlayer player;
        public SiegmundBoss boss;
        public HowToPlayOverlay howToPlay;
        public List<CreditPhoto> creditPhotos = new List<CreditPhoto>();
        [TextArea] public string creditsText = "Made with love for Dr. Johannes Dorn's PhD defense.";

        [Header("Layout")]
        public int seed = 4242;
        public float floorHeight = 70f;
        public float halfWidth = 11f;
        public float cameraLead = 2.2f;

        [Header("Rules")]
        public float fallPenaltySeconds = 10f;
        public float academicQuarterSeconds = 75f;
        [Tooltip("Paper % at the start if no earlier level was played (e.g. testing Level 4 directly).")]
        [Range(0f, 1f)] public float defaultCarryOver = 0.6f;
        [Tooltip("Share of the Paper meter that comes from Levels 1-3 (the rest is collected here).")]
        [Range(0f, 1f)] public float carryOverShare = 0.55f;
        public int minShield = 2;
        public int bonusShieldAtFull = 4;
        public float cupInterval = 3.2f;
        public float bossPaperInterval = 14f;
        [Header("Boss fight (on foot, throwing Mate)")]
        public int startMates = 3;
        public float bossMateInterval = 3f;
        public int maxMatesOnFloor = 2;

        public static readonly string[] FloorNames = { "Foyer", "Lecture Halls", "Library", "Felix-Klein-Hörsaal" };
        static readonly string[] FloorSubs =
        {
            "The lift is \"AUSSER BETRIEB\". Climb!",
            "1st floor: mind the moving beamers",
            "2nd floor: \"Desk Reject\" stacks crumble",
            "3rd floor: organ pipes bounce you high",
        };

        public float ArenaY => 4f * floorHeight;
        public int CurrentFloor { get; private set; }
        public bool BossStarted { get; private set; }
        public bool Won { get; private set; }
        public int Shield { get; private set; }
        public int MaxShield { get; private set; }
        public float CarryOver { get; private set; }
        public bool Running { get; private set; }
        public float Altitude01 => player ? Mathf.Clamp01(player.Feet / ArenaY) : 0f;
        public int Falls { get; private set; }

        readonly List<float> checkpoints = new List<float>();
        float checkpointY;
        float camY;
        int bannerFloor = -1;
        bool ctUsed, missed;
        float ctStart;
        float cupTimer, bossPaperTimer;
        Transform world;
        System.Random rnd;
        float bossMateTimer;

        void Awake()
        {
            Instance = this;
            GameInput.Locked = false;
            Time.timeScale = 1f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            world = new GameObject("World").transform;
            Generate();
            SetupPaper();
            var clock = DeadlineClock.Instance;
            if (clock) { clock.Running = false; clock.OnExpired += OnDeadline; }
            var hud = HUD.Instance;
            if (hud)
            {
                hud.HideMatePanel();
                hud.clockTextProvider = ClockText;
                hud.clockSubProvider = () => BossStarted ? "The defense has begun" : missed ? "the committee is waiting" : (ctUsed ? "academic quarter!" : $"left {clock.RemainingText}   {FloorNames[CurrentFloor]}");
            }
            player.OnShieldHit += OnShieldHit;
            player.Frozen = true;
            player.Teleport(new Vector2(0f, 0f), 0f);
            camY = cam.orthographicSize - 0.8f;
            PlaceCamera();
            if (boss)
            {
                boss.arenaFloorY = ArenaY;
                boss.halfWidth = halfWidth - 1.5f;
                boss.transform.position = new Vector3(0, boss.HoverY, 0);
                boss.OnPhaseChanged += OnBossPhase;
                boss.OnDefeated += () => StartCoroutine(WinFlow());
            }
            yield return null;
            if (howToPlay) yield return howToPlay.ShowIfNew(); // only the game mechanics screen; the story card was dropped
            player.Frozen = false;
            player.Bounce(1f);
            if (clock) clock.Running = true;
            Running = true;
            HUD.Instance?.Banner("Level 4: Climb the Paulinum", $"The defense starts at {(clock ? clock.DeadlineText.Trim() : "14:00")} in the Felix-Klein-Hörsaal, top floor!");
            bannerFloor = 0;
        }

        string IntroText()
        {
            string outcome = SaveSystem.Outcome("L3");
            string arrive = outcome == "broken" ? "The camper gave up on the Autobahn, but Johannes made it to the Paulinum on foot." :
                            outcome == "arrived" ? "The camper is parked, the code worked: Johannes is in the Paulinum." :
                            "Johannes is in the Paulinum.";
            return arrive + "\n\nThe defense is in the <b>Felix-Klein-Hörsaal</b> on the top floor at <b>14:00</b>. And the lift? <b>\"AUSSER BETRIEB\"</b>.\nThere is only one way up: climb!";
        }

        // ---------------- paper & shield ----------------
        void SetupPaper()
        {
            var meter = PaperMeter.Instance;
            if (!meter) return;
            meter.maxPoints = 100;
            float sum = 0f; int n = 0;
            foreach (var id in new[] { "L1", "L2", "L3" })
            {
                float f = SaveSystem.BestCompletion(id);
                if (id == "L3" && SaveSystem.Outcome("L3") != "") f = SaveSystem.LastCompletion("L3");
                if (f > 0f) { sum += f; n++; }
            }
            CarryOver = n > 0 ? sum / 3f : defaultCarryOver;
            meter.Add(Mathf.RoundToInt(CarryOver * carryOverShare * 100f));
        }

        int ComputeShield()
        {
            float fill = PaperMeter.Instance ? PaperMeter.Instance.Fill : 0.5f;
            return minShield + Mathf.RoundToInt(fill * bonusShieldAtFull);
        }

        void OnShieldHit(string reason)
        {
            if (!BossStarted || Won) return;
            Shield = Mathf.Max(0, Shield - 1);
            if (Shield > 0) HUD.Instance?.Toast($"{reason}! Paper shield: {Shield} left");
            else
            {
                HUD.Instance?.Toast("Out of arguments... let's discuss this chapter again.");
                GameAudio.Play("toast_bad");
                Shield = MaxShield;
                boss?.RestartPhase();
            }
        }

        public void OnPickup(ClimbPickupKind kind)
        {
            if (BossStarted && kind == ClimbPickupKind.Paper && Shield < MaxShield)
            {
                Shield++;
                GameAudio.Play("toast_good", 0.7f);
                HUD.Instance?.Toast("Fresh paper: +1 shield");
            }
        }

        // ---------------- clock ----------------
        string ClockText()
        {
            var clock = DeadlineClock.Instance;
            if (!clock) return "";
            if (BossStarted) return missed ? "LATE" : clock.DeadlineText.Trim();
            if (missed) return Mathf.Repeat(Time.time, 1f) > 0.5f ? "LATE!" : "14:15 c.t.";
            if (ctUsed)
            {
                float used = 1f - clock.Remaining / Mathf.Max(1f, academicQuarterSeconds);
                int m = Mathf.Clamp(Mathf.FloorToInt(used * 15f), 0, 15);
                return $"{clock.deadlineHour:00}:{clock.deadlineMinute + m:00} c.t.";
            }
            return clock.ClockText.Trim();
        }

        void OnDeadline()
        {
            if (BossStarted || Won) return;
            var clock = DeadlineClock.Instance;
            if (!ctUsed)
            {
                ctUsed = true;
                clock.Extend(academicQuarterSeconds);
                GameAudio.Play("bell");
                HUD.Instance?.Banner($"{clock.DeadlineText.Trim()}! But it's the academic quarter", "\"c.t.\": the defense starts 15 minutes late. Hurry!");
                return;
            }
            StartCoroutine(MissedFlow());
        }

        /// <summary>
        /// Second expiry: no game over. The committee waits (the clock shows LATE) so everyone can still reach
        /// the defense; Ctrl+Shift+R restarts the level for a better time.
        /// </summary>
        IEnumerator MissedFlow()
        {
            if (missed) yield break;
            missed = true;
            var clock = DeadlineClock.Instance;
            if (clock) clock.Running = false;
            GameAudio.Play("deadline");
            HUD.Instance?.Banner("Even the academic quarter is over!", "The committee is waiting for you... keep climbing!");
        }

        // ---------------- per frame ----------------
        void Update()
        {
            if (!player) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                if (kb.rKey.wasPressedThisFrame) { GameInput.Locked = false; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
                if (kb.pKey.wasPressedThisFrame && Running && !BossStarted) DebugJumpTo(ArenaY - 3f); // organizer skip to the defense
            }
            if (!Running) { PlaceCamera(); return; }

            float feet = player.Feet;
            CurrentFloor = Mathf.Clamp(Mathf.FloorToInt(feet / floorHeight), 0, 3);
            if (CurrentFloor > bannerFloor && !BossStarted && feet > CurrentFloor * floorHeight + 0.5f)
            {
                bannerFloor = CurrentFloor;
                HUD.Instance?.Banner(FloorNames[CurrentFloor], FloorSubs[CurrentFloor]);
                GameAudio.Play("postcard", 0.8f);
            }
            // checkpoint = highest landing Johannes has bounced on
            // (also counts landings Johannes flew past with a Mate or a spring)
            foreach (var c in checkpoints) if (c <= feet + 0.05f && c > checkpointY) checkpointY = c;

            if (!BossStarted)
            {
                camY = Mathf.Max(camY, Mathf.Lerp(camY, feet + cameraLead, 1f - Mathf.Exp(-8f * Time.deltaTime)));
                camY = Mathf.Min(camY, ArenaY + ArenaCamOffset);
                if (feet < camY - cam.orthographicSize - 0.6f) FellDown();
                if (feet >= ArenaY - 0.01f && player.LastPlatform && player.LastPlatform.Top >= ArenaY - 0.01f) StartCoroutine(BeginBoss());
                Cups();
            }
            else
            {
                camY = Mathf.Lerp(camY, ArenaY + ArenaCamOffset, 1f - Mathf.Exp(-4f * Time.deltaTime));
                BossPapers();
                BossMates();
                if (reticle) { reticle.enabled = !Won && player.mateAmmo > 0; reticle.transform.position = (Vector3)ClimbPlayer.MouseWorld(); }
            }
            PlaceCamera();
        }

        float ArenaCamOffset => cam.orthographicSize - 0.6f;

        void PlaceCamera()
        {
            cam.transform.position = new Vector3(0f, camY, -10f);
        }

        void FellDown()
        {
            Falls++;
            float y = checkpointY;
            Debug.Log($"[Climb] fall #{Falls} at t={Time.timeSinceLevelLoad:0} from feet {player.Feet:0.0} (cam {camY:0.0}) back to {y:0.0}");
            player.Teleport(new Vector2(0f, y + 0.02f), player.bounceSpeed);
            camY = Mathf.Max(cam.orthographicSize - 0.8f, y + cameraLead);
            DeadlineClock.Instance?.AddPenalty(fallPenaltySeconds);
            HUD.Instance?.Toast($"Fell down the stairwell! Back to the last landing, -{fallPenaltySeconds:0}s");
            foreach (var pl in ClimbPlatform.All) if (pl.Top > y && pl.Top < y + 30f) pl.Restore();
            // a second chance: the imposter shadows nearby back off for a while
            foreach (var sh in FindObjectsByType<ImposterShadow>(FindObjectsSortMode.None))
                if (Mathf.Abs(sh.transform.position.y - y) < 30f) sh.Banish(10f, true);
        }

        void Cups()
        {
            if (CurrentFloor != 1 && CurrentFloor != 3) return;
            float floorBase = CurrentFloor * floorHeight;
            if (player.Feet < floorBase + 6f) return;
            cupTimer -= Time.deltaTime;
            if (cupTimer > 0f) return;
            cupTimer = cupInterval * (CurrentFloor == 3 ? 0.85f : 1f) * Random.Range(0.8f, 1.2f);
            float x = Mathf.Clamp(player.transform.position.x + Random.Range(-5f, 5f), -halfWidth + 1f, halfWidth - 1f);
            StartCoroutine(DropCup(x));
        }

        IEnumerator DropCup(float x)
        {
            // warning sign at the top edge first
            var w = Sprite("CupWarning", art.warn, cam.transform, new Vector3(x, cam.orthographicSize - 0.7f, 10f), 70);
            w.color = new Color(1f, 0.9f, 0.3f);
            for (float t = 0; t < 0.9f; t += Time.deltaTime)
            {
                if (w) w.enabled = Mathf.Repeat(t * 6f, 1f) > 0.3f;
                yield return null;
            }
            if (w) Destroy(w.gameObject);
            if (BossStarted || !Running) yield break;
            var cup = Sprite("Coffee", art.coffee, world, new Vector3(x, camY + cam.orthographicSize + 0.8f, 0), 12);
            var f = cup.gameObject.AddComponent<ClimbFaller>();
            f.speed = 6.5f; f.spin = 120f; f.radius = 0.45f; f.reason = "Coffee spill";
            f.killBelow = camY - cam.orthographicSize - 30f;
        }

        void BossPapers()
        {
            if (Won || boss == null || !boss.Active) return;
            bossPaperTimer -= Time.deltaTime;
            if (bossPaperTimer > 0f) return;
            bossPaperTimer = bossPaperInterval;
            foreach (var pk in FindObjectsByType<ClimbPickup>(FindObjectsSortMode.None)) if (pk.kind == ClimbPickupKind.Paper) return;
            Pickup(FloorSpotAwayFromPlayer(), ArenaY + 0.7f, ClimbPickupKind.Paper);
        }

        /// <summary>Boss fight ammo: Mate bottles appear at random spots on the Felix-Klein-Hörsaal floor.</summary>
        void BossMates()
        {
            if (Won || boss == null || !boss.Active) return;
            bossMateTimer -= Time.deltaTime;
            if (bossMateTimer > 0f) return;
            bossMateTimer = bossMateInterval * Random.Range(0.75f, 1.25f);
            int onFloor = 0;
            foreach (var pk in FindObjectsByType<ClimbPickup>(FindObjectsSortMode.None)) if (pk.kind == ClimbPickupKind.MateAmmo) onFloor++;
            if (onFloor >= maxMatesOnFloor) return;
            Pickup(FloorSpotAwayFromPlayer(), ArenaY + 0.7f, ClimbPickupKind.MateAmmo);
        }

        float FloorSpotAwayFromPlayer()
        {
            float x = 0f;
            for (int i = 0; i < 8; i++)
            {
                x = Random.Range(-halfWidth + 1.5f, halfWidth - 1.5f);
                if (Mathf.Abs(x - player.transform.position.x) > 2.5f) break;
            }
            return x;
        }

        SpriteRenderer reticle;

        SpriteRenderer MakeReticle()
        {
            const int n = 17;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 8) * (x - 8) + (y - 8) * (y - 8));
                    bool ring = d > 6.2f && d < 7.6f;
                    bool cross = (x == 8 && (y < 5 || y > 11)) || (y == 8 && (x < 5 || x > 11));
                    bool dot = x == 8 && y == 8;
                    px[y * n + x] = ring || cross ? new Color(1f, 0.85f, 0.3f) : dot ? Color.white : Color.clear;
                }
            tex.SetPixels(px); tex.Apply();
            var sr = Sprite("AimReticle", UnityEngine.Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f), world, Vector3.zero, 80);
            return sr;
        }

        IEnumerator BeginBoss()
        {
            if (BossStarted) yield break;
            BossStarted = true;
            player.BossMode = true;
            var clock = DeadlineClock.Instance;
            if (clock) clock.Running = false;
            foreach (var d in FindObjectsByType<ClimbDrone>(FindObjectsSortMode.None)) Destroy(d.gameObject);
            foreach (var s in FindObjectsByType<ImposterShadow>(FindObjectsSortMode.None)) Destroy(s.gameObject);
            foreach (var f in FindObjectsByType<ClimbFaller>(FindObjectsSortMode.None)) Destroy(f.gameObject);
            MaxShield = ComputeShield();
            Shield = MaxShield;
            player.GroundY = ArenaY;
            player.mateAmmo = startMates;
            HUD.Instance?.Banner("THE DEFENSE", "Run left/right, grab Mate and click to throw it at Prof. Siegmund!");
            GameAudio.Play("boss_phase");
            GameAudio.PlayMusic("boss", 1f);
            ClimbHUD.Instance?.ShowBoss(boss);
            bossPaperTimer = bossPaperInterval;
            bossMateTimer = 1f;
            reticle = MakeReticle();
            yield return new WaitForSeconds(2.5f);
            boss?.Begin();
        }

        void OnBossPhase(int phase)
        {
            string[] subs = { "", "", "\"Just one more configuration!\"", "\"And what about the threats to validity?\"" };
            HUD.Instance?.Banner($"Phase {phase} of 3", subs[Mathf.Clamp(phase, 0, 3)]);
            GameAudio.Play("boss_phase");
        }

        IEnumerator WinFlow()
        {
            Won = true;
            Running = false;
            yield return new WaitForSeconds(1.2f);
            player.SetVictory();
            Effects.Burst(player.transform.position + Vector3.up, new Color(1f, 0.85f, 0.3f), 40);
            yield return new WaitForSeconds(2.8f);
            float fill = PaperMeter.Instance ? PaperMeter.Instance.Fill : 0f;
            SaveSystem.SaveBestCompletion("L4", fill);
            SaveSystem.SaveLastCompletion("L4", fill);
            SaveSystem.SaveOutcome("L4", "defended");
            SaveSystem.Checkpoint = "";
            GameInput.Locked = true;
            var hud = ClimbHUD.Instance;
            if (hud)
            {
                yield return hud.DoctorScreen(fill, Falls);
                yield return hud.Credits(creditPhotos, creditsText);
            }
            GameInput.Locked = false;
            SceneManager.LoadScene("StartScreen");
        }

        // ---------------- level generation ----------------
        struct FloorParams
        {
            public float stepMin, stepMax, wMin, wMax, moving, crumble, spring, droneEvery, droneSpeed;
            public int shadows;
        }

        static FloorParams Params(int f)
        {
            switch (f)
            {
                case 0: return new FloorParams { stepMin = 1.6f, stepMax = 2.3f, wMin = 3.0f, wMax = 3.8f, moving = 0f, crumble = 0f, spring = 0.05f, droneEvery = 32f, droneSpeed = 2f };
                case 1: return new FloorParams { stepMin = 1.8f, stepMax = 2.5f, wMin = 2.6f, wMax = 3.4f, moving = 0.55f, crumble = 0f, spring = 0.05f, droneEvery = 24f, droneSpeed = 2.4f };
                case 2: return new FloorParams { stepMin = 1.9f, stepMax = 2.7f, wMin = 2.5f, wMax = 3.2f, moving = 0.15f, crumble = 0.35f, spring = 0.06f, droneEvery = 24f, droneSpeed = 2.6f, shadows = 2 };
                default: return new FloorParams { stepMin = 2.0f, stepMax = 2.8f, wMin = 2.3f, wMax = 3.0f, moving = 0.3f, crumble = 0.12f, spring = 0.12f, droneEvery = 20f, droneSpeed = 3f, shadows = 1 };
            }
        }

        void Generate()
        {
            rnd = new System.Random(seed);
            for (int f = 0; f < 4; f++) BuildFloor(f);
            BuildArena();
            checkpointY = 0f;
        }

        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        bool Chance(float p) => rnd.NextDouble() < p;

        void BuildFloor(int f)
        {
            float b = f * floorHeight;
            var P = Params(f);
            Background(f, b, floorHeight);
            Landing(b, f == 0 ? "FOYER  (EG)" : $"{f}. OG  {FloorNames[f].ToUpperInvariant()}", f);
            // stair landings at 1/3 and 2/3 of each floor (checkpoints)
            var mids = new[] { b + floorHeight / 3f, b + floorHeight * 2f / 3f };
            string fl = f == 0 ? "EG" : f + ". OG";
            Landing(mids[0], $"{fl}  STAIRS 1/3", f);
            Landing(mids[1], $"{fl}  STAIRS 2/3", f);
            int midIdx = 0;
            float mid = mids[0];
            Decor(f, b);

            float y = b + 0.3f;
            float prevX = 0f;
            bool prevCrumble = false;
            float nextDrone = b + (f == 0 ? 26f : 12f);
            float nextMate = b + R(20f, 30f);
            int shadowsLeft = P.shadows;
            float nextShadow = b + 16f;
            while (true)
            {
                float step = R(P.stepMin, P.stepMax);
                // a little harder towards the top of each floor
                step += (y - b) / floorHeight * 0.25f;
                float lastY = y;
                y += Mathf.Min(step, 2.9f);
                if (midIdx < 2 && y > mid - 1.4f && mid - lastY <= 2.9f) { y = mid; midIdx++; if (midIdx < 2) mid = mids[midIdx]; continue; }
                if (y > b + floorHeight - 1.8f)
                {
                    // never leave a gap the bounce can barely make to the next floor's landing
                    float gap = b + floorHeight - lastY;
                    if (gap > 2.7f) Plat(R(-halfWidth + 3f, halfWidth - 3f), lastY + gap * 0.5f, R(P.wMin, P.wMax), PlatKind.Normal, art.platforms[f], PlatH(f), Tiling.Tiled);
                    break;
                }

                float w = R(P.wMin, P.wMax);
                float x;
                // next platform 2.5-6.5 units to the side of the previous one: always reachable, never straight above
                float lim = halfWidth - 0.8f - w / 2f;
                float side = Chance(0.5f) ? 1f : -1f, off = R(2.5f, 6.5f);
                x = prevX + side * off;
                if (x > lim || x < -lim) x = prevX - side * off;
                x = Mathf.Clamp(x, -lim, lim);
                // right after a landing: keep the next platform close to the middle and the jump short
                if (lastY == b + 0.3f || Mathf.Abs(lastY - mids[0]) < 0.01f || Mathf.Abs(lastY - mids[1]) < 0.01f) { x = R(-4f, 4f); y = Mathf.Min(y, lastY + 2.2f); }
                prevX = x;

                // the real path is always static platforms; beamers and Desk Rejects are extras beside it,
                // so a crumbled stack or a beamer on the far side never cuts the way up
                var pl = Plat(x, y, w, PlatKind.Normal, art.platforms[f], PlatH(f), Tiling.Tiled);
                if (Chance(P.spring))
                {
                    float sx = x + R(-w / 2f + 0.6f, w / 2f - 0.6f);
                    if (f == 3) Spring(sx, y, art.pipe, 1.5f, 1.8f);
                    else Spring(sx, y, art.paperStack, 0.8f, 1.6f);
                }
                else if (Chance(0.5f))
                {
                    double k = rnd.NextDouble();
                    Pickup(x + R(-w / 3f, w / 3f), y + 0.9f, k < 0.12 ? ClimbPickupKind.Paper : k < 0.38 ? ClimbPickupKind.Citation : ClimbPickupKind.DataPoint);
                }
                // free side of the row for an extra
                float ew = R(P.wMin, P.wMax);
                float lo = x > 0 ? -halfWidth + 1f + ew / 2f : x + w / 2f + ew / 2f + 1.2f;
                float hi = x > 0 ? x - w / 2f - ew / 2f - 1.2f : halfWidth - 1f - ew / 2f;
                bool room = hi - lo > 0.5f;
                if (room && !prevCrumble && Chance(P.crumble))
                {
                    var cp = Plat(R(lo, hi), y + R(0.4f, 1.1f), ew, PlatKind.Crumble, art.reject, PlatH(f), Tiling.Tiled);
                    Label(cp.transform, "DESK REJECT", new Vector2(0, -0.2f), 0.035f, new Color(1f, 0.85f, 0.85f), 55, TextAnchor.UpperCenter);
                    prevCrumble = true;
                }
                else if (room && Chance(P.moving))
                {
                    float range = Mathf.Min(R(1.5f, 3.5f), (hi - lo) / 2f + 1f);
                    var mp = Plat((lo + hi) / 2f, y + R(0.8f, 1.4f), f == 1 ? 1.6f : 2.2f, PlatKind.Moving, f == 1 ? art.beamer : art.platforms[f], f == 1 ? 0.5f : PlatH(f), f == 1 ? Tiling.Simple : Tiling.Tiled);
                    mp.moveRange = range;
                    mp.moveSpeed = R(1.8f, 3f);
                    prevCrumble = false;
                }
                else prevCrumble = false;
                // Mate on its own, floating in the air next to the path
                if (y > nextMate)
                {
                    nextMate = y + R(30f, 40f);
                    Pickup(Mathf.Clamp(x + R(-3f, 3f), -halfWidth + 1f, halfWidth - 1f), y + 1.6f, ClimbPickupKind.Mate);
                }
                if (y > nextDrone && Mathf.Abs(y - mids[0]) > 4f && Mathf.Abs(y - mids[1]) > 4f && y < b + floorHeight - 6f)
                {
                    nextDrone = y + P.droneEvery * R(0.85f, 1.15f);
                    Drone(R(-3f, 3f), y + 1.3f, R(4f, 7f), P.droneSpeed);
                }
                if (shadowsLeft > 0 && y > nextShadow && Mathf.Abs(y - mids[0]) > 5f && Mathf.Abs(y - mids[1]) > 5f)
                {
                    shadowsLeft--;
                    nextShadow = y + floorHeight / Mathf.Max(1, P.shadows) * 0.9f;
                    Shadow(R(-1f, 1f), y - 0.6f);
                }
            }
        }

        static float PlatH(int f) => f == 0 ? 10f / 32f : f == 3 ? 10f / 32f : 12f / 32f;

        void BuildArena()
        {
            float a = ArenaY;
            Background(3, a, 22f);
            Landing(a, "FELIX-KLEIN-HÖRSAAL", 3, false);
            var organ = Sprite("Organ", art.organ, world, new Vector3(0f, a + art.organ.bounds.size.y * 0.8f, 0), -18);
            // the organ stands on the Felix-Klein-Hörsaal floor behind Johannes (no platforms: the defense is fought on foot)
            organ.transform.localScale = Vector3.one * 0.95f;
            organ.transform.position = new Vector3(halfWidth - 3.6f, a + art.organ.bounds.size.y * 0.475f, 0);
            organ.color = new Color(0.62f, 0.62f, 0.6f);
            // committee banner
            Label(world, "PUBLIC DEFENSE  ·  JOHANNES DORN", new Vector2(0, a + 17f), 0.07f, new Color(0.35f, 0.25f, 0.1f), -10, TextAnchor.MiddleCenter);
            // photo spot next to the organ
            var spot = Sprite("PhotoSpot", art.photo, world, new Vector3(halfWidth - 7.2f, a + 1.2f, 0), 4);
            var ps = spot.gameObject.AddComponent<ClimbPhotoSpot>();
            ps.view = spot;
            ps.spotId = "L4_photo1";
            ps.caption = "Felix-Klein-Hörsaal organ";
        }

        // ---------------- builders ----------------
        enum Tiling { Tiled, Simple }

        ClimbPlatform Plat(float x, float y, float w, PlatKind kind, Sprite s, float h, Tiling tiling)
        {
            var go = new GameObject(kind + "Platform");
            go.transform.SetParent(world);
            go.transform.position = new Vector3(x, y, 0);
            var view = new GameObject("View").AddComponent<SpriteRenderer>();
            view.transform.SetParent(go.transform, false);
            view.sprite = s;
            view.sortingOrder = kind == PlatKind.Solid ? -2 : 0;
            if (tiling == Tiling.Tiled)
            {
                view.drawMode = SpriteDrawMode.Tiled;
                view.size = new Vector2(w, h);
                view.transform.localPosition = new Vector3(0, -h / 2f, 0);
            }
            else view.transform.localPosition = new Vector3(0, -s.bounds.size.y / 2f, 0);
            if (kind != PlatKind.Solid)
            {
                // dark drop shadow + bright rim so platforms read on every wallpaper
                var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
                sh.transform.SetParent(view.transform, false);
                sh.transform.localPosition = new Vector3(0.1f, -0.12f, 0);
                sh.sprite = s; sh.drawMode = view.drawMode; if (view.drawMode == SpriteDrawMode.Tiled) sh.size = view.size;
                sh.color = new Color(0f, 0f, 0.05f, 0.6f);
                sh.sortingOrder = view.sortingOrder - 1;
                var rim = new GameObject("Rim").AddComponent<SpriteRenderer>();
                rim.transform.SetParent(go.transform, false);
                rim.sprite = art.pixel;
                rim.transform.localScale = new Vector3((tiling == Tiling.Tiled ? w : s.bounds.size.x) / art.pixel.bounds.size.x, 0.06f / art.pixel.bounds.size.y, 1f);
                rim.transform.localPosition = new Vector3(0, -0.02f, 0);
                rim.color = new Color(1f, 1f, 0.9f, 0.85f);
                rim.sortingOrder = view.sortingOrder + 1;
            }
            var pl = go.AddComponent<ClimbPlatform>();
            pl.kind = kind;
            pl.width = w;
            pl.view = view;
            return pl;
        }

        ClimbPlatform Spring(float x, float baseY, Sprite s, float height, float boost)
        {
            var go = new GameObject("Spring");
            go.transform.SetParent(world);
            go.transform.position = new Vector3(x, baseY + height, 0);
            var view = new GameObject("View").AddComponent<SpriteRenderer>();
            view.transform.SetParent(go.transform, false);
            view.sprite = s;
            view.sortingOrder = 1;
            float sh = s.bounds.size.y;
            view.transform.localScale = Vector3.one * (height / sh);
            view.transform.localPosition = new Vector3(0, -height / 2f, 0);
            var pl = go.AddComponent<ClimbPlatform>();
            pl.kind = PlatKind.Spring;
            pl.width = Mathf.Max(0.8f, s.bounds.size.x * height / sh);
            pl.springBoost = boost;
            pl.view = view;
            return pl;
        }

        void Landing(float y, string name, int f, bool sign = true)
        {
            var pl = Plat(0f, y, halfWidth * 2f + 0.2f, PlatKind.Solid, art.slab, 14f / 32f, Tiling.Tiled);
            pl.canVanish = false;
            checkpoints.Add(y);
            if (!sign) return;
            var s = Sprite("CheckpointSign", art.sign, world, new Vector3(-halfWidth + 3.2f, y + 1.5f, 0), -5);
            s.transform.localScale = new Vector3(3.6f, 2.4f, 1f);
            Label(world, name.Replace("  ", "\n"), new Vector2(-halfWidth + 3.2f, y + 1.75f), 0.045f, Color.white, -4, TextAnchor.MiddleCenter);
        }

        void Background(int f, float y0, float h)
        {
            float w = halfWidth * 2f + 16f;
            var bg = Sprite("Wall_" + FloorNames[f], art.walls[f], world, new Vector3(0, y0 + h / 2f, 0), -50);
            bg.drawMode = SpriteDrawMode.Tiled;
            bg.size = new Vector2(w, h + 0.5f);
            float dim = f == 0 ? 0.8f : f == 1 ? 0.62f : f == 2 ? 0.42f : 0.74f;
            bg.color = new Color(dim, dim, dim * 1.08f);
            // darker strips outside the climbing column (Johannes wraps around there)
            foreach (float side in new[] { -1f, 1f })
            {
                var dark = Sprite("OutsideShade", art.pixel, world, new Vector3(side * (halfWidth + 4.5f), y0 + h / 2f, 0), -40);
                dark.transform.localScale = new Vector3(9f / art.pixel.bounds.size.x, (h + 0.5f) / art.pixel.bounds.size.y, 1f);
                dark.color = new Color(0.05f, 0.05f, 0.12f, 0.45f);
                var pil = Sprite("Pillar", art.pillar, world, new Vector3(side * (halfWidth + 0.4f), y0 + h / 2f, 0), -30);
                pil.drawMode = SpriteDrawMode.Tiled;
                pil.size = new Vector2(art.pillar.bounds.size.x, h + 0.5f);
                pil.color = f == 0 ? new Color(0.85f, 0.9f, 0.95f) : f == 1 ? new Color(0.85f, 0.65f, 0.45f) : f == 2 ? new Color(0.6f, 0.45f, 0.32f) : Color.white;
            }
        }

        void Decor(int f, float b)
        {
            switch (f)
            {
                case 0:
                {
                    var lift = Sprite("Lift", art.lift, world, new Vector3(-3.5f, b + art.lift.bounds.size.y / 2f, 0), -20);
                    Label(world, "AUSSER\nBETRIEB", new Vector2(-3.5f, b + 1.7f), 0.055f, new Color(0.85f, 0.1f, 0.1f), -19, TextAnchor.MiddleCenter);
                    Label(world, "LIFT", new Vector2(-3.5f, b + 3.0f), 0.04f, Color.white, -19, TextAnchor.MiddleCenter);
                    var desk = Sprite("InfoDesk", art.infoDesk, world, new Vector3(6.5f, b + art.infoDesk.bounds.size.y / 2f, 0), -20);
                    desk.transform.localScale = Vector3.one * 1.2f;
                    desk.transform.position = new Vector3(6.5f, b + art.infoDesk.bounds.size.y * 0.6f, 0);
                    var bench = Sprite("Bench", art.bench, world, new Vector3(2.5f, b + 0.4f, 0), -20);
                    bench.transform.localScale = Vector3.one * 3f;
                    bench.transform.position = new Vector3(2.5f, b + art.bench.bounds.size.y * 1.5f, 0);
                    Label(world, "↑ STAIRS TO THE FELIX-KLEIN-HÖRSAAL ↑", new Vector2(1.5f, b + 4.6f), 0.045f, new Color(0.15f, 0.25f, 0.4f), -19, TextAnchor.MiddleCenter);
                    break;
                }
                case 1:
                {
                    string[] slides = { "RQ1:\nDoes it scale?", "Related Work", "Threats to\nValidity", "Future Work", "Questions?" };
                    for (int i = 0; i < slides.Length; i++)
                    {
                        float x = i % 2 == 0 ? -5.5f : 5.5f;
                        float y = b + 8f + i * 12f;
                        var sl = Sprite("Slide", art.slide, world, new Vector3(x, y, 0), -25);
                        sl.transform.localScale = Vector3.one * 1.6f;
                        Label(world, slides[i], new Vector2(x, y + 0.2f), 0.045f, new Color(0.15f, 0.2f, 0.35f), -24, TextAnchor.MiddleCenter);
                    }
                    break;
                }
                case 2:
                    for (int i = 0; i < 3; i++)
                        Label(world, i % 2 == 0 ? "SILENCE PLEASE" : "BITTE RUHE", new Vector2(i % 2 == 0 ? 5f : -5f, b + 14f + i * 20f), 0.06f, new Color(0.95f, 0.85f, 0.6f, 0.8f), -24, TextAnchor.MiddleCenter);
                    break;
                case 3:
                    Label(world, "FELIX-KLEIN-\nHÖRSAAL", new Vector2(5.5f, b + 10f), 0.06f, new Color(0.6f, 0.5f, 0.3f), -24, TextAnchor.MiddleCenter);
                    break;
            }
        }

        void Pickup(float x, float y, ClimbPickupKind kind)
        {
            Sprite s = kind == ClimbPickupKind.Mate || kind == ClimbPickupKind.MateAmmo ? art.mate : kind == ClimbPickupKind.Paper ? art.paper : kind == ClimbPickupKind.Citation ? art.citation : art.dataPoint;
            var sr = Sprite(kind.ToString(), s, world, new Vector3(x, y, 0), 5);
            if (kind == ClimbPickupKind.DataPoint) sr.transform.localScale = Vector3.one * 0.8f;
            var p = sr.gameObject.AddComponent<ClimbPickup>();
            p.kind = kind;
            p.points = kind == ClimbPickupKind.Paper ? 3 : kind == ClimbPickupKind.Citation ? 2 : kind == ClimbPickupKind.DataPoint ? 1 : 0;
        }

        void Drone(float x, float y, float range, float speed)
        {
            var sr = Sprite("Reviewer2Drone", art.drone, world, new Vector3(x, y, 0), 8);
            sr.transform.localScale = Vector3.one * 1.2f;
            var d = sr.gameObject.AddComponent<ClimbDrone>();
            d.view = sr; d.range = range; d.speed = speed;
            Label(sr.transform, "REVIEWER 2", new Vector2(0, 0.55f), 0.025f, new Color(1f, 0.5f, 0.5f), 9, TextAnchor.LowerCenter);
        }

        void Shadow(float x, float y)
        {
            var root = new GameObject("ImposterShadow");
            root.transform.SetParent(world);
            root.transform.position = new Vector3(x, y, 0);
            var sr = Sprite("View", art.imposter, root.transform, new Vector3(0, 0.75f, 0), 7);
            sr.transform.localPosition = new Vector3(0, 0.75f, 0);
            sr.transform.localScale = Vector3.one * 1.3f;
            var s = root.AddComponent<ImposterShadow>();
            s.view = sr;
            Label(root.transform, "imposter syndrome", new Vector2(0, 2.0f), 0.025f, new Color(0.8f, 0.65f, 1f), 9, TextAnchor.LowerCenter);
        }

        SpriteRenderer Sprite(string name, Sprite s, Transform parent, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = parent == cam.transform ? cam.transform.position + new Vector3(pos.x, pos.y, pos.z) : pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh Label(Transform parent, string text, Vector2 pos, float size, Color color, int order, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = parent == world ? (Vector3)pos : parent.position + (Vector3)pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        // ---------------- test helpers ----------------
        public void DebugJumpTo(float y)
        {
            player.Teleport(new Vector2(0f, y + 0.5f), player.bounceSpeed);
            camY = Mathf.Max(cam.orthographicSize - 0.8f, y + cameraLead);
            foreach (var c in checkpoints) if (c <= y && c > checkpointY) checkpointY = c;
        }

        public float CheckpointY => checkpointY;
    }
}
