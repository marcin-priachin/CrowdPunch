# Bud Wizard verification (2026-10-25)

Requirements: WIZARD-001/006 and INFO-004/006. Wizard gameplay, physics capsule,
cast settings, zone effects and campaign encounter counts are preserved.

- `EnemyWizard.prefab` nests `Assets/Bud Cute Series/Prefabs/Bud.prefab` with exact
  source transform scales. Its origin is positioned at the existing capsule floor.
- Bud's texture uses `CrowdPunch/Cel/Enemy`; the palette updater preserves it.
  Baking marks Wizard renderers AnimationDriven, bypassing body tint, hit flash,
  squash, procedural gait and shaking.
- Spawn From Ground is enabled. Motion 7 is Spawn to Ground Idle (0.8333334 s),
  then Ground Idle to Fly Idle provides takeoff. Fly Projectile Upward loops
  across telegraph and active; Fly Take Damage supplies non-interrupting hit
  feedback, Fly Spin Attack supplies launched motion, Fly Die supplies defeat.
  Recovery holds Ground Idle and returns through authored takeoff.
- Sampling preserves the underground spawn and includes its complete bounds.
  A small translation correction prevents Fly Die dipping through the floor.
  Root motion is disabled; sampled animation never writes physics transforms.

Validation:

- Unity 6000.3.10f1 compilation succeeded.
- All 38 tests matching Wizard passed, including gameplay, cast-loop continuity,
  launch interruption, spawn/takeoff, hit/death, exact prefab scales, source
  texture/shader, bounds, source hashes and floor clearance.
- Broader EnemyAnimationTests: 48/49 passed. Existing Dasher prefab scale is
  1.3 while its source-scale test expects 1. GroundSpawn filter: 16/17 passed;
  the existing Armored prefab has the same 1.3 versus 1 scale mismatch.
  Neither unrelated prefab was changed.
- Live Campaign 31 loaded the baked Bud renderer with 15 skin matrices,
  AnimationDriven=1, neutral `(1,1,1,1)` material multiplier, owner scale 1 and
  the correct spawn duration. Frame count/time advanced from 587/10.416 s to
  1938/17.589 s; the Wizard reached Active casting and its violet zone rendered.
  The unattended player later died and paused simulation. This was an automated
  runtime/render inspection, not a complete human playthrough.
- Isolated Burst animation workload: 250 Bud Wizards, 15 bones each, 30 warmups
  and 200 completed updates, approximately 0.1215 ms/update on i9-14900KF.
  This excludes physics, rendering, GPU and full-frame cost and is not an FPS claim.
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q` exited 1
  with zero reported warnings/errors and no actionable diagnostics. Unity
  compilation and Editor test execution are the successful compilation evidence.
- Console retained an existing Synty Sidekick Tool Downloader invalid-window
  error on Play entry; no Bud gameplay, baking or animation error was observed.

No target-device profiling or full campaign playthrough was performed.
