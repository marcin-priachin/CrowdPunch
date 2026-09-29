# Knock Into Place validation

Date: 2026-09-29. Requirements: TRACK-001..005, PLAYER-003/004, LOOP-006.

## Implemented

Gauntlet_16 is registered after Gauntlet_15. One kinematic block slides along a 10m bidirectional
rail in a 32 x 34m open arena. Five net forward hits are required by default, with 0.35-second
smooth slides. Hits retarget immediately, excess endpoint hits are discarded, and only physical
arrival locks the block and completes the level. Body impacts and explosions share launch
identity; direct punches do nothing. Shared targeting, rebound, completion and replenishment
remain in their existing systems. Current geometry feeds navigation and safe spawn placement.

Tuning lives in `Assets/CrowdPunch/Data/Settings/TrackObjectSettings.asset` and
`TrackSolidSettings.asset`. The two `CP16` wave assets under `Data/Settings/Waves/Progression`
provide 12 Baselines after two seconds and two Explosives after ten seconds, capped at 14 roots.
Use **Crowd Punch > Levels > Build Track Gauntlet 16** to reapply the authored recipe.

## Verification

- Unity 6000.3.10f1 compiles the implementation and bakes the saved scene without gameplay errors.
- `dotnet build Assembly-CSharp.csproj --no-restore` passes with zero errors. Existing Unity
  package/reference/deprecated-aspect warnings remain; the final build reported 381 warnings.
- 17 `TrackObjectTests` cover directions, shallow/perpendicular hits, impact/blast deduplication,
  relaunch, owner filters and provisional alternatives, direct-punch immunity, clamping, reversal,
  exact stops, locking, survivor-independent completion, navigation footprint replacement/cleanup,
  player/enemy displacement and optional once-per-slide damage including solver-separated bodies.
- 21 `GauntletProgressionTests` pass, including both staged wave sequences and all authored levels.
- Shared objective regressions pass: 12 `BarricadeTests`, 10 `RotatingCoverTests`, 5 `ShellTargetTests`.
- `CrowdPunch.Editor.TrackPlayCheck.Start()` runs a repeatable controlled Play Mode check through
  the actual baked world. It verifies initial Baseline-only allocation, existing aim eligibility,
  actual launched-body impact and rebound, 2m motion, updated player collision, player displacement,
  relaunch, backward impacts, delayed Explosives, actual explosive impact/blast deduplication,
  bounded pooled replenishment, physical socket completion with survivors and fresh restart.
  Results are written to `Temp/TrackValidation/playcheck.txt`; the final run reached `COMPLETE`.
  Positions/velocities are injected for repeatability; this is not an ordinary-input balance test.
- At 14 enemy roots, 30 forced navigation footprint rebuilds measured **0.234ms mean / 0.242ms max**
  after Burst compilation. Managed fallback during asynchronous Editor Burst compilation measured
  roughly 17ms. A disabled Profiler capture initially returned zero and was discarded; these final
  timings use a Stopwatch around the real system. They exclude whole-frame cost and player-build performance.

The Editor also reports an existing Synty `ToolDownloader` window error on entering Play Mode.
An existing inactive-Animator warning appeared when the restored crowd killed the idle player
during the separate warm profiling observation; it did not fail the controlled objective run.
The validation harness was corrected to observe rebounds before later arena-wall contact, use the
player movement bridge for injected placements, and wait for completion's ECS-to-Mono handoff.
Those intermediate harness assertions are not unresolved gameplay failures.

## Provisional details and limits

No accepted core rule was intentionally changed. The conversation's unresolved implementation
edge cases remain explicit settings: all explosions count by default even with the player-only
body filter; only destination-changing hits spend eligibility; optional push damage affects player
and enemies once per continuous slide for one damage. Damage is off by default. Alternatives and
their status are recorded in `Docs/Design/OpenQuestions.md`, not promoted to accepted requirements.

Numerical perpendicular tolerance is 0.00001 on a normalized planar direction. Centered explosions
have no direction. Displacement checks both sides and both ends; the authored arena guarantees
open space around the track. Arbitrarily enclosed custom tracks need separate validation. Optional
push damage follows existing enemy armor/health and player invulnerability rules. Normal movement
still uses physics velocity; lateral position corrections only resolve moving-obstacle penetration.

## Playtest

1. Select **16 Knock Into Place**. Confirm the rail, forward arrows, block and destination read
   clearly from the normal camera, including the socket's green locked appearance.
2. Compare shallow and straight shots, opposite-direction hits during a slide, repeated hits,
   endpoint overflow and a final-step reversal before docking. Watch preview/launch agreement.
3. Try player movement and dash around both sides and in front of the moving block. Check crowd
   routing, body rebounds, pinching and displacement with several enemies near the rail.
4. Check the Baseline-only opening, Explosive introduction and replenishment pacing. An unlaunched
   explosion should move the block; an Explosive impact plus blast should move it only once.
5. Try optional player-only ownership and damaging pushes. Review the provisional edge-case defaults
   and tune slide duration, crowd timing and damage. Restart after partial progress and after winning.

Final art/audio, encounter balance, controller feel and representative whole-game crowd targets
remain playtesting/future work. No additional objectives, hit counter or survivor-cleanup phase was added.
