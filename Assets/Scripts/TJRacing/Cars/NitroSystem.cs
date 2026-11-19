// TJ Racing 3D — nitro meter. Drift charges it (research §A.4: Asphalt's winning
// loop, deterministic and skill-based — no random nitro pickups). Double-tap nitro
// triggers a shockwave boost: stronger force + camera kick handled by RaceCamera.
using UnityEngine;

namespace TJRacing
{
    public class NitroSystem : MonoBehaviour
    {
        public float capacity = 100f;
        public float meter = 30f;          // start a race with a little nitro
        public float drainPerSec = 34f;
        public float chargePerDriftSec = 14f;

        public bool IsActive { get; private set; }
        public float Normalized { get { return Mathf.Clamp01(meter / capacity); } }

        private float _lastTapTime = -10f;
        private bool _shockwavePending;

        public bool ShockwavePending
        {
            get { bool v = _shockwavePending; _shockwavePending = false; return v; }
        }

        /// <summary>Called by CarController each physics step while nitro button held.</summary>
        public bool TryConsume(float dt)
        {
            if (meter <= 0.5f) { IsActive = false; return false; }
            meter = Mathf.Max(0f, meter - drainPerSec * dt);
            IsActive = true;
            return true;
        }

        /// <summary>Double-tap detection: two nitro presses within 0.35s = shockwave.</summary>
        public void RegisterTap()
        {
            if (Time.time - _lastTapTime < 0.35f && meter > 25f)
            {
                _shockwavePending = true;
                meter = Mathf.Max(0f, meter - 25f);
            }
            _lastTapTime = Time.time;
        }

        public void AddCharge(float amount)
        {
            meter = Mathf.Min(capacity, meter + amount);
        }

        public void AddPickupCharge(float amount) { AddCharge(amount); }
    }
}
