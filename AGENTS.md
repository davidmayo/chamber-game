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

## Reusable prefabs

- Reusable project prefabs live under `Assets/_Project/Prefabs/`. The first two migration waves cover shared monitor/keyboard/mouse/chair components, desks and server cabinets, complete DOC/general workstation assemblies, the common ceiling-light fixtures, plate-glass panes, and the institutional door.
- `Assets/_Project/Editor/GroundOpsPrefabLibrary.cs` bootstraps a prefab only when its asset is missing. Once created, the prefab asset is the editable source of truth; ordinary scene synchronization must not overwrite later prefab edits.
- `GroundOpsSceneBuilder` continues to own room layout, station placement, unique equipment, and interaction/controller wiring. It places reusable prefab instances beneath stable generated wrapper objects instead of reconstructing their primitive children.
- Keep shared geometry changes in the base prefab so every linked instance inherits them. Use prefab variants for durable visual families (for example, `Chair Black` derives from `Chair`) and use per-instance overrides only for intentional placement or configuration differences.
- Do not unpack generated prefab instances or make durable manual edits to their scene copies. Edit the prefab asset for shared changes, or change the builder for layout and per-instance configuration.

## Generated chamber scene

- `Assets/_Project/Editor/ChamberSceneBuilder.cs` is the source of truth for generated chamber geometry, materials, fixtures, and controller wiring.
- The normal builder command synchronizes the `Chamber Geometry` hierarchy in place by stable parent/name paths, preserving Unity file IDs and keeping scene diffs small. Do not make durable manual edits beneath that object; unrecognized generated children are removed by the next sync, so update the builder instead.
- Use `Tools > Facility > Sync Continuous Facility` for ordinary generator changes. `Tools > Facility > Full Rebuild Continuous Facility` is an explicit destructive fallback for major structural/type changes that cannot be reconciled safely in place.
- Preserve the chamber's established world-space dimensions and openings unless the task explicitly changes them.
- Chamber builder coordinates are ordinary Unity coordinates. Placement helpers and plain `Transform` groups both accept their final positions and rotations directly; do not reintroduce a hidden YZ-plane reflection. The former three.js reflection was baked into the generator's authored coordinates in August 2026.
- The chamber has a full-size rectangular rear half (`z = 0..5`) and a rectangular frustum (`z = -5..0`) that converges on the source-end throat centered at `(0, 2.5, -5)`.
- The throat is approximately 1 m wide by 1 m tall externally and retains the 0.75 m square source opening.
- The source antenna is a 15 cm by 5 cm rectangular pyramid/horn extending 10 cm into the throat. Its broad base faces into the chamber; its rotation about the chamber axis is called polarity, with the horizontally wide base defined as 0 degrees.
- Walls, floors, and ceilings use ordinary opaque rendering, collision, and shadow casting. Do not reintroduce camera-only cutaway or wall-opacity systems unless explicitly requested.
- Generated ceilings are hidden only in the Unity Scene View to make editing practical; they remain opaque, collidable, and light-blocking in Play Mode and builds.
- The chamber and containing-room hallway connections are currently plain rectangular wall openings with no generated leaves, handles, trim, or door colliders. The source-window frame remains opaque.
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

Useful commands include `editor_state`, `refresh`, `save_scene`, `rebuild_chamber`, `build_ground_ops`, `hierarchy`, `audit_geometry`, `capture_game_view`, `capture_scene_view`, `enter_play_mode`, `exit_play_mode`, and `get_logs`.

