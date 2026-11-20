// TJ Racing 3D — bootstrap: builds the race scene contents and wires managers.
// The .unity scenes are intentionally minimal (camera, lights, bootstrap objects);
// everything else — track, cars, AI, traffic, HUD — is assembled here at runtime
// so the project stays fully text-based and reproducible.
//
// Race setup:
//   - Player car from Flutter's selected car (default Street Hawk).
//   - 5 AI: 2 slow, 2 medium, 1 fast (fixed skills, research §A.2). Race 1:
//     AI slowed by config.firstRaceAiSlowdownPct (first race must be winnable).
//   - Grid: player starts LAST (row 3) — the win is earned, not given.
//   - Traffic: lap 2 bridge, lap 3 hard-level road (TrafficManager).
//   - Camera, HUD, results, ads, audio, tilt all wired here.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TJRacing
{
    public class TJRacingBootstrap : MonoBehaviour
    {
        public static TJRacingBootstrap Instance { get; private set; }

        public const string MenuScene = "MainMenu";
        public const string RaceScene = "Race_NightCity";

        [Header("Scene refs (wired in the .unity scenes)")]
        public Camera mainCamera;
        public Light sunLight;

        private TrackBuilder _trackBuilder;
        private RaceManager _raceManager;
        private RacePositionTracker _positionTracker;
        private TrafficManager _trafficManager;
        private readonly List<GameObject> _spawnedCars = new List<GameObject>();
        private bool _isFirstRace = true;

        // Race-scene preload (v1.0.32): the race scene is loaded in the
        // background with allowSceneActivation=false while the menu /
        // splash shows, so the splash can show REAL loading 1-100%.
        // The scene itself is minimal (camera/lights/bootstrap); the
        // heavy build (track, cars, AI) runs in SetupRaceScene on
        // activation, so progress maps the scene load and 100% means
        // ready to activate. The race can never be blocked by the
        // preload: ActivateRaceScene falls back to a synchronous load.
        private AsyncOperation _raceLoadOp;
        private AsyncOperation _raceActivatingOp;
        private float _raceLoadStartTime;
        private bool _raceLoadFailed;
        // Guards the race-scene setup coroutine against a double start
        // (sceneLoaded + Start() fallback firing for the same scene).
        private bool _raceSetupRunning;
        // v1.0.33: 8 s, not 30 — a user staring at 1% must get a
        // retry path (START → sync load) in seconds, not half a minute.
        private const float RaceLoadStallSecs = 8f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Core singletons that live across both scenes.
            EnsureSingleton<FlutterBridge>("FlutterBridge");
            EnsureSingleton<UnityAdsManager>("UnityAdsManager");
            EnsureSingleton<AudioManager>("AudioManager");
            EnsureSingleton<TiltSteering>("TiltSteering");
            EnsureSingleton<TouchControls>("TouchControls");
            EnsureEventSystem();

            // Gap fix (19 Nov 2026): Flutter's StartRace / QuitToMenu messages
            // fire OnStartRaceRequested / OnQuitToMenuRequested — nothing
            // subscribed before, so both did nothing inside Unity.
            // v1.0.32: race starts go through ActivateRaceScene (uses the
            // preloaded scene when ready, sync load otherwise).
            FlutterBridge.Instance.OnStartRaceRequested += ActivateRaceScene;
            FlutterBridge.Instance.OnQuitToMenuRequested += LoadMenuScene;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (FlutterBridge.Instance != null)
            {
                FlutterBridge.Instance.OnStartRaceRequested -= ActivateRaceScene;
                FlutterBridge.Instance.OnQuitToMenuRequested -= LoadMenuScene;
            }
        }

        /// <summary>
        /// Fallback for the case where the bootstrap awakes into a scene
        /// that is ALREADY loaded, so SceneManager.sceneLoaded never fires
        /// for it (Editor playmode with scene reload disabled; any future
        /// flow that adds the bootstrap to a live scene). On a phone the
        /// normal flow fires sceneLoaded before Start, so the setup runs
        /// via the event and this is a no-op (both setup methods guard
        /// against a double run).
        /// </summary>
        void Start()
        {
            var active = SceneManager.GetActiveScene();
            if (active.name == MenuScene) SetupMenuScene();
            else if (active.name == RaceScene) StartCoroutine(SetupRaceSceneAsync());
        }

        /// <summary>
        /// UI buttons need an EventSystem. The .unity scenes deliberately do NOT
        /// contain one (its YAML references built-in scripts by GUID and is fragile
        /// to hand-author); create it in code instead.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static T EnsureSingleton<T>(string name) where T : Component
        {
            var existing = FindObjectOfType<T>();
            if (existing != null) return existing;
            var go = new GameObject(name);
            DontDestroyOnLoad(go);
            return go.AddComponent<T>();
        }

        public void LoadMenuScene()
        {
            SceneManager.LoadScene(MenuScene);
        }

        public void LoadRaceScene()
        {
            SceneManager.LoadScene(RaceScene);
        }

        /// <summary>Starts the background race-scene preload (once per menu visit).</summary>
        public void PreloadRaceScene()
        {
            if (_raceLoadOp != null || _raceLoadFailed) return;
            _raceLoadOp = SceneManager.LoadSceneAsync(RaceScene, LoadSceneMode.Single);
            _raceLoadOp.allowSceneActivation = false;
            _raceLoadStartTime = Time.unscaledTime;
        }

        /// <summary>Real load progress 0..1 (AsyncOperation caps at 0.9 until activation).</summary>
        public float RaceLoadProgress01
        {
            get
            {
                if (_raceLoadOp == null) return 0f;
                return Mathf.Clamp01(_raceLoadOp.progress / 0.9f);
            }
        }

        public bool IsRaceReady => _raceLoadOp != null && _raceLoadOp.progress >= 0.9f;

        public bool IsRaceLoading => _raceLoadOp != null && !IsRaceReady
            && (Time.unscaledTime - _raceLoadStartTime) <= RaceLoadStallSecs;

        /// <summary>Preload running but past the stall window without reaching 90%.</summary>
        public bool IsRaceLoadStalled => _raceLoadOp != null && !IsRaceReady
            && (Time.unscaledTime - _raceLoadStartTime) > RaceLoadStallSecs;

        /// <summary>A preload failed (or activation found nothing to activate).</summary>
        public bool HasRaceLoadFailed => _raceLoadFailed;

        /// <summary>True while a preload operation exists (running or ready).</summary>
        public bool IsPreloadRunning => _raceLoadOp != null;

        /// <summary>
        /// Activates the preloaded race scene; falls back to a
        /// synchronous load if nothing was preloaded (or it stalled)
        /// — the race can never be blocked by the preload. The
        /// _raceActivatingOp guard stops a double start: the splash
        /// START tap fires BOTH the raw-input handler (pointer down)
        /// and the Button onClick (pointer up), and with async
        /// activation the first call no longer destroys the menu
        /// immediately, so the second call must be a no-op while the
        /// first activation is in flight.
        /// </summary>
        public void ActivateRaceScene()
        {
            if (_raceActivatingOp != null && !_raceActivatingOp.isDone) return;
            if (IsRaceReady)
            {
                var op = _raceLoadOp;
                _raceLoadOp = null;
                _raceActivatingOp = op;
                op.allowSceneActivation = true;
                return;
            }
            _raceLoadFailed = true;
            _raceLoadOp = null;
            _raceActivatingOp = null;
            LoadRaceScene();
        }

        public void RestartRace()
        {
            SceneManager.LoadScene(RaceScene);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _raceActivatingOp = null;
            CleanupSpawnedCars();
            if (scene.name == MenuScene) SetupMenuScene();
            else if (scene.name == RaceScene) StartCoroutine(SetupRaceSceneAsync());
        }

        private void SetupMenuScene()
        {
            // Idempotency: Start() runs after sceneLoaded in the normal
            // flow, so the menu may already be set up — skip the rebuild
            // (and, critically, do NOT reset a preload that is running).
            if (GameObject.Find("MenuUI") != null) return;
            // Fresh preload state per menu visit (a stalled/failed
            // preload from the last visit must not poison this one);
            // MenuUI.Start restarts the preload one frame later.
            _raceLoadOp = null;
            _raceLoadFailed = false;
            // Menu camera: slow orbit around the hero car (MenuUI spawns it).
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(0f, 2.6f, -8.5f);
                mainCamera.transform.LookAt(new Vector3(0f, 1f, 0f));
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0.05f, 0.05f, 0.16f);
                // AudioListener: the menu scene has none of its own, so
                // WITHOUT this the background music + UI clicks are
                // SILENT in the menu (Unity only plays audio through an
                // active listener) and the log fills with "no audio
                // listeners" warnings every frame. The race scene adds
                // its own listener to the race camera; this one is
                // destroyed with the menu scene, so the two never coexist.
                if (mainCamera.GetComponent<AudioListener>() == null)
                    mainCamera.gameObject.AddComponent<AudioListener>();
            }
            var menuGo = new GameObject("MenuUI");
            menuGo.AddComponent<MenuUI>();
            // Menu backdrop: a few skyline silhouettes behind the hero car.
            var backdrop = new GameObject("MenuBackdrop");
            var rnd = new System.Random(42);
            for (int i = 0; i < 12; i++)
            {
                float x = -26f + i * 4.8f;
                float h = 8f + (float)rnd.NextDouble() * 16f;
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.transform.SetParent(backdrop.transform, false);
                b.transform.position = new Vector3(x, h / 2f - 1f, 12f + (float)rnd.NextDouble() * 6f);
                b.transform.localScale = new Vector3(4f, h, 4f);
                b.GetComponent<Renderer>().material.color = new Color(0.03f, 0.03f, 0.08f);
            }
        }

        /// <summary>
        /// Builds the race scene in phases with a visible "BUILDING
        /// RACE…" overlay and a frame yield between phases. The track
        /// (heightmap meshes, ~1500 objects), 6 cars, AI and HUD are
        /// built synchronously; on a slow phone each phase blocks the
        /// main thread for ~0.5-2 s, which looks like a freeze right
        /// after START if nothing renders. The overlay + yields make
        /// the wait visible progress instead. Aborts cleanly if the
        /// player quits to the menu mid-build (the coroutine lives on
        /// the DontDestroyOnLoad bootstrap, so it must not keep
        /// building a race in the menu scene).
        /// </summary>
        private IEnumerator SetupRaceSceneAsync()
        {
            // Re-entrancy guard (sceneLoaded + Start() fallback).
            if (_raceSetupRunning) yield break;
            _raceSetupRunning = true;
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : RacingConfig.Defaults();
            int level = FlutterBridge.Instance != null ? FlutterBridge.Instance.Level : 1;

            var overlayCanvas = UIBuilder.MakeCanvas(gameObject, "BuildOverlayCanvas");
            var overlayBg = UIBuilder.MakePanel(overlayCanvas.transform, "Bg",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.13f, 1f));
            var overlayText = UIBuilder.Label(overlayBg, "Text", "BUILDING RACE…", 56, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Place(overlayText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -60), new Vector2(520, 60));
            yield return null; // let the overlay draw once before the heavy build

            // Night sky + fog (flat ambient: no skybox asset in this text-only project).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.12f);
            RenderSettings.fogDensity = 0.012f;
            if (mainCamera == null) mainCamera = Camera.main;

            // Track.
            overlayText.text = "Building track…";
            var trackGo = new GameObject("Track");
            _trackBuilder = trackGo.AddComponent<TrackBuilder>();
            yield return null;
            if (SceneManager.GetActiveScene().name != RaceScene) { Destroy(overlayCanvas.gameObject); _raceSetupRunning = false; yield break; }

            // Race managers.
            overlayText.text = "Placing cars…";
            var raceGo = new GameObject("RaceManager");
            _raceManager = raceGo.AddComponent<RaceManager>();
            var posGo = new GameObject("RacePositionTracker");
            _positionTracker = posGo.AddComponent<RacePositionTracker>();
            var trafficGo = new GameObject("TrafficManager");
            _trafficManager = trafficGo.AddComponent<TrafficManager>();
            _trafficManager.Init(_trackBuilder.Spline, level, cfg.hardLevelThreshold);
            _raceManager.trafficManager = _trafficManager;
            _raceManager.positionTracker = _positionTracker;

            // Player car (selected in menu / by Flutter).
            string carId = FlutterBridge.Instance != null ? FlutterBridge.Instance.SelectedCarId : "street_hawk";
            CarDef playerDef = cfg.GetCar(carId) ?? CarCatalogue.DefaultCars()[0];
            var player = CarFactory.Build(playerDef, true);
            player.root.transform.position = _trackBuilder.GridSlots[5].position;
            player.root.transform.rotation = _trackBuilder.GridSlots[5].rotation;
            player.root.AddComponent<PlayerDriver>();
            var playerTracker = player.root.AddComponent<LapTracker>();
            playerTracker.IsPlayer = true;
            playerTracker.Init(_trackBuilder.Spline, _trackBuilder.Checkpoints.Count, cfg.race.laps);
            _positionTracker.Register(playerTracker);
            _raceManager.playerLapTracker = playerTracker;
            _raceManager.playerNitro = player.controller.Nitro;
            _spawnedCars.Add(player.root);

            // AI cars: 2 slow, 2 medium, 1 fast — fixed skills, no rubber-banding.
            AISkill[] skills = { AISkill.Slow, AISkill.Slow, AISkill.Medium, AISkill.Medium, AISkill.Fast };
            var aiCars = new List<CarDef>(cfg.cars);
            var rnd = new System.Random();
            for (int i = 0; i < 5; i++)
            {
                CarDef def = aiCars[rnd.Next(aiCars.Count)];
                var ai = CarFactory.Build(def, false);
                ai.root.transform.position = _trackBuilder.GridSlots[i].position;
                ai.root.transform.rotation = _trackBuilder.GridSlots[i].rotation;
                var aiDriver = ai.root.AddComponent<AICarController>();
                aiDriver.Init(_trackBuilder.Spline, skills[i]);
                if (_isFirstRace)
                {
                    // First race must be winnable: AI top speed cut by the configured %.
                    ai.controller.topSpeedKmh *= 1f - cfg.firstRaceAiSlowdownPct / 100f;
                }
                var aiTracker = ai.root.AddComponent<LapTracker>();
                aiTracker.IsPlayer = false;
                aiTracker.Init(_trackBuilder.Spline, _trackBuilder.Checkpoints.Count, cfg.race.laps);
                _positionTracker.Register(aiTracker);
                _spawnedCars.Add(ai.root);
            }
            yield return null;
            if (SceneManager.GetActiveScene().name != RaceScene) { Destroy(overlayCanvas.gameObject); _raceSetupRunning = false; yield break; }

            // Chase camera.
            overlayText.text = "Starting race…";
            var camGo = mainCamera != null ? mainCamera.gameObject : new GameObject("Main Camera");
            var raceCam = camGo.GetComponent<RaceCamera>() ?? camGo.AddComponent<RaceCamera>();
            raceCam.target = player.root.transform;
            if (camGo.GetComponent<Camera>() == null) camGo.AddComponent<Camera>();
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();

            // HUD + results.
            var hudGo = new GameObject("RaceHUD");
            hudGo.AddComponent<RaceHUD>();
            var resultsGo = new GameObject("ResultsUI");
            resultsGo.AddComponent<ResultsUI>();

            // Banner stays visible (passive top strip).
            UnityAdsManager.Instance?.ShowBanner();

            // Build done — overlay off, GO.
            Destroy(overlayCanvas.gameObject);
            _raceSetupRunning = false;
            _raceManager.StartRace(cfg.race.laps, _isFirstRace);
            _isFirstRace = false;
        }

        private void CleanupSpawnedCars()
        {
            foreach (var go in _spawnedCars) if (go != null) Destroy(go);
            _spawnedCars.Clear();
            if (_trafficManager != null) _trafficManager.ResetRace();
            if (_positionTracker != null) _positionTracker.Clear();
        }
    }
}
