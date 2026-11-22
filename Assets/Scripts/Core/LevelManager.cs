using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GetYourPhD
{
    /// <summary>
    /// Level flow: play → (boss) → Submit (or deadline auto-submits) → grade screen → Peer Review hallway → gate → next level.
    /// Saves a checkpoint at the gate so a restart puts Johannes back at the door.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        public string levelId = "L1";
        public string levelTitle = "Level 1: The Measurement Lab";
        public string nextScene = "Level2_HomeOffice";
        public string nextLevelTitle = "Level 2: Home Sweet Home Office";
        public Transform hallwaySpawn;

        public bool Submitted { get; private set; }
        public bool InHallway { get; private set; }
        public float LastCompletion { get; private set; }

        string GateCheckpoint => levelId + "_GATE";
        static string DeadlineLabel => DeadlineClock.Instance ? DeadlineClock.Instance.DeadlineText : "Deadline";

        void Awake()
        {
            Instance = this;
            Physics2D.queriesHitTriggers = false;
            Physics2D.queriesStartInColliders = false;
            GameInput.Locked = false;
            Time.timeScale = 1f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            if (DeadlineClock.Instance) DeadlineClock.Instance.OnExpired += OnDeadline;
            yield return null; // let HUD build
            if (SaveSystem.Checkpoint == GateCheckpoint)
            {
                EnterHallway();
                HUD.Instance?.Toast("Welcome back! Your rebuttal is waiting.");
            }
            else
            {
                var how = FindAnyObjectByType<HowToPlayOverlay>();
                if (how) yield return how.ShowIfNew();
                HUD.Instance?.Banner(levelTitle, $"Submit before {DeadlineLabel}!");
            }
        }

        void OnDeadline()
        {
            HUD.Instance?.Toast($"{DeadlineLabel}! Submitting as is...");
            GameAudio.Play("deadline");
            Submit(true);
        }

        public void Submit(bool forced)
        {
            if (Submitted) return;
            Submitted = true;
            if (DeadlineClock.Instance) DeadlineClock.Instance.Running = false;
            GameInput.Locked = true;
            PlayerController.Instance?.SetVictory();
            var meter = PaperMeter.Instance;
            LastCompletion = meter ? meter.Fill : 0f;
            SaveSystem.SaveBestCompletion(levelId, LastCompletion);
            GameAudio.Play("submit");
            StartCoroutine(GradeFlow(forced));
        }

        IEnumerator GradeFlow(bool forced)
        {
            yield return new WaitForSeconds(forced ? 1.2f : 0.8f);
            HUD.Instance?.ShowRebuttal(forced, LastCompletion);
            GameAudio.Play(forced ? "jingle_fail" : "jingle_win");
            GameAudio.DuckMusic(5f);
            // Wait for Continue (Enter / A) or Retry (R / X)
            while (true)
            {
                if (GameInput.ConfirmPressed) { HUD.Instance?.HideGrade(); EnterHallway(); yield break; }
                if (GameInput.RetryPressed) { Retry(); yield break; }
                yield return null;
            }
        }

        public void EnterHallway()
        {
            Submitted = true;
            InHallway = true;
            if (DeadlineClock.Instance) DeadlineClock.Instance.Running = false;
            var p = PlayerController.Instance;
            if (p && hallwaySpawn)
            {
                p.ClearVictory();
                p.Teleport(hallwaySpawn.position);
                var cam = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
                if (cam) cam.SnapToTarget();
            }
            GameInput.Locked = false;
            SaveSystem.Checkpoint = GateCheckpoint;
            HUD.Instance?.SetReviewMode();
        }

        public void Retry()
        {
            SaveSystem.Checkpoint = "";
            GameInput.Locked = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GateOpened()
        {
            SaveSystem.Checkpoint = "";
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
            else
                HUD.Instance?.ShowEndCard("Accepted!", $"{nextLevelTitle}\nis still under construction.\n\nPress R to replay this level.");
        }

        void Update()
        {
            // Debug/organizer: Ctrl+Shift+R resets save and restarts.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
                Retry();
        }
    }
}
