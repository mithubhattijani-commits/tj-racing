# TJ Racing 3D — Unity project

Night-city arcade street racer embedded **inside TJ Earn** (Flutter, `com.tjearn.app`)
via [flutter_embed_unity](https://pub.dev/packages/flutter_embed_unity). One track,
5 cars, 3 laps, 5 fixed-skill AI, nitro + drift, tilt + touch controls, Unity Ads.

> **Status (19 Nov 2026):** complete Unity project as text. NOT yet opened in the
> Unity Editor and NOT yet exported — the first Editor open + the first
> `unityLibrary` export happen in CI (see `docs/EXPORT.md`). No Unity is installed
> on this VM and no Unity account licence is available here.

## Pinned Unity version: **2022.3.62f2**

- `flutter_embed_unity` (current docs, checked 19 Nov 2026) requires **2022.3.62f2
  or newer** on Android (security advisory) and **2022.3.56+** for the Android 15
  16 KB page-size rule. 2022.3.62f2 satisfies both.
- 2022.3 is the plugin's **default Android implementation** — no extra
  `flutter_embed_unity_6000_0_android` package, minSdk 22 (vs 25 for 6000.3),
  and the vendored exporter (`Assets/FlutterEmbed`) is the 2022.3 package (v1.4.0).
- Unity 6000.0/6000.3 LTS is also supported by the plugin, but it needs the extra
  Android package + newer Gradle/NDK and buys nothing for this game. Stay on 2022.3.
- See `ProjectSettings/ProjectVersion.txt`. The revision hash in that file is a
  placeholder — the Editor rewrites it on first open.

## What is in this repo

```
Assets/
  Config/tj-racing-default.json   Default config (plan §9 schema + research fixes)
  FlutterEmbed/                   flutter_embed_unity Unity side, v1.4.0 (MIT, vendored)
  Scenes/MainMenu.unity           Menu (bootstrap + camera + light; UI built in code)
  Scenes/Race_NightCity.unity     Race (same; track/cars/HUD built in code at runtime)
  Scripts/TJRacing/
    Config/    RacingConfig.cs    (plan §9 schema, JSON-driven, nothing hardcoded)
    Core/      RaceManager, FlutterBridge, RaceResult, RaceCamera, TJRacingBootstrap
    Cars/      CarController (own WheelCollider physics), CarFactory (procedural
               low-poly cars), AICarController (fixed skills), NitroSystem
    Track/     TrackSpline + NightCityTrackData, TrackBuilder (procedural night city:
               road, ramps, tunnel, bridge, buildings, lights, checkpoints),
               LapTracker, TrafficManager (lap-based traffic)
    Input/     TiltSteering (auto-calibrate + ±10° dead zone + sensitivity),
               PlayerDriver, TouchControls
    UI/        RaceHUD (mockup layout), MenuUI (garage), ResultsUI, UIBuilder
    Ads/       UnityAdsManager (Game ID 800417967)
    Audio/     AudioManager (procedural engine tone; clip slots for Freesound)
```

**No binary assets.** Cars, track, city, HUD and sounds are generated in code, so
the whole project is text and the `unityLibrary` export needs no Asset Store
download. See `docs/ASSETS.md` for the swap guide (official free Asset Store car
models) and `docs/LICENCES.md` for every licence finding.

## The rules that shape this code (from the plan + competitor research)

- **Unity NEVER credits coins.** It reports `race_result` (position, laps, race
  id, time) to Flutter; Flutter + the server (`claim-race-level`) validate and
  credit. Same for rewarded ads: Unity only sends `rewarded_completed`; Flutter
  credits Ad Coins server-side. Car unlock state comes FROM Flutter.
- **Economy (final):** win 10 Level Coins, 2nd 5, else 0; daily cap 200.
  Cars: Street Hawk free, Night Falcon 200, Midnight Ghost 500, Storm Rider 800,
  Thunder King 1200 Ad Coins. Direct unlocks only — no random rewards.
- **AI: fixed skills** — 2 slow, 2 medium, 1 fast, ±3% per-race variance. NO
  rubber-banding: an AI car never exceeds its own top speed to catch up.
- **Tilt:** auto-calibrate at every race start, ±10° dead zone, sensitivity
  slider (0.5–2.0), HUD re-calibrate button, tilt ↔ buttons switch in pause menu.
- **Laps feel different:** lap 1 clean, lap 2 bridge traffic, lap 3 (+ hard
  level ≥ 3) light road traffic. First race: AI slowed 25% — it must be winnable.
- **Ads:** banner passive top strip; interstitial every 3rd race, max 1 per
  3 min, NEVER right after a win (only after Continue on results), never two ads
  back-to-back (90 s gap); rewarded opt-in only, never mid-race, never at start.
- **Drift charges nitro;** double-tap nitro = shockwave boost. No random pickups.

## Open in the Editor (first time)

1. Unity Hub → install **2022.3.62f1** + Android Build Support (SDK/NDK/JDK 17).
2. Open this folder. The Editor imports, compiles, and normalises the
   hand-written `.unity` scenes (expected — it rewrites formatting only).
3. `File → Build Settings`: add `MainMenu` (index 0) and `Race_NightCity` (1),
   platform Android.
4. `TJ Racing → Export Android` (flutter_embed_unity menu, from the vendored
   `Assets/FlutterEmbed/Editor`) — or batchmode, see `docs/EXPORT.md`.
5. Play the menu in the Editor to smoke-test (tilt falls back to accelerometer
   keys in the Editor; buttons work with the mouse).

## Export (unityLibrary for Flutter)

See `docs/EXPORT.md`. Two paths:

- **CI (chosen):** `.github/workflows/export-unitylibrary.yml` — manual
  `workflow_dispatch` run, game-ci `unity-builder@v4`, uploads the `unityLibrary`
  artifact. Needs repo secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
  (user's Unity account — only the user/main agent can provide these).
- **Local:** Unity batchmode + `ProjectExporterBatchmode.ExportProject`
  (`-exportPath <dir>`). Not possible on this VM (no Unity, no licence).

## Flutter side

`docs/FLUTTER_INTEGRATION.md` — the full bridge protocol (every message both
ways, with Dart examples), `settings.gradle` snippet, and the config wiring.
The Flutter embed work is a sibling agent's job; this repo only defines the
contract.

## Policy risk (do not hide)

Unity ticket **00945584** already ruled coins-to-cash **prohibited** on self-serve
Unity Ads; ticket **00946156** (gameplay cash coins) is still unanswered. This game
follows the exact Arrow Puzzle coin model, so it carries the **same suspension
risk**. The user proceeds knowingly.
