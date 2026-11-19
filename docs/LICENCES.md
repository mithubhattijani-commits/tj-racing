# TJ Racing 3D — licence findings

Checked 19 Nov 2026. Rule followed throughout: **official sources only** (Unity
Asset Store, publisher's own site, Freesound). No pirate "free download" mirrors
were used — one such mirror (`unityassetcollection.com`, a pirate copy of Racing
Project Kit) appeared in search results and was **not** touched.

## What the game actually ships on (in this repo)

| Item | Source | Licence / finding |
|---|---|---|
| All game code (`Assets/Scripts/TJRacing/`) | Written for this project | Original code, ours to ship. No third-party code. |
| All cars, track, city, HUD, sounds | Generated in code at runtime (primitives, procedural textures, procedural engine tone) | No asset licence involved — nothing to clear. |
| `Assets/FlutterEmbed/` (flutter_embed_unity Unity side, v1.4.0) | `github.com/learntoflutter/flutter_embed_unity`, `example_unity_2022_3_project/Assets/FlutterEmbed` (fetched via raw.githubusercontent, byte-verified) | **MIT** (stated in its `package.json`: `"license": "MIT"`). Vendored with attribution; see `Assets/FlutterEmbed/NOTICE.md`. |
| Unity engine | Unity Personal | Free under $100k revenue. The user's Unity account. |
| Unity Ads | Unity (Game ID 800417967) | Unity Ads terms; already in use for Arrow Puzzle. **Policy note:** ticket 00945584 ruled coins-to-cash prohibited on self-serve Unity Ads — same risk as Arrow Puzzle, user proceeds knowingly. |
| `com.unity.ads` 4.12.0 (UPM) | Unity Package Manager | Unity Package Manager terms; standard. |

## Assets the plan named — findings

### Realistic Car Controller Free (BoneCracker Games) — NOT USED

- Publisher's own site calls it a **"free, fully functional evaluation version
  of RCCP"** — every runtime feature works, no time limit / watermark / DRM.
- The word **"evaluation version"** is the problem: it is framed as a trial of the
  paid Pro product, not as a shippable free asset. No explicit statement was
  found on the publisher's pages permitting commercial shipment of a game built
  on the Free edition.
- **Decision: do NOT ship on it.** The game uses its own arcade WheelCollider
  physics (`CarController.cs`, written from scratch). If the user buys RCC Pro
  later, it can replace `CarController` — the swap point is documented in
  `docs/ASSETS.md`. Cost avoided: RCC Pro is ~$25–50 on the Asset Store.

### Racing Starter Kit FREE (Spin Motion Games) — NOT OBTAINED, NOT NEEDED

- Free on the official Unity Asset Store; standard Asset Store EULA (commercial
  use permitted for assets obtained through the Store). Reviews confirm it has
  lap + checkpoint + live positioning + AI.
- **Could not be obtained on this VM:** Asset Store packages have no direct
  download URL — they are acquired through a Unity account in Unity Hub / the
  Package Manager. There is no Unity install or Unity account here, and the only
  "free download" copies found are pirate mirrors (not used).
- **Decision: implemented from scratch instead.** Lap + checkpoint system,
  live race positioning and waypoint AI are all in `Assets/Scripts/TJRacing/`
  (`LapTracker`, `RacePositionTracker`, `AICarController`) with the same
  architecture the kit uses. If the user later obtains the kit through their
  Unity account, it can be dropped in as an alternative — it is NOT a
  dependency and nothing in this repo assumes it.

### Free cars (ARCADE: Ultimate Vehicles Pack, 60 Fantastic Race Cars) — NOT OBTAINED

- Both free on the official Unity Asset Store, standard EULA (commercial use
  permitted). Same acquisition blocker as above: no Unity account / Hub here,
  no direct download, pirate mirrors not used.
- **Decision: procedural low-poly cars in `CarFactory.cs`.** Swap guide in
  `docs/ASSETS.md`: drop a Store car model onto the `BodyRoot` child and delete
  the primitive body — physics/stats/colours keep working.

### Free sounds (Freesound / Unity free packs) — NOT SHIPPED YET

- No audio files ship in v1. Engine sound is a runtime-generated tone
  (`AudioManager.MakeEngineTone`). Skid / crash / nitro / countdown / win slots
  exist as empty `AudioClip` fields.
- **Before any clip is added:** check its individual licence (Freesound CC0 is
  safe; CC-BY needs attribution recorded here; CC-BY-NC is NOT usable in this
  commercial app). Record each clip + licence in this file when added.

## To record later

- [ ] If/when a Freesound clip is added: clip name, author, licence, URL.
- [ ] If/when a Store car pack is imported: pack name, publisher, date, Asset
      Store URL (proves official acquisition).
- [ ] If/when Racing Starter Kit FREE is obtained via the user's Unity account:
      same — record the acquisition.
