# TJ Racing 3D — Flutter integration contract

This is the contract the Flutter side (sibling agent) builds against. Unity side
is implemented in `Assets/Scripts/TJRacing/Core/FlutterBridge.cs`.

## 1. Embedding

- Flutter package: `flutter_embed_unity` (latest) + the Unity project in this repo.
  For Unity 2022.3 no extra platform package is needed (it is the default Android
  implementation). Flutter 3.47.8 is fine (plugin needs ≥ 3.35).
- Unity export: Android `unityLibrary` (see `docs/EXPORT.md`). In the Flutter app's
  `android/settings.gradle`:

```gradle
include ":unityLibrary"
project(":unityLibrary").projectDir = file("./unityLibrary")
```

  and in `android/app/build.gradle` add the `unityLibrary` dependency (per the
  `flutter_embed_unity` README for the 2022.3 flow). Also copy the
  `unityStreamingAssets=...` line the exporter prints into
  `android/gradle.properties`.
- Unity project's bundle identifier is `com.tjearn.app` (matches the Flutter app).
- The Unity view is a `UnityWidget`. Scenes: `MainMenu` (index 0) loads first;
  Unity switches to `Race_NightCity` internally when the player taps RACE.

## 2. Message channel

The plugin's channel is a single string both ways.

### Flutter → Unity

Flutter calls `unityWidgetController.postMessage("FlutterBridge", "OnFlutterMessage", json)`
where `json` is:

```json
{ "method": "<name>", "payload": "<JSON string>" }
```

**Important:** `payload` must be a JSON-encoded *string* (an object serialised to a
string), because Unity's `JsonUtility` cannot parse a nested raw object here.
Dart example:

```dart
Future<void> _send(String method, Map<String, dynamic> payload) async {
  await _unity.postMessage('FlutterBridge', 'OnFlutterMessage',
      jsonEncode({'method': method, 'payload': jsonEncode(payload)}));
}
```

| Method | Payload | When |
|---|---|---|
| `SetConfig` | `{config: <games.tj_racing object>, level, levelCoins, adCoins, rsValue, totalEarnedRs}` — `config` itself is a JSON string of the `games.tj_racing` block | At Unity launch, and whenever remote config / balances change |
| `SetSelectedCar` | `{carId}` | Garage selection changes |
| `SetUnlockedCars` | `{carIds: [...]}` | After any unlock / at launch. Unlock state is decided by **Flutter**, never Unity. `street_hawk` is always included |
| `SetBalances` | `{levelCoins, adCoins, rsValue, totalEarnedRs}` | After any balance change (incl. after a race is credited server-side) |
| `SetSensitivity` | `{value}` 0.5–2.0 | Tilt sensitivity slider in Settings |
| `SetControlMode` | `{value: "tilt" \| "buttons" \| "both"}` | Control-mode switch |
| `RecalibrateTilt` | `{}` | Re-calibrate button (Flutter-side settings) |
| `StartRace` | `{carId, level}` | (Optional — Unity also starts from its own RACE button) |
| `QuitToMenu` | `{}` | Back button / close Unity view |
| `ShowRewardedAd` | `{requestId}` | Opt-in rewarded prompt (garage / "need Ad Coins?"). Unity shows the ad; completion is reported back (below) — Flutter credits afterwards |
| `PowerupGranted` | `{powerupId}` (`"nitro_boost"` \| `"shield"`) | Sent AFTER Flutter spends Ad Coins server-side for a power-up (`spend-race-item`, item `race_powerup_<id>`) — fresh spend AND idempotent replay (either way the item IS granted). NEVER sent on a failed spend. Unity applies the effect in the race (nitro: full meter; shield: 20 s traffic pass-through — see `RaceManager.ApplyPowerup`) or holds it as a pending grant (`FlutterBridge.PendingPowerups`) for the next race if no race is active |

