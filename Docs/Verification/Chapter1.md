# Chapter 1 implementation and verification

## 2026-11-16 encounter revision

Requirements: LOOP-011, LOOP-002/006/007/008, MUSHROOM-001..003, SCORE-001.
The approved campaign revision is implemented for Chapter 1 only. Chapters 2-9 are
documented future work in `Docs/Design/Campaign80Proposal.md`; their assets are unchanged.

### Current authored content

- Mushroom Shooter introduction at 5: 6B+1M, then 6B+2R+1M after a full clear.
- Level 8: 28B+8R+4M, 42B+12R+6M, 52B+16R+8M, 64B+20R+10M. The 270 total now
  contains 186 Baselines (68.9%), 56 Ranged and 28 Mushroom Shooters.
- Level 9: 6B+2R, then 6B+2R+1M; 17 total and exactly one Mushroom Shooter.
- Levels 2/8/9 advance intermediate phases on time; their final gate includes
  survivors from every phase. Levels 1/3/5 keep introductory clear checkpoints.
- Initial delay 3s; batches of 4 every 6s, or 8 every 6s at level 8. Timed phases
  wait 6s after their final successful batch and have no next-phase pre-delay.
  Clear-gated phases retain a 3s pause. Safe-placement rejection can delay spawning.
- Objective/boss support counts at 4/6/7/10 and per-body 4s replacement are retained;
  their initial populations now enter in batches of 4 every 6s. No static hazards.
- All 19 wave GUIDs and spawn rectangles are retained. The only scene edits are
  opening hints at 2/5/8/9. No catalog IDs, save files, shared enemy profiles,
  legacy assets, geometry, materials, lighting or later chapter content change.

Reapply the authored recipes with **Crowd Punch > Campaign > Apply Chapter One
Encounter Revision**. This explicit command overwrites Chapter 1 encounter tuning
after validating existing scene/wave wiring, and preserves the authored spawn regions.
Use Level Play's Campaign tab or the normal campaign to play the revised levels.

### Revision verification

- Unity 6000.3.10f1 compilation passed, including the Editor authoring/probe scripts.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with 0 errors and
  309 existing package/reference/deprecation warnings. The initial sandboxed attempt
  could not read the installed SDK directory; the permitted build completed outside it.
- All 9 `CampaignTests` passed: saved progression/replay, scene references, objective
  and boss geometry, campaign-owned settings, 81 installed IDs and level 8's 270
  enemies/100 x 100m footprint.
- All 11 `MushroomShooterTests` passed: authored prefab profile, locked warning/cover,
  hostile beam damage, player launch beam, piercing/ownership behavior, launch
  interruption and recovery cooldown.
- `ChapterOneReinforcementCheck` uses a unique save under `Temp/ChapterOneRevision`,
  injected enemy/objective/boss defeats and shortened waits. Its retained log records
  the distinction from normal gameplay; it does not modify campaign save progress.
- Controlled checks completed all ten encounters and exercised all 19 phase
  definitions, including a retry of level 5 with an active, fully healthy baked
  Mushroom Shooter. Levels 2/8 recorded natural first batch intervals of 6.00/6.01s.
  Earlier survivors remained through timed reinforcement and blocked final completion
  at 2/8/9 until explicitly defeated. Objective/boss completion at 4/6/7/10 remained
  independent of clearing their support populations.
- The first probe stopped at 9 because its overlap assertion required the earlier
  enemy to be visible exactly on the phase boundary. Accelerated clocks can advance
  while an earlier safe-spawn warning is still pending. The probe now checks ownership
  after activation and moves its stationary player off blocked reservations. Levels
  5-10 then passed on rerun; no runtime gameplay change was needed for this probe issue.
- Two short level-8 samples reached 99/100 mixed enemy roots with median Editor frame
  times of 29.15/21.65ms. These include rendering and tool overhead, use accelerated
  spawning, and are not target-hardware acceptance or maximum-overlap measurements.
  The level-5 screenshot was visually checked with Mushroom Shooter and Baselines
  present. Local evidence: `Temp/ChapterOneRevision/live-check.txt`, `level05.png`,
  `level08.png`. No gameplay exceptions were observed; the initial probe assertion
  is retained in its log. Unity was returned to Edit mode with Bootstrap open.

### Remaining balance work

Human combat playtesting is still needed for Shooter readability, overlapping pressure,
fast-clear empty intervals and the 3.5-4.5 minute level 8 target. The unobstructed
authored schedule delivers its last batch around 207s plus fixed-step overhead; this
does not establish a measured clear duration. OQ-001 target hardware/FPS remains open.
No standalone player build is claimed. The historical evidence below describes the
original campaign implementation, not the current composition or current build roster.

