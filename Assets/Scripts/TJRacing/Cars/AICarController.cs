// TJ Racing 3D — AI drivers with FIXED skill levels (research §A.2).
// 5 AI: 2 slow, 2 medium, 1 fast. Small per-race variance (±3%). An AI car NEVER
// exceeds its own car's top speed to catch up — NO rubber-banding, ever. Difficulty
// comes from LEVELS (traffic on hard levels), not from AI cheating. A clean lap
// must be worth something.
//
// AI follows the track spline waypoints with a look-ahead point, steers toward it,
// brakes for sharp corners, and slows (never swerves into the player) when a car
// is directly ahead. Stuck detection resets the car onto the spline.
using UnityEngine;

namespace TJRacing
{
    public enum AISkill { Slow, Medium, Fast }

    [RequireComponent(typeof(CarController))]
    public class AICarController : MonoBehaviour
    {
        public AISkill skill = AISkill.Medium;
        public TrackSpline spline;           // assigned at spawn
        public float skillVariance = 0f;     // ±0.03, rolled per race

        private CarController _car;
        private int _nearestIndex;
        private float _stuckTimer;

        // Skill table: fraction of the car's top speed + corner braking.
        private float SkillSpeedFactor
        {
            get
            {
                switch (skill)
                {
                    case AISkill.Slow: return 0.78f;
                    case AISkill.Medium: return 0.88f;
                    default: return 0.97f; // Fast — still BELOW the car's top speed
                }
            }
        }

        void Awake() { _car = GetComponent<CarController>(); }

        public void Init(TrackSpline trackSpline, AISkill s)
        {
            spline = trackSpline;
            skill = s;
            // Per-race variance only — the skill level itself never changes mid-race.
            skillVariance = Random.Range(-0.03f, 0.03f);
        }

        void FixedUpdate()
        {
            if (spline == null || spline.Points.Count < 4) return;

            // Nearest spline point (search locally around last index — cheap).
            _nearestIndex = spline.NearestIndex(transform.position, _nearestIndex);

            // Look-ahead target: further ahead at higher speed.
            float lookAhead = Mathf.Lerp(6f, 16f, _car.SpeedKmh / 200f);
            Vector3 target = spline.PointAtDistance(_nearestIndex, lookAhead);

            // Steering: angle to the target point.
            Vector3 localTarget = transform.InverseTransformPoint(target);
            float steer = Mathf.Clamp(localTarget.x / 7f, -1f, 1f);

            // Corner braking: if the spline turns sharply ahead, brake.
            float cornerAhead = spline.CurvatureAhead(_nearestIndex, 18f);
            float targetSpeedMs = _car.topSpeedKmh / 3.6f
                                * (SkillSpeedFactor + skillVariance);
            if (cornerAhead > 0.35f) targetSpeedMs *= 0.55f;
            else if (cornerAhead > 0.18f) targetSpeedMs *= 0.75f;

            // Slow down for a car directly ahead (no swerving into the player).
            if (CarAheadWithin(9f)) targetSpeedMs = Mathf.Min(targetSpeedMs, _car.SpeedMs * 0.6f);

            _car.SteerInput = steer;
            _car.ThrottleInput = _car.SpeedMs < targetSpeedMs ? 1f : 0f;
            _car.BrakeInput = _car.SpeedMs > targetSpeedMs + 2f ? 0.8f : 0f;
            _car.NitroRequested = false; // AI never uses nitro — the player keeps that edge.

            // Stuck detection: barely moved in 3s while trying to drive.
            if (_car.SpeedMs < 1.5f && _car.ThrottleInput > 0.5f)
            {
                _stuckTimer += Time.fixedDeltaTime;
                if (_stuckTimer > 3f) RecoverOntoSpline();
            }
            else _stuckTimer = 0f;
        }

        private bool CarAheadWithin(float dist)
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            if (Physics.Raycast(origin, transform.forward, out hit, dist))
            {
                return hit.collider.GetComponentInParent<CarController>() != null;
            }
            return false;
        }

        private void RecoverOntoSpline()
        {
            _stuckTimer = 0f;
            Vector3 p = spline.Points[_nearestIndex];
            Quaternion rot = Quaternion.LookRotation(spline.DirectionAt(_nearestIndex), Vector3.up);
            _car.ResetTo(p + Vector3.up * 0.5f, rot);
        }
    }
}
