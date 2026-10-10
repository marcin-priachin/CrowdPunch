# Chapter 1 implementation and verification

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
