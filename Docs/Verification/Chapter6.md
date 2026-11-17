# Chapter 6 implementation and verification

2026-10-10, revised 2026-11-17. Requirements: LOOP-002..011, CHICKEN-001..006, PROTECT-001..004,
BARRICADE-006, TRACK-001..005, COVER-001..005, SHELL-001..006, GROUND-001..007,
ENEMY-009/011/014/015, WIZARD-001..007, TRAIL-001..007.

## Chapter 6 variety revision

The current wave recipes and timing are in
[Campaign80Proposal.md](../Design/Campaign80Proposal.md#chapter-6-deliberate-reactions).
The revision adds shooters to levels 53/54, changes reinforcement timing on
55/57/58/59 and adds periodic warning strips to 54/55/57/58. With clear spawn
areas, Moving Batteries schedules 41 batches and reaches the last batch
around 240s plus time spent clearing the Elite checkpoint.

Unity compilation and the .NET build passed. The targeted suites passed 182
checks, including every revised Chapter 6 scene, exact wave recipes, spawn
and obstacle clearance, the track route, Dragon and the three shooters.
Two existing assertions fail. Level 56's saved gates require three hits and
the room test expects four. The level 60 arena snapshot points to a Canyon
baked mesh where the legacy snapshot expects a Gauntlet mesh path.

The isolated Editor preview observed active Dragon and Sporella together at
55, followed by Alien Spike while first-phase enemies remained. Level 57
brought in a timed defense group while first-group attackers were still active.
Moving Batteries held its midpoint with 110 active enemies and advanced after
controlled defeats. Its final clearance held with 35 earlier-phase survivors
and completed after the remaining crowd was cleared. The accelerated preview
recorded 358 Editor frames with at least 64 live bodies, peaking at 111. Median
frame time was 28.57ms and the 95th percentile was 125.67ms, including Editor
overhead, accelerated spawning and the observer. Normal clear times and
target-device performance remain for playtesting.

The level 54 follow-up moved its three strips into the shot setup lanes for
turns 2, 3 and 6. The level-specific and campaign track tests confirm clearance
for the moving carrier, corners and spawn banks, and check that each strip
intersects a direct approach to an outgoing leg.

## Original chapter release (2026-10-10)

The timings, populations and measurements below describe the original release.

## Playing and tuning

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Continue after Chapter 5
loads level 51. Window > Crowd Punch > Level Play > Campaign offers previews without
changing normal progress. Finishing level 60 unlocks the future, unavailable Chapter 7.
Ten main scenes and ECS SubScenes, 20 wave assets, and separate objective settings live
under `Assets/CrowdPunch/Scenes/Campaign` and `Assets/CrowdPunch/Data/Campaign`.
The creation recipe refuses to overwrite saved encounters. Existing stable-ID saves
need no migration. Shared enemy profiles and runtime mechanics remain unchanged.

| Level | Encounter | Initial population / objective |
|---|---|---|
| 51 | Fresh Fuse | Hold 10B plus specialized pair of 2X; three shell explosions, then exposed core |
| 52 | Crossing Paths | 16B+2D+1T > 20B+2R+2T |
| 53 | Reverse the Window | Hold 14B+2X; four hits through 90-degree opening; 30 degrees/s, reversal every 5s |
| 54 | Push Under Pressure | Hold 14B+2X; six net forward hits on a diagonal rail; two periodic side crossings |
| 55 | Disrupt the Casters | 14B+2W > 18B+2R+2W > 18B+2A+1W; Wizard and armor safeguards |
| 56 | Clean Break | Hold 12B+2X; four-hit 2m gate with recessed, sealed sides |
| 57 | Long Watch | 20B+2R > 24B+2D > 24B+2X; finite defense, no Elites |
| 58 | Moving Batteries | 77 > 96 > 102 > 118 > 138; 100 x 100m wave-clear arena |
| 59 | Open Air | 12B+2X > 16B+2R |
| 60 | Chicken Crossfire | Hold 8B+2X; 2100 health; movement-leading shots; two periodic edge sectors |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite,
W = Wizard, T = Trail. Level 58's exact compositions are 72B+4R+1E, 88B+6X+2D,
96B+4A+2E, 112B+4R+2W, and 128B+6X+4D. Its 531 initial enemies include three
Elites; existing Elite, armor and Wizard supplies can add more. There is no timer.
Defense has four-enemy batches every three seconds, five-second cleared-wave pauses,
no replenishment and first-breach failure. Periodic hazards in 54/58/60 use 3 x 8m
rectangles, four-second stagger and 4/1.5/2.5s inactive/warning/active timing. Spawn banks
and rail endpoints stay clear of the patches. Chicken keeps .8s wind-up, 1.5s shot spacing
and its original single/paired/paired stage patterns. Boss arena geometry is preserved.

## Verification

- Unity compilation passed. The single-worker .NET build passed with zero errors and
  331 existing reference warnings.
- All 29 campaign tests passed, including Chapter 6's exact populations, navigation
  clearance, objective settings, hazard/spawn separation, gate front clearance,
  rail/socket wiring, save/unlock boundaries and unchanged legacy Chicken geometry.
- Related tests passed: protected-point 17, Chicken 24, track 20, cover 10 and ground 32.
  The protected-point suite scans authored protected-zone levels for Elite entries.
- Live ten-second Editor captures held the player alive without combat input. Level 58
  wave 3 (102 initial enemies) recorded 596 samples: 16.625ms median main-thread frame,
  31.476ms maximum; Elite punch median 1.024ms and support median 2.309ms. Wave 5 (138
  initial enemies) recorded 599 samples: 16.677ms median, 21.451ms maximum; Wizard
  collision-job median .192ms. Both advanced 10.02 game seconds at time scale 1.
  All 185/400 hidden pooled roots at the respective snapshots were outside the actual
  physics world. The latter snapshot also had three visible landing bodies, counted
  separately. These are Editor samples with natural enemy casualties, not target-device
  or worst-case combat benchmarks. PhysicsSimulationGroup scheduling medians were
  .115/.187ms; these do not measure total worker cost. The BuildPhysicsWorld recorder
  returned zero and is not used as performance evidence.
  Evidence: `Temp/CampaignValidation/chapter6-profile.txt`.
- The live lifecycle passed all 20 new waves plus the preceding Gatekeeper boundary,
  player death/retry, first-breach failure/retry, partial defense batches, periodic
  hazard phases, physical six-hit track destination arrival, chapter completion,
  save/reload, replay/retry and unavailable Chapter 7 handling. It uses an isolated
  temporary save and injected defeats/objective completion; the normal save is
  untouched. Evidence: `Temp/CampaignValidation/chapter6-lifecycle.txt`.
- Runtime screenshots were inspected for the crossing layout, reversing opening,
  diagonal rail/socket and hazard separation, gate/terrain front clearance, large
  arena and Chicken's clear center with edge hazards. No runtime errors occurred.
  Level 52's second wave briefly deferred two safe placements and retried successfully,
  reaching all 24 enemies. The injected player death emitted the existing inactive
  Animator warning from PlayerHitAnimation.CancelReaction. Neither prevented progress.
- All 61 enabled build scenes (Bootstrap plus levels 1-60) are registered. New assets
  have their Unity metadata, and the diff passes whitespace checks. Unity is left
  stopped on Bootstrap.

## Remaining playtesting

Human clear times, level 58's 4-5 minute target, the Chicken rematch's 3-4 minute target,
encounter difficulty, and target-device performance require playtesting. OQ-001 hardware
and FPS acceptance remain unresolved. Controlled Editor probes do not establish these.
