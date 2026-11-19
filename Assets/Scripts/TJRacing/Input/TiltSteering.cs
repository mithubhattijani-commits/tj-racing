// TJ Racing 3D — tilt steering (research §A.1: the #1 make-or-break).
// - Auto-calibrate neutral at EVERY race start: however the player holds the phone
//   when the countdown begins IS zero (RaceManager calls RequestRecalibrate()).
// - Dead zone ±10° so small shakes don't steer (config tiltDeadZoneDeg).
// - Sensitivity slider (0.5..2.0, from Flutter Settings) scales the response.
// - Re-calibrate button on the HUD during a race (RaceHUD wires it) + the
//   Flutter "RecalibrateTilt" message.
// - Fallback: devices without a gyroscope use the accelerometer.
// - Player can switch tilt <-> buttons from the pause menu (control mode lives
//   on FlutterBridge; TouchControls reads it too).
using UnityEngine;

namespace TJRacing
{
    public class TiltSteering : MonoBehaviour
    {
        public static TiltSteering Instance { get; private set; }

        /// <summary>Current steer value -1..1 (0 when tilt is disabled / not racing).</summary>
        public float Steer { get; private set; }

        private static bool _recalibrateRequested;
        private Quaternion _neutralAttitude = Quaternion.identity;
        private Vector3 _neutralAccel = Vector3.zero;
        private bool _useGyro;
        private bool _calibrated;

        public static void RequestRecalibrate() { _recalibrateRequested = true; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _useGyro = SystemInfo.supportsGyroscope;
            if (_useGyro) Input.gyro.enabled = true;
        }

        void Update()
        {
            if (_recalibrateRequested)
            {
                _recalibrateRequested = false;
                Calibrate();
            }
            if (!_calibrated) return;
            if (RaceManager.Instance != null && RaceManager.Instance.State != RaceState.Racing)
            {
                Steer = 0f;
                return;
            }

            float sensitivity = FlutterBridge.Instance != null
                ? FlutterBridge.Instance.TiltSensitivity : 1.0f;
            float deadZoneDeg = FlutterBridge.Instance != null
                ? FlutterBridge.Instance.Config.tiltDeadZoneDeg : 10f;

            float rollDeg = _useGyro ? GyroRollDeg() : AccelRollDeg();

            // Dead zone: ignore the first ±10° around neutral.
            float sign = Mathf.Sign(rollDeg);
            float over = Mathf.Abs(rollDeg) - deadZoneDeg;
            if (over <= 0f) { Steer = 0f; return; }

            // Map 10°..55° of tilt to 0..1 steer, scaled by sensitivity.
            float normalized = Mathf.Clamp01(over / 45f);
            Steer = Mathf.Clamp(sign * normalized * sensitivity, -1f, 1f);
        }

        private void Calibrate()
        {
            if (_useGyro)
            {
                Input.gyro.enabled = true;
                _neutralAttitude = Input.gyro.attitude;
            }
            else
            {
                _neutralAccel = Input.acceleration;
            }
            _calibrated = true;
        }

        private float GyroRollDeg()
        {
            // Relative rotation from the calibrated neutral attitude, Z axis = roll.
            Quaternion relative = Quaternion.Inverse(_neutralAttitude) * Input.gyro.attitude;
            // Convert to Unity space (gyro is right-handed, screen-relative).
            Quaternion unity = new Quaternion(relative.x, relative.y, -relative.z, -relative.w);
            return unity.eulerAngles.z > 180f
                ? unity.eulerAngles.z - 360f
                : unity.eulerAngles.z;
        }

        private float AccelRollDeg()
        {
            Vector3 a = Input.acceleration - _neutralAccel;
            // Roll from the gravity vector's X component (portrait phone held upright).
            return Mathf.Atan2(a.x, -a.y) * Mathf.Rad2Deg;
        }
    }
}
