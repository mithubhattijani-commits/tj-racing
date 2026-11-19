# TJ Racing 3D — export: getting the Android `unityLibrary` for Flutter

The Flutter side needs an Android library export of this Unity project
(`unityLibrary`), produced by the vendored flutter_embed_unity exporter
(`Assets/FlutterEmbed/Editor/ProjectExporterBatchmode.cs`).

## Chosen path: CI export (on demand, manual trigger)

Workflow: `.github/workflows/export-unitylibrary.yml`

- Trigger: **manual only** (`workflow_dispatch`) — no push/PR trigger, so it
  costs nothing unless someone deliberately runs it.
- Runner: `ubuntu-latest`, game-ci `unity-builder@v4`, Unity **2022.3.62f2**.
- It runs the batchmode export (not a player build):
  `ProjectExporterBatchmode.ExportProject` with `-exportPath ./unityLibraryExport`.
- Output: artifact named `unityLibrary` (the transformed export — launcher
  removed, `strings.xml` moved, `ndkPath` stripped, activity removed — all done
  by the vendored exporter). Retention 14 days — download promptly.
- After download: place the folder at `~/workspace/tj-racing-export/unityLibrary`
  (the parent uploads/stages it; the Flutter agent includes it in TJ Earn's
  `android/settings.gradle` per `docs/FLUTTER_INTEGRATION.md`).

### Secrets the workflow needs (BLOCKER — only the user / main agent can provide)

| Secret | What it is | How to get it |
|---|---|---|
| `UNITY_LICENSE` | Unity licence file content (`.ulf`) | Unity Hub → sign in with the user's Unity account → activate a Personal licence → the `.ulf` file. Or generate via `game-ci/unity-activate` once. |
| `UNITY_EMAIL` | Unity account email | The user's Unity account (same account that owns Game ID 800417967 / Unity project 800438955). |
| `UNITY_PASSWORD` | Unity account password | Same. (Use a dedicated password / the account's real one — stored as a GitHub secret, never in the repo.) |

**Without these three secrets the CI export cannot run** — Unity will not export
without an activated licence. This is the only hard blocker on the export path.
No code change can substitute for it.

### Cost

- **$0.00 against the $2 GitHub billing cap.** The repo is PUBLIC, and GitHub
  Actions is free for public repositories (unlimited minutes). The Unity
  Personal licence is free (under $100k revenue). The game-ci actions are free.
- The export run itself takes ~10–25 min of runner time (Unity download is
  cached between runs via `actions/cache` on `Library/`), but runner minutes on
  a public repo are not billed.
- **Do NOT trigger the workflow until the three secrets exist** — a run without
  them fails at activation and wastes nothing but a few minutes; still, there is
  no point running it blind.

## Path NOT taken: local headless export on this VM

Attempted 19 Nov 2026, time-boxed, **stopped**:

1. No Unity installed on this VM (checked: nothing under `/opt/unity*`,
   `/usr/local/unity*`, no `unity`/`unityhub` binary).
2. A local install would need Unity Hub + the 2022.3.62f2 Editor (~2 GB) +
   Android Build Support modules (~1.5 GB) — downloadable in principle (98 GB
   free disk), but:
3. **Hard blocker, same as CI:** a headless export needs an activated Unity
   licence. No Unity account credentials exist on this VM and none may be
   invented or requested by this agent. Without activation, `Unity -batchmode`
   refuses to export.
4. The guessed Unity Hub download URL returned 404, so even the Hub fetch
   would need the current official link from unity.com/download.

Conclusion: local export is not viable here. CI is the correct path — same
licence requirement, but the licence lives as a repo secret (normal practice,
used by the other app repos' CI) and the heavy download happens on GitHub's
runner, not this VM.

## Local export (for a machine that HAS Unity + a licence)

```bash
# From the repo root, with Unity 2022.3.62f1 installed:
<unity-path> -projectPath . -batchmode -buildTarget Android \
  -executeMethod ProjectExporterBatchmode.ExportProject \
  -exportPath ./unityLibraryExport -quit -logFile export.log
```

Prerequisites on that machine: Android Build Support module, JDK 17, Android
SDK/NDK as shipped with Unity 2022.3.62f1. First do one export from the Editor
(`TJ Racing → Export Android`) to confirm project settings, then use batchmode.

## After the export (Flutter side)

1. Download the `unityLibrary` artifact from the workflow run.
2. Unzip to `~/workspace/tj-racing-export/unityLibrary` (or straight into the
   TJ Earn checkout's `android/unityLibrary`).
3. Follow `docs/FLUTTER_INTEGRATION.md` §1 (settings.gradle + gradle.properties).
4. The Flutter agent then builds TJ Earn v1.0.31/vc32 (racing + Arrow Puzzle
   removal together, one build).
