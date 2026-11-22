using UnityEngine;

namespace GetYourPhD
{
    public class FxParticle : MonoBehaviour
    {
        public Vector2 vel;
        float life = 0.5f;
        void Update()
        {
            life -= Time.deltaTime;
            vel += Vector2.down * 12f * Time.deltaTime;
            transform.position += (Vector3)(vel * Time.deltaTime);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
