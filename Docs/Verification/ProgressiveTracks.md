# Progressive knock-into-place tracks

2026-10-11. Requirements: TRACK-001..006, LOOP-002/006, VISION-004.

## Implemented routes

| Campaign level | Route length | 10m legs | Turns | Required net forward hits | Court |
|---|---:|---:|---:|---:|---|
| 16 | 10m | 1 | 0 | 5 | Original 32 x 38m introduction |
| 19 | 20m | 2 | 1 | 10 | 34 x 40m, first turn left |
| 26 | 30m | 3 | 2 | 15 | 34 x 42m, first turn right |
| 32 | 40m | 4 | 3 | 20 | 42 x 42m, first turn left |
| 39 | 50m | 5 | 4 | 25 | 42 x 52m, first turn right |
| 46 | 60m | 6 | 5 | 30 | 52 x 52m, first turn left |
| 54 | 70m | 7 | 6 | 35 | 52 x 62m, first turn right |
| 69 | 80m | 8 | 7 | 40 | 62 x 62m, first turn left |

Each later route adds one original 10m length. Turns alternate left/right along each
mirrored staircase route. The original 2m step remains, so each added length adds five
net forward hits. The request is implemented as bends within the route.
These corner/step choices are implementation tuning and remain open to playtest feedback.

Each scene has a larger clipped court, room around the entire route, a repositioned entry,
updated navigation anchor/bounds, continuous paired rail joints, directional arrows and a
socket facing the final leg. Existing support population/composition and replenishment are
preserved. The level 26 permanent patch and level 54 periodic strips occupy outer pockets
clear of all shooting setups and spawn banks. The solid block retains its original rotation.
Legacy track gauntlet assets and the level 16 introduction were not regenerated.

At a corner, a hit along the outgoing leg advances; a reverse hit along the incoming leg
moves backward. Smooth motion, immediate retargeting, deduplicated impact/blast hits,
character pushing, launch rebound and physical-arrival completion remain active.
Queued progress visits every corner through physics and cannot cut diagonally across it
or lock at an intermediate corner. Retry resets motion and hit history.

## Verification

- Unity compilation passed. The single-worker .NET build passed with zero errors and
  344 existing reference/deprecation/unused-field warnings; no warnings named changed
  track runtime files. Evidence: `Temp/TrackValidation/progressive-build.txt`.
- All 26 track-filter tests passed. Track mechanic tests cover forward/reverse local directions at both left and right
  corners, impact/blast deduplication, perpendicular rejection, queued movement through
  corners without premature locking, reverse travel across a vertex and outgoing-leg
  character displacement. Existing eligibility, straight motion, navigation and arrival
  checks remain passing.
- All 40 campaign tests passed after replacing obsolete straight-rail assumptions.
  Scene checks verify all eight route lengths/hit counts, every 10m segment and turn,
  safe navigation anchors, floor enclosure/clearance, hazard and spawn-bank separation,
  unchanged support counts, visible materials and socket references/orientation.
  The final rail-joint check also verifies both lines meet at every corner.
- All 12 barricade tests passed, covering the shared impact path used by tracks.
- The live baked-world probe passed all eight routes. It verified advancing player-loop
  frames, exact baked route buffers/tuning, actual launched-body contact and rebound,
  2m forward movement, physical reverse hits, outgoing-leg impacts at corners, reversal
  back onto the incoming leg, rail-following queued finishing motion, physical socket
  completion with bounded surviving support, replay progress preservation and retry
  resetting start pose/hit history. An isolated temporary save, frozen/parked crowds and
  injected launches/retargets were used; normal campaign saves were untouched. This is
  controlled runtime verification, not a human playtest. Evidence:
  `Temp/TrackValidation/progressive-live.txt` and `route-16.png` through `route-69.png`.
- Thirty warm forced navigation footprint rebuilds across the entire level 54 route
  (16 support roots) averaged .550ms, maximum .617ms. Level 69 (12 roots, larger court)
  averaged .664ms, maximum .697ms. These Editor stopwatch samples include the route's
  different positions and turns; they are not whole-frame or target-device benchmarks.
- Game views were inspected for the first bend and the complete 80m staircase. After
  joining visible rail endpoints, the saved scenes and complete-route Scene view were checked
  again; evidence is `Temp/TrackValidation/route-69-joints.png`. No gameplay
  runtime or baking errors occurred. The scene-test fixture originally failed to restore
  an empty Editor scene setup; its cleanup was corrected and the route check passed.

- Unity is stopped on Bootstrap with no dirty scene. New assets have metadata, scene IDs
  are preserved, and the final diff passes whitespace checks.

## Reauthoring and remaining playtesting

`Crowd Punch > Campaign > Apply Progressive Track Routes` reapplies the existing-scene
layout recipe while preserving scene/asset IDs. Chapter builders invoke that same recipe
for fresh track levels. Existing custom edits inside those seven layouts would be replaced
by reapplying the authoring command; normal play does not run it.

The longer objectives need human difficulty and clear-time playtesting. The former short
breather targets do not establish timings for the 20-80m routes and 10-40 net hits. OQ-001
hardware/FPS acceptance remains unresolved. No claim of target-device performance is made.
