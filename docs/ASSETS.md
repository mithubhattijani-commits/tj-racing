# TJ Racing 3D — assets: what is in, what to obtain, how to swap

## Shipped in v1 (all generated in code — zero binary assets)

| System | Where | Notes |
|---|---|---|
| 5 cars | `Cars/CarFactory.cs` | Procedural low-poly sports cars (body, cabin, spoiler, lights, nitro flames). Per-car colour + stats from `CarDef`. |
| Night City track | `Track/TrackBuilder.cs` + `Track/TrackSpline.cs` | Road ribbon, barriers + neon strips, ramp wedge, tunnel shell + lights, bridge deck + pillars, chicane barrels, 46 procedural buildings with lit-window textures, street lights, start gantry, 10 checkpoint triggers, 6 grid slots. |
| Physics | `Cars/CarController.cs` | Own arcade WheelCollider physics. |
| Race systems | `Track/LapTracker.cs`, `Track/RacePositionTracker.cs`, `Track/TrafficManager.cs`, `Cars/AICarController.cs` | Ordered checkpoints, live positions, lap-based traffic, fixed-skill AI. |
| HUD / menu / results | `UI/` | Built in code from the mockup palette. |
| Sounds | `Audio/AudioManager.cs` | Procedural engine tone only. Clip slots empty. |

## Optional upgrades (official Asset Store only — never pirate mirrors)

### 1. Real car models — ARCADE: Ultimate Vehicles Pack / 60 Fantastic Race Cars

- Both FREE on the Unity Asset Store (standard EULA, commercial use OK).
- **How to obtain:** the user (or main agent with the user's Unity account) opens
  Unity Hub → Asset Store, searches the pack, adds it to the account, imports it
  into this project via the Package Manager. There is no direct download link.
- **How to swap in:** each car is built by `CarFactory.Build()` with a child
  called `BodyRoot` holding the primitive body. Drag the Store car model onto
  `BodyRoot` (scale to ~4.4 m long, 1.9 m wide, nose toward +Z), delete the
  primitive body children, keep the wheels/colliders where they are. Physics,
  stats, colours and nitro flames keep working — they live on `CarController`,
  not on the mesh. Record the acquisition in `docs/LICENCES.md`.

### 2. Racing Starter Kit FREE (Spin Motion Games)

- FREE on the Asset Store; lap + checkpoint + positioning + AI. Same
  obtain-via-Unity-account route as above.
- **Not required.** The systems it provides are already implemented from scratch
  in this repo. Only adopt it if the user wants its extra features (replay,
  minimap, championships) in a later version — it would replace `LapTracker` /
  `RacePositionTracker` / `AICarController`, not sit alongside them.

### 3. Realistic Car Controller Pro (BoneCracker, ~$25–50) — only if bought

- The FREE edition is an "evaluation version" — **do not ship on it**
  (see `docs/LICENCES.md`). If the user buys Pro, it replaces `CarController`
  (keep `NitroSystem`, `PlayerDriver`, `AICarController` interfaces the same).
  Not needed for v1: the arcade physics here is tuned for the NFS feel.

### 4. Sounds — Freesound

- Candidates when the game needs real audio: engine loop, skid, crash, nitro
  whoosh, countdown beep, win jingle. Search Freesound, filter licence **CC0**
  (or CC-BY with attribution recorded in `docs/LICENCES.md`). **Never CC-BY-NC.**
- Drop clips into `Assets/Audio/` and assign them on the `AudioManager`
  GameObject fields (`skidClip`, `crashClip`, `nitroClip`, `countdownClip`,
  `winClip`). No code change needed.

## What NOT to do

- Do not download any Asset Store package from a "free download" mirror site
  (e.g. `unityassetcollection.com`, `gameassetsfree.com`) — those are pirate
  copies; using them would expose the user to copyright claims and possible
  malware. If a package cannot be obtained through the official Store with the
  user's Unity account, implement it or leave it out and say so.
- Do not add binary `.fbx` / `.png` / `.wav` files to this repo by hand without
  recording their source + licence in `docs/LICENCES.md`.
