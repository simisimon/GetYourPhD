using System.IO;
using UnityEngine;

namespace GetYourPhD
{
    /// <summary>PlayerPrefs-backed save: checkpoint (e.g. at a Peer Review Gate), best grades, photos.</summary>
    public static class SaveSystem
    {
        const string CheckpointKey = "GYP_Checkpoint";

        public static string Checkpoint
        {
            get => PlayerPrefs.GetString(CheckpointKey, "");
            set { PlayerPrefs.SetString(CheckpointKey, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>Best paper completion (0..1) reached in a level.</summary>
        public static void SaveBestCompletion(string levelId, float fill)
        {
            string k = "GYP_BestFill_" + levelId;
            if (PlayerPrefs.GetFloat(k, -1f) < fill) PlayerPrefs.SetFloat(k, fill);
            PlayerPrefs.Save();
        }

        public static float BestCompletion(string levelId) => PlayerPrefs.GetFloat("GYP_BestFill_" + levelId, 0f);

        /// <summary>Paper completion (0..1) of the most recent submission of a level, e.g. read by the next level.</summary>
        public static void SaveLastCompletion(string levelId, float fill)
        {
            PlayerPrefs.SetFloat("GYP_LastFill_" + levelId, fill);
            PlayerPrefs.Save();
        }

        public static float LastCompletion(string levelId) => PlayerPrefs.GetFloat("GYP_LastFill_" + levelId, 0f);

        /// <summary>How a level ended last time (Level 3: "arrived" or "broken").</summary>
        public static void SaveOutcome(string levelId, string outcome) { PlayerPrefs.SetString("GYP_Outcome_" + levelId, outcome ?? ""); PlayerPrefs.Save(); }
        public static string Outcome(string levelId) => PlayerPrefs.GetString("GYP_Outcome_" + levelId, "");

        public static bool HasPhoto(string id) => PlayerPrefs.GetInt("GYP_Photo_" + id, 0) == 1;
        public static void AddPhoto(string id) { PlayerPrefs.SetInt("GYP_Photo_" + id, 1); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Peer Review Gate codes, read from StreamingAssets/peer_review_codes.json so organizers can change them without rebuilding.
    /// In a Windows build the file sits in GetYourPhD_Data/StreamingAssets/.
    /// </summary>
    [System.Serializable]
    public class GateCodes
    {
        public string gate1 = "1111";
        public string gate2 = "2222";
        public string gate3 = "3333";

        public static GateCodes Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "peer_review_codes.json");
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<GateCodes>(File.ReadAllText(path));
                Debug.LogWarning("peer_review_codes.json not found at " + path + ", using defaults.");
            }
            catch (System.Exception e) { Debug.LogError("Could not read gate codes: " + e.Message); }
            return new GateCodes();
        }

        public string For(int gate)
        {
            switch (gate) { case 1: return gate1; case 2: return gate2; default: return gate3; }
        }
    }
}
