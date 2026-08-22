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
- The game uses one continuous facility scene: `Assets/_Project/Scenes/Main.unity`. It contains the chamber, containing room, hallway, DOC, server room, high bay, terrain, and dishes simultaneously.
- `Assets/_Project/Scenes/GroundOps.unity` is retained only as a standalone editor preview for its generator; it is not part of the runtime architecture or player builds.
- Do not commit Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.
- Always preserve `.meta` files for assets that remain in the project.

## Generated chamber scene

- `Assets/_Project/Editor/ChamberSceneBuilder.cs` is the source of truth for generated chamber geometry, materials, fixtures, and controller wiring.
- The normal builder command synchronizes the `Chamber Geometry` hierarchy in place by stable parent/name paths, preserving Unity file IDs and keeping scene diffs small. Do not make durable manual edits beneath that object; unrecognized generated children are removed by the next sync, so update the builder instead.
- Use `Tools > Facility > Sync Continuous Facility` for ordinary generator changes. `Tools > Facility > Full Rebuild Continuous Facility` is an explicit destructive fallback for major structural/type changes that cannot be reconciled safely in place.
- Preserve the chamber's established world-space dimensions and openings unless the task explicitly changes them.
- Chamber builder coordinates are ordinary Unity coordinates. Placement helpers and plain `Transform` groups both accept their final positions and rotations directly; do not reintroduce a hidden YZ-plane reflection. The former three.js reflection was baked into the generator's authored coordinates in August 2026.
- The chamber has a full-size rectangular rear half (`z = 0..5`) and a rectangular frustum (`z = -5..0`) that converges on the source-end throat centered at `(0, 2.5, -5)`.
- The throat is approximately 1 m wide by 1 m tall externally and retains the 0.75 m square source opening.
- The source antenna is a 15 cm by 5 cm rectangular pyramid/horn extending 10 cm into the throat. Its broad base faces into the chamber; its rotation about the chamber axis is called polarity, with the horizontally wide base defined as 0 degrees.
- The shell uses closed volumetric shadow casters plus one-sided cutaway surfaces. Keep camera visibility, collision, and light/shadow behavior separate.
- Containing-room and chamber camera opacity are independently adjustable. Opacity changes must never alter shell collision or shadow casting.
- The chamber and containing-room hallway connections are currently plain rectangular wall openings with no generated leaves, handles, trim, or door colliders. The source-window frame remains opaque in both opaque and cutaway modes.
- The player starts just outside the door, centered in its opening and facing into the chamber.

## Chamber coordinate system

Use directions from the table's point of view when discussing or placing chamber objects:

- **Forward** points from the table toward the throat/source antenna: world `-Z`.
- **Backward** points away from the throat: world `+Z`.
- **Right** points from the table toward the chamber door: world `-X`.
- **Left** points away from the door: world `+X`.
- **Up** is world `+Y`; **down** is world `-Y`.

These directions describe the chamber's final Unity world-space layout directly. The door happens to be on world `-X`; no coordinate conversion is applied by the builder.

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
- Player mode uses `WASD` movement and mouse look. While standing, `Escape` opens the shared pause menu and releases the mouse; resuming captures it again.
- `InteractionPromptDisplay` is the shared display-only runtime UI for proximity/control prompts. Controllers register their current message instead of drawing fixed-pixel `OnGUI` panels. Preserve its screen-space Canvas, `CanvasScaler` reference resolution of 1920 by 1080, and non-raycastable graphics so prompts remain readable across windowed, fullscreen, and high-DPI builds without interfering with the pause menu.
- Near the computer console, `F` enters the seated console mode; `F` or `Escape` exits it.
- Every seated console should support normalized mouse-wheel camera zoom by default and restore the player's standing field of view on exit. Omit zoom only when a task explicitly calls for different behavior.
- Console mode uses `A`/`D` for pan, `W`/`S` for tilt, and `Q`/`E` for source-antenna polarity. It must never expose, display, or change positioner height.
- The red rear-wall control is the only player interface for positioner height: `F` enters its mode, `Q`/`E` lower or raise, and `F` or `Escape` exits.
- The chamber's back-wall lights respond only to player motion inside the chamber and time out after 30 seconds. The floodlights start off and are toggled with `F` near their stand.
- Wall visualization is editor-only and is controlled from `Window > Scene Tools`.
- `RuntimeSceneSwitcher` is the legacy-named shared runtime pause menu. While the player is standing, `Escape` pauses and opens its Resume-only menu. It uses a runtime-created uGUI Canvas plus an `EventSystem`/`InputSystemUIInputModule`; explicitly assign that module's default UI actions so Point and Click remain wired when Enter Play Mode Options skip domain reload. Do not replace interactive runtime controls with IMGUI/`OnGUI`, because this project uses the new Input System exclusively and IMGUI will render without receiving pointer events. Keep the end-to-end Play Mode test that sends real Input System pointer events and verifies Resume. While seated at a console or using the lift, the first `Escape` retains its interaction-specific exit behavior; press `Escape` again after standing to pause.

