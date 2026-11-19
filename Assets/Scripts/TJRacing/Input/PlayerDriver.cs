// TJ Racing 3D — touch buttons + the player driver that merges tilt & buttons.
// Buttons (mockup): steer ◀ ▶ bottom-left, BRAKE bottom-right, NITRO above the
// nitro bar bottom-centre. Throttle is automatic (arcade: the car always drives;
// brake + steering are the skill). Control mode comes from FlutterBridge:
// "tilt" | "buttons" | "both" (switchable from the pause menu, research §A.1).
using UnityEngine;

namespace TJRacing
{
    /// <summary>Button state, written by RaceHUD's UI buttons (pointer down/up).</summary>
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        public bool SteerLeftHeld { get; private set; }
        public bool SteerRightHeld { get; private set; }
        public bool BrakeHeld { get; private set; }
        public bool NitroHeld { get; private set; }

        public float ButtonSteer
        {
            get
            {
                if (SteerLeftHeld && !SteerRightHeld) return -1f;
                if (SteerRightHeld && !SteerLeftHeld) return 1f;
                return 0f;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetSteerLeft(bool held) { SteerLeftHeld = held; }
        public void SetSteerRight(bool held) { SteerRightHeld = held; }
        public void SetBrake(bool held) { BrakeHeld = held; }
        public void SetNitro(bool held)
        {
            if (held && !NitroHeld)
            {
                var nitro = PlayerDriver.Instance?.Car?.Nitro;
                if (nitro != null) nitro.RegisterTap(); // double-tap = shockwave
            }
            NitroHeld = held;
        }
    }

    /// <summary>Merges tilt + touch into the player's CarController every physics step.</summary>
    [RequireComponent(typeof(CarController))]
    public class PlayerDriver : MonoBehaviour
    {
        public static PlayerDriver Instance { get; private set; }

        public CarController Car { get; private set; }

        private float _steerSmooth;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Car = GetComponent<CarController>();
        }

        void FixedUpdate()
        {
            if (Car == null) return;
            bool racing = RaceManager.Instance != null && RaceManager.Instance.State == RaceState.Racing;
            if (!racing)
            {
                Car.ThrottleInput = 0f;
                Car.BrakeInput = 0f;
                Car.NitroRequested = false;
                Car.SteerInput = 0f;
                return;
            }

            string mode = FlutterBridge.Instance != null ? FlutterBridge.Instance.ControlMode : "both";
            float steer = 0f;
            if (mode == "tilt" || mode == "both")
                steer += TiltSteering.Instance != null ? TiltSteering.Instance.Steer : 0f;
            if (mode == "buttons" || mode == "both")
                steer += TouchControls.Instance != null ? TouchControls.Instance.ButtonSteer : 0f;

            // Smooth the merged steer so tilt noise never jerks the car.
            _steerSmooth = Mathf.MoveTowards(_steerSmooth, Mathf.Clamp(steer, -1f, 1f),
                6f * Time.fixedDeltaTime);
            Car.SteerInput = _steerSmooth;
            Car.ThrottleInput = 1f; // arcade auto-throttle
            Car.BrakeInput = TouchControls.Instance != null && TouchControls.Instance.BrakeHeld ? 1f : 0f;
            bool nitroEnabled = FlutterBridge.Instance == null || FlutterBridge.Instance.Config.race.nitro_enabled;
            Car.NitroRequested = nitroEnabled && TouchControls.Instance != null && TouchControls.Instance.NitroHeld;

            // Shockwave: double-tap nitro spends 25 meter for a 1.2s surge.
            if (Car.Nitro != null && Car.Nitro.ShockwavePending)
                StartCoroutine(ShockwaveRoutine());
        }

        private System.Collections.IEnumerator ShockwaveRoutine()
        {
            float originalTop = Car.topSpeedKmh;
            Car.topSpeedKmh = originalTop * 1.25f;
            if (RaceCamera.Instance != null) RaceCamera.Instance.KickFov(8f);
            AudioManager.Instance?.PlayNitro();
            yield return new WaitForSeconds(1.2f);
            Car.topSpeedKmh = originalTop;
        }
    }
}
