// TJ Racing 3D — lap-based traffic (research §A.3: each lap must feel different).
// Lap 1: clean track, learn the corners. Lap 2: bridge traffic appears (slow cars
// on the elevated bridge section only). Lap 3 AND hard level (level >=
// config.hardLevelThreshold): light traffic on the race road itself.
// Traffic cars are slow, clearly visible, and never appear on lap 1 — the first
// race must be winnable (research §A.8).
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    public class TrafficManager : MonoBehaviour
    {
        public TrackSpline spline;
        public int hardLevelThreshold = 3;

        private readonly List<TrafficCar> _active = new List<TrafficCar>();
        private int _currentLap = 1;
        private int _level = 1;

        public void Init(TrackSpline trackSpline, int level, int hardThreshold)
        {
            spline = trackSpline;
            _level = level;
            hardLevelThreshold = hardThreshold;
        }

        public void SetLap(int lap)
        {
            if (lap == _currentLap) return;
            _currentLap = lap;
            if (lap == 2) SpawnBridgeTraffic();
            else if (lap == 3 && _level >= hardLevelThreshold) SpawnRoadTraffic();
        }

        public void ResetRace()
        {
            _currentLap = 1;
            foreach (var t in _active) if (t != null) Destroy(t.gameObject);
            _active.Clear();
        }

        private void SpawnBridgeTraffic()
        {
            // 3 slow cars spread along the bridge deck (waypoints 22..27).
            for (int i = 0; i < 3; i++)
            {
                int wp = NightCityTrackData.BridgeStart + 1 + i * 2;
                SpawnOne(wp, 11f, new Color(0.9f, 0.9f, 0.85f)); // pale city cars
            }
        }

        private void SpawnRoadTraffic()
        {
            // Light road traffic: 4 cars on the start straight + tunnel exit.
            int[] wps = { 2, 9, 20, 31 };
            foreach (int wp in wps) SpawnOne(wp, 13f, new Color(0.75f, 0.78f, 0.85f));
        }

        private void SpawnOne(int waypointIndex, float speedMs, Color color)
        {
            if (spline == null || waypointIndex >= spline.Count) return;
            Vector3 p = spline.Points[waypointIndex];
            Quaternion rot = Quaternion.LookRotation(spline.DirectionAt(waypointIndex), Vector3.up);
            var go = new GameObject("TrafficCar_" + waypointIndex);
            go.transform.position = p;
            go.transform.rotation = rot;
            // Shield power-up: traffic lives on its own layer so the
            // shielded player car (layer PlayerCar) can pass through
            // it via Physics.IgnoreLayerCollision (see RaceManager).
            // AI cars stay on Default and still collide with traffic.
            go.layer = RaceLayers.Traffic;
            var car = go.AddComponent<TrafficCar>();
            car.Init(spline, waypointIndex, speedMs, color);
            _active.Add(car);
        }

        void Update()
        {
            // Despawn traffic far behind the player (keeps the scene light).
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (t == null) { _active.RemoveAt(i); continue; }
                if (t.HasFinishedLoop) { Destroy(t.gameObject); _active.RemoveAt(i); }
            }
        }
    }

    /// <summary>Slow traffic car: follows the spline at a fixed crawl, no AI brain.</summary>
    public class TrafficCar : MonoBehaviour
    {
        public bool HasFinishedLoop { get; private set; }

        private TrackSpline _spline;
        private int _index;
        private float _speedMs;
        private float _travelled;

        public void Init(TrackSpline spline, int startIndex, float speedMs, Color color)
        {
            _spline = spline;
            _index = startIndex;
            _speedMs = speedMs;
            // Simple boxy city car (distinct from race cars: taller, duller).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(1.8f, 1.1f, 4.0f);
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0.2f);
            mat.SetFloat("_Smoothness", 0.4f);
            body.GetComponent<Renderer>().material = mat;
            Destroy(body.GetComponent<Collider>());
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Cabin";
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(0f, 1.45f, -0.2f);
            top.transform.localScale = new Vector3(1.6f, 0.6f, 2.0f);
            var glass = new Material(Shader.Find("Standard"));
            glass.color = new Color(0.1f, 0.14f, 0.2f);
            top.GetComponent<Renderer>().material = glass;
            Destroy(top.GetComponent<Collider>());
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(1.9f, 1.6f, 4.1f);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // spline-driven, not physics-driven
        }

        void Update()
        {
            if (_spline == null) return;
            _travelled += _speedMs * Time.deltaTime;
            Vector3 target = _spline.PointAtDistance(_index, _travelled);
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir.normalized, Vector3.up), Time.deltaTime * 4f);
            transform.position = Vector3.Lerp(transform.position,
                new Vector3(target.x, _spline.Points[_index].y, target.z), Time.deltaTime * 6f);
            if (_travelled > 400f) HasFinishedLoop = true; // drove its stretch, despawn
        }
    }
}
