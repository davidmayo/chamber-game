# Chamber Game

A simple first-person Unity game set in an anechoic chamber and the surrounding facility. Explore the chamber, inspect the operations consoles, point the antennas, and ride out to the ridge.

## Play in Unity

Open the project in Unity 6.3 LTS, open `Assets/_Project/Scenes/Main.unity`, and press Play. The entire facility is in this one scene.

| Where | Controls |
| --- | --- |
| Walking | **WASD** to move, mouse to look, **F** to use nearby equipment |
| Pause menu | **Esc** to pause or resume; click **Resume** or press **Enter** |
| Any seated console | Mouse to look, wheel to zoom, **F** or **Esc** to stand up |
| Chamber computer | **A / D** pan, **W / S** tilt, **Q / E** source polarity |
| Red rear-wall control | **Q / E** lower or raise the positioner; **F / Esc** to leave |
| DOC hardware-control station | **A / D** azimuth, **W / S** elevation; hold **Shift** for fine movement or **Ctrl** for fast movement |
| Floodlight stand | **F** to toggle the floodlights |

The chamber lights respond to movement inside the chamber and normally switch off after 30 seconds of stillness. The floodlights are independent. Darkness is part of the experience.

At the outside end of the hallway, **F** enters the truck. Press **W once** to start the trip; it drives itself and stops at the destination. At either stop, **F** exits and **W** starts the next leg. Mouse look and wheel zoom work in the cab.

## Edit and verify

Use **Window > Scene Tools** to inspect lighting, time of day, equipment poses, and the truck route. These saved editor settings may differ from the standalone game's starting settings.

Room layout and generated geometry belong in the builders under `Assets/_Project/Editor/`. Shared objects belong in the linked prefabs under `Assets/_Project/Prefabs/`. After changing a builder, use **Tools > Facility > Sync Continuous Facility**. See [AGENTS.md](AGENTS.md) for project conventions.

With Unity open, run the gameplay checks through the [local Editor bridge](docs/unity-bridge.md):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 run_tests -Argument play
```

The tests cover walking between rooms, the truck round trip, console interactions and zoom, pause input, and prompt layout. Test reports and interaction screenshots are saved under `Library/CodexBridge/Artifacts/`.

**Tools > Build > Clean and Build Windows + Linux** creates both standalone players under `Builds/`; both Unity platform modules must be installed. This command replaces the project's previous `Builds/` outputs.
