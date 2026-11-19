// TJ Racing 3D — ordered checkpoints, lap counting, live race positions.
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    /// <summary>Trigger volume placed on the spline by TrackBuilder.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public int checkpointIndex;

        void OnTriggerEnter(Collider other)
        {
            var tracker = other.GetComponentInParent<LapTracker>();
            if (tracker != null) tracker.OnCheckpoint(checkpointIndex);
        }
    }

    /// <summary>
    /// Per-racer progress: laps + last checkpoint + distance to next checkpoint.
    /// A lap only counts when checkpoints are hit IN ORDER (no cutting the track).
    /// ProgressMetric drives the live position board — same value for player and AI.
    /// </summary>
    public class LapTracker : MonoBehaviour
    {
        public int TotalCheckpoints { get; set; } = 10;
        public int TotalLaps { get; set; } = 3;

        public int CurrentLap { get; private set; } = 1;
        public int NextCheckpoint { get; private set; } = 1; // 0 is the start line
        public bool HasFinished { get; private set; }
        public bool IsPlayer { get; set; }

        // Progress metric: higher = further ahead in the race.
        public float ProgressMetric { get; private set; }

        private TrackSpline _spline;
        private int _nearestSplineIndex;

        public void Init(TrackSpline spline, int totalCheckpoints, int totalLaps)
        {
            _spline = spline;
            TotalCheckpoints = totalCheckpoints;
            TotalLaps = totalLaps;
            _nearestSplineIndex = 0;
        }

        void Update()
        {
            if (_spline == null) return;
            _nearestSplineIndex = _spline.NearestIndex(transform.position, _nearestSplineIndex);
            float distAlong = _spline.DistanceAlong(_nearestSplineIndex);
            // Progress = completed laps + distance along track. Checkpoint gating
            // (OnCheckpoint) is what stops track-cutting: a lap only increments at
            // the start-line trigger after every checkpoint was hit in order.
            ProgressMetric = (CurrentLap - 1) * _spline.TotalLength + distAlong;
        }

        public void OnCheckpoint(int index)
        {
            if (HasFinished) return;
            if (index == NextCheckpoint)
            {
                NextCheckpoint++;
                if (NextCheckpoint >= TotalCheckpoints)
                {
                    // All checkpoints done — next trigger is the start line (index 0).
                    NextCheckpoint = 0;
                }
            }
            else if (index == 0 && NextCheckpoint == 0)
            {
                // Crossed the start line after every checkpoint: lap complete.
                if (IsPlayer) RaceManager.Instance?.OnPlayerLapCompleted(CurrentLap);
                CurrentLap++;
                NextCheckpoint = 1;
                if (CurrentLap > TotalLaps)
                {
                    HasFinished = true;
                    if (!IsPlayer) RacePositionTracker.Instance?.NotifyAiFinished(this);
                }
            }
            // Any other index = out of order (wrong way / missed) — ignored.
        }

        public void ResetForRace()
        {
            CurrentLap = 1;
            NextCheckpoint = 1;
            HasFinished = false;
            ProgressMetric = 0f;
        }
    }

    /// <summary>Live positions 1..6 from ProgressMetric. No rubber-banding here either.</summary>
    public class RacePositionTracker : MonoBehaviour
    {
        public static RacePositionTracker Instance { get; private set; }

        private readonly List<LapTracker> _racers = new List<LapTracker>();
        private LapTracker _playerTracker;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Register(LapTracker tracker)
        {
            if (!_racers.Contains(tracker)) _racers.Add(tracker);
            if (tracker.IsPlayer) _playerTracker = tracker;
        }

        public void Clear() { _racers.Clear(); _playerTracker = null; }

        public int GetPlayerPosition()
        {
            if (_playerTracker == null) return 6;
            int pos = 1;
            foreach (var r in _racers)
                if (r != _playerTracker && r.ProgressMetric > _playerTracker.ProgressMetric) pos++;
            return pos;
        }

        public int GetPositionOf(LapTracker tracker)
        {
            int pos = 1;
            foreach (var r in _racers)
                if (r != tracker && r.ProgressMetric > tracker.ProgressMetric) pos++;
            return pos;
        }

        public void NotifyAiFinished(LapTracker ai) { /* reserved: finish-order board */ }
    }
}
