using UnityEngine;

namespace GetYourPhD
{
    /// <summary>
    /// Playtest autopilot for Level 3 (not used in the game). Add it at runtime to drive the camper like an
    /// average player: dodges what is ahead, slows for tolls/cows/curves, uses stations when needed.
    /// <see cref="skill"/> 0..1 sets reaction distance and how often it "misses" a hazard.
    /// </summary>
    public class RoadBot : MonoBehaviour
    {
        public float skill = 0.6f;
        public string log = "";
        public int hits, fails;
        int lastHp = 3; float lastY;
        float decideT;
        int wantCol = 2;
        System.Random rnd = new System.Random(7);

        void OnEnable() { CamperController.InputOverride = Drive; }
        void OnDisable() { if (CamperController.InputOverride == (System.Func<Vector2>)Drive) CamperController.InputOverride = null; }

        Vector2 Drive()
        {
            var trip = RoadTrip.Instance; var c = CamperController.Instance;
            if (!trip || !c) return Vector2.zero;
            if (c.Hp < lastHp) hits++;
            if (c.Y < lastY - 20f) { fails++; log += $"fail@{lastY:0} "; }
            lastHp = c.Hp; lastY = c.Y;

            float look = Mathf.Lerp(10f, 20f, skill);
            float[] danger = new float[5];
            float[] reward = new float[5];
            float slowFor = 99f;
            foreach (var t in trip.GetComponentsInChildren<RoadThing>())
            {
                float dy = t.y - c.Y;
                if (dy < -3f || dy > look) continue;
                int col = Mathf.Clamp(Mathf.RoundToInt(ColOf(trip, t)), 0, 4);
                if (t.pickup) { reward[col] += t.kind == 'j' ? 3 : 1; continue; }
                if (!t.solid && t.kind != 'r') continue;
                if (t.kind == 'B') { if (dy < 9f) slowFor = trip.tollSpeed - 0.8f; continue; }
                if (t.kind == 'k') { if (dy < 12f) slowFor = Mathf.Min(slowFor, 6f); for (int k = 0; k < 5; k++) danger[k] += Mathf.Abs(ColOf(trip, t) - k) < 1f ? 2f : 0f; continue; }
                if (t.kind == 'R' || t.kind == 'T' || t.kind == 'f') { if (dy < 0f && dy > -8f) danger[col] += 0.5f; continue; }
                if (dy > 0f || t.kind == 'v') danger[col] += 10f / Mathf.Max(1f, dy);
            }
            // stations
            bool needGas = c.Fuel < c.fuelMax * 0.65f, needFix = c.Hp < c.maxHp;
            foreach (var e in trip.events)
                if ((e.kind == "G" && needGas || e.kind == "S" && needFix) && c.Y > e.y - 8f && c.Y < e.y + 6f) reward[4] += 50f;

            decideT -= Time.deltaTime;
            if (decideT <= 0f)
            {
                decideT = Mathf.Lerp(0.5f, 0.15f, skill);
                float best = -999f; int bc = c.Col;
                for (int k = 0; k < 5; k++)
                {
                    if (!trip.LaneAllowed(c.Y, k) && k != c.Col) continue;
                    float s = reward[k] - danger[k] * 3f - Mathf.Abs(k - c.Col) * 0.4f;
                    if (s > best) { best = s; bc = k; }
                }
                if (rnd.NextDouble() > skill * 0.5f + 0.5f) bc = c.Col; // sometimes too slow to react
                wantCol = bc;
            }
            float h = wantCol > c.Col ? 1f : wantCol < c.Col ? -1f : 0f;
            // tap-style: release between lane changes so lane repeat does not overshoot
            if (Time.frameCount % 2 == 0 && Mathf.Abs(c.Lateral - trip.LaneX(c.Y, c.Col)) > 0.4f) h = 0f;

            float target = 12f;
            if (trip.Leg.curveAmp > 2f) target = 11.5f;
            target = Mathf.Min(target, slowFor);
            if (danger[c.Col] > 3f) target = Mathf.Min(target, 7f);
            float v = c.Speed < target - 0.3f ? 1f : c.Speed > target + 0.5f ? -1f : 0f;
            return new Vector2(h, v);
        }

        static float ColOf(RoadTrip trip, RoadThing t)
        {
            var l = trip.LegAt(t.y);
            if (t.lateral > l.Lanes * trip.laneWidth * 0.5f + 0.3f) return 4f;
            return t.lateral / trip.laneWidth + (l.Lanes - 1) * 0.5f + l.firstCol;
        }
    }
}
