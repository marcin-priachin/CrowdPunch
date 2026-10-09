# Ground hazard validation - 2026-10-09

Agreed source: Ground Hazard Design, conversation 6ac8f571-f0a4-83ed-bdf9-c5b4b25b9c73.
Requirements: GROUND-001..007; COMBAT-011/014/015 and LOOP-006 remain authoritative.

## Content and tuning

Select **23 Hot Footing** in **Crowd Punch > Levels > Level Play Window**. Gauntlet_23 is
registered after Dino Pillars in Bootstrap and Build Settings. Its SubScene contains
both shapes and activation modes. Finite Baseline counts are 12 then 20. Wave two adds
periodic patches; there is no replenishment or hazard completion gate.

Damage is 12 every .75 seconds. Periodic timing is 3 seconds inactive, 1.5 seconds warning
and 2.5 seconds active, with 0/2 second offsets. Scene authoring exposes dimensions,
damage, interval, durations and offset. GroundHazardSettings.asset exposes Active-only/
Warning-and-Active avoidance and Wait Safely/Cross As Last Resort. Defaults are Active-only
and Wait Safely. Numbers, colors and layout are delegated provisional tuning.

## Automated checks

Unity 6000.3.10f1 compilation passes. `dotnet build Assembly-CSharp.csproj --no-restore`
also passes (a single MSBuild worker was used); existing package/obsolete-aspect warnings
remain. `git diff --check` passes.

`GroundHazardTestRunner.Run()` covers hazard cases plus terrain navigation, Trail, Armored
and gauntlet progression regressions. The suite passes 122 cases with no failures/skips.
Reports: `Temp/GroundHazardValidation/editmode-results.xml` and `editmode-summary.txt`.

- Both shapes, rotated rectangles, radius-inclusive XZ overlap and safe phases.
- Active/launched/recovering damage without velocity changes; immediate first contact,
  ticks, overlap/reentry/patch-switch/activation continuity and pooled lifetimes.
- Armor health/stage protection, post-break protection, no death detonation, current-launch
  lethal ownership/depth and separate earlier pending damage resolution.
- Whole-footprint routing with moving intent, both fallback/avoidance modes, caught-body
  escape, armored avoidance, committed lunge/Dasher and launched exclusions.
- Activation invalidates routes; search limits never permit unproven crossing.
- Warning/active spawn rejection before the new wave's cycle update; wave/retry offsets.
- Patches cannot delay finite-wave completion; 23 ordered progression entries, valid
  scene/wave/settings references and separate validation scenes outside progression.

Navigation tests move transforms only as a harness following generated intent. They
do not claim solver-level collision or motor verification.

## Advancing baked-world checks

`GroundHazardPlayCheck.Start()` loads Bootstrap and Gauntlet_23 through normal progression.
Report: `Temp/GroundHazardValidation/playcheck.txt`. The scene baked four patches, safe
opening spawns, hazard components and route buffers. More than 600 game frames advanced
through both waves and an actual `RestartCurrentLevel()`.

Injected contacts exercised the real damage system/bridge: horizontal damage affects a
launched target at height without changing velocity; a same-time second update cannot
double-hit. A player hit during an injected committed dash and ordinary invulnerability
reduces health while preserving dash/protection state. Retry restores wave one and patch
introduction. Injected enemy defeats advance to 20 Baselines with all four patches.
Final-wave defeat sets RunComplete while permanent hazards remain active. These are
controlled integration checks, not a balance playtest.

The Game view shows visible circles/rectangles, brighter red activation and the slate
inactive footprint. Warning rendering uses the same clipping/scale and pulses amber
across the full footprint. Capture: `Temp/GroundHazardValidation/gauntlet23-game.png`.

## Crowd CPU measurements and limits

`GroundHazardPerformanceCapture.Run()` completes synchronous Burst warmup with safety
checks enabled and restores the previous compiler option. It measures four mixed patches,
frozen actor positions, opposing goals, cached routes and queued searches. 20 warmups
precede 120 completed updates per system. No managed bytes are allocated in measured
loops. Native scratch is bounded and reused.

On an i9-14900KF / RTX 4090, isolated samples were approximately:

| Bodies | Hazard routing | Hazard damage |
| --- | ---: | ---: |
| 500 | 0.40 ms/update | 0.015 ms/update |
| 2,000 | 1.17 ms/update | 0.049 ms/update |

These exclude physics, GPU rendering, activation-overlay rebuilds and whole-frame cost;
they are not standalone FPS or target-hardware proof. OQ-001 remains unresolved.
Early samples before Burst completed used managed fallback and are not representative
of warmed compiled execution. Report: `Temp/GroundHazardValidation/performance.txt`.

Human playtesting remains for damage/cycle balance, warning readability, crowd compression
forcing bodies across boundaries, encounter pacing and full-run feel.
