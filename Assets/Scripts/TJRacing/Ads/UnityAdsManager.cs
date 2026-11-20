// TJ Racing 3D — Unity Ads (Game ID 800417967, same placements as Arrow Puzzle).
// Rules (plan §8 + research §A.6):
//  - Banner: passive top strip, never covers controls or buttons (HUD top padding
//    reserves the strip; banner is hidden during the race countdown if it would
//    overlap — it never does: the HUD top row sits BELOW the banner).
//  - Interstitial: every 3rd race, max 1 per 3 min, NEVER right after a win —
//    only after the player taps Continue on the results screen. Never two ads
//    back-to-back (90 s minimum gap from ANY ad).
//  - Rewarded: opt-in ONLY (garage / "need Ad Coins?" prompt). Unity NEVER credits
//    coins — on completion it sends a "rewarded_completed" event to Flutter with
//    the requestId; Flutter credits Ad Coins server-side.
//  - No ad at game start.
using System;
using UnityEngine;
#if UNITY_ADS
using UnityEngine.Advertisements;
#endif

namespace TJRacing
{
    public class UnityAdsManager : MonoBehaviour
#if UNITY_ADS
        , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
    {
        public static UnityAdsManager Instance { get; private set; }

        public const string GameId = "800417967";
        public const string BannerPlacement = "BP_Banner_Android";
        public const string InterstitialPlacement = "BP_Interstitial_Android";
        public const string RewardedPlacement = "BP_Rewarded_Android";

        private bool _initialized;
        private int _racesSinceInterstitial;
        private DateTime _lastAdShown = DateTime.MinValue;
        private DateTime _lastInterstitialShown = DateTime.MinValue;
        private string _pendingRewardedRequestId = "";
        private bool _interstitialLoaded;
        private bool _rewardedLoaded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // Gap fix (19 Nov 2026): Flutter's ShowRewardedAd message fires
            // OnRewardedAdRequested — nothing subscribed to it before, so Watch Ad
            // in the unlock dialog did nothing. Subscribe here (Start runs after
            // every Awake, so FlutterBridge is guaranteed to exist).
            if (FlutterBridge.Instance != null)
                FlutterBridge.Instance.OnRewardedAdRequested += ShowRewarded;
#if UNITY_ADS
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : null;
            if (cfg != null && !cfg.ads.banner_enabled && !cfg.ads.interstitial_enabled && !cfg.ads.rewarded_enabled)
            {
                Debug.Log("[TJ Racing] All ads disabled by remote config.");
                return;
            }
            Advertisement.Initialize(GameId, false, this);
#else
            Debug.LogWarning("[TJ Racing] Unity Ads package not present (UNITY_ADS undefined) — ads disabled. Add com.unity.ads to Packages/manifest.json.");
#endif
        }

        void OnDestroy()
        {
            if (FlutterBridge.Instance != null)
                FlutterBridge.Instance.OnRewardedAdRequested -= ShowRewarded;
        }

        // ---- Banner ------------------------------------------------------------
        public void ShowBanner()
        {
#if UNITY_ADS
            if (!_initialized) return;
            if (FlutterBridge.Instance != null && !FlutterBridge.Instance.Config.ads.banner_enabled) return;
            Advertisement.Banner.SetPosition(BannerPosition.TOP_CENTER);
            Advertisement.Banner.Show(BannerPlacement);
#endif
        }

        public void HideBanner()
        {
#if UNITY_ADS
            if (_initialized) Advertisement.Banner.Hide();
#endif
        }

        // ---- Race counting / interstitial eligibility ---------------------------
        public void NotifyRaceFinished(bool playerWon)
        {
            _racesSinceInterstitial++;
            // The win itself NEVER triggers an ad. ResultsUI decides after Continue.
        }

        /// <summary>Called ONLY from ResultsUI after the player taps Continue.</summary>
        public void MaybeShowInterstitialAfterContinue()
        {
#if UNITY_ADS
            if (!_initialized) return;
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : RacingConfig.Defaults();
            if (!cfg.ads.interstitial_enabled) return;
            if (_racesSinceInterstitial < cfg.ads.interstitial_every_n_races) return;
            if ((DateTime.UtcNow - _lastInterstitialShown).TotalSeconds < cfg.ads.interstitial_min_interval_secs) return;
            if ((DateTime.UtcNow - _lastAdShown).TotalSeconds < cfg.adsMinGapSecs) return; // never back-to-back
            if (!_interstitialLoaded)
            {
                Advertisement.Load(InterstitialPlacement, this);
                return;
            }
            _racesSinceInterstitial = 0;
            Advertisement.Show(InterstitialPlacement, this);
#endif
        }

