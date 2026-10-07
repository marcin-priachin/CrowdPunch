# Chicken Boss Validation - 2026-10-07

Scope: CHICKEN-001..008, PLAYER-003/004/005/009, COMBAT-015 and LOOP-006.

## Completed checks

- Unity 6000.3.10f1 imported and compiled runtime/editor code and generated Gauntlet_20,
  its SubScene, Chicken model animation samples, projectile prefab, dedicated settings,
  supporting wave and Bootstrap/build/selector registration.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` passed with zero errors.
  Existing Unity package assembly-conflict warnings, deprecated aspect warnings and the
  existing unused aspect field remain. The default parallel MSBuild invocation exited
  without diagnostics; a single build worker succeeded.
- `ChickenTestRunner.Run` passed 103 EditMode cases with zero failures/skips. This includes
  Chicken interactions, first-boss regressions, player-body impacts, launch homing, Trail
  interactions and all twenty progression scene/reference/bounds checks. NUnit results
  and summaries are saved under `Temp/ChickenValidation`.
- Chicken cases exercise single/paired release-and-rush, minimum spacing, pause alternatives,
  committed wind-up proximity, real punch resolution/cooldown confirmation, fresh return/reset,
  both fire-aim modes, multi-bounce limit cleanup, speed-preserving reflection and cleared homing, both ownerships damaging/consuming at player,
  protected boss consumption, untouched-shot rejection, body ownership, maximum combined damage,
  protected/history repeat rejection, enemy pass-through/once-per-launch/re-punch eligibility,
  stagger/cancellation, defeat cleanup/completion and soft-reset state.
- The rebound correction adds Chicken-contact tests for Player and Boss launch ownership:
  outgoing velocity has no player-bound horizontal component, speed and vertical velocity
  are preserved, the contacted boss homing lock clears, and the body retains its launch
  ownership, damage, sequence and player-impact eligibility. Existing first-boss rebound
  geometry regressions also pass. The C# build after this correction passed with zero errors.
- A follow-up live Chicken solver contact produced lateral velocity (17.7673, -0.8175, 0),
  a player-bound horizontal component effectively zero, and a cleared boss homing target.
  The body retained Player ownership. Evidence is `Temp/ChickenValidation/rebound-live.txt`.
  During the scripted switch into the encounter, the Editor also reported an invalid collider
  blob in `PlayerObstacleCollisionSystem.Depenetrate`. That separate scene-transition error
  was outside the changed rebound path; this check does not establish error-free scene loading.

## Instrumented live Editor evidence

The real player loop advanced (frame counter 445 to 452 during the server wait), then level
selection loaded Gauntlet 20. The baked world contained one Chicken, twelve ordinary roots,
its render owner and autonomous shots, with no gameplay exception. Camera and composited UI
captures were inspected; the Chicken mesh animates, shots are visible and the single boss
health bar uses the existing canvas. Captures remain in `Temp/ChickenValidation`.

Stage one fired and rushed repeatedly. A 12-second stage-two observation recorded paired
releases from distinct positions, for example at 124.460s (-14.277, 1, 9.609) and 125.977s
(-8.991, 1, 14.044), with intervening reposition/wind-up and a final pause. Third-stage
paired firing and faster physical rushes were also observed. The log is `stage-two-live.csv`.

A real player-owned Baseline launched from inside the perimeter at 24m/s collided with the
solid boss and reduced health from 375 to 345. An injected returned shot reduced 450 to 375.
An unlaunched ownerless source explosion damaged it. A physical player-owned Exploder with
30 body damage and 42 explosion damage changed health 1716 to 1674 and accepted-hit count by
exactly one, confirming same-step maximum resolution in actual solver/detonation ordering.

A lethal return produced health zero/Defeated, zero remaining boss shots, disabled support
respawn and run completion at the final authored level. Scripted level restart restored
1800 health, stage one, zero accepted hits/history, then the twelve-member crowd after
resuming the existing completion menu. Temporary player-health restoration and injected
stage/hit setups were used for inspection; this is not a normal-input difficulty test.
The Play session was stopped and the background setting was restored.

At the authored 12-member cap, 120 warm Editor CPU samples of
`Default World CrowdPunch.Systems.Combat.ChickenProjectileSystem` averaged 0.0582ms,
maximum 0.5353ms. This measures that system's current Editor work, not total frame time,
GPU cost, a player build or an OQ-001 hardware/large-crowd guarantee. The system scans the
small support crowd per swept shot segment; larger custom compositions need profiling.

## Remaining human playtests

- Play the full fight with keyboard/mouse and controller, using normal and dash punches.
  Validate response windows, aiming forgiveness and repeated wall-bounce readability.
- Tune toward CHICKEN-008's 2-3 minute duration. Automated/injected stage changes do not
  demonstrate duration, difficulty or player skill requirements.
- Judge arena camera framing, animation/impact readability, damage pressure and whether
  ownership colors clearly communicate that returned shots still hurt the player.
- If authoring another arena, keep collision rails aligned with the rectangular bounds.
  Arbitrary interior obstacles and nonrectangular projectile ricochets are outside this slice.
