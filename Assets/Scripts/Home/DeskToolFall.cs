using System.Collections.Generic;
using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Johannes' tool wall above the desk (he built his own camper). Now and then a tool falls off the wall onto the desk.
    /// Every tool on the desk slows the typing until Johannes hangs them back up (hold E at the desk).
    /// </summary>
    public class DeskToolFall : MonoBehaviour
    {
        public HomeTask toolTask;
        public Sprite[] toolSprites;
        public Vector2 wallMin = new Vector2(10.6f, 3.4f), wallMax = new Vector2(13.4f, 4.4f);
        public float deskTopY = 2.1f;
        public float deskMinX = 10.8f, deskMaxX = 13.2f;
        public float holdPerTool = 0.45f;
        public int maxTools = 4;

        readonly List<GameObject> pile = new List<GameObject>();
        readonly string[] names = { "hammer", "wrench", "drill", "saw" };

        public int Count => pile.Count;
        public bool Full => pile.Count + falling >= maxTools;
        int falling;

        void Start()
        {
            if (toolTask) toolTask.OnResolved += _ => Clear();
        }

        public void Drop()
        {
            if (Full || toolSprites == null || toolSprites.Length == 0) return;
            int i = Random.Range(0, toolSprites.Length);
            var go = new GameObject("FallingTool");
            go.transform.position = new Vector3(Random.Range(wallMin.x, wallMax.x), Random.Range(wallMin.y, wallMax.y), 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = toolSprites[i];
            sr.sortingOrder = 6;
            var f = go.AddComponent<FallingTool>();
            f.landY = deskTopY;
            f.landX = Random.Range(deskMinX, deskMaxX);
            f.onLand = () => Landed(go, i);
            falling++;
            HUD.Instance?.Toast($"CLONK! A {names[i % names.Length]} fell off the tool wall.");
        }

        void Landed(GameObject go, int i)
        {
            falling--;
            pile.Add(go);
            Effects.Burst(go.transform.position, new Color(0.8f, 0.8f, 0.85f), 8);
            GameAudio.Play("tools_fall");
            if (toolTask)
            {
                toolTask.holdToFix = holdPerTool * pile.Count;
                toolTask.bubbleText = pile.Count == 1 ? "CLONK!" : $"{pile.Count} TOOLS!";
                toolTask.Activate();
                toolTask.RefreshBubble();
            }
        }

        void Clear()
        {
            if (pile.Count > 0) GameAudio.Play("tools_hang");
            foreach (var g in pile) if (g) Destroy(g);
            pile.Clear();
        }
    }

    /// <summary>A tool tumbling from the wall onto the desk.</summary>
    public class FallingTool : MonoBehaviour
    {
        public float landY, landX;
        public System.Action onLand;
        float vy, vx, spin;
        bool landed;

        void Start()
        {
            vx = (landX - transform.position.x) / 0.6f;
            vy = 2f;
            spin = Random.Range(-540f, 540f);
        }

        void Update()
        {
            if (landed) return;
            vy -= 20f * Time.deltaTime;
            transform.position += new Vector3(vx, vy, 0) * Time.deltaTime;
            transform.Rotate(0, 0, spin * Time.deltaTime);
            if (transform.position.y <= landY && vy < 0f)
            {
                landed = true;
                transform.position = new Vector3(transform.position.x, landY, 0);
                transform.rotation = Quaternion.Euler(0, 0, Random.Range(-25f, 25f));
                onLand?.Invoke();
            }
        }
    }
}
