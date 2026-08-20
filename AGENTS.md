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
- The Ground Ops level is `Assets/_Project/Scenes/GroundOps.unity`.
- Do not commit Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.
- Always preserve `.meta` files for assets that remain in the project.

## Generated chamber scene

- `Assets/_Project/Editor/ChamberSceneBuilder.cs` is the source of truth for generated chamber geometry, materials, fixtures, and controller wiring.
- The normal builder command synchronizes the `Chamber Geometry` hierarchy in place by stable parent/name paths, preserving Unity file IDs and keeping scene diffs small. Do not make durable manual edits beneath that object; unrecognized generated children are removed by the next sync, so update the builder instead.
- Use `Tools > Chamber > Sync Main Scene Geometry` for ordinary generator changes. `Tools > Chamber > Full Rebuild Main Scene Geometry` is an explicit destructive fallback for major structural/type changes that cannot be reconciled safely in place.
- Preserve the chamber's mirrored X-axis convention and its established dimensions and openings unless the task explicitly changes them.
- The chamber has a full-size rectangular rear half (`z = 0..5`) and a rectangular frustum (`z = -5..0`) that converges on the source-end throat centered at `(0, 2.5, -5)`.
- The throat is approximately 1 m wide by 1 m tall externally and retains the 0.75 m square source opening.
- The source antenna is a 15 cm by 5 cm rectangular pyramid/horn extending 10 cm into the throat. Its broad base faces into the chamber; its rotation about the chamber axis is called polarity, with the horizontally wide base defined as 0 degrees.
- The shell uses closed volumetric shadow casters plus one-sided cutaway surfaces. Keep camera visibility, collision, and light/shadow behavior separate.
- Containing-room and chamber camera opacity are independently adjustable. Opacity changes must never alter shell collision or shadow casting.
- Door and source-window frames must remain opaque in both opaque and cutaway modes.
- The player starts just outside the door, centered in its opening and facing into the chamber.

## Chamber coordinate system

Use directions from the table's point of view when discussing or placing chamber objects:

- **Forward** points from the table toward the throat/source antenna: world `-Z`.
- **Backward** points away from the throat: world `+Z`.
- **Right** points from the table toward the chamber door: world `-X`.
- **Left** points away from the door: world `+X`.
- **Up** is world `+Y`; **down** is world `-Y`.

This project-facing meaning of left/right intentionally reflects Unity's usual X-axis intuition because the imported chamber geometry is mirrored across the YZ plane.

## Working with the open Unity Editor

Use the local bridge instead of launching a second Unity instance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 <command>
```

Useful commands include `editor_state`, `refresh`, `save_scene`, `rebuild_chamber`, `build_ground_ops`, `hierarchy`, `capture_game_view`, `capture_scene_view`, `enter_play_mode`, `exit_play_mode`, and `get_logs`.

- Save an intentionally dirty scene before rebuilding; do not force away the user's Editor changes.
- After changing C# code, run `refresh` and resolve all compiler diagnostics.
- After generator changes, run `rebuild_chamber` (the bridge's normal in-place sync) and inspect the hierarchy or a captured Game view as appropriate. Do not use the full rebuild for routine script or geometry changes.
- For runtime changes, perform a short Play Mode smoke test and check logs. Exit Play Mode when finished.

## Controls

- In Edit Mode, use `Window > Scene Tools` for scene visualization controls.
- `Scene Tools` exposes the active scene's wall opacity. In the chamber it also exposes chamber-light mode/timeout/status, floodlight state, and editable positioner pan/tilt/height. Generator rebuilds must preserve these editor values.
- `Scene Tools` is an editor-only visualization and debugging panel. Its saved values are useful for inspecting scenes and for Editor Play Mode, but standalone builds intentionally use opaque walls and start chamber gameplay from `ChamberBuildDefaults`: chamber lights Auto with a 30-second timer, floodlights off, pan/tilt/polarity at 0 degrees, and height at 0.2 m.
- Player mode uses `WASD` movement and mouse look. `Escape` releases the captured mouse.
- Near the computer console, `F` enters the seated console mode; `F` or `Escape` exits it.
- Console mode uses `A`/`D` for pan, `W`/`S` for tilt, and `Q`/`E` for source-antenna polarity. It must never expose, display, or change positioner height.
- The red rear-wall control is the only player interface for positioner height: `F` enters its mode, `Q`/`E` lower or raise, and `F` or `Escape` exits.
- The chamber's back-wall lights respond only to player motion inside the chamber and time out after 30 seconds. The floodlights start off and are toggled with `F` near their stand.
- Wall visualization is editor-only and is controlled from `Window > Scene Tools`.

## Ground Ops scene

- `Assets/_Project/Editor/GroundOpsSceneBuilder.cs` is the source of truth for the generated Dish Operations Center blockout in `GroundOps.unity`.
- Use `Tools > Ground Ops > Sync and Open Ground Ops Blockout`, or the bridge command `build_ground_ops`, to synchronize and open it. Make durable geometry changes in the builder rather than beneath the generated `Ground Ops Blockout` hierarchy.
- The current dimensions are photographic estimates based on `.local/reference`; keep the dimensional constants centralized and easy to replace when measurements become available.
- Ground Ops uses its own intuitive coordinate convention: `+Z` runs from the main Ops entrance toward the Server Room, `-X` points toward the curved window wall, `+X` points toward the straight/right wall, and `+Y` is up.
- Ground Ops wall opacity is controlled independently from `Window > Scene Tools`. Its volumetric wall shells always retain collision and shadow casting; the slider affects only camera visibility, with near walls at the selected opacity and inward-facing far walls remaining opaque.
- The Ground Ops exterior is a deliberately compressed stage set inspired by the real site. USGS elevations put the DOC around 230.5 m and the antenna complex around 349.2 m; the real antennas are about 777 m almost due west of the DOC. The checked-in offline elevation grid is `Assets/_Project/Editor/GroundOpsTerrainElevationData.cs`; do not introduce a network dependency into scene generation. In game, the generated proxies use 1:10 diameters (2.1 m/1.3 m) and intentionally stylized placement toward world `(-X, +Z)`. Preserve the dish complex's shared generated root offset `(-41.5, 13.7, 18.01)` unless the user moves it again. The full low-poly terrain field surrounds the DOC, begins around world `Y = -4.2` near the building so the Ops floor reads as a second story, rises toward the west and north, and forms a broad shoulder around world `Y = 12.56` beneath the dishes. Keep exterior terrain and dishes separate from the room blockout and omit the intervening building unless explicitly requested.
- The Ground Ops player starts just inside the main Ops entrance, facing toward the Server Room, and uses the shared first-person controls.
- Keep this early blockout deliberately sparse. The curved glazing, walls, doorway openings, floors, four equipped dish stations, five individual 2-by-4-foot non-dish stations in a U arrangement, the DSN rack pair, and the simple server-rack row are intentional; do not infer further furniture, equipment, ceiling infrastructure, or exterior scenery without a task requesting it.
