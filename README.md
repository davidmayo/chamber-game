# Chamber Game

A simple first-person Unity game set in an anechoic chamber and the surrounding facility. Explore the chamber, inspect the operations consoles, point the antennas, and ride out to the ridge.

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

At the outside end of the hallway, **F** enters the truck. Press **W once** to start the trip; it drives itself and stops at the destination. At either stop, **F** exits and **W** starts the next leg. Mouse look and wheel zoom work in the cab, and **Esc** pauses the ride.

## Edit and verify

Use **Window > Scene Tools** to inspect lighting, time of day, equipment poses, and the truck route. These saved editor settings may differ from the standalone game's starting settings.

Room layout and generated geometry belong in the builders under `Assets/_Project/Editor/`. Shared objects belong in the linked prefabs under `Assets/_Project/Prefabs/`. After changing a builder, use **Tools > Facility > Sync Continuous Facility**. See [AGENTS.md](AGENTS.md) for project conventions.

With Unity open, run the gameplay checks through the [local Editor bridge](docs/unity-bridge.md):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 run_tests -Argument play
```

The tests cover walking between rooms, the truck round trip, console interactions and zoom, pause input, prompt layout, and a complete Signal Watch shift driven by keyboard input. Test reports and interaction screenshots are saved under `Library/CodexBridge/Artifacts/`.

**Tools > Build > Clean and Build Windows + Linux** creates both standalone players under `Builds/`; both Unity platform modules must be installed. This command replaces the project's previous `Builds/` outputs.
