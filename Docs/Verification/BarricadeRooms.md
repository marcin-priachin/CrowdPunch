# Progressive barricade room chains

2026-10-11. Requirements: BARRICADE-001..007, LOOP-002/004/006, GROUND-001..007,
COMBAT-017, VISION-004.

## Implemented levels

| Level | Rooms / gates | Original room size | Total footprint | Hits per gate | Crowd per entered room |
|---|---:|---|---|---:|---|
| 13 | 1 | 30 x 42m | Original layout | 3 | 10B+2X |
| 22 | 2 | 30 x 40m | 30 x 80m | 3 | 12B+2X |
| 34 | 3 | 32 x 44m | 32 x 132m | 4 | 12B+2X |
| 42 | 4 | 34 x 44m | 34 x 176m | 4 | 14B+2X |
| 56 | 5 | 32 x 42m | 32 x 210m | 4 | 12B+2X |
| 62 | 6 | 36 x 44m | 36 x 264m | 4 | 14B+2X |
| 74 | 7 | 38 x 46m | 38 x 322m | 4 | 16B+2X |

B = Baseline, X = Exploder. Each successive barricade level adds one room-length segment.
Every later room copies its level's original rocks, spawn rectangles and hazard pattern.
Widths are unchanged; a continuous floor/perimeter and translated bounds enclose the chain.
Every 2m gate has sealed sides behind its exposed front. Level 62 retains northeast x=8
openings. Levels 4/6/13 and legacy gauntlet assets were not regenerated.

A room activates only after its predecessor gate breaks and the player enters. Its original
three-second spawn delay then starts. Activation persists on backtracking. Its bounded
crowd replenishes in the room's own spawn banks until its own gate breaks, after which
survivors stay hostile and supply stops. Every gate must be broken, and the player must
reach the one exit beyond the last gate. There are no intermediate exits or room checkpoints.
Retry restores the entire level. The existing opening hint explains this through the same HUD.

Permanent/periodic hazard timing and placement relative to each copied room are preserved:
level 34 has three periodic patches in total; 42 has eight permanent pockets; 62 has twelve
periodic strips; 74 has fourteen permanent pockets. Hazard copies reference their own room
sequence. Only the original owner carries the shared hazard policy. Separate navigation
settings extend bounded path capacity for long halls and align normal-size grid paths with
the narrow openings. Ordinary enemy profiles, navigation search budgets and collider mass
settings are unchanged. The campaign now contains 176 wave assets and still 81 build scenes.

## Verification

- Unity compilation passed. The single-worker .NET build passed with zero errors and
  344 existing reference/deprecation/unused-field warnings. Evidence:
  `Temp/BarricadeRoomValidation/build.txt`.
- All 19 barricade-filter tests passed, including both entity orders for the early-exit
  case, predecessor/entry activation, deferred spawn clocks, independent replenishment
  shutdown, per-gate feedback deduplication and the seven campaign layouts.
- All 41 campaign tests passed. Updated scene checks cover every seal's width/front
  clearance, all original room lengths and gate spacing, only one final exit, correct
  owners/predecessors/hazard policies, stable support counts, hazard/spawn separation,
  floor extents and normal-size connectivity to every room's spawn banks.
- The live baked-world probe passed all 28 distinct gates across the seven levels. It
  tested actual launched-body edge shots at x +/- .65m, three/four counted hits, centred
  destroying-hit pass-through, immediate per-room supply shutdown, later rooms dormant
  until entry, final-exit bypass rejection, completion only after every gate and final
  exit, and replay save preservation. Level 22 also verified a defeated body returns in
  its original room through the same bounded slot. Mid-chain retries in levels 22/74
  restored every gate, hit/feedback history and dormant future rooms. The original level
  13 passed the same single-gate behavior checks.
- The probe uses an isolated temporary save, parked/healed crowds, injected launches and
  player placement. Normal campaign saves are untouched. It is controlled verification,
  not a human combat/duration playtest. An initial pass-through assertion sampled too
  late and caught a later rear-wall bounce; the corrected probe observes forward passage
  immediately after the destroying hit. Evidence: `Temp/BarricadeRoomValidation/live.txt`.
- In level 74's final room, a controlled Editor sample captured 351 main-thread frames:
  16.639ms median / 24.331ms maximum. There were 126 owned slots and 70 nonpooled enemies
  at the end; permanent hazards caused natural casualties among the parked actors.
  EnemyNavigationSystem measured .185ms median / .515ms maximum, and
  GroundHazardNavigationSystem .297ms / .915ms. Spawn/impact system markers returned zero
  resolution in this recording and are not used as cost estimates. These samples include
  Editor overhead and controlled input; they do not establish target-device or worst-case
  combat performance.
- Shared objective tests passed: TrackObjectTests 22, shell 5 and rotating cover 10.
  A settled level 74 game view was inspected with the player, sealed gates, repeated room
  obstacles and hazards visible. Evidence: `Temp/BarricadeRoomValidation/level-74-settled.png`.
  Some probe screenshots were queued just before a player teleport and therefore show a
  moving camera; the settled capture avoids that timing artifact.
- No gameplay runtime or baking errors occurred. Stopping the healed-player probe emitted
  the existing inactive Animator warning. New assets have Unity metadata, scene/asset IDs
  and 81 build-scene entries are preserved, and the final diff passes whitespace checks.
  Unity is stopped on Bootstrap with no dirty scene.

## Remaining playtesting

The longer chains need human pacing/difficulty, sustained ammunition access, old-room enemy
pursuit and controller aiming checks. OQ-001 hardware/FPS acceptance remains unresolved.
`Crowd Punch > Campaign > Apply Progressive Barricade Rooms` can reapply the in-place recipe;
chapter builders use the same recipe for fresh levels. Reapplying replaces custom geometry
inside the six extended layouts while retaining scene/asset identities. Normal play does
not run that authoring command.