## Original implementation evidence (historical)

2026-10-10. Requirements: LOOP-002..008, PLAYER-001/003/004/005/010,
VISION-001..005, BARRICADE-001..005, COVER-001..005, BOSS-001..007.

## Play and tune

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and enter Play for the normal campaign
main menu and local save. Use **Window > Crowd Punch > Level Play > Campaign** to
preview any of the ten implemented encounters without modifying the normal save.
The Legacy tab still launches all 23 original encounters outside build registration.

Scenes: `Assets/CrowdPunch/Scenes/Campaign/`. Each main scene has its own ECS SubScene.
Catalog, 19 wave assets, floor/nature meshes and objective/boss settings:
`Assets/CrowdPunch/Data/Campaign/`. Edit these saved assets directly; the initial
creation recipe deliberately refuses to overwrite an existing campaign catalog.

| Level | Encounter | Main objective / initial population |
|---|---|---|
| 1 | First Line | Clear 3, 6, 8 Baselines |
| 2 | Choose the Angle | Clear 8, 12 Baselines around a rock island |
| 3 | Across the Court | Clear 6B+1R, 10B+2R |
| 4 | Break Through | Three-hit barricade, reach exit; bounded 8B supply |
| 5 | Crossfire | Clear 12B+2R, 16B+3R |
| 6 | Open the Lane | Three-hit barricade around divider; bounded 10B supply |
| 7 | Through the Opening | Three-hit cover target; bounded 8B; 100-degree gap at 20 degrees/s |
| 8 | Changing Sides | 100 x 100m; 40B, 56B+4R, 72B+4R, 88B+6R; 270 total |
| 9 | One Good Shot | Clear 10B, 12B+1R |
| 10 | The Gatekeeper | Existing round arena, bounded 10B; 300 HP; 0.8/0.8/1.0s attack warnings |

B = Baseline, R = Ranged. No ordinary enemy profile changes. Geometry and timings
are authored starting values; the larger wave targets 3.5-4.5 minutes, not a timer.
Chapter 2 unlocks after Gatekeeper, but is clearly unavailable until its scenes exist.

## Verification evidence

- Unity 6000.3.10f1 compilation passed.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` passed with zero errors;
  344 existing package/generated-code/legacy warnings in the initial full build.
- Six `CampaignTests` passed: save/reload, replay preservation, sequential chapter
  unlocks, out-of-order completion, backup recovery, 80-level completion sentinel,
  catalog/scene/settings references and large-wave population/footprint checks.
- 28 `GauntletProgressionTests` passed, including existing legacy layout/spawn data,
  completion, restart and lifetime safeguards. Build registration now expects only
  Bootstrap and ten campaign levels, while verifying all legacy scenes still exist.
- 12 `BarricadeTests`, 10 `RotatingCoverTests` and 27 `BossEncounterTests` passed:
  83 passing tests across the five relevant suites.
- Main, chapter and level menus, and the large arena visually inspected in Play Mode.
  Virtual controller directional/submit input and keyboard Enter navigated the menus.
  Pointer raycast/click dispatch reached the Main Menu button. New Campaign cancel
  preserved ten completions; confirmed reset persisted zero completions and loaded
  level 1 at full health, using only the isolated test save.
- `CampaignLifecycleCheck` is a controlled Editor probe. It installs a unique save
  under `Temp/CampaignValidation`, restores player health, injects ordinary damage,
  and explicitly injects objective/boss completion. Its output and screenshots are
  local verification artifacts, not gameplay behavior or a duration measurement.
- The probe completed all ten encounters and 19 waves, including 94 enemies in
  the last large-arena wave. It verified death/retry, full-health entry, ownership
  cleanup, saved Chapter 2 unlock with unavailable content blocked, replay without
  auto-advance or progress loss, and replay retry. No new Unity runtime errors occurred
  during the completed run. Median Editor frame samples were approximately 16.7ms;
  this is a capped local observation, not a target-hardware performance guarantee.
- Legacy `Gauntlet_13` launched additively outside build registration with campaign
  progression disabled and its normal save unchanged. Attempting to register a legacy
  scene restored the expected 11 campaign build scenes automatically.
- Campaign Editor preview loaded level 8 directly with its opening hint and left
  the normal save unchanged. Unity was left in Edit Mode with Bootstrap open.

## Remaining balance work

Real player clear times, the level 8 greater-than-three-minute target, overall
difficulty and aim/readability need human playtesting. A successful lifecycle probe
does not establish those outcomes. Hardware/FPS acceptance remains OQ-001; Editor
frame samples include tooling overhead and do not substitute for a player-build
performance profile. No standalone player build has been produced in this task.
