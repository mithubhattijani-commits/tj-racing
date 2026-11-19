# Attribution — vendored third-party code

## flutter_embed_unity (Unity side), v1.4.0

Files under `Assets/FlutterEmbed/` are vendored from:

- Repository: https://github.com/learntoflutter/flutter_embed_unity
- Path: `example_unity_2022_3_project/Assets/FlutterEmbed`
- Package: `com.learntoflutter.flutter_embed_unity_2022_3`, version 1.4.0
- Author: James Allen (https://learntoflutter.com/)
- Licence: **MIT** (as declared in the package's `package.json`)

Files were fetched from the public repository via raw.githubusercontent.com on
19 Nov 2026 and verified byte-identical to upstream. No modifications were made.
The vendored code provides:

- `SendToFlutter/SendToFlutter.cs` — the Unity → Flutter message sender.
- `Editor/ProjectExporter*.cs` — the Android/iOS project exporter used to produce
  the `unityLibrary` export for Flutter (including the batchmode entry point
  `ProjectExporterBatchmode.ExportProject`).

## Version note — verified 19 Nov 2026 (Unity gaps fix)

The vendored scripts are v1.4.0 while the Dart package is 2.0.0 — **this is
NOT a skew that needs fixing, do not upgrade blindly:**

- Every file under `Assets/FlutterEmbed/` is **byte-identical** to the
  upstream `v2.0.0` tag (and to `main`) of
  `learntoflutter/flutter_embed_unity` — checked by direct diff on
  19 Nov 2026. Upstream's own `package.json` at the `v2.0.0` tag still
  declares version `1.4.0`: the Unity-side scripts were never bumped,
  the 2.0.0 versioning is Dart-side only (iOS SPM support, Android
  default-implementation switch to Unity 6000, min Flutter 3.35).
- The 2.0.0 Android glue keeps the exact reflection contract the C#
  relies on: `com.learntoflutter.flutter_embed_unity_android.messaging.SendToFlutter`
  with static `sendToFlutter(String)` — the 2.0.0 source carries an
  explicit "DO NOT change the name of this class, its package location
  or function names" comment (they are referenced from this C# by
  reflection, and the class must stay Java so the method compiles to a
  Java static). `SendToUnity` delivers Flutter → Unity via the generic
  `UnityPlayer.UnitySendMessage(gameObject, method, data)`, which is
  what `FlutterBridge.OnFlutterMessage` expects.
- So: **no upgrade needed.** Re-check only if the Dart package moves
  past 2.0.0 AND upstream changes the Unity-side scripts in the same
  release (diff `Assets/FlutterEmbed/` against the new tag the same way).
