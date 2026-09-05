# Unity Editor bridge

The project includes a local command bridge that lets development tools make verified changes through the already-open Unity Editor.

The bridge is implemented by `Assets/_Project/Editor/CodexEditorBridge.cs`. It polls JSON requests under `Library/CodexBridge`, executes only allowlisted operations on Unity's main thread, and writes structured responses and artifacts back under `Library`. The entire runtime directory is already excluded from Git because `Library` is ignored.

## Usage

Run the PowerShell client from the project root:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 ping
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 editor_state
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 refresh
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 hierarchy
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 capture_game_view
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 capture_scene_view
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 capture_scene_view -Argument top
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 build_ground_ops
powershell -ExecutionPolicy Bypass -File tools/unity-bridge.ps1 run_tests -Argument edit
```

`run_tests` accepts `edit` (the default) or `play`. Test requests survive Play Mode script reloads and return a pass/fail summary plus an NUnit XML report in `artifactPath`. Screenshot and hierarchy commands also return an `artifactPath` in their response.

Bridge-started Play Mode and test runs mute the Unity Editor's audio output for their duration. The previous mute setting is restored when automation finishes, including across domain reloads. Pressing Play yourself and running standalone builds retain normal sound. `editor_state` reports `automationAudioMuted` and `editorAudioMuted` so this can be verified without playing sound.

`capture_game_view -Argument ridge-recorder` frames the Signal Watch recorder, and `-Argument hallway-directory` frames the hallway sign. These camera captures omit screen-space UI; Play Mode interaction tests also save screenshots including the prompts and field notebook under `Library/CodexBridge/Artifacts/`.

The first-floor review presets are `null-stair`, `null-gallery`, `null-lab`, and `null-cell`. The null-lab walkthrough also captures actual Play Mode views of the descent, bench, notebook, and certified test cell.

For the Signal Archive, start a fresh Play session and run `capture_archive -NoWait`. Poll its response under `Library/CodexBridge/Responses/`. The photography pass advances the live sculpture, lighting, and reflection for each recording and writes three 1920 by 1080 PNGs to `docs/screenshots/`. It positions the player camera and stages receiver/playback state for reproducibility, excludes the overlay HUD, and automatically exits Play Mode to discard the staged state. It never saves the scene.

For the Skunk Works campus, use `capture_skunk_works -NoWait` from a fresh Play session. It writes sixteen 1920 by 1080 views covering the approach, atrium, all three experiments, First Light, and the campus at night. The actual runtime controllers animate each staged view before capture. HDR is retained through post processing before conversion to sRGB PNG. As with the archive pass, it exits Play Mode and discards the demonstration state and temporary time of day. Neither photography command should be run in a Play session whose progress you want to keep.

Scene-mutating commands are rejected during Play Mode. `rebuild_chamber` also rejects a dirty scene unless `-Force` is supplied.

## Unity controls

Use **Tools > Codex Bridge** to enable or disable the bridge, open its runtime folder, or force a status update. The bridge is enabled by default.

If the bridge has never loaded, focus Unity once or choose **Assets > Refresh**. After that initial compilation, the `refresh` command can ask the live editor to import subsequent external changes without opening a second Unity instance.
