// TJ Racing 3D — race state machine.
// Owns countdown, timer, laps, finish detection and the race-result event.
// Coin amounts here are INFORMATIONAL ONLY (win 10 / 2nd 5 / else 0, plan §7) —
// Flutter + server re-derive and enforce everything.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    public enum RaceState { Menu, Countdown, Racing, Paused, Finished }

    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public RaceState State { get; private set; } = RaceState.Menu;
        public string RaceId { get; private set; }
        public int TotalLaps { get; private set; } = 3;
        public float RaceTime { get; private set; }
        public float BestLapTime { get; private set; } = float.MaxValue;
        public int CurrentLap { get; private set; } = 1;
        public bool IsFirstRace { get; private set; }
        public int PlayerPosition { get; private set; } = 6;

        /// <summary>Shield pass-through window, in race-time seconds.</summary>
        public const float ShieldDurationSecs = 20f;

        private float _shieldUntil = 0f; // Time.time value; 0 = no shield active

        public event Action<RaceState> OnStateChanged;
        public event Action<int> OnLapChanged;          // new lap number
        public event Action<float> OnCountdownTick;     // 3, 2, 1, 0 = GO
        public event Action<RaceResult> OnRaceFinished;

        [Header("Refs (wired in scene / bootstrap)")]
        public LapTracker playerLapTracker;
        public RacePositionTracker positionTracker;
        public TrafficManager trafficManager;
        public NitroSystem playerNitro;

        private float _lapStartTime;
        private bool _resultSent;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Power-up grants (Flutter's PowerupGranted, after a
            // successful server spend) apply here. FlutterBridge is
            // DontDestroyOnLoad and outlives this manager, so
            // unsubscribe in OnDestroy.
            if (FlutterBridge.Instance != null)
                FlutterBridge.Instance.OnPowerupGranted += OnPowerupGranted;
        }

        void OnDestroy()
        {
            if (FlutterBridge.Instance != null)
                FlutterBridge.Instance.OnPowerupGranted -= OnPowerupGranted;
            // Never leak a shield into the next race (e.g. the
            // player quit mid-shield): traffic must collide again.
            Physics.IgnoreLayerCollision(RaceLayers.PlayerCar, RaceLayers.Traffic, false);
        }

        void Update()
        {
            if (State != RaceState.Racing) return;
            RaceTime += Time.deltaTime;
            if (playerLapTracker != null) CurrentLap = Mathf.Clamp(playerLapTracker.CurrentLap, 1, TotalLaps);
            if (positionTracker != null) PlayerPosition = positionTracker.GetPlayerPosition();
            if (trafficManager != null) trafficManager.SetLap(CurrentLap);
            // Shield expiry (race time only — Time.time is frozen
            // while paused, so a shield bought from the pause panel
            // starts counting down when the race resumes, not before).
            if (_shieldUntil > 0f && Time.time >= _shieldUntil)
            {
                _shieldUntil = 0f;
                Physics.IgnoreLayerCollision(RaceLayers.PlayerCar, RaceLayers.Traffic, false);
            }
        }

        public void StartRace(int totalLaps, bool isFirstRace)
        {
            TotalLaps = Mathf.Max(1, totalLaps);
            IsFirstRace = isFirstRace;
            RaceId = Guid.NewGuid().ToString("N");
            RaceTime = 0f;
            BestLapTime = float.MaxValue;
            CurrentLap = 1;
            _resultSent = false;
            _lapStartTime = 0f;
            // Fresh race: no shield carries over, and any grant that
            // arrived while no race was active applies now — the
            // player car and playerNitro exist by this point (the
            // bootstrap builds them before calling StartRace).
            _shieldUntil = 0f;
            Physics.IgnoreLayerCollision(RaceLayers.PlayerCar, RaceLayers.Traffic, false);
            var bridge = FlutterBridge.Instance;
            if (bridge != null)
            {
                foreach (var id in new List<string>(bridge.PendingPowerups)) ApplyPowerup(id);
            }
            SetState(RaceState.Countdown);
            StartCoroutine(CountdownRoutine());
            FlutterBridge.Instance?.SendEvent("race_started", "", RaceId,
                "{\"track\":\"night_city_1\",\"laps\":" + TotalLaps + "}");
        }

        private void OnPowerupGranted(string powerupId) { ApplyPowerup(powerupId); }

        /// <summary>
        /// Applies a purchased power-up in the race. Called from the
        /// `PowerupGranted` event (grant arrived mid-race) and from
        /// `StartRace` (grant arrived while no race was active).
        /// A successfully applied grant is removed from
        /// `FlutterBridge.PendingPowerups`; a nitro grant that
        /// arrives before the player car exists stays queued and is
        /// applied by the StartRace drain instead.
        ///
        /// Effects (the only two power-ups in config, plan §9):
        /// - `nitro_boost`: fills the nitro meter — same
        ///   `NitroSystem.AddCharge` path as drift charging.
        /// - `shield`: the player car passes through traffic for
        ///   `ShieldDurationSecs` of race time. There was NO shield
        ///   mechanic anywhere in the project before this (only the
        ///   config def) — the effect is the smallest real one on
        ///   the game's actual penalty: traffic cars are kinematic
        ///   physical obstacles, so the shield ignores
        ///   PlayerCar-vs-Traffic collisions for the window (AI
        ///   cars still collide with traffic; the player still
        ///   collides with AI cars and the track).
        /// </summary>
        private void ApplyPowerup(string powerupId)
        {
            if (string.IsNullOrEmpty(powerupId)) return;
            bool applied = false;
            if (powerupId == "nitro_boost")
            {
                if (playerNitro != null)
                {
                    playerNitro.AddCharge(playerNitro.capacity);
                    RaceHUD.Instance?.ShowMessage("NITRO BOOST!");
                    applied = true;
                }
                // else: player car not built yet — leave the grant in
                // PendingPowerups; StartRace drains it once
                // playerNitro exists.
            }
            else if (powerupId == "shield")
            {
                Physics.IgnoreLayerCollision(RaceLayers.PlayerCar, RaceLayers.Traffic, true);
                _shieldUntil = Time.time + ShieldDurationSecs;
                RaceHUD.Instance?.ShowMessage("SHIELD ACTIVE");
                applied = true;
            }
            else
            {
                // Unknown id: drop it rather than clogging the
                // pending list forever (Flutter only sends ids from
                // the remote-config powerups list).
                Debug.LogWarning("[TJ Racing] Unknown power-up granted: " + powerupId);
                applied = true;
            }
            if (applied) FlutterBridge.Instance?.PendingPowerups.Remove(powerupId);
        }

        private IEnumerator CountdownRoutine()
        {
            // Tilt auto-calibrates at race start (research §A.1): however the player
            // holds the phone when the countdown begins IS zero.
            TiltSteering.RequestRecalibrate();
            for (int i = 3; i >= 1; i--)
            {
                OnCountdownTick?.Invoke(i);
                yield return new WaitForSecondsRealtime(1f);
            }
            OnCountdownTick?.Invoke(0); // GO
            _lapStartTime = 0f;
            SetState(RaceState.Racing);
        }

        public void OnPlayerLapCompleted(int lap)
        {
            float lapTime = RaceTime - _lapStartTime;
            _lapStartTime = RaceTime;
            if (lapTime > 1f && lapTime < BestLapTime) BestLapTime = lapTime;
            OnLapChanged?.Invoke(Mathf.Min(lap + 1, TotalLaps));
            if (lap >= TotalLaps) FinishRace();
        }

        public void FinishRace()
        {
            if (State == RaceState.Finished || _resultSent) return;
            SetState(RaceState.Finished);
            _resultSent = true;

            int pos = positionTracker != null ? positionTracker.GetPlayerPosition() : 6;
            PlayerPosition = pos;
            var bridge = FlutterBridge.Instance;
            var coinCfg = bridge != null ? bridge.Config.level_coins : new LevelCoinsConfig();
            int coins = pos == 1 ? coinCfg.win : pos == 2 ? coinCfg.second : 0;

            var result = new RaceResult
            {
                race_id = RaceId,
                car_id = FlutterBridge.Instance != null ? FlutterBridge.Instance.SelectedCarId : "street_hawk",
                position = pos,
                laps_completed = TotalLaps,
                race_time_secs = RaceTime,
                best_lap_secs = BestLapTime == float.MaxValue ? RaceTime : BestLapTime,
                level_coins_earned = coins,
                is_first_race = IsFirstRace,
            };
            OnRaceFinished?.Invoke(result);
            FlutterBridge.Instance?.SendRaceResult(result);
        }

        public void PauseRace()
        {
            if (State != RaceState.Racing) return;
            Time.timeScale = 0f;
            SetState(RaceState.Paused);
        }

        public void ResumeRace()
        {
            if (State != RaceState.Paused) return;
            Time.timeScale = 1f;
            SetState(RaceState.Racing);
        }

        public void QuitRace()
        {
            Time.timeScale = 1f;
            FlutterBridge.Instance?.SendEvent("race_cancelled", "", RaceId ?? "", "{}");
            SetState(RaceState.Menu);
        }

        private void SetState(RaceState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
