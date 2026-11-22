using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Anything on the road in Level 3: traffic, hazards, pickups, signs. Spawned and ticked by <see cref="RoadTrip"/>.
    /// Positions are along the road: <see cref="y"/> forward, <see cref="lateral"/> sideways from the road centre
    /// (the road bends in the Alps, so world x = road offset + lateral).
    /// </summary>
    public class RoadThing : MonoBehaviour
    {
        public char kind;
        public int col;
        public float y;
        public float lateral;
        public float speed;          // forward speed, units/s
        public Vector2 half;         // hit box half size
        public bool solid;           // hurts the camper
        public bool pickup;
        public bool passive;         // decoration / sign, no collision
        public SpriteRenderer sr;
        public SpriteRenderer warn;  // "!" shown at the bottom edge while a chaser is still off screen
        public RoadEvent ev;

        // behaviour state
        public float timer, timer2, baseSpeed, latSpeed = 3f;
        public int state;
        public int targetCol;
        public bool hitDone;
        public System.Random rng;

        float bobPhase;

        public void Setup(RoadEvent e, char k)
        {
            ev = e;
            kind = k;
            col = e != null ? e.col : 0;
            targetCol = col;
            rng = new System.Random(e != null ? e.seed : 1);
            bobPhase = (float)rng.NextDouble() * 6f;
        }

        public float WorldX(RoadTrip trip) => trip.RoadOffset(y) + lateral;

        public void Place(RoadTrip trip)
        {
            transform.position = new Vector3(WorldX(trip), y, 0);
            if (pickup && sr)
            {
                float s = 1f + Mathf.Sin(Time.time * 4f + bobPhase) * 0.08f;
                sr.transform.localScale = new Vector3(s, s, 1);
            }
        }

        public bool Overlaps(Vector2 c, Vector2 h, RoadTrip trip)
        {
            if (passive) return false;
            float x = WorldX(trip);
            return Mathf.Abs(x - c.x) < half.x + h.x && Mathf.Abs(y - c.y) < half.y + h.y;
        }

        /// <summary>Slide sideways toward the target lane.</summary>
        public void SteerToTarget(RoadTrip trip, float dt)
        {
            float tx = trip.LaneX(y, targetCol);
            lateral = Mathf.MoveTowards(lateral, tx, latSpeed * dt);
            if (Mathf.Abs(lateral - tx) < 0.05f) col = targetCol;
            if (sr) sr.transform.localRotation = Quaternion.Euler(0, 0, -(tx - lateral) * 6f);
        }
    }
}
