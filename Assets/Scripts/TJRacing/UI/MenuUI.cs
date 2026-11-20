// TJ Racing 3D — main menu (mockup): TJ RACING title, 3D STREET RACING subtitle,
// "Night city • Nitro • Drift • AI opponents", coins pill + LEVEL pill, selected
// car name, green RACE button, "Race vs 5 AI opponents • 3 laps • Win = +10 coins"
// (from live config — the mockup's "+5" was stale; plan §7 says win = 10),
// HOW TO PLAY + SETTINGS, car carousel with direct unlocks only (research §A.9:
// fixed Ad Coin price, the player gets EXACTLY what they paid for — no random
// rewards). Locked luxury cars show a gold LUXURY tag + progress "340 / 1200".
// Unlocking itself happens in FLUTTER (it owns the Ad Coin balance); Unity only
// displays the state Flutter sends via SetUnlockedCars / SetBalances.
// SPLASH (user order 19 Nov, corrected same day — "Jo bheji ha Wohi lagani
// thi / Portrait"): when the game opens, a full-screen splash shows the
// user's own racing poster WHOLE (Assets/Resources/jaani-photo.jpg,
// contain-fit) — the poster exactly as sent, NOT a cropped face in a circle.
// v1.0.32 (20 Nov): the poster is now the LANDSCAPE 1600x900 image (the
// game runs landscape) and its painted yellow START button is a REAL
// button: tapping it starts the race immediately (same OnRacePressed as
// the menu RACE button). Any other tap dismisses to the menu. The timer
// runs on UNSCALED time with a 10 s failsafe, and taps are read as raw
// input in Update — the 19 Nov build's splash could stick forever if
// Unity's UI froze (flutter_embed_unity's own UnityPlayerSingleton
// documents the VISIBLE→GONE→VISIBLE freeze; the play screen rebuilds
// right after mount, which is exactly that trigger — Flutter now also
// pause+resumes Unity after the rebuild). Title-only if the poster is
// missing — never a crash.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TJRacing
{
    public class MenuUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _coinsText;
        private Text _levelText;
        private Text _carNameText;
        private Text _carPriceText;
        private Text _raceSubText;
        private Text _sensitivityValueText;
        private GameObject _settingsPanel;
        private GameObject _howToPanel;
        private int _selectedIndex;
        private List<CarDef> _cars = new List<CarDef>();

        // The menu shows a slowly rotating hero car (procedural, same factory).
        private GameObject _heroCar;
        private float _heroAngle;

        // Splash (once per session, when the game opens): the user's whole
        // racing poster, full-screen, contain-fit. Auto-dismisses after
        // SplashHoldSecs with a fade; tapping the poster's START button
        // starts the race, any other tap dismisses to the menu. Runs on
        // UNSCALED time with a 10 s failsafe, so nothing can stick it on
        // screen. Title-only if the poster is missing — the splash never
        // blocks the menu and never crashes.
        private GameObject _splashPanel;
        private CanvasGroup _splashGroup;
        private RectTransform _splashStartBtnRt;
        private float _splashTimer;
        private bool _splashFading;
        private static bool _splashShown;
        private const float SplashHoldSecs = 2.5f;
        private const float SplashFadeSecs = 0.5f;
        private const float SplashFailsafeSecs = 10f;
        private const float SplashInputGraceSecs = 0.25f;
        private const string SplashPhotoPath = "jaani-photo";

        // START button on the landscape poster (measured from the
        // 1600x900 image, fractions of the poster): yellow START
        // bottom-right, x 0.656-0.989, y-from-top 0.711-0.931.
        // Unity anchors are bottom-up, so y 0.069-0.289; inset
        // slightly to stay on the solid yellow body.
        private static readonly Vector2 SplashStartAnchorMin = new Vector2(0.67f, 0.08f);
        private static readonly Vector2 SplashStartAnchorMax = new Vector2(0.98f, 0.28f);

        void Awake()
        {
            _cars = FlutterBridge.Instance != null
                ? FlutterBridge.Instance.Config.cars
                : CarCatalogue.DefaultCars();
            _selectedIndex = 0;
            BuildUI();
            BuildSplash();
            RefreshCarCard();
        }

        void Start()
        {
            if (FlutterBridge.Instance != null)
            {
                FlutterBridge.Instance.OnConfigApplied += _ => { _cars = FlutterBridge.Instance.Config.cars; RefreshCarCard(); };
                FlutterBridge.Instance.OnBalancesChanged += () => { RefreshCoinsPill(); RefreshCarCard(); };
                FlutterBridge.Instance.OnCarSelected += id =>
                {
                    for (int i = 0; i < _cars.Count; i++)
                        if (_cars[i].id == id) _selectedIndex = i;
                    RefreshCarCard();
                };
            }
            RefreshCoinsPill();
            UnityAdsManager.Instance?.ShowBanner();
        }

        void Update()
        {
            // Hero car turntable.
            if (_heroCar != null)
            {
                _heroAngle += Time.deltaTime * 0.35f;
                _heroCar.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(_heroAngle) * 24f, 0f);
            }

            // Splash: hold, then fade out. Runs on UNSCALED time so a
            // paused timeScale can never stick the splash on screen, and
            // after SplashFailsafeSecs it is force-dismissed no matter
            // what. Taps are raw input here (not Button-only): a tap on
            // the poster's START button starts the race immediately
            // (same path as the menu RACE button); any other tap
            // dismisses to the menu. A Button-only dismiss dies
            // silently if the EventSystem/raycast ever breaks — raw
            // input has no such failure mode.
            if (_splashPanel != null && _splashPanel.activeSelf)
            {
                _splashTimer += Time.unscaledDeltaTime;
                if (_splashTimer >= SplashFailsafeSecs)
                {
                    DismissSplash();
                    return;
                }
                if (_splashTimer >= SplashInputGraceSecs && SplashTapDown(out Vector2 tapPos))
                {
                    if (_splashStartBtnRt != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(_splashStartBtnRt, tapPos))
                    {
                        OnRacePressed();
                    }
                    else
                    {
                        DismissSplash();
                    }
                    return;
                }
                if (!_splashFading && _splashTimer >= SplashHoldSecs)
                {
                    _splashFading = true;
                }
                if (_splashFading)
                {
                    _splashGroup.alpha -= Time.unscaledDeltaTime / SplashFadeSecs;
                    if (_splashGroup.alpha <= 0f) DismissSplash();
                }
            }
        }

        // True on the frame a touch/click begins; tapPos is screen space.
        private static bool SplashTapDown(out Vector2 tapPos)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                tapPos = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                tapPos = Input.mousePosition;
                return true;
            }
            tapPos = Vector2.zero;
            return false;
        }

        private void BuildUI()
        {
            _canvas = UIBuilder.MakeCanvas(gameObject, "MenuCanvas");

            var title = UIBuilder.Label(_canvas.transform, "Title", "TJ RACING", 130, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-520, -330), new Vector2(520, -160));
            // Pink outline effect: a pink shadow copy behind the white title.
            var titleShadow = UIBuilder.Label(_canvas.transform, "TitleShadow", "TJ RACING", 130, UIBuilder.Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
            titleShadow.rectTransform.SetSiblingIndex(0);
            UIBuilder.Place(titleShadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-514, -336), new Vector2(526, -166));

            var sub = UIBuilder.Label(_canvas.transform, "Subtitle", "3 D   S T R E E T   R A C I N G", 52, UIBuilder.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-520, -410), new Vector2(520, -330));
            var feats = UIBuilder.Label(_canvas.transform, "Features", "Night city  •  Nitro  •  Drift  •  AI opponents", 40, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(feats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-520, -470), new Vector2(520, -410));

            // Coins pill (left) + level pill (right).
            var coinsPanel = UIBuilder.MakePanel(_canvas.transform, "CoinsPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60, -590), new Vector2(500, -500), UIBuilder.Panel);
            _coinsText = UIBuilder.Label(coinsPanel, "CoinsText", "● 0", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_coinsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
            var levelPanel = UIBuilder.MakePanel(_canvas.transform, "LevelPill",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-500, -590), new Vector2(-60, -500), UIBuilder.Panel);
            _levelText = UIBuilder.Label(levelPanel, "LevelText", "LEVEL 1", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_levelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));

            // Selected car name + price/progress.
            _carNameText = UIBuilder.Label(_canvas.transform, "CarName", "STREET HAWK • YOUR CAR", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_carNameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -60), new Vector2(520, 0));
            _carPriceText = UIBuilder.Label(_canvas.transform, "CarPrice", "FREE", 40, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_carPriceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -120), new Vector2(520, -60));

            // Car carousel arrows.
            var prevBtn = UIBuilder.MakeButton(_canvas.transform, "PrevCar", "◀", 56, UIBuilder.Panel, () => CycleCar(-1));
            UIBuilder.Place(prevBtn.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30, -70), new Vector2(130, 70));
            var nextBtn = UIBuilder.MakeButton(_canvas.transform, "NextCar", "▶", 56, UIBuilder.Panel, () => CycleCar(1));
            UIBuilder.Place(nextBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-130, -70), new Vector2(-30, 70));

            // RACE button.
            var raceBtn = UIBuilder.MakeButton(_canvas.transform, "RaceBtn", "RACE", 96, UIBuilder.Green, OnRacePressed);
            UIBuilder.Place(raceBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-380, 220), new Vector2(380, 380));
            _raceSubText = UIBuilder.Label(_canvas.transform, "RaceSub", "", 36, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(_raceSubText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-520, 150), new Vector2(520, 220));

            // HOW TO PLAY + SETTINGS.
            var howBtn = UIBuilder.MakeButton(_canvas.transform, "HowBtn", "HOW TO PLAY", 40, UIBuilder.Panel, () => _howToPanel.SetActive(true));
            UIBuilder.Place(howBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-480, 40), new Vector2(-20, 130));
            var setBtn = UIBuilder.MakeButton(_canvas.transform, "SettingsBtn", "SETTINGS", 40, UIBuilder.Panel, () => _settingsPanel.SetActive(true));
            UIBuilder.Place(setBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20, 40), new Vector2(480, 130));

            BuildSettingsPanel();
            BuildHowToPanel();
        }

        private void BuildSplash()
        {
            // Only the first menu load of the session: quitting a race back to
            // the menu must NOT show the splash again.
            if (_splashShown) return;
            _splashShown = true;

            // Built last, so it draws above the menu (and its panels).
            _splashPanel = new GameObject("SplashPanel");
            _splashPanel.transform.SetParent(_canvas.transform, false);
            var rt = _splashPanel.AddComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _splashPanel.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.13f, 1f);
            _splashGroup = _splashPanel.AddComponent<CanvasGroup>();

            // Full-screen poster (the user's own racing poster, exactly as
            // sent — shown WHOLE, contain-fit, NOTHING cut off).
            // AspectRatioFitter in FitInParent mode = contain: the whole
            // image is visible, preserving its aspect, centred on the dark
            // background. The poster is 16:9 landscape and the game runs
            // landscape, so it fills the view near full-bleed (thin bars
            // only if the phone's aspect differs). If the texture is
            // missing, the splash is title-only: no crash, no hang, the
            // menu is still reachable.
            var photo = Resources.Load<Texture2D>(SplashPhotoPath);
            if (photo != null)
            {
                var rawGo = new GameObject("Poster");
                rawGo.transform.SetParent(_splashPanel.transform, false);
                var rawRt = rawGo.AddComponent<RectTransform>();
                UIBuilder.Place(rawRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = rawGo.AddComponent<RawImage>();
                raw.texture = photo;
                raw.raycastTarget = false;
                var fitter = rawGo.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = photo.width / (float)photo.height;

                // REAL START button over the poster's own yellow START
                // (bottom-right). Child of the poster, so it tracks the
                // contain-fit rect exactly on any screen size: the tap
                // zone is always exactly on the painted button. It calls
                // the same OnRacePressed as the menu RACE button (locked
                // car -> Flutter unlock prompt, else load the race scene).
                // The other painted buttons (Daily Rewards / Achievements
                // / Leaderboard / Shop) are NOT built — image only.
                var startGo = new GameObject("SplashStartButton");
                startGo.transform.SetParent(rawGo.transform, false);
                var startRt = startGo.AddComponent<RectTransform>();
                startRt.anchorMin = SplashStartAnchorMin;
                startRt.anchorMax = SplashStartAnchorMax;
                startRt.offsetMin = Vector2.zero;
                startRt.offsetMax = Vector2.zero;
                var startImg = startGo.AddComponent<Image>();
                startImg.color = new Color(0f, 0f, 0f, 0f);
                var startBtn = startGo.AddComponent<Button>();
                startBtn.targetGraphic = startImg;
                startBtn.onClick.AddListener(OnRacePressed);
                _splashStartBtnRt = startRt;
            }
            else
            {
                // Title-only fallback (same white + pink shadow as the menu title).
                var titleShadow = UIBuilder.Label(_splashPanel.transform, "TitleShadow", "TJ RACING", 110, UIBuilder.Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIBuilder.Place(titleShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-514, -46), new Vector2(526, 114));
                var title = UIBuilder.Label(_splashPanel.transform, "Title", "TJ RACING", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -40), new Vector2(520, 120));
                var by = UIBuilder.Label(_splashPanel.transform, "PresentedBy", "Presented by Jaani", 44, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIBuilder.Place(by.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -120), new Vector2(520, -40));
                var sub = UIBuilder.Label(_splashPanel.transform, "Subtitle", "3 D   S T R E E T   R A C I N G", 40, UIBuilder.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIBuilder.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -190), new Vector2(520, -120));
            }

            // No full-screen TapToSkip Button: ALL splash taps are handled
            // in Update() via raw input (press frame), hit-tested against
            // the START button rect — START starts the race, anywhere
            // else dismisses to the menu. A Button-only dismiss was the
            // old path: it dies silently if the EventSystem/raycast ever
            // breaks, and a full-screen catcher would sit on top of the
            // START button and swallow its taps. Raw input has neither
            // failure mode; the START Button above is a second path.

            _splashTimer = 0f;
            _splashFading = false;
        }

        private void DismissSplash()
        {
            if (_splashPanel == null) return;
            _splashPanel.SetActive(false);
            _splashPanel = null;
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(_canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _settingsPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            var title = UIBuilder.Label(_settingsPanel.transform, "Title", "SETTINGS", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(-300, -50), new Vector2(300, 50));
            var sensLabel = UIBuilder.Label(_settingsPanel.transform, "SensLabel", "TILT SENSITIVITY", 40, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(sensLabel.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(-400, -30), new Vector2(400, 30));
            float sens = FlutterBridge.Instance != null ? FlutterBridge.Instance.TiltSensitivity : 1f;
            var slider = UIBuilder.MakeSlider(_settingsPanel.transform, "SensSlider", 0.5f, 2f, sens);
            UIBuilder.Place(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(-380, -25), new Vector2(380, 25));
            _sensitivityValueText = UIBuilder.Label(_settingsPanel.transform, "SensValue", sens.ToString("0.0") + "x", 40, UIBuilder.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_sensitivityValueText.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(-200, -30), new Vector2(200, 30));
            slider.onValueChanged.AddListener(v =>
            {
                _sensitivityValueText.text = v.ToString("0.0") + "x";
                FlutterBridge.Instance?.ApplySensitivity(v);
            });
            var modeLabel = UIBuilder.Label(_settingsPanel.transform, "ModeLabel", "CONTROL: TILT + BUTTONS (change in pause menu)", 32, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(modeLabel.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(-480, -30), new Vector2(480, 30));
            var recalBtn = UIBuilder.MakeButton(_settingsPanel.transform, "RecalBtn", "⟳ RE-CALIBRATE TILT NOW", 36, UIBuilder.Panel, () => TiltSteering.RequestRecalibrate());
            UIBuilder.Place(recalBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), new Vector2(-350, -45), new Vector2(350, 45));
            var muteBtn = UIBuilder.MakeButton(_settingsPanel.transform, "MuteBtn", "SOUND: ON", 36, UIBuilder.Panel, () =>
            {
                bool muted = !(AudioManager.Instance != null && AudioManager.Instance.Muted);
                AudioManager.Instance?.SetMuted(muted);
            });
            UIBuilder.Place(muteBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(-350, -45), new Vector2(350, 45));
            var closeBtn = UIBuilder.MakeButton(_settingsPanel.transform, "CloseBtn", "CLOSE", 44, UIBuilder.Green, () => _settingsPanel.SetActive(false));
            UIBuilder.Place(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(-300, -50), new Vector2(300, 50));
            _settingsPanel.SetActive(false);
        }

        private void BuildHowToPanel()
        {
            _howToPanel = new GameObject("HowToPanel");
            _howToPanel.transform.SetParent(_canvas.transform, false);
            var rt = _howToPanel.AddComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _howToPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            var title = UIBuilder.Label(_howToPanel.transform, "Title", "HOW TO PLAY", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(-300, -50), new Vector2(300, 50));
            string body = "TILT your phone to steer — hold it however feels\n" +
                "natural at the countdown, that IS your zero.\n\n" +
                "◀ ▶ buttons steer too. BRAKE slows you.\n" +
                "Hold NITRO to boost — DRIFTING charges it.\n" +
                "Double-tap NITRO for a shockwave boost.\n\n" +
                "Race vs 5 AI, 3 laps. Win = Level Coins.\n" +
                "No fuel, no timers, no repair bills — just race.";
            var text = UIBuilder.Label(_howToPanel.transform, "Body", body, 38, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480, -300), new Vector2(480, 300));
            var closeBtn = UIBuilder.MakeButton(_howToPanel.transform, "CloseBtn", "GOT IT", 44, UIBuilder.Green, () => _howToPanel.SetActive(false));
            UIBuilder.Place(closeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(-300, -50), new Vector2(300, 50));
            _howToPanel.SetActive(false);
        }

        private void CycleCar(int dir)
        {
            if (_cars.Count == 0) return;
            _selectedIndex = ((_selectedIndex + dir) % _cars.Count + _cars.Count) % _cars.Count;
            RefreshCarCard();
            FlutterBridge.Instance?.ApplySelectedCar(_cars[_selectedIndex].id);
        }

        private void RefreshCarCard()
        {
            if (_cars.Count == 0) return;
            var car = _cars[_selectedIndex];
            bool unlocked = FlutterBridge.Instance == null
                || FlutterBridge.Instance.UnlockedCarIds.Contains(car.id);
            string luxury = car.@class == "luxury" ? "  ★ LUXURY" : "";
            _carNameText.text = car.name.ToUpper() + (unlocked ? " • YOUR CAR" : " • LOCKED") + luxury;
            if (unlocked)
            {
                _carPriceText.text = car.ad_coins_price == 0 ? "FREE" : "UNLOCKED";
            }
            else
            {
                // Progress toward the unlock (research §A.5: show "340 / 1200").
                int have = FlutterBridge.Instance != null ? FlutterBridge.Instance.AdCoins : 0;
                _carPriceText.text = have + " / " + car.ad_coins_price + " Ad Coins — unlock in TJ Earn";
            }
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : RacingConfig.Defaults();
            _raceSubText.text = "Race vs " + cfg.race.ai_opponents + " AI opponents  •  " + cfg.race.laps
                + " laps  •  Win = +" + cfg.level_coins.win + " coins";
            SpawnHeroCar(car);
        }

        private void SpawnHeroCar(CarDef car)
        {
            if (_heroCar != null) Destroy(_heroCar);
            var built = CarFactory.Build(car, true);
            _heroCar = built.root;
            _heroCar.transform.position = new Vector3(0f, 0f, 0f);
            _heroCar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            // Hero car is a display model: no physics while in the menu.
            Destroy(_heroCar.GetComponent<Rigidbody>());
            Destroy(built.controller);
            _heroAngle = 0f;
        }

        private void RefreshCoinsPill()
        {
            if (FlutterBridge.Instance == null) return;
            string rs = string.IsNullOrEmpty(FlutterBridge.Instance.RsValue) ? "" : " • " + FlutterBridge.Instance.RsValue;
            _coinsText.text = "● " + FlutterBridge.Instance.LevelCoins.ToString("N0") + rs;
            _levelText.text = "LEVEL " + FlutterBridge.Instance.Level;
        }

        private void OnRacePressed()
        {
            var car = _cars.Count > 0 ? _cars[_selectedIndex] : null;
            if (car != null && FlutterBridge.Instance != null
                && !FlutterBridge.Instance.UnlockedCarIds.Contains(car.id))
            {
                // Locked car: Flutter shows the unlock / rewarded-ad prompt.
                FlutterBridge.Instance.SendEvent("car_locked_tapped", "", "",
                    "{\"carId\":\"" + car.id + "\",\"price\":" + car.ad_coins_price + "}");
                return;
            }
            TJRacingBootstrap.Instance?.LoadRaceScene();
        }
    }
}
