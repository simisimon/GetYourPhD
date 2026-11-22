using UnityEngine;

namespace GetYourPhD
{
    /// <summary>Smooth side-scroller follow with look-ahead and level bounds.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smooth = 6f;
        public float lookAhead = 2f;
        public float verticalOffset = 1.5f;
        public Vector2 minBounds = new Vector2(-1000, -1000);
        public Vector2 maxBounds = new Vector2(1000, 1000);

        float ahead;
        Camera cam;

        void Awake() { cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (!target) { if (PlayerController.Instance) target = PlayerController.Instance.transform; else return; }
            var p = PlayerController.Instance;
            float wantAhead = p ? (p.FacingRight ? lookAhead : -lookAhead) : 0f;
            ahead = Mathf.MoveTowards(ahead, wantAhead, Time.deltaTime * 4f);
            Vector3 want = new Vector3(target.position.x + ahead, target.position.y + verticalOffset, transform.position.z);
            Vector3 pos = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            if (cam && cam.orthographic)
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                pos.x = Mathf.Clamp(pos.x, minBounds.x + w, Mathf.Max(minBounds.x + w, maxBounds.x - w));
                pos.y = Mathf.Clamp(pos.y, minBounds.y + h, Mathf.Max(minBounds.y + h, maxBounds.y - h));
            }
            transform.position = pos;
        }

        public void SnapToTarget()
        {
            if (!target) return;
            transform.position = new Vector3(target.position.x, target.position.y + verticalOffset, transform.position.z);
            LateUpdate();
        }
    }
}
