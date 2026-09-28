# Rotating cover validation - 2026-09-28

Implemented requirements: COVER-001 through COVER-005; reuses BARRICADE-002/004,
PLAYER-003/004, COMBAT-002/003/015 and LOOP-006. Gauntlet_13 retains break-and-exit behavior.

## Automated checks

- Unity 6000.3.10f1 compiled the new runtime and editor code successfully.
- `dotnet build Assembly-CSharp.csproj --no-restore` completed with zero errors.
  Its 382 warnings are package assembly conflicts and existing obsolete aspect/API warnings.
- All 210 CrowdPunch EditMode cases passed, including ten new rotating-cover cases:
  all rotation modes across timing boundaries, hit pause and accumulated acceleration,
  reflection multipliers 0/1/1.7, launch-state preservation, outside explosion rejection,
  owner filtering, impact/blast deduplication, persistent progress and immediate completion.
  Existing barricade, aim/homing, player impact, physics, navigation, boss and progression
  regressions are included. Progression tests now inspect fourteen levels and Gauntlet_14's wiring.

## Controlled Play Mode checks

`CrowdPunch.Editor.RotatingCoverPlayCheck.Start()` runs from Edit Mode and uses the actual saved
Bootstrap/Gauntlet_14, baked prefab colliders and running fixed-step systems. It injects launch
velocities and positions, freezes rotation during individual collision cases and freezes crowd
movement to isolate contacts. It is not a natural-input or balance test. Output is written to
`Temp/RotatingCoverValidation/playcheck.txt`; it ends paused after completion.

Final run passed:

- One four-hit target, one rotating shield, 36 rendered panels and 16 crowd roots bake correctly;
  the default cover actually rotates while the first crowd spawns.
- A blocked body reflects toward an offset player, clears homing and leaves the target undamaged.
- A blocked exploder reflects without detonating or damaging the target.
- The hybrid player's swept movement and a non-launched body's solver contacts both block
  entry through the visual shot opening.
- A launched body crosses the stationary enclosure through the opening and damages the target once.
- An outside explosion with an intentionally oversized radius cannot damage the target.
- An admitted exploder's target impact and explosion together consume one hit.
- The fourth successful hit immediately activates normal final-level completion and stops replenishment.

The initial live run found stripped enclosure transforms and missing panel scale data. The baker now
uses `ManualOverride` for the additional enclosure entity and `NonUniformScale` for panel entities.
Both fixes were verified in the final live run and a Game view capture. A validation-only timeout
after winning was fixed: completion pauses simulation time, so the check now observes the run state.
These earlier diagnostic errors do not remain in the final live run; Unity's current Console has no errors.

After a stress capture, normal level restart removed the synthetic crowd and restored four hits,
16 roots, zero pending reflections, zero observed hits, the authored rotation rate and an incomplete
run. This exercises the standard scene reload/reset path rather than manually clearing the world.

## Crowd cost

Reused `BarricadeCrowdPerformanceCapture.Capture()` against the paused Gauntlet_14 collision world:
20 warmups and 200 repeated sweep-system samples, synthetic launched bodies at fixed positions.

| Crowd roots | Sweep mean | Sweep p95 | Maximum |
| --- | ---: | ---: | ---: |
| 16 (authored population) | 0.1121 ms | 0.1208 ms | 0.1894 ms |
| 128 (synthetic stress) | 8.0043 ms | 8.1555 ms | 9.9393 ms |

These are Editor managed-system timings, not a player-build frame-rate benchmark. The old capture's
hardcoded visual-count label does not describe this scene; its presentation measurement is omitted.
The 128-body case is a material scaling limit for the managed sweeps/compound shield and would need
profiling/optimization before raising the authored population that far. OQ-001 performance targets
remain unresolved. The current level intentionally uses 16 bounded roots.

## Playtest and tuning

Open Bootstrap and select **14 Return to Sender**. Tune:

- `Data/Settings/RotatingCoverSettings.asset`: aperture, geometry, rotation modes/timings,
  hit responses and reflection speed (default 1.0).
- `Data/Settings/RotatingTargetSettings.asset`: required hits, eligible owners and target feedback.
- `Data/Settings/Waves/Progression/CP14_01_Rotating_Cover_Crowd.asset`: composition and safe spawn regions.

Let Unity rebake and reload after settings changes. The explicit **Crowd Punch > Levels > Build
Rotating Cover Gauntlet 14** command regenerates the scene recipe and plinth; it reapplies the
default wave recipe while preserving existing target/cover settings assets.

Manual playtesting still needs to assess opening readability from the normal camera, natural punch
timing, return-shot danger and chain readability, sustained replenishment pressure, death/retry and
all alternate mode/response combinations. Shared collision/player-damage rules retain their regression
coverage, but the controlled shield run is not proof of every emergent return-path collision.
No player build or whole-run duration/balance assessment was performed.

The only design clarification is explicit: reflection retains the existing momentum/damping model,
launch identity and recovery state. There is no fixed remaining-distance cap; a multiplier above one
can increase subsequent physical travel distance. This was selected by the user during implementation.
