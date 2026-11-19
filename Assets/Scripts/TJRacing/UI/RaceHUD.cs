// TJ Racing 3D — race HUD, matching the gameplay mockup.
// Layout (portrait 1080x1920):
//   top row (below the passive Unity banner strip): coins pill left
//     ("● 1,240 • Rs 12" — values come from Flutter via SetBalances),
//     timer centre ("01:23" + "LAP 2 / 3" gold), pause button top-right.
//   position card, left below top row: "2nd" big gold + "of 6 racers".
//   re-calibrate button: small, under the position card (tilt zeroing mid-race).
//   speed bottom-right: huge "187" + "km/h" gold.
//   steer ◀ ▶ bottom-left, BRAKE red bottom-right, NITRO bar bottom-centre.
//   countdown "3 / 2 / 1 / GO!" centre. Message line centre (wrong way, etc.).
//   pause panel: RESUME / control mode / QUIT RACE, plus one button per
//   configured power-up (tap sends "powerup_purchase_requested" to Flutter —
//   Flutter shows its own purchase dialog and spends Ad Coins server-side).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TJRacing
{
    public class RaceHUD : MonoBehaviour
    {
        public static RaceHUD Instance { get; private set; }

        private Canvas _canvas;
        private Text _coinsText;
        private Text _timerText;
        private Text _lapText;
        private Text _positionText;
        private Text _positionSubText;
        private Text _speedText;
        private Text _countdownText;
        private Text _messageText;
        private Image _nitroFill;
        private GameObject _pausePanel;
        private Text _pauseTitle;

        private CarController _playerCar;
        private float _messageTimer;
        private readonly List<GameObject> _powerUpButtons = new List<GameObject>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
        }

        void Start()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnCountdownTick += OnCountdown;
                RaceManager.Instance.OnLapChanged += lap => ShowMessage("LAP " + lap);
                RaceManager.Instance.OnStateChanged += OnStateChanged;
            }
            if (FlutterBridge.Instance != null)
            {
                FlutterBridge.Instance.OnBalancesChanged += RefreshCoinsPill;
                // Rebuild the power-up buttons when Flutter sends (new) config —
                // prices / the power-up list come from remote config, not code.
                FlutterBridge.Instance.OnConfigApplied += _ => BuildPowerUpButtons();
            }
            RefreshCoinsPill();
        }

        void Update()
        {
            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0f) _messageText.text = "";
            }
            if (RaceManager.Instance == null || RaceManager.Instance.State != RaceState.Racing) return;
            if (_playerCar == null && PlayerDriver.Instance != null) _playerCar = PlayerDriver.Instance.Car;

            _timerText.text = FormatTime(RaceManager.Instance.RaceTime);
            _lapText.text = "LAP " + RaceManager.Instance.CurrentLap + " / " + RaceManager.Instance.TotalLaps;
            int pos = RaceManager.Instance.PlayerPosition;
            _positionText.text = Ordinal(pos);
            _positionSubText.text = "of 6 racers";
            if (_playerCar != null)
            {
                _speedText.text = Mathf.RoundToInt(_playerCar.SpeedKmh).ToString();
                if (_playerCar.Nitro != null)
                    _nitroFill.fillAmount = _playerCar.Nitro.Normalized;
            }
        }

        // ---- UI construction ---------------------------------------------------
        private void BuildUI()
        {
            _canvas = UIBuilder.MakeCanvas(gameObject, "RaceHUDCanvas");

            // Top row — coins pill (left).
            var coinsPanel = UIBuilder.Panel(_canvas.transform, "CoinsPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -150), new Vector2(430, -70), UIBuilder.Panel);
            _coinsText = UIBuilder.Label(coinsPanel, "CoinsText", "● 0", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_coinsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));

            // Timer centre + lap below.
            _timerText = UIBuilder.Label(_canvas.transform, "TimerText", "00:00", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-160, -160), new Vector2(160, -70));
            _lapText = UIBuilder.Label(_canvas.transform, "LapText", "LAP 1 / 3", 44, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_lapText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-160, -215), new Vector2(160, -160));

            // Pause (top-right).
            var pauseBtn = UIBuilder.MakeButton(_canvas.transform, "PauseBtn", "II", 44, UIBuilder.Panel, OnPausePressed);
            UIBuilder.Place(pauseBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-130, -170), new Vector2(-24, -70));

            // Position card (left, below top row).
            var posPanel = UIBuilder.Panel(_canvas.transform, "PositionCard",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -400), new Vector2(300, -240), UIBuilder.Panel);
            _positionText = UIBuilder.Label(posPanel, "PositionText", "6th", 72, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_positionText.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _positionSubText = UIBuilder.Label(posPanel, "PositionSubText", "of 6 racers", 32, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(_positionSubText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);

            // Re-calibrate tilt (small, under position card — research §A.1).
            var recalBtn = UIBuilder.MakeButton(_canvas.transform, "RecalBtn", "⟳ RE-CALIBRATE", 30, UIBuilder.PanelSoft, OnRecalibratePressed);
            UIBuilder.Place(recalBtn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -490), new Vector2(330, -420));

            // Speed bottom-right.
            _speedText = UIBuilder.Label(_canvas.transform, "SpeedText", "0", 150, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UIBuilder.Place(_speedText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-360, 330), new Vector2(-30, 540));
            var kmh = UIBuilder.Label(_canvas.transform, "KmhText", "km/h", 44, UIBuilder.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            UIBuilder.Place(kmh.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-360, 270), new Vector2(-30, 330));

            // Nitro bar bottom-centre.
            var nitroBg = UIBuilder.Panel(_canvas.transform, "NitroBarBg",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260, 210), new Vector2(260, 250), UIBuilder.Panel);
            var nitroFillGo = new GameObject("NitroFill");
            nitroFillGo.transform.SetParent(nitroBg, false);
            var frt = nitroFillGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _nitroFill = nitroFillGo.AddComponent<Image>();
            _nitroFill.color = UIBuilder.Cyan;
            _nitroFill.type = Image.Type.Filled;
            _nitroFill.fillMethod = Image.FillMethod.Horizontal;
            _nitroFill.fillAmount = 0.3f;
            var nitroLabel = UIBuilder.Label(_canvas.transform, "NitroLabel", "NITRO", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(nitroLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160, 140), new Vector2(160, 210));

            // Steer buttons bottom-left.
            var leftBtn = UIBuilder.MakeHoldButton(_canvas.transform, "SteerLeft", "◀", 72, UIBuilder.Panel,
                held => TouchControls.Instance?.SetSteerLeft(held));
            UIBuilder.Place(leftBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 60), new Vector2(184, 220));
            var rightBtn = UIBuilder.MakeHoldButton(_canvas.transform, "SteerRight", "▶", 72, UIBuilder.Panel,
                held => TouchControls.Instance?.SetSteerRight(held));
            UIBuilder.Place(rightBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200, 60), new Vector2(360, 220));

            // NITRO hold button (bottom-centre, above the bar).
            var nitroBtn = UIBuilder.MakeHoldButton(_canvas.transform, "NitroBtn", "NITRO", 44, UIBuilder.Cyan,
                held => TouchControls.Instance?.SetNitro(held));
            UIBuilder.Place(nitroBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110, 270), new Vector2(110, 380));

            // BRAKE bottom-right.
            var brakeBtn = UIBuilder.MakeHoldButton(_canvas.transform, "BrakeBtn", "BRAKE", 48, UIBuilder.Red,
                held => TouchControls.Instance?.SetBrake(held));
            UIBuilder.Place(brakeBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260, 60), new Vector2(-24, 260));

            // Countdown + message (centre).
            _countdownText = UIBuilder.Label(_canvas.transform, "CountdownText", "", 220, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -160), new Vector2(300, 160));
            _messageText = UIBuilder.Label(_canvas.transform, "MessageText", "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, 200), new Vector2(400, 280));

            BuildPausePanel();
            _canvas.gameObject.SetActive(true);
        }

        private void BuildPausePanel()
        {
            _pausePanel = new GameObject("PausePanel");
            _pausePanel.transform.SetParent(_canvas.transform, false);
            var rt = _pausePanel.AddComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = _pausePanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            _pauseTitle = UIBuilder.Label(_pausePanel.transform, "PauseTitle", "PAUSED", 80, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_pauseTitle.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(-300, -60), new Vector2(300, 60));
            var resumeBtn = UIBuilder.MakeButton(_pausePanel.transform, "ResumeBtn", "RESUME", 48, UIBuilder.Green, OnResumePressed);
            UIBuilder.Place(resumeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -40), new Vector2(300, 80));
            var tiltBtn = UIBuilder.MakeButton(_pausePanel.transform, "TiltModeBtn", "CONTROL: TILT + BUTTONS", 36, UIBuilder.Panel, OnCycleControlMode);
            UIBuilder.Place(tiltBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -180), new Vector2(300, -70));
            var quitBtn = UIBuilder.MakeButton(_pausePanel.transform, "QuitBtn", "QUIT RACE", 44, UIBuilder.Red, OnQuitPressed);
            UIBuilder.Place(quitBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -320), new Vector2(300, -210));
            BuildPowerUpButtons();
            _pausePanel.SetActive(false);
        }

        /// <summary>
        /// One button per power-up in the (remote) config, below QUIT RACE.
        /// Tapping one does NOT spend anything in Unity — it sends a
        /// "powerup_purchase_requested" event to Flutter, which shows its own
        /// purchase dialog and spends Ad Coins server-side (spend-race-item,
        /// item "race_powerup_&lt;id&gt;"). Rebuilt whenever config arrives,
        /// so prices always match the server config. No buttons when the
        /// config carries no power-ups.
        /// </summary>
        private void BuildPowerUpButtons()
        {
            foreach (var go in _powerUpButtons) if (go != null) Destroy(go);
            _powerUpButtons.Clear();
            if (_pausePanel == null) return;
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : RacingConfig.Defaults();
            if (cfg == null || cfg.powerups == null) return;
            float yMin = -460f;
            foreach (var def in cfg.powerups)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                var powerup = def;
                var btn = UIBuilder.MakeButton(_pausePanel.transform, "PowerUp_" + powerup.id,
                    PowerUpLabel(powerup), 36, UIBuilder.PanelSoft, () => OnPowerUpPressed(powerup));
                UIBuilder.Place(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-300, yMin), new Vector2(300, yMin + 110));
                _powerUpButtons.Add(btn.gameObject);
                yMin -= 140f;
            }
        }

        private static string PowerUpLabel(PowerUpDef def)
        {
            return def.name.ToUpper() + "  •  " + def.ad_coins_price + " AD COINS";
        }

        private void OnPowerUpPressed(PowerUpDef def)
        {
            FlutterBridge.Instance?.SendEvent("powerup_purchase_requested", "", "",
                "{\"powerupId\":\"" + def.id + "\",\"price\":" + def.ad_coins_price + "}");
        }

        // ---- Events --------------------------------------------------------------
        private void OnCountdown(float v)
        {
            _countdownText.text = v <= 0f ? "GO!" : Mathf.RoundToInt(v).ToString();
            if (v <= 0f) Invoke(nameof(ClearCountdown), 0.8f);
            AudioManager.Instance?.PlayCountdown();
        }

        private void ClearCountdown() { _countdownText.text = ""; }

        private void OnStateChanged(RaceState s)
        {
            _pausePanel.SetActive(s == RaceState.Paused);
            if (s == RaceState.Racing) _countdownText.text = "";
        }

        private void OnPausePressed() { RaceManager.Instance?.PauseRace(); }
        private void OnResumePressed() { RaceManager.Instance?.ResumeRace(); }

        private void OnQuitPressed()
        {
            RaceManager.Instance?.QuitRace();
            TJRacingBootstrap.Instance?.LoadMenuScene();
        }

        private void OnRecalibratePressed()
        {
            TiltSteering.RequestRecalibrate();
            ShowMessage("TILT RE-CALIBRATED");
        }

        private void OnCycleControlMode()
        {
            if (FlutterBridge.Instance == null) return;
            string next = FlutterBridge.Instance.ControlMode == "both" ? "tilt"
                        : FlutterBridge.Instance.ControlMode == "tilt" ? "buttons" : "both";
            FlutterBridge.Instance.ApplyControlMode(next);
            ShowMessage("CONTROL: " + next.ToUpper());
        }

        public void ShowMessage(string msg)
        {
            _messageText.text = msg;
            _messageTimer = 2.5f;
        }

        public void RefreshCoinsPill()
        {
            if (FlutterBridge.Instance == null) return;
            string rs = string.IsNullOrEmpty(FlutterBridge.Instance.RsValue) ? "" : " • " + FlutterBridge.Instance.RsValue;
            _coinsText.text = "● " + FlutterBridge.Instance.LevelCoins.ToString("N0") + rs;
        }

        private static string FormatTime(float secs)
        {
            int m = Mathf.FloorToInt(secs / 60f);
            int s = Mathf.FloorToInt(secs % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return n + "th";
            }
        }
    }
}
