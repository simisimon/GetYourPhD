using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// How complete the paper is, 0% to 100%. Collectibles add points, hits remove a few.
    /// There is no grade: every submission gets "rebuttal required" and the rebuttal is the real-life challenge.
    /// </summary>
    public class PaperMeter : MonoBehaviour
    {
        public static PaperMeter Instance { get; private set; }

        [Tooltip("Points for 100%. If 0, it is computed from all collectibles in the scene at start.")]
        public int maxPoints = 0;
        [Tooltip("Fraction of all available points that counts as 100% (lets players miss a few).")]
        [Range(0.5f, 1f)] public float fullAt = 0.9f;

        public int Points { get; private set; }
        public float Fill => maxPoints <= 0 ? 0f : Mathf.Clamp01((float)Points / maxPoints);

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (maxPoints <= 0)
            {
                int sum = 0;
                foreach (var c in FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    sum += c.paperPoints;
                maxPoints = Mathf.Max(1, Mathf.RoundToInt(sum * fullAt));
            }
        }

        public void Add(int pts) { Points += pts; }

        static readonly string[][] Parts =
        {
            new[] { "paragraph", "paragraphs" }, new[] { "section", "sections" }, new[] { "chapter", "chapters" },
            new[] { "table", "tables" }, new[] { "figure", "figures" }, new[] { "experiment result", "experiment results" },
            new[] { "equation", "equations" }, new[] { "footnote", "footnotes" }, new[] { "reference", "references" },
            new[] { "plot", "plots" }, new[] { "proof", "proofs" }, new[] { "research question", "research questions" },
            new[] { "related-work paragraph", "related-work paragraphs" }, new[] { "appendix", "appendices" },
        };
        static int lastPart = -1;

        /// <summary>"3 paragraphs", "1 table", ...: a random piece of paper content for toasts instead of the bare word "paper".</summary>
        public static string Pieces(int n)
        {
            n = Mathf.Abs(n);
            int i = Random.Range(0, Parts.Length);
            if (i == lastPart) i = (i + 1) % Parts.Length; // no same word twice in a row
            lastPart = i;
            return $"{n} {Parts[i][n == 1 ? 0 : 1]}";
        }
        public void Remove(int pts) { Points = Mathf.Max(0, Points - pts); }

        /// <summary>Red when empty, through yellow, to green when complete.</summary>
        public static Color FillColor(float fill)
        {
            var red = new Color(1f, 0.4f, 0.35f);
            var yellow = new Color(1f, 0.85f, 0.3f);
            var green = new Color(0.4f, 0.95f, 0.45f);
            return fill < 0.5f ? Color.Lerp(red, yellow, fill * 2f) : Color.Lerp(yellow, green, (fill - 0.5f) * 2f);
        }
    }
}
