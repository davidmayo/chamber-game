# Chamber Game

This is a Unity 6.3 LTS project using URP. Keep changes small, readable, and friendly to someone who is still learning modern Unity.

## Game intent

- The game uses a first-person perspective and takes place primarily in and around a modeled anechoic chamber.
- The chamber should feel dark, with intense practical lighting and hard or unusual shadows. Lighting and darkness are central to the game's identity and intended experience, not merely decorative polish.
- Graphics and gameplay should remain intentionally simple.
- The player can walk around the space and interact with equipment or other objects.

## Author info

- The project author (David Mayo) is not a game developer. He has some experience with Unity, but it is quite out of date.
- He has decent understanding of game dev concepts and graphics concepts, but NOT jargon.
- He is a strong software developer in general with a fair amount of C# experience.
- He is an operator who works in the actual chamber being modeled herein.

## Project layout

- Put project-owned assets under `Assets/_Project/`.
- Runtime scripts belong in `Assets/_Project/Scripts/`.
- Editor-only tools belong in `Assets/_Project/Editor/`.
- The main scene is `Assets/_Project/Scenes/Main.unity`.
- Do not commit Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.
- Always preserve `.meta` files for assets that remain in the project.

## Generated chamber scene

- `Assets/_Project/Editor/ChamberSceneBuilder.cs` is the source of truth for generated chamber geometry, materials, fixtures, and controller wiring.
- The builder replaces the `Chamber Geometry` hierarchy when it runs. Do not make durable manual edits beneath that object; update the builder instead.
- Preserve the chamber's mirrored X-axis convention and its established dimensions and openings unless the task explicitly changes them.
- The shell uses closed volumetric shadow casters plus one-sided cutaway surfaces. Keep camera visibility, collision, and light/shadow behavior separate.
- Door and source-window frames must remain opaque in both opaque and cutaway modes.

## Working with the open Unity Editor

Use the local bridge instead of launching a second Unity instance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 <command>
```

Useful commands include `editor_state`, `refresh`, `save_scene`, `rebuild_chamber`, `hierarchy`, `capture_game_view`, `enter_play_mode`, `exit_play_mode`, and `get_logs`.

- Save an intentionally dirty scene before rebuilding; do not force away the user's Editor changes.
- After changing C# code, run `refresh` and resolve all compiler diagnostics.
- After generator changes, run `rebuild_chamber` and inspect the hierarchy or a captured Game view as appropriate.
- For runtime changes, perform a short Play Mode smoke test and check logs. Exit Play Mode when finished.

## Controls

- In Edit Mode, use `Window > Chamber Tools` for chamber visualization controls.