### Runtime UI requirements

The pause menu previously rendered correctly while its buttons were completely unclickable. Preserve all of the following; a visible uGUI button is not proof that its input path works.

- Every runtime-created uGUI Canvas containing interactive controls must have a `GraphicRaycaster`.
- There must be an active `EventSystem` with an enabled `InputSystemUIInputModule`. Explicitly call `AssignDefaultActions()` for a runtime-created module; verify that its Point and Click actions are assigned and enabled.
- While the pause menu is open, it exclusively owns pointer input. `FirstPersonPlayerController`, `ComputerConsoleController`, and any future component with click-to-capture behavior must not lock or hide the cursor, consume the click, or process look/movement input.
- Do not accept a test that invokes `Button.onClick` directly. `RuntimePauseMenuTests` must drive pointer movement, press, and release through the new Input System and assert the Canvas/EventSystem/raycaster/action wiring.
- When runtime UI fails, first check cursor ownership and every independent cursor-recapture handler. The chamber computer controller previously duplicated the player's recapture behavior and stole clicks only in the chamber scene.

## Player builds

- Standalone players default to a resizable 1600 by 900 window. Unity's normal fullscreen switching remains enabled, and `-screen-fullscreen`, `-screen-width`, and `-screen-height` command-line overrides remain supported.
- `Assets/_Project/Editor/ProjectBuildPipeline.cs` owns the local release build workflow. Run `Tools > Build > Clean and Build Windows + Linux` to delete only the validated project-root `Builds` directory, then create `Builds/Windows/Chamber.exe` and `Builds/Linux/Chamber.x86_64` from every enabled scene in Build Profiles.
- The same pipeline entry point is `ProjectBuildPipeline.CleanAndBuildAll` for a later `-batchmode -executeMethod` wrapper. It builds only the continuous `Main.unity` facility scene and must fail clearly when that scene is missing, a platform module is unavailable, or either player build fails. Never weaken its exact `Builds` path validation.

## Ground Ops scene

