// TJ Racing 3D — arcade car physics on Unity WheelColliders.
// Written from scratch for this project (see LICENCES.md): the Realistic Car
// Controller FREE edition is marketed by its publisher as an *evaluation* version
// of the paid Pro product, so we do NOT ship a commercial game on it. This
// controller is deliberately arcade (NFS feel, not simulation): strong grip,
// forgiving steering, drift = handbrake-style rear slip that charges nitro.
using UnityEngine;

namespace TJRacing
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Wheels")]
        public WheelCollider wheelFL;
        public WheelCollider wheelFR;
        public WheelCollider wheelRL;
        public WheelCollider wheelRR;
        public Transform wheelMeshFL;
        public Transform wheelMeshFR;
        public Transform wheelMeshRL;
        public Transform wheelMeshRR;

        [Header("Tuning (overridden per car by CarFactory from CarDef)")]
        public float topSpeedKmh = 180f;
        public float acceleration = 12f;      // m/s^2
        public float brakeForce = 3200f;
        public float maxSteerAngle = 28f;
        public float handling = 1.0f;
        public float nitroCapacity = 100f;
        public float downforce = 60f;

        // Live inputs (-1..1 steer, 0..1 throttle, 0..1 brake). Set by player input
        // (TiltSteering / TouchControls) or by AICarController.
        public float SteerInput { get; set; }
        public float ThrottleInput { get; set; }
        public float BrakeInput { get; set; }
        public bool NitroRequested { get; set; }
        public bool IsPlayer { get; set; }

        public float SpeedKmh { get; private set; }
        public float SpeedMs { get; private set; }
        public bool IsDrifting { get; private set; }
        public NitroSystem Nitro { get; private set; }

        private Rigidbody _rb;
        private float _steerSmooth;
        private bool _offTrackSlowdown;
        private bool _wasNitroActive;
        private float _lastCrashSoundTime = -10f;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, -0.55f, 0f); // low CoM: no rollovers on ramps
            Nitro = gameObject.AddComponent<NitroSystem>();
        }

        void Start()
        {
            // CarFactory sets nitroCapacity AFTER Awake (AddComponent order), so sync here.
            if (Nitro != null) Nitro.capacity = nitroCapacity;
        }

        void FixedUpdate()
        {
            SpeedMs = _rb.velocity.magnitude;
            SpeedKmh = SpeedMs * 3.6f;

            // --- Steering (speed-sensitive: less angle at high speed) ---
            float speedFactor = Mathf.Clamp01(1f - (SpeedKmh / (topSpeedKmh * 1.35f)));
            float targetSteer = Mathf.Clamp(SteerInput, -1f, 1f) * maxSteerAngle
                              * handling * (0.45f + 0.55f * speedFactor);
            _steerSmooth = Mathf.MoveTowards(_steerSmooth, targetSteer, 90f * Time.fixedDeltaTime);
            wheelFL.steerAngle = _steerSmooth;
            wheelFR.steerAngle = _steerSmooth;

            // --- Drive ---
            float topSpeedMs = topSpeedKmh / 3.6f;
            bool nitroActive = NitroRequested && Nitro.TryConsume(Time.fixedDeltaTime);
            float boost = nitroActive ? 1.6f : 1f;
            if (ThrottleInput > 0.01f && SpeedMs < topSpeedMs * boost)
            {
                float force = acceleration * ThrottleInput * boost * _rb.mass;
                if (_offTrackSlowdown) force *= 0.45f;
                _rb.AddForce(transform.forward * force, ForceMode.Force);
            }

            // --- Braking / reverse ---
            if (BrakeInput > 0.01f)
            {
                if (SpeedMs > 1f)
                {
                    wheelRL.brakeTorque = brakeForce * BrakeInput;
                    wheelRR.brakeTorque = brakeForce * BrakeInput;
                }
                else // stopped: brake becomes reverse
                {
                    wheelRL.brakeTorque = 0f;
                    wheelRR.brakeTorque = 0f;
                    _rb.AddForce(-transform.forward * acceleration * 0.45f * BrakeInput * _rb.mass, ForceMode.Force);
                }
            }
            else
            {
                wheelRL.brakeTorque = 0f;
                wheelRR.brakeTorque = 0f;
            }

            // --- Downforce (keeps the car planted over jumps/ramps) ---
            _rb.AddForce(-transform.up * downforce * SpeedMs, ForceMode.Force);

            // --- Drift detection: strong lateral slip while steering at speed ---
            Vector3 localVel = transform.InverseTransformDirection(_rb.velocity);
            float lateralSlip = Mathf.Abs(localVel.x);
            IsDrifting = SpeedKmh > 60f && lateralSlip > 4.5f && Mathf.Abs(SteerInput) > 0.35f;
            if (IsDrifting && Nitro != null) Nitro.AddCharge(lateralSlip * Time.fixedDeltaTime * 2.2f);

            // --- Sounds (player car only): brake screech while braking
            // at speed, skid loop while drifting, nitro whoosh the
            // moment the meter starts burning.
            if (IsPlayer)
            {
                var audio = AudioManager.Instance;
                if (audio != null)
                {
                    if (BrakeInput > 0.01f && SpeedKmh > 30f) audio.StartBrake(); else audio.StopBrake();
                    if (IsDrifting) audio.StartSkid(); else audio.StopSkid();
                    if (nitroActive && !_wasNitroActive) audio.PlayNitro();
                    _wasNitroActive = nitroActive;
                }
            }

            // --- Wheel visuals ---
            UpdateWheelVisual(wheelFL, wheelMeshFL);
            UpdateWheelVisual(wheelFR, wheelMeshFR);
            UpdateWheelVisual(wheelRL, wheelMeshRL);
            UpdateWheelVisual(wheelRR, wheelMeshRR);
        }

        private void UpdateWheelVisual(WheelCollider col, Transform mesh)
        {
            if (col == null || mesh == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        // Called by TrackBuilder's off-road trigger volumes.
        public void SetOffTrack(bool offTrack) { _offTrackSlowdown = offTrack; }

        // Crash sound (player only, throttled): a real impact, not a
        // brush — traffic, barriers and AI cars all report here.
        void OnCollisionEnter(Collision collision)
        {
            if (!IsPlayer) return;
            if (collision.relativeVelocity.magnitude < 3f) return;
            if (Time.time - _lastCrashSoundTime < 0.5f) return;
            _lastCrashSoundTime = Time.time;
            AudioManager.Instance?.PlayCrash();
        }

        public void ResetTo(Vector3 pos, Quaternion rot)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position = pos;
            transform.rotation = rot;
            _steerSmooth = 0f;
        }
    }
}