`rsValue` is a pre-formatted string, e.g. `"Rs 12"`. `totalEarnedRs` e.g. `"Rs 37"`.
Unity displays exactly what Flutter sends — it never computes Rs itself (the Rs
display in 3 places is the Flutter side's job).

### Unity → Flutter

Flutter receives these on the widget's `onUnityMessage` callback as JSON strings:

| `type` | Fields | Meaning / Flutter action |
|---|---|---|
| `race_result` | `race_id` (unique Guid), `track_id`, `car_id`, `position` (1–6), `total_racers` (6), `laps_completed`, `race_time_secs`, `best_lap_secs`, `level_coins_earned` (info only: 10/5/0), `is_first_race` | Call server `claim-race-level` with `race_id` + `position`. **Re-derive the coins server-side; never trust `level_coins_earned`.** Credit once per `race_id`, enforce the 200/day cap server-side |
| `race_started` | `race_id`, `detail` (JSON: track, laps) | Analytics / disable double-start |
| `race_cancelled` | `race_id` | Player quit mid-race — no credit |
| `rewarded_completed` | `placement` = `BP_Rewarded_Android`, `detail` (JSON: `requestId`) | Credit Ad Coins server-side (10/ad, cap 100/day), keyed by `requestId` (idempotency). **Unity never credits anything** |
| `rewarded_failed` | `placement`, `detail` (`requestId`, `reason`) | Show "ads unavailable, try later" |
| `interstitial_shown` | `placement` | Analytics |
| `car_locked_tapped` | `detail` (JSON: `carId`, `price`) | Player tapped a locked car in the garage — Flutter shows the unlock / rewarded-ad prompt |
| `powerup_purchase_requested` | `detail` (JSON: `powerupId`, `price`) | Player tapped a power-up button in the race pause panel — Flutter shows its own purchase dialog and spends Ad Coins server-side via `spend-race-item` (item `race_powerup_<id>`). On a successful spend Flutter replies with `PowerupGranted` (above); Unity applies the effect or holds it as a pending grant for the next race. Unity never spends anything itself. |

## 3. Config schema

Full `games.tj_racing` block: see `Assets/Config/tj-racing-default.json` (the
shipped defaults, plan §9) and plan §9 in the concept dir. Defaults if Flutter
sends nothing: same file's values. Every gameplay value is config-driven:
coins, caps, ad rules, laps, AI count, cars + prices, `tiltDeadZoneDeg` (10),
`tiltSensitivity` (1.0), `hardLevelThreshold` (3), `adsMinGapSecs` (90),
`firstRaceAiSlowdownPct` (25).

## 4. Unity Ads

- Game ID `800417967`; placements `BP_Banner_Android` (top strip, passive),
  `BP_Interstitial_Android`, `BP_Rewarded_Android`.
- Banner shows automatically when Unity initialises. It is a native strip ABOVE
  the Unity view — the HUD top row is laid out below it.
- Interstitial rules live in Unity (`UnityAdsManager`): every 3rd race, max 1
  per 3 min, only after Continue on the results screen, 90 s gap from any ad.
  Flutter does not trigger interstitials.

## 5. Checklist for the Flutter agent

- [ ] Add `flutter_embed_unity` to `pubspec.yaml` (2022.3 = default Android impl).
- [ ] Include the exported `unityLibrary` (from CI artifact — see `docs/EXPORT.md`).
- [ ] `games.tj_racing` config block in `tj-earn.json` + admin panel section.
- [ ] Send `SetConfig` (+ `SetUnlockedCars`, `SetBalances`) before/just after the
      Unity widget first loads; re-send on balance/config change.
- [ ] Implement `claim-race-level` server function (mirror `claim-game-level`).
- [ ] Rs display in 3 places (result screen, balance pill, total earned) — Unity
      only shows the strings Flutter sends.
- [ ] Rewarded-ad prompt only at moment of need (garage / "need Ad Coins?").
- [ ] Car unlock flow (Ad Coins, server-side) + `SetUnlockedCars` after purchase.
- [ ] Games-section entry for TJ Racing next to Arrow Puzzle's (removal is the
      sibling removal job; both land in v1.0.31/vc32, one build).
