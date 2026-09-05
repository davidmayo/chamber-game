# Chamber Game

A simple first-person Unity game set in an anechoic chamber and the surrounding facility. Explore the chamber, inspect the operations consoles, point the antennas, and ride out to the ridge.

## Skunk Works / Level 02

The truck now reaches a second building: **Space Science Center Skunk Works**, a futuristic campus of ceramic towers, luminous guides, and three connected prototype experiments. At the DOC truck stop, board with **F**, choose **2**, then press **W once**. **F** exits onto the arrival terrace. Choose **1** at the DOC to retain the original antenna destination.

Commission **First Light** in three steps:

1. **Helios Forge:** tune the amber source with **A/D** for phase and **W/S** for containment. Match **126 / 0.680**, then **hold Space** to certify it. **Shift** gives fine control.
2. **Vector Garden:** walk between the levitation anchors. **F** advances the nearby anchor and its next neighbor, wrapping through four heights. Match **A 2 / B 1 / C 3** and let the field settle.
3. **Horizon Engine:** use its bench to match yaw **+7.5** and pitch **+4.0**, then press **Space**. The iris opens, the stellar window comes alive, and the survey probe emerges. You can stand up and walk around during the sequence, or replay it afterward.

**Tab** opens the campus procedure. Both benches support mouse look, wheel zoom, and **F/Escape** to stand up. Progress lasts for the session, independently of the original facility assignment. The truck returns to the DOC. These are fictional experiments with deliberately simple gameplay measurements. [Campus guide](docs/skunk-works.md) · [Live screenshot gallery](docs/screenshots/README.md).

## Afterglow / Signal Archive

Beneath the DOC, a low amber passage opens into a tall, dark room containing a suspended light sculpture and a polished reflection inset. Reach it by taking **Stair 01**, then following the cable gallery past the Null Reference Lab and the **Afterglow** signs.

Walk around the inset and press **F** at each of the three receiver banks. Sit at the playback bench beside the entrance with **F**, choose a recording with **A / D**, and press **Space once** to play its full 18-second sequence. **Orbital** weaves intersecting paths, **Pulsar** opens a luminous hourglass, and **Aurora** folds a curtain of light. Each has its own harmonic sound. You can leave the bench and walk around the sculpture while it plays; pausing freezes the recording. Recover all three recordings, or replay your favorite. Progress lasts for the session.

This is a fictional, telemetry-inspired art installation. Its patterns are imagined recordings, not scientific measurements. [View the actual Play Mode screenshots](docs/screenshots/README.md).

## Null Reference Lab / Level 01

A new first-floor lab sits directly beneath the chamber. Follow the chamber hallway to its far end and look for **Stair 01**. Two flights and an intermediate landing lead to the amber-lit cable gallery, then into the lab and its glazed null cell. The high bay remains sealed and has no connection to this area.

Beside the passage to Afterglow, **F** operates the bench supply isolator on the gallery wall. Sit at the lab bench with **F**, then use **A / D** for phase and **W / S** for amplitude. Hold **Shift** for fine adjustment. The amber waveform is the incoming reference, teal is your cancellation signal, and white is their sum. Make the white trace flat, then **hold Space** to certify the null. The tone quiets as you approach balance; certification changes the test-cell lights. **Tab** opens the lab procedure and a tuning hint.

The copper reference assembly can be inspected through the opening beside the lab window. **F / Esc** leaves the bench and restores standing zoom. **R** at a powered, certified bench starts another test. The experiment is an optional, session-only activity; your Signal Watch progress continues independently. Return upstairs by the same staircase.

## Signal Watch

Your shift has four checks: capture a reference in the chamber, acquire the satellite from the DOC hardware station, collect Recorder 07 on the ridge, and file the handover at the DSN racks. The task tracker guides you through them; **Tab** opens your field notes.

The chamber analyzer now responds to the positioner's pan and tilt and the source antenna's polarity. Tune to the displayed reference, then **hold Space** to record a stable measurement. **Shift** gives finer control. The satellite check uses the target saved in Scene Tools and accepts pointing within one degree. These are deliberately simple gameplay measurements, not predictions of the real facility's RF performance.

At the ridge, leave the truck and walk to the green recorder panel. **F** starts copying; stay nearby until it finishes. Back at the DSN racks, sit down and **hold Space** to file the report. You can keep exploring afterward, or press **Space** again at the racks to begin another shift. Progress belongs to the current play session.

**L** toggles a narrow inspection light while standing. It starts off and respects the room lighting zones. Soft footsteps, ventilation in the working rooms, and wind on the ridge give each area a distinct sound; the chamber stays quiet.

## Play in Unity

Open the project in Unity 6.3 LTS, open `Assets/_Project/Scenes/Main.unity`, and press Play. The entire facility is in this one scene.

| Where | Controls |
| --- | --- |
| Walking | **WASD** to move, mouse to look, **F** to use nearby equipment |
| Pause menu | **Esc** to pause or resume; click **Resume** or press **Enter** |
| Any seated console | Mouse to look, wheel to zoom, **F** or **Esc** to stand up |
| Chamber computer | **A / D** pan, **W / S** tilt, **Q / E** source polarity; **Shift** for fine movement |
| Red rear-wall control | **Q / E** lower or raise the positioner; **F / Esc** to leave |
| DOC hardware-control station | **A / D** azimuth, **W / S** elevation; hold **Shift** for fine movement or **Ctrl** for fast movement |
| Floodlight stand | **F** to toggle the floodlights |

The chamber lights respond to movement inside the chamber and normally switch off after 30 seconds of stillness. The floodlights are independent. Darkness is part of the experience.

At the outside end of the hallway, **F** enters the truck. At the DOC, **1** selects Antennas and **2** selects Skunk Works. Press **W once** to start the trip; it drives itself and stops at the destination. At either destination, **F** exits and **W** starts the return leg. Mouse look and wheel zoom work in the cab, and **Esc** pauses the ride.

## Edit and verify

Use **Window > Scene Tools** to inspect lighting, time of day, equipment poses, and the truck route. These saved editor settings may differ from the standalone game's starting settings.

Room layout and generated geometry belong in the builders under `Assets/_Project/Editor/`. Shared objects belong in the linked prefabs under `Assets/_Project/Prefabs/`. After changing a builder, use **Tools > Facility > Sync Continuous Facility**. See [AGENTS.md](AGENTS.md) for project conventions.

With Unity open, run the gameplay checks through the [local Editor bridge](docs/unity-bridge.md):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 run_tests -Argument play
```

The tests cover walking between rooms, both truck destinations, console interactions and zoom, real pause-menu clicks, prompt layout, a complete Signal Watch shift, the Null Lab, and all three archive recordings. The Skunk Works checks walk every wing and complete its commissioning sequence with actual keyboard events, including interlocks, coupled anchors, interrupted captures, pause, replay, and restored standing zoom. Test reports and interaction screenshots are saved under `Library/CodexBridge/Artifacts/`.

Bridge-owned Play sessions and test runs temporarily mute Editor audio and restore its previous setting afterward. Pressing Play yourself and running standalone builds retain normal sound.

**Tools > Build > Clean and Build Windows + Linux** creates both standalone players under `Builds/`; both Unity platform modules must be installed. This command replaces the project's previous `Builds/` outputs.