        // ---- Rewarded (opt-in) ---------------------------------------------------
        public void ShowRewarded(string requestId)
        {
#if UNITY_ADS
            var cfg = FlutterBridge.Instance != null ? FlutterBridge.Instance.Config : RacingConfig.Defaults();
            if (!cfg.ads.rewarded_enabled)
            {
                // Answer immediately so Flutter shows "ads unavailable" instead
                // of waiting out its 30 s reward timeout.
                SendRewardedFailed(requestId, "rewarded_disabled");
                return;
            }
            if (!_initialized)
            {
                // Ads still initialising (first seconds after game launch):
                // queue the request — OnInitializationComplete loads the
                // rewarded placement and OnUnityAdsAdLoaded retries it. If
                // init fails, OnInitializationFailed answers it instead.
                // (Never a silent drop: the old code returned without answering.)
                _pendingRewardedRequestId = requestId ?? "";
                return;
            }
            if ((DateTime.UtcNow - _lastAdShown).TotalSeconds < cfg.adsMinGapSecs)
            {
                SendRewardedFailed(requestId, "ad_cooldown");
                return;
            }
            _pendingRewardedRequestId = requestId ?? "";
            if (!_rewardedLoaded)
            {
                Advertisement.Load(RewardedPlacement, this);
                return;
            }
            Advertisement.Show(RewardedPlacement, this);
#else
            // No Ads package in the Editor: report a failed attempt so Flutter can
            // show "ads unavailable" instead of hanging.
            SendRewardedFailed(requestId, "ads_package_missing");
#endif
        }

        private static void SendRewardedFailed(string requestId, string reason)
        {
            FlutterBridge.Instance?.SendEvent("rewarded_failed", RewardedPlacement, "",
                "{\"requestId\":\"" + (requestId ?? "") + "\",\"reason\":\"" + reason + "\"}");
        }

        // ---- Unity Ads callbacks -------------------------------------------------
#if UNITY_ADS
        public void OnInitializationComplete()
        {
            _initialized = true;
            Debug.Log("[TJ Racing] Unity Ads initialised.");
            ShowBanner();
            Advertisement.Load(InterstitialPlacement, this);
            Advertisement.Load(RewardedPlacement, this);
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogWarning("[TJ Racing] Unity Ads init failed: " + error + " — " + message);
            if (!string.IsNullOrEmpty(_pendingRewardedRequestId))
            {
                // A Flutter ShowRewardedAd request was queued while init was in
                // flight — answer it now instead of letting Flutter time out.
                string req = _pendingRewardedRequestId;
                _pendingRewardedRequestId = "";
                SendRewardedFailed(req, "ads_init_failed");
            }
        }

        public void OnUnityAdsAdLoaded(string placementId)
        {
            if (placementId == InterstitialPlacement) _interstitialLoaded = true;
            if (placementId == RewardedPlacement)
            {
                _rewardedLoaded = true;
                if (!string.IsNullOrEmpty(_pendingRewardedRequestId))
                {
                    string req = _pendingRewardedRequestId;
                    _pendingRewardedRequestId = "";
                    ShowRewarded(req);
                }
            }
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            Debug.LogWarning("[TJ Racing] Ad load failed " + placementId + ": " + error + " — " + message);
            if (placementId == RewardedPlacement && !string.IsNullOrEmpty(_pendingRewardedRequestId))
            {
                // A Flutter ShowRewardedAd request is waiting on this load — answer
                // it now instead of letting Flutter wait out its 30 s timeout.
                string req = _pendingRewardedRequestId;
                _pendingRewardedRequestId = "";
                SendRewardedFailed(req, "ad_load_failed");
            }
        }

        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            Debug.LogWarning("[TJ Racing] Ad show failed " + placementId + ": " + error + " — " + message);
            if (placementId == RewardedPlacement)
                FlutterBridge.Instance?.SendEvent("rewarded_failed", placementId, "",
                    "{\"requestId\":\"" + _pendingRewardedRequestId + "\",\"reason\":\"" + error + "\"}");
            _pendingRewardedRequestId = "";
        }

        public void OnUnityAdsShowStart(string placementId) { _lastAdShown = DateTime.UtcNow; }

        public void OnUnityAdsShowClick(string placementId) { }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
        {
            if (placementId == InterstitialPlacement)
            {
                _lastInterstitialShown = DateTime.UtcNow;
                _interstitialLoaded = false;
                Advertisement.Load(InterstitialPlacement, this);
                FlutterBridge.Instance?.SendEvent("interstitial_shown", placementId, "", "{}");
            }
            else if (placementId == RewardedPlacement)
            {
                _rewardedLoaded = false;
                Advertisement.Load(RewardedPlacement, this);
                if (state == UnityAdsShowCompletionState.COMPLETED)
                {
                    // EVENT ONLY — Flutter credits the Ad Coins server-side.
                    string req = _pendingRewardedRequestId;
                    _pendingRewardedRequestId = "";
                    FlutterBridge.Instance?.SendEvent("rewarded_completed", placementId, "",
                        "{\"requestId\":\"" + req + "\"}");
                }
                else
                {
                    // Skipped / unknown: answer instead of dropping
                    // silently — Flutter's 30 s reward timeout would
                    // otherwise be the only feedback.
                    string req = _pendingRewardedRequestId;
                    _pendingRewardedRequestId = "";
                    SendRewardedFailed(req, "skipped");
                }
            }
        }
#endif
    }
}
