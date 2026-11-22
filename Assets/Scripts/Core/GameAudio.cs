using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GetYourPhD
{
    /// <summary>
    /// Music and sound effects. Created automatically before the first scene loads and kept across scenes.
    /// Clips come from Resources/Audio/Sfx/&lt;id&gt;.wav (or &lt;id&gt;_1.wav, &lt;id&gt;_2.wav ... = random variants) and
    /// Resources/Audio/Music/&lt;id&gt;.ogg, built by LevelTools/prep_audio.py. Sources and licenses: Resources/Audio/CREDITS.md.
    /// Each scene starts its own music track (see TrackForScene); scripts switch tracks for bosses, the night shift and the finale.
    /// M toggles all sound on/off (remembered).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        public static float MusicVolume = 0.42f;
        public static float SfxVolume = 0.85f;
        const string MuteKey = "GYP_Muted";
        const string MusicVolKey = "GYP_MusicVolume";
        public const float DefaultMusicVolume = 0.42f;

        AudioSource musicA, musicB, current;
        string currentTrack;
        float fade = 1f, fadeSpeed = 1f;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, AudioClip> music = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioListener listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            go.AddComponent<GameAudio>();
        }

        void Awake()
        {
            Instance = this;
            listener = gameObject.AddComponent<AudioListener>();
            musicA = NewSource("MusicA"); musicB = NewSource("MusicB");
            musicA.loop = musicB.loop = true;
            musicA.ignoreListenerPause = musicB.ignoreListenerPause = true; // music keeps playing in the pause menu
            AudioListener.volume = PlayerPrefs.GetInt(MuteKey, 0) == 1 ? 0f : 1f;
            MusicVolume = PlayerPrefs.GetFloat(MusicVolKey, DefaultMusicVolume);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (Instance == this) Instance = null; }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Exactly one listener: ours, unless the scene camera brings its own.
            int others = 0;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l != listener) others++;
            listener.enabled = others == 0;
            string t = TrackForScene(scene.name);
            if (t != null) PlayMusic(t, 1.2f);
        }

        static string TrackForScene(string scene)
        {
            if (scene.StartsWith("StartScreen")) return "start";
            if (scene.StartsWith("Level1")) return "level1";
            if (scene.StartsWith("Level2")) return "level2";
            if (scene.StartsWith("Level3")) return "level3";
            if (scene.StartsWith("Level4")) return "level4";
            return null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && !kb.ctrlKey.isPressed) ToggleMute();

            if (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * fadeSpeed);
                var other = current == musicA ? musicB : musicA;
                other.volume = MusicVolume * (1f - fade);
                if (fade >= 1f) other.Stop();
            }
            if (current) current.volume = MusicVolume * fade * musicDuck;
            musicDuck = Mathf.MoveTowards(musicDuck, duckTarget, Time.unscaledDeltaTime * 2f);
            if (duckTimer > 0f) { duckTimer -= Time.unscaledDeltaTime; if (duckTimer <= 0f) duckTarget = 1f; }
        }

        float musicDuck = 1f, duckTarget = 1f, duckTimer;

        public static bool Muted => AudioListener.volume <= 0f;

        /// <summary>Music volume 0..1 (start screen slider). Saved in PlayerPrefs.</summary>
        public static void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(MusicVolKey, MusicVolume);
            PlayerPrefs.Save();
        }

        public static void ToggleMute()
        {
            bool mute = !Muted;
            AudioListener.volume = mute ? 0f : 1f;
            PlayerPrefs.SetInt(MuteKey, mute ? 1 : 0);
            PlayerPrefs.Save();
            HUD.Instance?.Toast(mute ? "Sound off (M)" : "Sound on (M)");
        }

        // ---------------- music ----------------
        /// <summary>Crossfades to a track from Resources/Audio/Music. Same track again = no restart.</summary>
        public static void PlayMusic(string id, float fadeSeconds = 1f)
        {
            var a = Instance;
            if (!a || a.currentTrack == id) return;
            if (!a.music.TryGetValue(id, out var clip)) a.music[id] = clip = Resources.Load<AudioClip>("Audio/Music/" + id);
            if (!clip) { Debug.LogWarning("GameAudio: missing music " + id); return; }
            a.currentTrack = id;
            var next = a.current == a.musicA ? a.musicB : a.musicA;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            a.current = next;
            a.fade = 0f;
            a.fadeSpeed = 1f / Mathf.Max(0.05f, fadeSeconds);
        }

        /// <summary>Turns the music down for a moment (e.g. under a jingle).</summary>
        public static void DuckMusic(float seconds, float level = 0.25f)
        {
            var a = Instance;
            if (!a) return;
            a.duckTarget = level;
            a.duckTimer = seconds;
        }

        // ---------------- sound effects ----------------
        AudioClip[] Clips(string id)
        {
            if (clips.TryGetValue(id, out var arr)) return arr;
            var one = Resources.Load<AudioClip>("Audio/Sfx/" + id);
            if (one) arr = new[] { one };
            else
            {
                var list = new List<AudioClip>();
                for (int i = 1; i <= 12; i++)
                {
                    var c = Resources.Load<AudioClip>($"Audio/Sfx/{id}_{i}");
                    if (!c) break;
                    list.Add(c);
                }
                arr = list.ToArray();
                if (arr.Length == 0) Debug.LogWarning("GameAudio: missing sfx " + id);
            }
            clips[id] = arr;
            return arr;
        }

        public static AudioClip Clip(string id)
        {
            var a = Instance;
            if (!a) return null;
            var arr = a.Clips(id);
            return arr.Length == 0 ? null : arr[Random.Range(0, arr.Length)];
        }

        /// <summary>One-shot sound effect. The same id is not repeated within minGap seconds.</summary>
        public static void Play(string id, float volume = 1f, float pitchJitter = 0.06f, float minGap = 0.05f)
        {
            var a = Instance;
            if (!a || string.IsNullOrEmpty(id)) return;
            float now = Time.unscaledTime;
            if (a.lastPlayed.TryGetValue(id, out float last) && now - last < minGap) return;
            var clip = Clip(id);
            if (!clip) return;
            a.lastPlayed[id] = now;
            var s = a.FreeSource();
            s.clip = clip;
            s.volume = SfxVolume * volume;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.loop = false;
            s.ignoreListenerPause = PauseMenu.IsPaused; // menu clicks are audible while the game is paused
            s.Play();
        }

        AudioSource FreeSource()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            if (pool.Count < 24) { var n = NewSource("Sfx" + pool.Count); pool.Add(n); return n; }
            // all busy: steal the one closest to finishing
            AudioSource best = pool[0];
            float bestLeft = float.MaxValue;
            foreach (var s in pool)
            {
                float left = s.clip ? s.clip.length - s.time : 0f;
                if (left < bestLeft) { bestLeft = left; best = s; }
            }
            return best;
        }

        /// <summary>A looping sound owned by a game object (stops when it is destroyed). Call SetLoop to start/stop it.</summary>
        public static AudioSource MakeLoop(Component owner, string id, float volume = 1f)
        {
            if (!owner) return null;
            var clip = Clip(id);
            if (!clip) return null;
            var s = owner.gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = SfxVolume * volume;
            return s;
        }

        /// <summary>Starts or stops a loop made with MakeLoop (start = from a random point, so repeats sound less alike).</summary>
        public static void SetLoop(AudioSource s, bool on, float volume = -1f)
        {
            if (!s) return;
            if (volume >= 0f) s.volume = SfxVolume * volume;
            if (on && !s.isPlaying)
            {
                s.time = s.clip ? Random.Range(0f, s.clip.length * 0.8f) : 0f;
                s.Play();
            }
            else if (!on && s.isPlaying) s.Stop();
        }
    }
}
