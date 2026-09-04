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

Scene-mutating commands are rejected during Play Mode. `rebuild_chamber` also rejects a dirty scene unless `-Force` is supplied.

## Unity controls

Use **Tools > Codex Bridge** to enable or disable the bridge, open its runtime folder, or force a status update. The bridge is enabled by default.

If the bridge has never loaded, focus Unity once or choose **Assets > Refresh**. After that initial compilation, the `refresh` command can ask the live editor to import subsequent external changes without opening a second Unity instance.
