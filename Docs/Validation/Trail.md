# Trail Enemy validation

Date: 2026-10-06. Requirements: TRAIL-001..007, COMBAT-010/011/014/018, PLAYER-003/004/005, LOOP-006.

## Authored content

- `EnemyTrail.prefab` uses the supplied Blob/Fish.fbx, sampled Idle/Walk/Death, disabled root motion,
  instanced skinning materials and contact-producing physics capsule. Baseline health is 30.
- `TrailEnemySettings.asset` contains Both enemy damage, OwnSource immunity and DamagingTrails avoidance.
  Normal width/lifetime/damage: 1.2m / 5s / 4. Launched: 2m / 6s / 8. Tick .75s, player protection .35s,
  circling 8m, orbit/approach/retreat 3 / 3.5 / 4 m/s, reversal 4s. Colors are green and orange.
- Gauntlet_19 Slippery Circuit follows Hold the Line in Bootstrap, selector and Build Settings.
  The compact open court has editable finite 6+1 and 12+2 waves, no Baseline supply, and immediate trail cleanup on full enemy defeat.

## Completed checks

Unity Editor 6000.3.10f1 refreshed and compiled runtime/editor code. `dotnet build Assembly-CSharp.csproj
--no-restore -m:1` passed with zero errors and 344 existing package/obsolete-aspect warnings. The default
parallel project build exited without diagnostics; serial MSBuild completed successfully.

Controlled baked-world `TrailPlayCheck` ran in actual advancing Play mode (frame advancement confirmed):

- Both authored wave compositions, default settings and finite Fish skinning matrices.
- Actual circling, reversal and moving section production.
- Ordinary player punch launches Fish through the shared impulse path.
- Launched ground projection and independent width/damage/ownership, immutable older normal sections.
- Stationary/recovering emission stopped.
- XZ overlap ignores height; immediate and interval hits; adjacent/overlapping normal+launched sections
  share a source clock; exit/reentry retains it; independent source records stack.
- Own/all/no immunity, pooled identity, armor exclusion, actual avoidance intent, launched exemption,
  and no-avoidance setting.
- Lethal trail damage preserves launched physics/deferred defeat and records player kill/chain credit.
  Source death/reuse preserves old sections and ownership. The project has no numeric reward economy;
  ownership and lethal source lifetime/chain depth are retained in EnemyDamageState.
- The dedicated player bridge bypasses ordinary hit protection and delivers zero impulse.
- Real restart clears old clocks/sections; first-wave advancement and final completion clear trails without waiting for expiry.

The same full check passed after replacing repeated physics-candidate lookups with a snapshot/spatial grid.
Game-camera renders were inspected for the Fish model, green normal trail, wider orange launched trail,
and independent fading. Captures: `Temp/TrailValidation/gauntlet19.png` and `gauntlet19-launched.png`.
Logs: `Temp/TrailValidation/playcheck.txt`. States/positions/defeats were deliberately injected and the
player restored; these checks do not establish encounter balance or a human controller playtest.

Unity EditMode regressions passed: 24 GauntletProgression, 29 Wizard, 20 ArmoredEnemy,
1 EnemyLandingLifetime, 7 EnemyPrefabCollisionContact, and 16 TrailInteraction cases (97 total).
Focused Trail cases cover intact armor and its clock, elite damage without force/launch, boss/environment
exclusion, lethal damage without explosive detonation, independent player protection/source stacking,
pooled target clocks, committed Dasher/launched avoidance exemptions, source death and scene-owner cleanup.
The follow-up TRAIL-007 correction adds cases for immediate section/clock cleanup, preserving trails
while enemies remain, ignoring empty spawn-batch gaps, and advancing despite unexpired trails.
The revised baked-world check injects 60-second trails before clearing each wave and verifies they
disappear instead of delaying the second wave or final completion. Wizard hazard waiting remains unchanged.
`TrailPlayCheck.StartClearProgression()` passed with the user's current authored tuning; it does not
require or replace the original tuning defaults. All 16 Trail interaction tests and 29 Wizard
regressions passed after this correction, and the serial runtime build reported zero errors.

## Crowd cost

`TrailCrowdPerformanceCapture` injected 250 roots, including 10 Trail sources, then ran five seconds of
advancing Editor simulation. The optimized capture contained 218 live sections. Twenty warmups and
120 completed updates per system (jobs completed; simulation time/physics frozen for measurement):

| System | Mean ms/update | Calling-thread managed allocation over 120 updates |
|---|---:|---:|
| Circling | 0.0023 | 0 bytes |
| Avoidance | 0.1131 | 0 bytes |
| Emission, stationary sample | 0.0023 | 0 bytes |
| Damage, live sections with cooldown protection | 0.0321 | 0 bytes |

Report: `Temp/TrailValidation/performance.txt`. This is a warmed Editor microbenchmark, not standalone
FPS, active-emission/first-contact worst case, impact-burst evidence or a resolved OQ-001 target.

## Remaining playtest checks

Human keyboard/mouse and controller playtesting should assess orbit readability, trail color/fade
against the arena, launch usefulness, damage pacing, circling distance and finite-wave difficulty.
Whole-frame profiling with sustained emission and frequent first contacts remains appropriate before
setting large-crowd performance targets. Existing obstacle navigation remains the movement owner;
complex/sloped custom arenas require their own path/projection validation.
