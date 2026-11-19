// TJ Racing 3D — track spline + Night City circuit data.
// The circuit is DATA, not a binary mesh: a closed loop of waypoints with elevation,
// plus per-segment adventure metadata (ramps, tunnel, bridge, sharp turns, obstacles).
// TrackBuilder turns this into road, barriers, buildings and triggers at runtime.
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    /// <summary>Closed-loop spline helper: nearest point, look-ahead, curvature.</summary>
    public class TrackSpline
    {
        public List<Vector3> Points { get; private set; }
        public float TotalLength { get; private set; }

        private float[] _cumulative; // distance at each point

        public TrackSpline(List<Vector3> points)
        {
            Points = points;
            _cumulative = new float[points.Count + 1];
            TotalLength = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                TotalLength += Vector3.Distance(a, b);
                _cumulative[i + 1] = TotalLength;
            }
        }

        public int Count { get { return Points.Count; } }

        public int NearestIndex(Vector3 pos, int hint)
        {
            // Local search around the hint (±12 points) — O(1)-ish per frame.
            int best = hint;
            float bestD = float.MaxValue;
            for (int o = -12; o <= 12; o++)
            {
                int i = ((hint + o) % Count + Count) % Count;
                float d = (Points[i] - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            // If the hint was stale (teleport/reset), fall back to a full scan.
            if (bestD > 60f * 60f)
            {
                for (int i = 0; i < Count; i++)
                {
                    float d = (Points[i] - pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
            }
            return best;
        }

        public Vector3 DirectionAt(int index)
        {
            Vector3 a = Points[index];
            Vector3 b = Points[(index + 1) % Count];
            Vector3 d = b - a;
            d.y = 0f;
            return d.sqrMagnitude < 0.001f ? Vector3.forward : d.normalized;
        }

        /// <summary>Point `distance` metres ahead along the spline from point `index`.</summary>
        public Vector3 PointAtDistance(int index, float distance)
        {
            float target = _cumulative[index] + distance;
            while (target >= TotalLength) target -= TotalLength;
            int i = index;
            while (i < Count && _cumulative[i + 1] < target) i++;
            i %= Count;
            int j = (i + 1) % Count;
            float segLen = _cumulative[i + 1] - _cumulative[i];
            float t = segLen > 0.001f ? (target - _cumulative[i]) / segLen : 0f;
            return Vector3.Lerp(Points[i], Points[j], t);
        }

        /// <summary>0..1 — how sharply the spline turns in the next `distance` metres.</summary>
        public float CurvatureAhead(int index, float distance)
        {
            Vector3 d0 = DirectionAt(index);
            Vector3 ahead = PointAtDistance(index, distance);
            Vector3 d1 = (ahead - PointAtDistance(index, distance * 0.5f));
            d1.y = 0f;
            if (d1.sqrMagnitude < 0.001f) return 0f;
            d1.Normalize();
            return 1f - Vector3.Dot(d0, d1); // 0 = straight, ~1 = hairpin
        }

        public float DistanceAlong(int index) { return _cumulative[index]; }
    }

    /// <summary>
    /// Night City circuit — one adventure track (plan §5). ~1.9 km loop:
    /// start straight, ramp jump, sharp-turn sector, tunnel, elevated bridge
    /// (lap-2 traffic), downhill sweep, obstacle chicane, back to start.
    /// Hazards are signposted early (research §A.3): ramps get chevron boards
    /// 40 m before them, the tunnel mouth is lit, the bridge is visibly raised.
    /// </summary>
    public static class NightCityTrackData
    {
        public static readonly int RampCheckpoint = 6;      // jump ramp after start straight
        public static readonly int TunnelStart = 14;        // tunnel entry
        public static readonly int TunnelEnd = 19;
        public static readonly int BridgeStart = 22;        // elevated bridge (lap-2 traffic)
        public static readonly int BridgeEnd = 27;
        public static readonly int ChicaneStart = 30;       // obstacle chicane

        public static List<Vector3> Waypoints()
        {
            // Y = elevation. Bridge deck sits at y=6; tunnel dips to y=-2.
            return new List<Vector3>
            {
                new Vector3(0f, 0f, -260f),   // 0  start/finish
                new Vector3(0f, 0f, -190f),
                new Vector3(4f, 0f, -120f),
                new Vector3(14f, 0f, -55f),
                new Vector3(30f, 0.6f, 0f),   // 4  ramp approach
                new Vector3(52f, 1.8f, 55f),  // 5  ramp crest (jump)
                new Vector3(78f, 0.4f, 105f), // 6  landing (signposted)
                new Vector3(110f, 0f, 145f),
                new Vector3(150f, 0f, 165f),
                new Vector3(185f, 0f, 150f),  // 8  sharp right
                new Vector3(205f, 0f, 115f),
                new Vector3(210f, 0f, 70f),
                new Vector3(200f, 0f, 25f),
                new Vector3(185f, -0.8f, -15f),
                new Vector3(175f, -2f, -55f), // 14 tunnel entry (lit mouth)
                new Vector3(170f, -2f, -100f),
                new Vector3(165f, -2f, -145f),
                new Vector3(155f, -2f, -185f),
                new Vector3(140f, -1f, -215f),
                new Vector3(115f, 0.5f, -235f),// 19 tunnel exit
                new Vector3(85f, 2f, -240f),
                new Vector3(55f, 4f, -230f),
                new Vector3(25f, 6f, -215f),  // 22 bridge start (raised deck)
                new Vector3(-5f, 6f, -195f),
                new Vector3(-35f, 6f, -170f),
                new Vector3(-60f, 6f, -140f),
                new Vector3(-80f, 6f, -105f),
                new Vector3(-95f, 4f, -70f),  // 27 bridge end (descent)
                new Vector3(-100f, 1f, -35f),
                new Vector3(-95f, 0f, 5f),
                new Vector3(-80f, 0f, 35f),   // 30 chicane (obstacles)
                new Vector3(-55f, 0f, 55f),
                new Vector3(-30f, 0f, 45f),
                new Vector3(-12f, 0f, 20f),
                new Vector3(-6f, 0f, -20f),
                new Vector3(-4f, 0f, -90f),
                new Vector3(-2f, 0f, -160f),
                new Vector3(-1f, 0f, -220f),  // 36 back to start
            };
        }
    }
}
