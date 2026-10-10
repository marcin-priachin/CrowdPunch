# Chapter 7 implementation and verification

2026-10-10. Requirements: LOOP-002..008, ROLL-001..007, BARRICADE-006,
TRACK-001..005, COVER-001..005, SHELL-001..006, GROUND-001..007,
ENEMY-009/011/014/015, WIZARD-001..007, TRAIL-001..007.

## Playing and tuning

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Continue after Chapter 6
loads level 61. Window > Crowd Punch > Level Play > Campaign offers previews without
changing normal progress. Completing level 70 unlocks Chapter 8 (now installed; see Chapter8.md).
Ten main scenes and ECS SubScenes, 20 wave assets, and separate objective settings live
under `Assets/CrowdPunch/Scenes/Campaign` and `Assets/CrowdPunch/Data/Campaign`.
Shared enemy profiles, runtime mechanics and earlier chapters retain their tuning.

| Level | Encounter | Initial population / objective |
|---|---|---|
| 61 | Stable Footing | 12B+1A > 16B+2R |
| 62 | Side Door | Hold 14B+2X; four-hit 2m northeast gate with sealed, recessed sides; two periodic strips |
| 63 | Draw a Line | 16B+2T+1W > 20B+2D+2T |
| 64 | Shell Island | Hold 14B plus specialized pair of 2X; four shell explosions; two permanent pockets |
| 65 | Three Approaches | 18B+2A+1E > 20B+2R+2W > 22B+2D+2T; one periodic side strip |
| 66 | Set the Pace | Hold 12B+2X; four-hit cover, 90-degree opening, 25 degrees/s, one-second successful-hit pause |
| 67 | Keep It Moving | 20B+2R+1E > 22B+2A+2D; two periodic island crossings |
| 68 | Shifting Front | 68 > 86 > 94 > 102 > 118 > 138; 100 x 100m wave-clear arena; three periodic outer strips |
| 69 | Straighten Up | Hold 10B+2X; straight rail, five net forward hits |
| 70 | Rolling Pressure | Hold 8B+2X; 600 health; physical reflection; two periodic outer pockets |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite,
W = Wizard, T = Trail. Level 68's exact compositions are 64B+4R, 80B+6X, 88B+4A+2E,
96B+4D+2T, 112B+4R+2W, and 128B+4A+6X. Its 606 initial enemies include two Elites;
existing ammunition safeguards can add more. It has no timer. Every finite wave spawns
together, three seconds after the previous wave clears. Wizard waves retain their
persistent-zone completion gate; Trails alone do not. Objective support replenishes
after four seconds; the shell's specialized pair follows its existing replacement rule.
Permanent patches measure 3 x 6m, periodic strips 3 x 8m with 4/1.5/2.5s
inactive/warning/active timing. Pairs have four-second offsets; the triple uses 0, 8/3,
16/3 seconds. The rematch retains 16/19/22 roll speeds and uses 2.5/1.8/1.2s pauses.
Chapter 7 has no protected-zone encounter; the global exclusion of Elites still applies.

## Verification

- Unity compilation and the single-worker .NET build passed with zero errors and
  331 existing reference warnings.
- The initial creation request timed out after saving the scenes. Campaign registration
  was completed separately, and the recipe now reloads the catalog before its final save.
- An initial scene test found level 65's periodic strip touching the east spawn safety
  margin. Moving it one metre west preserved the optional side route and cleared the bank.
- All 33 campaign tests passed, covering exact populations, safe navigation anchors,
  hazard/spawn separation, the offset gate's full-width seal and front clearance,
  shell/cover/rail settings, save/unlock boundaries and unchanged legacy boss geometry.
- Shared-mechanic suites passed: Rolling Blob 31, rotating cover 10, shell 5, ground
  hazards 32, protected points 17. The protected-point suite verifies Elite exclusion
  in every authored protected-zone scene.
- The final five Chapter 7 tests passed after correcting floor triangulation. The old
  vertex fan produced overlapping/reversed triangles on stepped and notched outlines;
  ear clipping now keeps visible top faces upward and matches the exact polygon area.
  Only floors 61/64/69 and their nature meshes were rebuilt. Their continuous convex
  collider setting remains unchanged, preserving protection against triangle-edge
  deflections. Direct collider checks covered 1152/1380/1404 interior points with zero
  holes. Convex hull extensions behind the enclosing walls are intentional.
  Evidence: `Temp/CampaignValidation/chapter7-floor-rays.txt`.
- The live lifecycle passed all 20 new waves plus the preceding Chicken boundary,
  player death/retry, hazard phase cycles, the four-explosion shell setting, physical
  rail destination arrival, chapter completion, save/reload, replay/retry and unavailable
  Chapter 8 handling. It uses an isolated temporary save and injected defeats/objective
  completion; the normal save is untouched. Evidence: `Temp/CampaignValidation/chapter7-lifecycle.txt`.
- A live cover replay injected a successful-hit sequence after wave initialization.
  At .25s the angle was unchanged with .75s pause remaining; at 1.25s rotation had
  resumed. An earlier probe injected before initialization and had its state reset;
  the corrected probe passed both assertions. Runtime RollingTuning confirmed 600
  health, Reflect aim, 2.5/1.8/1.2s pauses and 16/19/22 speeds. Evidence:
  `Temp/CampaignValidation/chapter7-cover-live.txt` and `chapter7-objectives.txt`.
- Ten-second Editor samples in level 68 used a healed stationary player without
  combat input. Wave 3 (94 initial enemies, 62 surviving at sample end) recorded 598
  main-thread samples: 16.686ms median, 27.634ms maximum; Elite punch/support medians
  .890/1.584ms. Wave 6 (138 initial, 55 surviving) recorded 573 samples: 16.732ms median,
  32.793ms maximum; Wizard collision-job median .181ms. Both advanced 10.03 game seconds
  at time scale 1. All 178/551 hidden pooled roots at the respective snapshots were
  outside the actual physics world; eight visible landing bodies in the first snapshot
  were counted separately. ECS PhysicsSimulationGroup scheduling medians .142/.181ms
  do not measure total worker cost. Evidence: `Temp/CampaignValidation/chapter7-profile.txt`.
  These captures include Editor overhead and natural enemy casualties; they are not
  target-device or worst-case combat benchmarks.
- Screenshots were inspected for the offset gate, shell pockets, six-wave arena and
  boss edge hazards. No gameplay runtime errors occurred. One safe-placement retry in
  level 65 recovered to its full 26-enemy wave; the injected death emitted the existing
  inactive Animator warning. The repaired Shell Island floor was also loaded in a
  separate Editor preview.
- Build registration now saves all 71 enabled scenes (Bootstrap plus levels 1-70),
  including Chapter 6's previously missing serialized build entries. New assets have
  Unity metadata, the diff passes whitespace checks, and Unity is stopped on Bootstrap.

## Remaining playtesting

Human clear times, level 68's 4-5 minute target, the Rolling Blob rematch's 3-4 minute
target, difficulty, and target-device performance require playtesting. OQ-001 hardware
and FPS acceptance remain unresolved. Controlled Editor probes do not establish these.
