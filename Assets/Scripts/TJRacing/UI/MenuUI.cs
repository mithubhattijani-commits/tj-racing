// TJ Racing 3D — main menu (mockup): TJ RACING title, 3D STREET RACING subtitle,
// "Night city • Nitro • Drift • AI opponents", coins pill + LEVEL pill, selected
// car name, green RACE button, "Race vs 5 AI opponents • 3 laps • Win = +10 coins"
// (from live config — the mockup's "+5" was stale; plan §7 says win = 10),
// HOW TO PLAY + SETTINGS, car carousel with direct unlocks only (research §A.9:
// fixed Ad Coin price, the player gets EXACTLY what they paid for — no random
// rewards). Locked luxury cars show a gold LUXURY tag + progress "340 / 1200".
// Unlocking itself happens in FLUTTER (it owns the Ad Coin balance); Unity only
// displays the state Flutter sends via SetUnlockedCars / SetBalances.
// SPLASH (user order 19 Nov): when the game opens, a full-screen splash shows
// the user's photo (Assets/Resources/jaani-photo.png, circular, gold ring),
// "TJ RACING" + "Presented by Jaani", once per session, ~2.5s with a fade,
// tap anywhere to skip. Title-only if the photo is missing — never a crash.
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

        // Splash (once per session, when the game opens): photo + title +
        // "Presented by Jaani". Auto-dismisses after SplashHoldSecs with a
        // fade; tap anywhere skips immediately. Title-only if the photo is
        // missing — the splash never blocks the menu and never crashes.
        private GameObject _splashPanel;
        private CanvasGroup _splashGroup;
        private float _splashTimer;
        private bool _splashFading;
        private static bool _splashShown;
        private const float SplashHoldSecs = 2.5f;
        private const float SplashFadeSecs = 0.5f;
        private const string SplashPhotoPath = "jaani-photo";

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

            // Splash: hold, then fade out. Tap dismisses instantly (DismissSplash).
            if (_splashPanel != null && _splashPanel.activeSelf)
            {
                _splashTimer += Time.deltaTime;
                if (!_splashFading && _splashTimer >= SplashHoldSecs)
                {
                    _splashFading = true;
                }
                if (_splashFading)
                {
                    _splashGroup.alpha -= Time.deltaTime / SplashFadeSecs;
                    if (_splashGroup.alpha <= 0f) DismissSplash();
                }
            }
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
            var coinsPanel = UIBuilder.Panel(_canvas.transform, "CoinsPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60, -590), new Vector2(500, -500), UIBuilder.Panel);
            _coinsText = UIBuilder.Label(coinsPanel, "CoinsText", "● 0", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(_coinsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
            var levelPanel = UIBuilder.Panel(_canvas.transform, "LevelPill",
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

            // Circular photo (gold disc behind = border ring). The photo is
            // 200x200, so it stays modest (360px) — never blown up full-screen.
            // If the texture is missing, the splash is title-only: no crash,
            // no hang, the menu is still reachable.
            var photo = Resources.Load<Texture2D>(SplashPhotoPath);
            if (photo != null)
            {
                var disc = MakeCircleTexture(256);
                var discSprite = Sprite.Create(disc, new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f));

                var ring = new GameObject("PhotoRing");
                ring.transform.SetParent(_splashPanel.transform, false);
                var ringRt = ring.AddComponent<RectTransform>();
                UIBuilder.Place(ringRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-188, 112), new Vector2(188, 488));
                var ringImg = ring.AddComponent<Image>();
                ringImg.sprite = discSprite;
                ringImg.color = UIBuilder.Gold;
                ringImg.raycastTarget = false;

                var maskGo = new GameObject("PhotoMask");
                maskGo.transform.SetParent(_splashPanel.transform, false);
                var maskRt = maskGo.AddComponent<RectTransform>();
                UIBuilder.Place(maskRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180, 120), new Vector2(180, 480));
                var maskImg = maskGo.AddComponent<Image>();
                maskImg.sprite = discSprite;
                maskImg.color = Color.white;
                maskImg.raycastTarget = false;
                maskGo.AddComponent<Mask>();

                var rawGo = new GameObject("Photo");
                rawGo.transform.SetParent(maskGo.transform, false);
                var rawRt = rawGo.AddComponent<RectTransform>();
                UIBuilder.Place(rawRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = rawGo.AddComponent<RawImage>();
                raw.texture = photo;
                raw.raycastTarget = false;
            }

            // Title (same white + pink shadow as the menu title) + presenter line.
            var titleShadow = UIBuilder.Label(_splashPanel.transform, "TitleShadow", "TJ RACING", 110, UIBuilder.Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(titleShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-514, -46), new Vector2(526, 114));
            var title = UIBuilder.Label(_splashPanel.transform, "Title", "TJ RACING", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -40), new Vector2(520, 120));
            var by = UIBuilder.Label(_splashPanel.transform, "PresentedBy", "Presented by Jaani", 44, UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(by.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -120), new Vector2(520, -40));
            var sub = UIBuilder.Label(_splashPanel.transform, "Subtitle", "3 D   S T R E E T   R A C I N G", 40, UIBuilder.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -190), new Vector2(520, -120));

            // Invisible full-screen tap catcher, topmost: any tap skips at once.
            var tapGo = new GameObject("TapToSkip");
            tapGo.transform.SetParent(_splashPanel.transform, false);
            var tapRt = tapGo.AddComponent<RectTransform>();
            UIBuilder.Place(tapRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0f, 0f, 0f, 0f);
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.onClick.AddListener(DismissSplash);

            _splashTimer = 0f;
            _splashFading = false;
        }

        /// <summary>White disc (transparent corners, 1px soft edge) used as a circular mask sprite.</summary>
        private static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
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
