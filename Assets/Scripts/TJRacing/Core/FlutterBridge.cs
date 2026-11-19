// TJ Racing 3D — Flutter bridge (flutter_embed_unity message channel).
//
// Unity -> Flutter: SendToFlutter.Send(json) — the plugin's Android/iOS glue forwards
//   the string to Flutter's UnityWidget onUnityMessage callback.
// Flutter -> Unity: the plugin calls SendMessage("FlutterBridge", "OnFlutterMessage", json)
//   on this GameObject. Every incoming message is a JSON object:
//   { "method": "<name>", "payload": { ... } }
//
// Methods Flutter may call:
//   SetConfig        { config: {...games.tj_racing...}, level, levelCoins, adCoins, rsValue }
//   SetSelectedCar   { carId }
//   SetUnlockedCars  { carIds: ["street_hawk", ...] }   (unlock state is decided by FLUTTER)
//   SetBalances      { levelCoins, adCoins, rsValue, totalEarnedRs }
//   SetSensitivity   { value }        (0.5 .. 2.0, tilt slider)
//   SetControlMode   { mode }         ("tilt" | "buttons" | "both")
//   RecalibrateTilt  {}
//   StartRace        { carId, level }
//   QuitToMenu       {}
//   ShowRewardedAd   { requestId }    (opt-in, from garage / "need Ad Coins?" prompt)
//   PowerupGranted   { powerupId }    (Flutter spent Ad Coins server-side
//     for a power-up — Unity applies the effect in the race, or holds
//     it as a pending grant for the next race if no race is active;
//     see RaceManager.ApplyPowerup + PendingPowerups)
// Outgoing messages (Unity -> Flutter) are JSON with a "type" field:
//   race_result, race_started, rewarded_completed, rewarded_failed,
//   interstitial_shown, race_cancelled, car_locked_tapped,
//   powerup_purchase_requested
//
// Unity NEVER credits any coin. Rewarded completion is only an EVENT; Flutter credits
// Ad Coins server-side after verifying it.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    public class FlutterBridge : MonoBehaviour
    {
        public static FlutterBridge Instance { get; private set; }

        public RacingConfig Config { get; private set; } = RacingConfig.Defaults();
        public string SelectedCarId { get; private set; } = "street_hawk";
        public HashSet<string> UnlockedCarIds { get; private set; } = new HashSet<string> { "street_hawk" };
        public int Level { get; private set; } = 1;
        public int LevelCoins { get; private set; }
        public int AdCoins { get; private set; }
        public string RsValue { get; private set; } = "";
        public string TotalEarnedRs { get; private set; } = "";
        public float TiltSensitivity { get; private set; } = 1.0f;
        public string ControlMode { get; private set; } = "both"; // tilt | buttons | both

        public event Action<RacingConfig> OnConfigApplied;
        public event Action<string> OnCarSelected;
        public event Action OnBalancesChanged;
        public event Action<float> OnSensitivityChanged;
        public event Action<string> OnControlModeChanged;
        public event Action OnStartRaceRequested;
        public event Action OnQuitToMenuRequested;
        public event Action<string> OnRewardedAdRequested; // requestId
        public event Action<string> OnPowerupGranted; // powerupId ("nitro_boost" | "shield")

        /// <summary>
        /// Power-up grants that arrived while no race was active
        /// (e.g. the purchase dialog finished after the player quit
        /// to the menu). RaceManager drains this at the next race
        /// start (StartRace → ApplyPowerup) and removes each id once
        /// applied, so a grant is never lost and never applied twice.
        /// </summary>
        public List<string> PendingPowerups { get; private set; } = new List<string>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.name = "FlutterBridge";
        }

        // ---- Unity -> Flutter -------------------------------------------------
        public void SendJson(string json)
        {
            try { global::SendToFlutter.Send(json); }
            catch (Exception e) { Debug.LogWarning("[TJ Racing] SendToFlutter failed: " + e.Message); }
        }

        public void SendRaceResult(RaceResult result) { SendJson(result.ToJson()); }

        public void SendEvent(string type, string placement, string raceId, string detail)
        {
            SendJson(new UnityEvent
            {
                type = type, placement = placement, race_id = raceId, detail = detail
            }.ToJson());
        }

        // ---- Public setters (same effect as the Flutter messages of the same name).
        // Used by in-Unity UI (pause menu, settings slider) so they never have to
        // fake a Flutter message through OnFlutterMessage.
        public void ApplySensitivity(float value)
        {
            TiltSensitivity = Mathf.Clamp(value, 0.5f, 2.0f);
            OnSensitivityChanged?.Invoke(TiltSensitivity);
        }

        public void ApplyControlMode(string mode)
        {
            if (mode != "tilt" && mode != "buttons" && mode != "both") return;
            ControlMode = mode;
            OnControlModeChanged?.Invoke(mode);
        }

        public void ApplySelectedCar(string carId)
        {
            if (string.IsNullOrEmpty(carId)) return;
            SelectedCarId = carId;
            OnCarSelected?.Invoke(carId);
        }

        // ---- Flutter -> Unity (called via UnityPlayer SendMessage) ------------
        // flutter_embed_unity delivers Flutter messages as a single string argument.
        public void OnFlutterMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                var envelope = JsonUtility.FromJson<FlutterMessage>(message);
                if (envelope == null || string.IsNullOrEmpty(envelope.method)) return;
                HandleMethod(envelope.method, envelope.payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TJ Racing] Bad Flutter message: " + e.Message + " :: " + message);
            }
        }

        [Serializable]
        private class FlutterMessage
        {
            public string method;
            public string payload; // JSON string (nested object serialised as string by Flutter)
        }

        [Serializable]
        private class ConfigPayload
        {
            public string config; // nested games.tj_racing JSON, as a string
            public int level;
            public int levelCoins;
            public int adCoins;
            public string rsValue;
            public string totalEarnedRs;
        }

        [Serializable]
        private class StringPayload { public string value; }
        [Serializable]
        private class CarPayload { public string carId; }
        [Serializable]
        private class UnlockedPayload { public string[] carIds; }
        [Serializable]
        private class BalancesPayload
        {
            public int levelCoins;
            public int adCoins;
            public string rsValue;
            public string totalEarnedRs;
        }
        [Serializable]
        private class FloatPayload { public float value; }
        [Serializable]
        private class StartRacePayload { public string carId; public int level; }
        [Serializable]
        private class RewardedPayload { public string requestId; }
        [Serializable]
        private class PowerupPayload { public string powerupId; }

        private void HandleMethod(string method, string payloadJson)
        {
            switch (method)
            {
                case "SetConfig":
                {
                    var p = JsonUtility.FromJson<ConfigPayload>(payloadJson ?? "{}");
                    if (p != null)
                    {
                        if (!string.IsNullOrEmpty(p.config)) Config = RacingConfig.FromJson(p.config);
                        Level = Mathf.Max(1, p.level);
                        LevelCoins = p.levelCoins;
                        AdCoins = p.adCoins;
                        RsValue = p.rsValue ?? "";
                        TotalEarnedRs = p.totalEarnedRs ?? "";
                        TiltSensitivity = Config.tiltSensitivity;
                        OnConfigApplied?.Invoke(Config);
                        OnBalancesChanged?.Invoke();
                    }
                    break;
                }
                case "SetSelectedCar":
                {
                    var p = JsonUtility.FromJson<CarPayload>(payloadJson ?? "{}");
                    if (p != null && !string.IsNullOrEmpty(p.carId))
                    {
                        SelectedCarId = p.carId;
                        OnCarSelected?.Invoke(p.carId);
                    }
                    break;
                }
                case "SetUnlockedCars":
                {
                    var p = JsonUtility.FromJson<UnlockedPayload>(payloadJson ?? "{}");
                    if (p != null && p.carIds != null)
                    {
                        UnlockedCarIds = new HashSet<string>(p.carIds);
                        if (!UnlockedCarIds.Contains("street_hawk")) UnlockedCarIds.Add("street_hawk");
                        OnBalancesChanged?.Invoke();
                    }
                    break;
                }
                case "SetBalances":
                {
                    var p = JsonUtility.FromJson<BalancesPayload>(payloadJson ?? "{}");
                    if (p != null)
                    {
                        LevelCoins = p.levelCoins;
                        AdCoins = p.adCoins;
                        RsValue = p.rsValue ?? "";
                        TotalEarnedRs = p.totalEarnedRs ?? "";
                        OnBalancesChanged?.Invoke();
                    }
                    break;
                }
                case "SetSensitivity":
                {
                    var p = JsonUtility.FromJson<FloatPayload>(payloadJson ?? "{}");
                    if (p != null) ApplySensitivity(p.value);
                    break;
                }
                case "SetControlMode":
                {
                    var p = JsonUtility.FromJson<StringPayload>(payloadJson ?? "{}");
                    if (p != null) ApplyControlMode(p.value);
                    break;
                }
                case "RecalibrateTilt":
                    TiltSteering.RequestRecalibrate();
                    break;
                case "StartRace":
                {
                    var p = JsonUtility.FromJson<StartRacePayload>(payloadJson ?? "{}");
                    if (p != null)
                    {
                        if (!string.IsNullOrEmpty(p.carId)) SelectedCarId = p.carId;
                        if (p.level > 0) Level = p.level;
                    }
                    OnStartRaceRequested?.Invoke();
                    break;
                }
                case "QuitToMenu":
                    OnQuitToMenuRequested?.Invoke();
                    break;
                case "ShowRewardedAd":
                {
                    var p = JsonUtility.FromJson<RewardedPayload>(payloadJson ?? "{}");
                    OnRewardedAdRequested?.Invoke(p != null ? p.requestId ?? "" : "");
                    break;
                }
                case "PowerupGranted":
                {
                    // Flutter spent Ad Coins server-side for this
                    // power-up (success or idempotent replay — either
                    // way the item IS granted). Queue it first so a
                    // grant that arrives with no race active is never
                    // lost; RaceManager applies it immediately when
                    // subscribed, or drains the queue at the next
                    // race start, and removes it once applied.
                    var p = JsonUtility.FromJson<PowerupPayload>(payloadJson ?? "{}");
                    if (p != null && !string.IsNullOrEmpty(p.powerupId))
                    {
                        PendingPowerups.Add(p.powerupId);
                        OnPowerupGranted?.Invoke(p.powerupId);
                    }
                    break;
                }
                default:
                    Debug.LogWarning("[TJ Racing] Unknown Flutter method: " + method);
                    break;
            }
        }
    }
}
