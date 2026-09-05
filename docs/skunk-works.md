# Level 02: Space Science Center Skunk Works

Skunk Works is a fictional prototype campus reached by truck in the same continuous Main scene. Its First Light commissioning sequence will link three experiments in a bright, futuristic building.

| Location | Commissioning goal | Visual identity |
| --- | --- | --- |
| Helios Forge | Stabilize the prototype source and certify its output | Amber plasma, moving magnetic rings, titanium machinery |
| Vector Garden | Set three levitation anchors to form a stable field | Floating crystals, mint light, suspended geometric structures |
| Horizon Engine | Align the experimental aperture and capture first light | A large mechanical iris, violet rings, a luminous stellar window |

## Current milestone

The Editor automation audio fix, truck destination selector, approach road, arrival terrace, commissioning hall, and three walkable lab wings are complete. All 12 Play Mode tests pass, including the new complete trip and campus walkthrough. The three experiments are the next milestones.

At the DOC truck stop, press F to board. Choose **1** for Antennas or **2** for Skunk Works, then press **W once** to depart. At Skunk Works, F exits onto the terrace. Board again and press W to return to the Space Science Center. The existing antenna journey retains its original default destination and controls.

## Implementation

`SkunkWorksLayout` centralizes the campus placement and lighting zones. The `GroundOpsSceneBuilder.SkunkWorks` and `.SkunkWorksRoad` partial files own its generated geometry and transport wiring. The campus is placed at Ground Ops local `(-78, 7.8, -48)`; the new road branches from the shared DOC roundabout junction and has its own forward turnaround on the arrival terrace.

`SkunkWorksJourneyTests` uses actual Input System events to select the destination, ride, walk the three wings, return, and restore the original truck destination. Bridge automation mutes Editor audio for these runs and restores its previous setting afterward.

## Helios milestone

The source experiment is playable. Match phase 126 degrees and containment 0.680 with A/D and W/S; Shift provides fine control. Hold Space for three stable seconds to certify the source. Releasing Space, drifting out of tolerance, or leaving the seat interrupts the capture. Certification persists for this session.

All 13 Play Mode tests pass, including real keyboard tuning, interrupted certification, zoom/FOV restoration, and pause-safe animation. `screenshots/skunk-works-helios.png` is a live render from that successful test. Vector Garden and Horizon are still in development.

## Vector milestone

The Garden now unlocks from the Helios power bus. Its three floating masses display the anchor heights directly. F advances the nearby anchor and its clockwise neighbor; each wraps through four levels. Matching A 2 / B 1 / C 3 and allowing 2.5 seconds to settle certifies the field.

All 13 tests pass with the commissioning test extended to physically walk between anchors, verify coupling and wraparound, reject input while paused, solve the field, and preserve its certification. The live certified Garden view is `screenshots/skunk-works-vector.png`.
