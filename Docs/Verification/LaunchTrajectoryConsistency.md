# Launch trajectory consistency

Follow-up: [Committed launch guidance](CommittedLaunchGuidance.md) now commits fixed
aim points and conditional expected rebounds from the last displayed snapshot.
Its comparison harness includes this gameplay path. Measurements and advisory-only
scope below describe the earlier prediction-fidelity slice.

Requirements: PLAYER-003/004, COMBAT-012, INFO-005. Verified 2026-11-02 in the
current CrowdPunch checkout with Unity 6000.3.10f1.

## Implemented scope

Accepted review items 1-3 and comparison checks from item 6:

- Presentation reads the current world's `FixedStepSimulationSystemGroup.Timestep`.
  This is the ECS clock, not `UnityEngine.Time.fixedDeltaTime`. Entities defaults to
  1/60s; isolated forecast callers use that fallback. No physics settings changed.
- Duration and step budget are independent caps. Whole fixed steps stop at the first
  cap instead of stretching to fill the duration. Saved settings remain 8 seconds /
  160 steps, now about 2.667s of forecast at 60Hz. More steps extend the horizon at
  extra CPU cost, up to the existing 400-step cap. Inspector tooltips explain this.
- Rebound aim correction runs after the forecast step integrates and applies
  damping. Its fresh guidance budget starts steering next step, matching runtime.
  Authored barricade/cover overrides use the incoming pre-contact velocity after
  ordinary integration, including their penetration correction and multipliers.
- `LaunchContactResponse` shares meaningful horizontal-impact thresholds,
  barricade reflection, penetration correction and Finale player-safety deflection.
  Cover reflection and boss-head safety reuse existing runtime functions. Ordinary
  impulses and friction remain approximate; Unity Physics retains their ownership.
- Forecasting shares ordinary barricade hit counts/source restrictions/history and
  exposed shell-core impact damage. A destroying hit passes through; protected,
  disallowed and already-counted hits rebound. Temporary per-source state removes
  virtually broken solids from later casts, obstruction tests and aim acquisition.
  It never writes damage, histories or collider changes into the live world.
- Punch damage now passes through the scalar player preview bridge to the ECS
  source snapshot for shell-core prediction. No enemy entities enter Mono scripts.

The prediction of future crowd/cover/track movement and simulation of struck-body
flights (review items 4-5) were not added. Neither a committed flight plan nor a
second Unity Physics world was introduced.

## Automated comparison checks

`LaunchTrajectoryComparisonTests` records a frozen pre-punch forecast, applies the
actual `PunchResolution`/`ApplyImpulseSystem` path, then runs homing, authored swept
contacts, Unity Physics, rebound/contact interpretation and ricochet assistance.
It samples actual physics-root positions and velocities at the same fixed times.
First contacts come from real collision events or the authored objective sweep.
Tests measure maximum/RMS position error, first-contact identity/timing and outgoing
angle at the actual first-contact step. Optional ECS-only diagnostic samples also
include guidance target/budget; ordinary presentation allocates none of this data.

These are short, controlled one-second fixtures with simple colliders and zero
gravity. They prove regression bounds for these cases, not universal accuracy in a
moving campaign crowd. Stationary/slow moving targets do not run AI in the fixture.

| Fixture | Max position error | RMS error | First-contact timing difference | Outgoing-angle error |
| --- | ---: | ---: | ---: | ---: |
| Free flight, 60Hz and 50Hz | <0.1mm | <0.1mm | No contact | N/A |
| Linear damping | <0.1mm | <0.1mm | No contact | N/A |
| Stationary enemy | 2.0mm | 1.7mm | 0ms | 0.020deg |
| Slowly moving enemy | 81.5mm | 38.1mm | 0ms | 1.396deg |
| Bouncy wall | 2.1mm | 1.6mm | 16.7ms | 0.000deg |
| Wall with assisted rebound | 57.8mm | 20.8mm | 16.7ms | 0.818deg |
| Rotating-cover response, held pose | 172.1mm | 131.4mm | 16.7ms | 0.000deg |
| Boss head safety | 281.4mm | 214.9mm | 16.7ms | 0.000deg |
| Finale safety | 170.4mm | 130.2mm | 16.7ms | 0.000deg |
| Two-wall corner | 139.0mm | 89.7mm | 0ms | 0.020deg |
| Intact barricade | 172.1mm | 131.4mm | 16.7ms | 0.000deg |
| Destroying barricade hit | 0.1mm | 0.1mm | 16.7ms | 0.000deg |
| Owner-restricted barricade | 172.1mm | 131.4mm | 16.7ms | 0.000deg |
| Already-recorded barricade hit | 172.1mm | 131.4mm | 16.7ms | 0.000deg |
| Destroying exposed shell-core hit | 0.1mm | 0.1mm | 16.7ms | 0.000deg |
| Protected shell | 172.1mm | 131.4mm | 16.7ms | 0.000deg |

The contact forecast is one step early for several surfaces. The measured offset
on authored/boss responses reflects the remaining swept-contact versus discrete
solver integration difference. Runtime remains authoritative; no body is moved to
the preview path to hide this error.

## Validation and cost

130 relevant Editor tests passed:

- `LaunchTrajectoryComparisonTests`: 20, including the 17 flight scenarios above,
  60Hz/50Hz work-budget checks and a presentation-bridge check using a changed
  0.01s ECS timestep. The bridge also verifies copied punch damage.
- `DirectLaunchTrajectoryTests`: 17; `RicochetAimTests`: 13.
- `StableLaunchAimTests`: 15; `LaunchImpactPreviewTests`: 27.
- `BarricadeTests`: 12; `RotatingCoverTests`: 10; `ShellTargetTests`: 5;
  `FinaleTests`: 11.

Unity compilation passed. `dotnet build Assembly-CSharp.csproj --no-restore`
passed with zero errors and 318 existing package/reference/deprecation warnings.
`git diff --check` passed. No new renderer or campaign playthrough was performed.

Warm synthetic Editor probes with 100/1,000 crowd bodies measured respectively
28.505/34.643ms for eight repeated assisted rebound forecasts at the saved 160-step
budget. This fixture repeats one source eight times to measure estimator CPU work;
it is not eight distinct simulated bodies. A full runtime mailbox scan plus one
rebound measured 0.137/0.327ms per update. These are isolated Editor timings, not a
frame-rate claim or a reliable before/after benchmark. Forecast cost remains
substantial for many simultaneous sources. OQ-001 target-hardware/FPS acceptance
remains unresolved.

Future target/geometry motion beyond the existing lead cap, simultaneous damage
from multiple sources, explosions, track docking, linked-pair motion, angular
contact response and later collisions with already-struck bodies remain estimates
or unmodelled. The dotted path remains advisory under PLAYER-003 and INFO-005.