- `Assets/_Project/Editor/GroundOpsSceneBuilder.cs` is the source of truth for the generated `Ground Ops Blockout` region. Normal chamber synchronization calls `SyncIntoFacility` and updates that region inside `Main.unity`; make durable geometry changes in the builder rather than beneath the generated hierarchy.
- `Tools > Ground Ops > Sync and Open Ground Ops Blockout` and the bridge command `build_ground_ops` create the retained standalone preview scene only. Use the normal chamber/facility sync to update the playable world.
- The current dimensions are photographic estimates based on `.local/reference`; keep the dimensional constants centralized and easy to replace when measurements become available.
- Ground Ops uses its own intuitive coordinate convention: `+Z` runs from the main Ops entrance toward the Server Room, `-X` points toward the curved window wall, `+X` points toward the straight/right wall, and `+Y` is up.
- Geographic directions are derived from the real DOC and antenna coordinates plus the final staged world-space dish/ridge center. The antenna complex is 775.75 m away at true bearing 278.06 degrees. In world XZ, true North is approximately `(0.4826, 0.8758)` and true East is `(0.8758, -0.4826)`. The generated `Geographic Reference` marker beneath the DOC makes those axes explicit; do not assume world `+Z` is north.
- Ground Ops wall opacity is controlled independently from `Window > Scene Tools`. Its volumetric wall shells always retain collision and shadow casting; the slider affects only camera visibility, with near walls at the selected opacity and inward-facing far walls remaining opaque.
- The Ground Ops ceiling belongs to the same room-shell opacity group as the walls: its camera surface follows the Ground Ops opacity slider, while its separate physical slab always retains collision and shadow casting. The Ground Ops player camera clears to the scene skybox; do not copy that setting into the chamber generator without an explicit request.
- `Window > Scene Tools` also controls the Ground Ops local date and time. `GroundOpsSkyController` uses the real DOC latitude/longitude, automatic US Eastern EST/EDT rules, and the derived world cardinal basis to position the procedural-sky sun. Preserve the saved editor date/time across ordinary generator syncs.
- Ground Ops suspended uplights and recessed can lights are deliberately simple realtime fixtures. Their saved on/off state is controlled by `Window > Scene Tools` through `GroundOpsCeilingLightsController` and must survive ordinary generator syncs.
- The Ground Ops exterior is a deliberately compressed stage set inspired by the real site. USGS elevations put the DOC around 230.5 m and the antenna complex around 349.2 m; the real antennas are about 777 m almost due west of the DOC. The checked-in offline elevation grid is `Assets/_Project/Editor/GroundOpsTerrainElevationData.cs`; do not introduce a network dependency into scene generation. In game, the generated proxies have intentionally enlarged, independently persisted transforms and stylized placement toward world `(-X, +Z)`; preserve the generated per-dish root positions, scales, and reflector-shell offsets unless the user moves or resizes them again. The shell offsets keep the concave surfaces mechanically joined to their mounts and subreflector legs. The proxies are vertically staged so both reflectors remain clearly visible above the forest canopy, as in the DOC-window reference. The full low-poly terrain field surrounds the DOC, begins around world `Y = -4.2` near the building so the Ops floor reads as a second story, rises toward the west and north, and forms an elongated ridge crest around world `Y = 12.56` through both dishes rather than an isolated knob. A deterministic low-poly forest is emitted as a handful of combined trunk/crown meshes, with varied green crowns and slope-compensated crown sizes to maintain a continuous canopy on steep ground. It occupies only the broad window-facing `-X` half of the landscape, leaves a large clearing around the DOC, and remains dense around the dishes; keep it combined rather than expanding it into thousands of scene objects. Keep exterior terrain, forest, and dishes separate from the room blockout and omit the intervening building unless explicitly requested.
- The Ground Ops player starts behind Dish Station 3 facing the curved window and uses the shared first-person controls.
- The Ground Ops region is placed at world position `(1, 0, 16.75)` with a 180-degree Y rotation. This aligns its hallway directly with the containing-room double doors: Ground Ops local `+X` becomes facility world `-X`, and its former chamber doorway at local `(5.5, 0, 11.25)` coincides with the real chamber doorway at world `(-4.5, 0, 5.5)`.
- The roofless second-floor hallway is a continuous L: one arm runs across the DOC entrance and is capped at its outside end, while the other runs past the DOC, server room, and full chamber length before its north cap. It overlooks an empty first-floor high-bay box through large windows. There is intentionally no hallway door into the high bay. All areas coexist in `Main`; there are no scene portals, arrival markers, fades, or duplicate players. Keep the Play Mode test that physically walks the shared player across the chamber/hallway seam and verifies level floor support on both hallway arms.
- The front-left dish station (`Hardware Control Station`) is an interactive seated console. Nearby `F` sits down; `F` or `Escape` stands up. `D` turns right/increases dish azimuth, `A` turns left/decreases it, `W` increases elevation, and `S` decreases it; the mouse wheel controls seated-camera zoom. Both dish reflectors move in unison and default to azimuth 0 degrees/elevation 90 degrees. Normal dish slew is 12 degrees/second; holding either Shift key selects one-fifth speed (2.4 degrees/second), while holding either Ctrl key selects five-times speed (60 degrees/second). If both modifiers are held, fine mode wins. Dish azimuth is a real compass bearing in the generated geographic basis: `0 degrees` is true north and `+90 degrees` is east; positive elevation is above the horizon. Azimuth hard-stops at -180 and +180 degrees and never wraps between them; elevation is clamped to 0 through 90 degrees. Generated chair geometry is intentionally non-colliding so the player can negotiate the crowded desk aisles. Each proxy reflector is a shallow, closed spherical-cap shell with a simple four-strut subreflector assembly that follows its pointing transform.
- The left DSN cabinet is the `DSN Uplink Rack`; the right cabinet is the `DSN Downlink Rack`. A centered stool and `F`-activated seated console let the player inspect both racks without controlling the dishes from that position; mouse-wheel zoom follows the standard seated-console behavior.
- `GroundOpsSatelliteTarget` stores the editable target data shown in `Window > Scene Tools`. Its initial target is GOES-19 at 75.2 degrees west: geometric topocentric pointing from the DOC is azimuth 166.823 degrees true, elevation 44.946 degrees, and range 37,409.234 km. Frequency 8,220 MHz and power 69.6 dBmi are the published X-band Raw Data Link frequency and minimum EIRP from the 75-degree-west slot; power is not the received signal level at Morehead. Ordinary generator syncs must preserve edits to these six target fields. The kludged DSN rack display reads this target and the current dish pointing. Its temporary gameplay model assumes -60 dBm at perfect alignment and applies `12 * (off-axis angle / 15-degree HPBW)^2` dB attenuation, clamped to -160 dBm. This is intentionally not a physically accurate model.
- Keep this early blockout deliberately sparse. The walls and five-segment curved glazing are 12 feet (3.6576 m) tall. The wooden Ops entrance door, floors, four equipped dish stations, five individual 2-by-4-foot non-dish stations in a U arrangement, the DSN rack pair, and the simple server-rack row are intentional; do not infer further furniture, equipment, ceiling infrastructure, or exterior scenery without a task requesting it.
