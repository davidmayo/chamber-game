# Space Science Center Skunk Works

Skunk Works is Level 02: a separate fictional prototype campus reached by truck in the continuous Main scene. Its First Light commissioning sequence connects three experiments. The original facility assignment remains available when you return.

## Getting there

At the outside end of the DOC hallway, **F** boards the truck. Select **2: Skunk Works**, then press **W once**. The truck drives the complete route and stops on the arrival terrace. **F** exits. Board again with F and press W to return to the DOC; destination **1** there selects the original antenna trip.

Follow the cyan entrance guides into the commissioning hall. Amber leads to Helios, mint to Vector, and violet to Horizon. **Tab** opens the local procedure. The central hall records each certification.

## First Light

| Location | Goal | Controls and behavior |
| --- | --- | --- |
| Helios Forge | Certify the source | F seats at the tuning bench. A/D changes phase; W/S changes containment. Match **126 / 0.680**, then hold Space for three stable seconds. Shift is fine control. Releasing Space, drifting, or leaving interrupts capture. |
| Vector Garden | Calibrate the coupled levitation anchors | Helios supplies its power. F advances the nearby anchor and its clockwise neighbor: A > B > C > A. Heights wrap through 0, 1, 2, 3. Match **2 / 1 / 3** and let the field settle for 2.5 seconds. The moving crystals show their actual heights. |
| Horizon Engine | Record First Light | Both earlier certifications release the interlock. F seats at the alignment bench. A/D adjusts yaw; W/S adjusts pitch, with Shift for fine control. Match **+7.5 / +4.0**, then tap Space. The 14-second sequence opens the mechanical iris, reveals a stellar window, and brings the survey probe forward. |

Both benches support mouse look and wheel zoom. F/Escape stands up and restores the standing field of view. The first Escape leaves a bench; a second Escape pauses. First Light continues while you walk around and freezes when paused. Space at the Horizon bench replays the sequence after completion while retaining the commissioning record.

Progress lasts for the current Play session. These are imagined prototypes and gameplay measurements, not physical simulations of real experiments.

<details>
<summary>Vector puzzle hint / solution</summary>

An anchor changes two heights at once. Four presses on the same anchor undo those changes. From all zeroes, press **B once**, then **C twice** to obtain 2 / 1 / 3.

</details>

## Implementation

- `SkunkWorksLayout` centralizes Ground Ops-local origin **(-78, 7.8, -48)** and four named lighting layers. The campus includes an atrium and three physically connected lab shells, a graded clearing, and a truck turnaround. It shares the existing player and Main scene.
- The `GroundOpsSceneBuilder.SkunkWorks`, `.SkunkWorksRoad`, `.Helios`, `.VectorGarden`, and `.Horizon` partials own the generated layout and equipment. Normal facility synchronization preserves stable object paths. Common desks, chairs, and glazing remain linked prefab instances.
- The three experiment controllers own their inputs and animation; `SkunkWorksCommissioning` supplies progress, notes, and the hall displays. Motion uses the scaled frame clock. Field lines reuse point arrays, the stellar window is one procedural shader, and no new reflection camera is needed.
- Lab rendering layers isolate task lighting. Exterior fixtures cast budgeted shadows, and local camera volumes provide bloom and tone mapping. The original chamber's darkness and its lighting zones remain independent.
- `CodexAutomationAudio` temporarily mutes bridge-owned Editor Play/test sessions and restores the prior setting across domain reloads. It never changes player audio sources or standalone sound.

## Verification and photographs

`SkunkWorksJourneyTests` rides both directions, checks the parked destination switch, walks every wing and doorway, verifies floor support, and returns to the original destination. `SkunkWorksCommissioningTests` uses real Input System events for the complete commissioning sequence, interlocks, coupled anchors, interruption, pause, zoom, terminal inspection, standing transitions, and replay. It also checks partially visible field animation, iris clearance, and gradual probe deployment. The existing facility, archive, null lab, and real-pointer pause tests remain in the suite.

[The screenshot gallery](screenshots/README.md) contains sixteen actual 1920 by 1080 campus views. To reproduce it, enter a fresh Main Play session through the bridge and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity-bridge.ps1 capture_skunk_works -NoWait
```

The photography pass stages only disposable runtime state, lets the equipment animate, captures through the player camera with HDR post processing, and writes sRGB PNGs under `docs/screenshots/`. It omits overlay HUDs and exits Play Mode afterward to restore the scene and time of day. These are live game renders, without image editing.

Validation on 2026-09-04: **14 Play Mode tests passed** with no failures. The original 6,988 serialized scene objects remain present, and their existing transform poses are unchanged from the pre-campus scene. All project assets have their `.meta` files.
