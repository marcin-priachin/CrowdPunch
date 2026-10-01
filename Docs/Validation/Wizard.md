# Wizard validation

Date: 2026-10-01. Requirements: WIZARD-001..007, PLAYER-003/004, COMBAT-002/003.

## Implemented

Wizard is a distinct ECS enemy with the agreed movement, probability-based casting,
telegraph, damage zones, independent target timers and three force modes. It uses the
Blob/Wizard model, sampled Idle/Walk/Dance/Death animation and violet body/zone presentation.
It has normal punch targeting and no melee/contact attack. A genuine launch permits one
immediate stationary impact zone; re-punching that same flight does not renew it.

Gauntlet_17, **17 Violet Court**, follows Gauntlet_16 in Bootstrap and Build Settings.
Its waves contain 6 Baselines + 1 Wizard, then 12 Baselines + 2 Wizards. Both wait for
enemy defeat and lingering zone expiry. The optional delayed Baseline safeguard is enabled;
Wizards are finite. Existing wave assets keep the new optional features disabled.

Tune `Data/Settings/Enemies/WizardEnemySpawnSettings.asset` and the two `CP17` assets in
`Data/Settings/Waves/Progression`. Changes require baking/reloading the level. Use
**Crowd Punch > Enemies > Rebuild Wizard Prefab** for model/controller/sample assets and
**Crowd Punch > Levels > Build Wizard Gauntlet 17** for the authored level recipe.
The complete accepted decisions remain in [Wizard design](../Design/Wizard.md); system
ownership and update order are in [Current architecture](../Architecture/CurrentArchitecture.md).

## Verification

- Unity 6000.3.10f1 compiles and bakes the saved content. The final Console compilation
  flag is clear. The existing Synty `ToolDownloader` window error remains on Play Mode entry;
  it is unrelated to the gameplay systems. Existing deprecated-aspect warnings remain.
- `dotnet build Assembly-CSharp.csproj --no-restore` passes: **0 errors, 271 warnings**,
  from the existing Unity package/reference/source-generator and legacy-aspect paths.
- **258/258 CrowdPunch editor tests pass**, including 23 Wizard cases and 22 progression
  cases. Coverage includes cast probability and control-loss cancellation, movement modes,
  per-target entry/tick/exit/re-entry, XZ radius with body overlap, stacking, player protection,
  armor, exploder damage without detonation, Dasher force immunity, elite non-launch,
  ordinary lethal damage, deferred projectile death with continued force, strong force
  preserving an existing flight, zone expiry/unload, supply eligibility, hazard-gated wave
  advancement and weighted cap rerolls. Progression tests validate the saved scene geometry,
  wave composition, references and ordered Build Settings.
- Controlled Play Mode checks in the actual baked Gauntlet_17 world passed nine assertions:
  opening allocation; enemy collision creating an immediate fixed zone; combined collision
  and zone damage; no second zone after a same-flight re-punch; a fixed zone staying at its
  impact position; renewed allowance for a later flight; persistence after source death;
  restart cleanup; and delayed Baseline replacement participating in wave accounting.
- Four additional Play Mode assertions passed: walls do not spend the impact allowance;
  ordinary player invulnerability is active before contact; an invulnerable player still
  creates the zone and takes its immediate 10 damage; an unprotected player takes ordinary
  projectile damage and zone entry damage independently. These checks were repeated after
  the final ECS-to-player hit-buffer split.
- A Game-view capture confirmed the imported violet Wizard and the ground disc/ring render
  in the saved level. This is visibility evidence, not approval of the final visual balance.

The controlled harness injects positions, launch state and velocity, freezes normal movement
and raises enemy health to isolate contacts. It exercises the real physics and player bridge,
but does not replace a normal-input playthrough. Temporary local evidence is under
`Temp/WizardValidation` (`playcheck.txt`, `player-wall.txt`, `first.png`, `dotnet-final.txt`).

## Performance

Warm, isolated Editor timings on an AMD Ryzen 9 5980HS, with Burst and safety checks enabled:

| Enemies / zones | Zone update mean / max | Avoidance mean |
| --- | --- | --- |
| 14 / 2 | 0.013 / 0.062 ms | 0.005 ms |
| 500 / 10 | 0.192 / 0.388 ms | 0.054 ms |

Each case measured 300 fixed updates after 50 warmup updates, with 0.5-second damage ticks,
spatially distributed sphere bodies and persistent active zones. Stopwatch measurements wrap
the real zone/avoidance systems and complete their jobs. Broadphase construction, the physics
solver, navigation, skinning and rendering are excluded. These are not total frame costs or
player-build guarantees. Unwarmed managed fallback and concurrent Burst compilation produced
much slower measurements and are not presented as steady-state performance. A synchronous
Burst compilation pass temporarily blocked Editor tools; it completed, settings were restored,
and the final checks ran successfully. Unity was returned to Edit Mode afterward.

## Deviations and remaining validation

No accepted gameplay rule was intentionally changed. Routine implementation choices include
an additive shared disc/ring mesh, a separate continuous-flight ID alongside existing punch
launch sequences, and an ECS hit buffer for the managed player boundary. This preserves the
existing player/enemy architecture and damage ownership.

Manual visual/balance testing and player-build profiling remain verification work, not future
feature promises. Automated coverage does not establish gamepad feel, crowd readability,
all possible overlapping force combinations, or production performance on target hardware.

## Playtest

1. Select **17 Violet Court**. Check the 6-9m positioning, approach-sensitive casts, one-second
   telegraph, three-second zone, Dance loop, and readability when two violet zones overlap.
2. Punch Wizards and launch other enemies into them. Check normal aim assist/preview,
   interruption, immediate impact zones and surviving zones after the Wizard dies. Re-punch
   a flying Wizard and confirm it cannot create another zone until a later genuine launch.
3. Cross and re-enter zones, including while dashing or protected from an ordinary hit.
   Check immediate damage, independent stacking, the small default push and corner escape.
4. Try all movement/force settings in a test wave: armor, elites, committed Dashers, exploders,
   recovering and zero-health launched enemies. Strong force should preserve an existing
   launch. Enemy/boss contact should create the intended special zone without zone damage
   to boss parts or puzzle objects; walls and puzzle objects alone must not trigger it.
5. Exhaust Baselines while a Wizard lives and verify delayed safe replacement. Kill the Wizard
   during the delay; pending supply should cancel. Finish both waves, wait out the last zone,
   then restart and switch levels to check clean encounter teardown.
