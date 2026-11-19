// TJ Racing 3D — chase camera: follows behind the player car, FOV widens with
// speed + nitro, small shake on shockwave / hard landing. Never clips the road.
using UnityEngine;

namespace TJRacing
{
    public class RaceCamera : MonoBehaviour
    {
        public static RaceCamera Instance { get; private set; }

        public Transform target;
        public Vector3 offset = new Vector3(0f, 4.2f, -9.5f);
        public float followSharpness = 6f;
        public float lookAhead = 8f;

        private Camera _cam;
        private float _baseFov = 62f;
        private float _fovKick;
        private float _shake;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Vector3 look = target.position + target.forward * lookAhead + Vector3.up * 1.4f;
            transform.LookAt(look);

            var car = target.GetComponent<CarController>();
            float speedFov = car != null ? Mathf.Clamp01(car.SpeedKmh / 240f) * 10f : 0f;
            _fovKick = Mathf.MoveTowards(_fovKick, 0f, 14f * Time.deltaTime);
            _cam.fieldOfView = _baseFov + speedFov + _fovKick;

            if (_shake > 0.001f)
            {
                transform.position += Random.insideUnitSphere * _shake * 0.12f;
                _shake = Mathf.MoveTowards(_shake, 0f, 2.5f * Time.deltaTime);
            }
        }

        public void KickFov(float amount) { _fovKick = Mathf.Max(_fovKick, amount); _shake = 1f; }

        public void Shake(float amount) { _shake = Mathf.Max(_shake, amount); }
    }
}