- Save an intentionally dirty scene before rebuilding; do not force away the user's Editor changes.
- After changing C# code, run `refresh` and resolve all compiler diagnostics.
- After generator changes, run `rebuild_chamber` (the bridge's normal in-place sync) and inspect the hierarchy or a captured Game view as appropriate. Do not use the full rebuild for routine script or geometry changes.
- For runtime changes, perform a short Play Mode smoke test and check logs. Exit Play Mode when finished.

## Controls

- In Edit Mode, use `Window > Scene Tools` for scene visualization controls.
- In the chamber, `Scene Tools` exposes chamber-light mode/timeout/status, floodlight state, and editable positioner pan/tilt/height. Generator rebuilds must preserve these editor values.
- `Scene Tools` is an editor-only visualization and debugging panel. Its saved values are useful for inspecting scenes and for Editor Play Mode, but standalone builds intentionally use opaque walls and start chamber gameplay from `ChamberBuildDefaults`: chamber lights Auto with a 30-second timer, floodlights off, pan/tilt/polarity at 0 degrees, and height at 0.2 m.
- Player mode uses `WASD` movement and mouse look. While standing, `Escape` opens the shared pause menu and releases the mouse; resuming captures it again.
- `InteractionPromptDisplay` is the shared display-only runtime UI for proximity/control prompts. Controllers register their current message instead of drawing fixed-pixel `OnGUI` panels. Preserve its screen-space Canvas, `CanvasScaler` reference resolution of 1920 by 1080, and non-raycastable graphics so prompts remain readable across windowed, fullscreen, and high-DPI builds without interfering with the pause menu.
- Near the computer console, `F` enters the seated console mode; `F` or `Escape` exits it.
- Every seated console should support normalized mouse-wheel camera zoom by default and restore the player's standing field of view on exit. Omit zoom only when a task explicitly calls for different behavior.
- Console mode uses `A`/`D` for pan, `W`/`S` for tilt, and `Q`/`E` for source-antenna polarity. It must never expose, display, or change positioner height.
- The red rear-wall control is the only player interface for positioner height: `F` enters its mode, `Q`/`E` lower or raise, and `F` or `Escape` exits.
- The chamber's back-wall lights respond only to player motion inside the chamber and time out after 30 seconds. The floodlights start off and are toggled with `F` near their stand.
- `Window > Scene Tools` includes an Edit Mode rail-truck route preview and route-gizmo toggle. These are debug controls only; Play Mode and standalone builds always start the truck at the DOC end.
- `RuntimeSceneSwitcher` is the legacy-named shared runtime pause menu. While the player is standing, `Escape` pauses and opens its Resume-only menu. It uses a runtime-created uGUI Canvas plus an `EventSystem`/`InputSystemUIInputModule`; explicitly assign that module's default UI actions so Point and Click remain wired when Enter Play Mode Options skip domain reload. Do not replace interactive runtime controls with IMGUI/`OnGUI`, because this project uses the new Input System exclusively and IMGUI will render without receiving pointer events. Keep the end-to-end Play Mode test that sends real Input System pointer events and verifies Resume. While seated at a console or using the lift, the first `Escape` retains its interaction-specific exit behavior; press `Escape` again after standing to pause.

### Runtime UI requirements

The pause menu previously rendered correctly while its buttons were completely unclickable. Preserve all of the following; a visible uGUI button is not proof that its input path works.

- Every runtime-created uGUI Canvas containing interactive controls must have a `GraphicRaycaster`.
- There must be an active `EventSystem` with an enabled `InputSystemUIInputModule`. Explicitly call `AssignDefaultActions()` for a runtime-created module; verify that its Point and Click actions are assigned and enabled.
- While the pause menu is open, it exclusively owns pointer input. `FirstPersonPlayerController`, `ComputerConsoleController`, and any future component with click-to-capture behavior must not lock or hide the cursor, consume the click, or process look/movement input.
- Do not accept a test that invokes `Button.onClick` directly. `RuntimePauseMenuTests` must drive pointer movement, press, and release through the new Input System and assert the Canvas/EventSystem/raycaster/action wiring.
- When runtime UI fails, first check cursor ownership and every independent cursor-recapture handler. The chamber computer controller previously duplicated the player's recapture behavior and stole clicks only in the chamber scene.

## Signal Watch activity

- `FacilityShiftController` owns a replayable, session-only verification shift: chamber reference capture, satellite acquisition at the DOC hardware station, the ridge recorder snapshot, and report filing at the DSN racks. Measurements require holding Space while seated at the relevant console; drift or releasing Space resets the capture. Completed checks remain complete for the session. A new Space press at the racks after completion restarts the activity without resetting the facility's equipment or editor settings.
- `ChamberReferenceSignal` is a deliberately simple gameplay model, with a reference at pan +30 degrees, tilt -10 degrees, and polarity 90 degrees. It does not use positioner height. `SpectrumAnalyzerDisplay` renders its live trace at 10 Hz and releases the runtime texture on destruction. The original texture remains the Edit Mode fallback.
- `FacilityShiftDisplay` is display-only, with a resolution-scaled task tracker and Tab-toggle field notebook. Its graphics must remain non-raycastable and hide while paused. Keep all current values and hints free of positioner height when using the chamber console.
- Holding Shift gives one-fifth-speed pan, tilt, and polarity at the chamber computer. The existing normal speeds and separate red-wall height controls remain intact.
- `RidgeRecorderController` owns Recorder 07. F begins a short download that cancels if the standing player leaves its range and freezes while paused. Keep its interaction point separate from the truck's and preserve a physically walkable route from the truck exit in both directions.
- The small activity additions are generated by `ChamberSceneBuilder.Activities.cs` and `GroundOpsSceneBuilder.Activities.cs`, partial definitions of the existing builders. Signs, the inspection light, recorder, and activity wiring participate in ordinary in-place synchronization.
- `FacilityPlayerEffects` provides the standing L-toggle inspection light, procedural footsteps, room ventilation, exterior wind, and measurement-confirmation sounds. The light starts off, switches off upon seating, and uses only the player's current named lighting zone. The chamber has no ventilation/wind bed. Audio clips are generated once per session and released on destruction.
- Escape can pause while riding the automatic truck, except during its short transfer fades. All other consoles retain their first-Escape exit behavior. Keep `SignalWatchTests` as an end-to-end real-Input-System shift test, including actual tuning, both truck legs, the walk to/from the recorder, pause handling, report completion, replay, light masks, and notebook layout.

## Player builds

- Standalone players default to a resizable 1600 by 900 window. Unity's normal fullscreen switching remains enabled, and `-screen-fullscreen`, `-screen-width`, and `-screen-height` command-line overrides remain supported.
- `Assets/_Project/Editor/ProjectBuildPipeline.cs` owns the local release build workflow. Run `Tools > Build > Clean and Build Windows + Linux` to delete only the validated project-root `Builds` directory, then create `Builds/Windows/Chamber.exe` and `Builds/Linux/Chamber.x86_64` from every enabled scene in Build Profiles.
- The same pipeline entry point is `ProjectBuildPipeline.CleanAndBuildAll` for a later `-batchmode -executeMethod` wrapper. It builds only the continuous `Main.unity` facility scene and must fail clearly when that scene is missing, a platform module is unavailable, or either player build fails. Never weaken its exact `Builds` path validation.

## Ground Ops scene

- `Assets/_Project/Editor/GroundOpsSceneBuilder.cs` is the source of truth for the generated `Ground Ops Blockout` region. Normal chamber synchronization calls `SyncIntoFacility` and updates that region inside `Main.unity`; make durable geometry changes in the builder rather than beneath the generated hierarchy.
- `Tools > Ground Ops > Sync and Open Ground Ops Blockout` and the bridge command `build_ground_ops` create the retained standalone preview scene only. Use the normal chamber/facility sync to update the playable world.
- The current dimensions are photographic estimates based on `.local/reference`; keep the dimensional constants centralized and easy to replace when measurements become available.
- Ground Ops uses its own intuitive coordinate convention: `+Z` runs from the main Ops entrance toward the Server Room, `-X` points toward the curved window wall, `+X` points toward the straight/right wall, and `+Y` is up.
- In the continuous facility, the DOC/server hallway wall and the chamber containing-room wall share Ground Ops-local centerline `X = 5.575` and thickness `0.15 m`; their hallway faces must remain flush. The chamber wall segments use the hallway material directly—never add a coplanar facade or finish quad over them.
- Geographic directions are derived from the real DOC and antenna coordinates plus the final staged world-space dish/ridge center. The antenna complex is 775.75 m away at true bearing 278.06 degrees. In world XZ, true North is approximately `(0.4826, 0.8758)` and true East is `(0.8758, -0.4826)`. The generated `Geographic Reference` marker beneath the DOC makes those axes explicit; do not assume world `+Z` is north.
- Ground Ops walls, floors, and ceilings use ordinary opaque rendering, collision, and shadow casting. Generated ceilings are hidden only in Scene View. The Ground Ops player camera clears to the scene skybox; do not copy that setting into the chamber generator without an explicit request.
- `Window > Scene Tools` also controls the Ground Ops local date and time. `GroundOpsSkyController` uses the real DOC latitude/longitude, automatic US Eastern EST/EDT rules, and the derived world cardinal basis to position the procedural-sky sun. Preserve the saved editor date/time across ordinary generator syncs.
- Ground Ops suspended uplights and recessed can lights are deliberately simple realtime fixtures. Their saved on/off state is controlled by `Window > Scene Tools` through `GroundOpsCeilingLightsController` and must survive ordinary generator syncs.
- The Ground Ops exterior is a deliberately compressed stage set inspired by the real site. USGS elevations put the DOC around 230.5 m and the antenna complex around 349.2 m; the real antennas are about 777 m almost due west of the DOC. The checked-in offline elevation grid is `Assets/_Project/Editor/GroundOpsTerrainElevationData.cs`; do not introduce a network dependency into scene generation. In game, the generated proxies have intentionally enlarged, independently persisted transforms and stylized placement toward world `(-X, +Z)`; preserve the generated per-dish root positions, scales, and reflector-shell offsets unless the user moves or resizes them again. The shell offsets keep the concave surfaces mechanically joined to their mounts and subreflector legs. The proxies are vertically staged so both reflectors remain clearly visible above the forest canopy, as in the DOC-window reference. The full low-poly terrain field surrounds the DOC, begins around world `Y = -4.2` near the building so the Ops floor reads as a second story, rises toward the west and north, and forms an elongated ridge crest around world `Y = 12.56` through both dishes rather than an isolated knob. A deterministic low-poly forest is emitted as a handful of combined trunk/crown meshes, with varied green crowns and slope-compensated crown sizes to maintain a continuous canopy on steep ground. It occupies only the broad window-facing `-X` half of the landscape, leaves a large clearing around the DOC, and remains dense around the dishes; keep it combined rather than expanding it into thousands of scene objects. Keep exterior terrain, forest, and dishes separate from the room blockout and omit the intervening building unless explicitly requested.
- The rail truck uses one continuous closed, forward-only route. It parks on the building-side roundabout, follows one shared access road to the antenna stop, turns around on the paved antenna apron, returns along that same road, and completes the other half of the building roundabout back to its parking point. At either arrival, `F` exits and one press of `W` starts the complete automatic next leg, allowing unlimited laps without leaving the cab. The player does not hold an accelerator. Do not implement the return trip by driving backward, silently flipping the truck, or generating a second mountain road. Keep the short split entrance/exit connectors at the roundabout broad and gently curved, and keep every paved mesh visibly above the terrain.
- The Ground Ops player starts behind Dish Station 3 facing the curved window and uses the shared first-person controls.
- `RailTruckController` owns the deliberately simple round trip between the outside end of the second-floor hallway and the antenna complex. Near `Hallway Exterior Interaction`, `F` fades directly into the generated truck, which remains stopped until the player presses `W` once. That single press starts the complete automatic leg at a constant 5 m/s; the player cannot accelerate, brake, or steer. There is one centerline mountain road used in both directions as part of the continuous loop described above. The camera position follows `Driver Camera Pose`, but mouse look remains available inside the cab and the normalized mouse-wheel zoom follows the standard console convention. At the antenna apron, `F` exits onto the walkable ridge; pressing `F` there again re-enters the stopped truck for the forward turnaround and return trip. At the building end, `F` exits back into the hallway. Both exits restore the standing field of view. `GroundOpsSceneBuilder` owns the road, roundabout, narrow forest corridor, truck primitives, waypoints, interaction/exit poses, and terrain/road colliders. Keep `RailTruckJourneyTests` as an end-to-end new-Input-System round-trip test of F, one-tap W automatic travel, mouse look, zoom, the closed route and intermediate antenna stop, both arrivals/exits, restored FOV, and ground support.
- The Ground Ops region is placed at world position `(1, 0, 16.75)` with a 180-degree Y rotation. This aligns its hallway directly with the containing-room double doors: Ground Ops local `+X` becomes facility world `-X`, and its former chamber doorway at local `(5.5, 0, 11.25)` coincides with the real chamber doorway at world `(-4.5, 0, 5.5)`.
- The second-floor hallway is a continuous L: one arm runs across the DOC entrance and ends at the rail-trip interaction point, while the other runs past the DOC, server room, and full chamber length before its north cap. Two large, regularly paneled windows occupy about half of the long hallway and overlook the first-floor high bay. There is intentionally no hallway door into the high bay. All areas coexist in `Main`; there are no scene-loading portals or duplicate players. The rail trip is an in-scene fade and camera handoff, not a scene load. Keep the Play Mode test that physically walks the shared player across the chamber/hallway seam and verifies level floor support on both hallway arms.
- The generated building shell follows `.local/reference/building`: its nominal ground-floor footprint is rectangular, from local X `-4.275..48` and Z `-18.5..27.5`, and its single full-height roof sits at local Y `6.35`. The west facade aligns with the exterior face of the server-room wall. The curved DOC portion projects prominently west of that plane as a second-floor cantilever only; the ground facade beneath it remains straight and the exterior space below the overhang remains open. The high-bay floor is the first-floor datum and the DOC/chamber/hallway floor is the second-floor datum. The shell's south wing and most first-floor interiors are intentionally undefined empty volumes. Do not invent rooms there without an explicit request.
- Treat the building as strict box-in-box construction. The exterior building envelope is a complete, independent shell around the room, hallway, chamber, and high-bay boxes. Never reuse an interior room wall as part of the exterior facade; never trim, patch, or omit an exterior slab merely because an interior wall occupies a similar location. North, south, and east are each one continuous full-height rectangular exterior slab. West is a small set of rectangular slabs arranged only around the DOC cantilever and entrance openings. The roof and foundation are each complete rectangular slabs. Maintain a small physical cavity between the exterior envelope and interior room shells so the nested surfaces are not coplanar and cannot Z-fight.
- The high bay is intentionally inaccessible. It uses the same simple low-poly vocabulary as the rest of the project, but serves as an alluring contrast to the mundane, drab working rooms: very tall, bright, cool-white, clean, gleaming, and slightly shiny, visible only through the DOC/hallway glazing. Its generated root is staged 3.1 m downward relative to the second-floor DOC/hallway, and the hall-side lower wall bridges all the way from the high-bay floor to the hallway elevation. Preserve its high industrial ceiling, high-mounted lighting, reflective light-gray floor, and lack of any player entrance.
- The cleanroom is a sparse, sealed pavilion inside the high bay: a distinct glossy white floor and roughly 2-foot-thick low white ceiling held by dense silver metallic framing, with clear plexiglass walls and an intentionally excessive grid of cold-white lights. Its generated root retains the user's staged local offset `(3.7, 0, 2.5)` within the high bay. Its long axis runs away from the hallway. Interior plexiglass-and-metal partitions define one large central room and two smaller rooms at each end. Its overwhelming brightness should make it look intrinsically intriguing and beguiling from the overlook even while empty. Do not make it accessible or populate it with equipment unless explicitly requested.
- The north-west cleanroom compartment nearest the hallway contains a deliberately restrained low-poly LEMS-A3 spacecraft: a wide polished aluminum box, a dark gridded solar array covering its hallway-facing front, and only a raised EVA handle plus four antenna panels on top. Do not add the other flight hardware visible in the reference photographs unless explicitly requested.
- The front-left dish station (`Hardware Control Station`) is an interactive seated console. Nearby `F` sits down; `F` or `Escape` stands up. `D` turns right/increases dish azimuth, `A` turns left/decreases it, `W` increases elevation, and `S` decreases it; the mouse wheel controls seated-camera zoom. Both dish reflectors move in unison and default to azimuth 0 degrees/elevation 90 degrees. Normal dish slew is 12 degrees/second; holding either Shift key selects one-fifth speed (2.4 degrees/second), while holding either Ctrl key selects five-times speed (60 degrees/second). If both modifiers are held, fine mode wins. Dish azimuth is a real compass bearing in the generated geographic basis: `0 degrees` is true north and `+90 degrees` is east; positive elevation is above the horizon. Azimuth hard-stops at -180 and +180 degrees and never wraps between them; elevation is clamped to 0 through 90 degrees. Generated chair geometry is intentionally non-colliding so the player can negotiate the crowded desk aisles. Each proxy reflector is a shallow, closed spherical-cap shell with a simple four-strut subreflector assembly that follows its pointing transform.
- The left DSN cabinet is the `DSN Uplink Rack`; the right cabinet is the `DSN Downlink Rack`. A centered stool and `F`-activated seated console let the player inspect both racks without controlling the dishes from that position; mouse-wheel zoom follows the standard seated-console behavior.
- `GroundOpsSatelliteTarget` stores the editable target data shown in `Window > Scene Tools`. Its initial target is GOES-19 at 75.2 degrees west: geometric topocentric pointing from the DOC is azimuth 166.823 degrees true, elevation 44.946 degrees, and range 37,409.234 km. Frequency 8,220 MHz and power 69.6 dBmi are the published X-band Raw Data Link frequency and minimum EIRP from the 75-degree-west slot; power is not the received signal level at Morehead. Ordinary generator syncs must preserve edits to these six target fields. The kludged DSN rack display reads this target and the current dish pointing. Its temporary gameplay model assumes -60 dBm at perfect alignment and applies `12 * (off-axis angle / 15-degree HPBW)^2` dB attenuation, clamped to -160 dBm. This is intentionally not a physically accurate model.
- Keep this early blockout deliberately sparse. The walls and five-segment curved glazing are 12 feet (3.6576 m) tall. Doorways are currently bare wall openings with no generated door panels. The floors, four equipped dish stations, five individual 2-by-4-foot non-dish stations in a U arrangement, the DSN rack pair, and the simple server-rack row are intentional; do not infer further furniture, equipment, ceiling infrastructure, or exterior scenery without a task requesting it.

## Facility lighting zones

- The continuous `Main` scene deliberately has different lighting identities in one shared world. Preserve the named URP Rendering Layers: `Exterior`, `Dish Operations Center`, `Hallway`, `Chamber Containing Room`, and `Chamber Interior`.
- The generated sun affects only `Exterior` and `Dish Operations Center`. DOC ceiling lights affect only the DOC, hallway fixtures affect only the hallway, containing-room lights affect only the containing room, and chamber wall/flood fixtures affect only the chamber interior. Assign both renderer and light rendering-layer masks when adding generated geometry or fixtures.
- Global sky ambient intensity stays near zero. The DOC and hallway receive explicit realtime fill instead of depending on ambient sky light, so the chamber can remain genuinely dark.
- `Ground Ops Blockout/Facility Lighting Zones` owns the generated local camera volumes and custom reflection probes for the DOC, hallway, containing room, and chamber interior. The chamber volumes have higher priority than adjacent-room volumes. Keep the shared player camera's URP post-processing enabled.
- The hallway has a generated physical ceiling and neutral fixtures. Its visible fixtures are emissive and use non-shadowing point lights, so they illuminate the corridor without consuming the punctual-shadow atlas. Like the other generated ceilings, it remains present for runtime rendering, collision, and shadowing while `GeneratedCeilingSceneVisibility` hides it only in Scene View. That editor helper also hides generated local Volume and Reflection Probe objects so their green bounds do not clutter Scene View; this does not disable either runtime effect.
