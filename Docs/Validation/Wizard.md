# Wizard validation

Date: 2026-10-01. Requirements: WIZARD-001..007, PLAYER-003/004, COMBAT-002/003.

Follow-up correction: cast zones now affect only the player. Impact zones retain enemy
effects and can also be triggered when a launched enemy hits an active/recovering Wizard.
The 2026-10-02 settings change separates Cast Radius and Impact Radius. Both current authored
values were migrated from 12m to 12m; the existing 0.5s telegraph tuning is preserved.

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

2026-10-03: Unity recompilation succeeds and all **265/265 editor tests pass**, including
30 Wizard cases and the prior independent-radius regression. The new WIZARD-002 tests
also pass the command-line C# build with 0 errors and the existing 271 warnings. They
verify zero-chance guaranteed casting, cooldown/range/player-availability gates, and
bypassing a pending check interval. Cast Whenever In Range is implemented and defaults off.
Playtest both modes after rebaking/reloading: enter engagement range with cooldown ready,
then confirm telegraph starts immediately only in the guaranteed mode.

2026-10-02 radius settings update: `dotnet build Assembly-CSharp.csproj --no-restore`
passes with 0 errors and the existing 271 warnings. Added `Wizard003_CastAndImpactUseIndependentRadii`
to exercise different player/enemy bounds, and extended probability coverage so Impact Radius
cannot affect casting chance. Unity tools were unavailable for this session; these updated
tests have now passed in Unity on 2026-10-03; the Inspector migration still needs visual confirmation. The checks below were
completed on 2026-10-01 before the radius split.

- Unity 6000.3.10f1 compiles and bakes the saved content. The final Console compilation
  flag is clear. The existing Synty `ToolDownloader` window error remains on Play Mode entry;
  it is unrelated to the gameplay systems. Existing deprecated-aspect warnings remain.
- `dotnet build Assembly-CSharp.csproj --no-restore` passes: **0 errors, 271 warnings**,
  from the existing Unity package/reference/source-generator and legacy-aspect paths.
- **261/261 CrowdPunch editor tests pass**, including 26 Wizard cases and 22 progression
  cases. Coverage includes cast probability and control-loss cancellation, movement modes,
  per-target entry/tick/exit/re-entry, XZ radius with body overlap, stacking, player protection,
  armor, exploder damage without detonation, Dasher force immunity, elite non-launch,
  ordinary lethal damage, deferred projectile death with continued force, strong force
  preserving an existing flight, zone expiry/unload, supply eligibility, hazard-gated wave
  advancement and weighted cap rerolls. Progression tests validate the saved scene geometry,
  wave composition, references and ordered Build Settings. The corrected Wizard tests cover
  player-only cast zones, incoming-flight deduplication, and fast launched Dasher sweeps.
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
- The follow-up baked-world check passed seven assertions: a launched Baseline hitting a Wizard
  creates an active detached zone and takes its immediate enemy damage; continued contact and a
  same-flight re-punch cannot repeat it; a later genuine flight can trigger another zone; and a
  natural cast zone damages the player without damaging or pushing the nearby Baseline. Two
  further controlled Play Mode assertions confirm a launched Dasher suppresses enemy solver
  contacts yet its sweep still creates a Wizard impact zone. Ordinary-input feel remains for playtest.
- A Game-view capture confirmed the imported violet Wizard and the ground disc/ring render
  in the saved level. This is visibility evidence, not approval of the final visual balance.

The controlled harness injects positions, launch state and velocity, freezes normal movement
and raises enemy health to isolate contacts. It exercises the real physics and player bridge,
but does not replace a normal-input playthrough. Temporary local evidence is under
`Temp/WizardValidation` (`playcheck.txt`, `player-wall.txt`, `correction-check.txt`,
`dasher-correction-check.txt`, `first.png`, `dotnet-correction.txt`).

## Performance

Earlier warm, isolated Editor timings on an AMD Ryzen 9 5980HS, with Burst and safety checks enabled:

| Enemies / zones | Zone update mean / max | Avoidance mean |
| --- | --- | --- |
| 14 / 2 | 0.013 / 0.062 ms | 0.005 ms |
| 500 / 10 | 0.192 / 0.388 ms | 0.054 ms |

Each case measured 300 fixed updates after 50 warmup updates, with 0.5-second damage ticks,
spatially distributed sphere bodies and persistent active impact zones. These timings
precede the correction; they do not measure the new incoming-contact scan. Stopwatch measurements wrap
the real zone/avoidance systems and complete their jobs. Broadphase construction, the physics
solver, navigation, skinning and rendering are excluded. These are not total frame costs or
player-build guarantees. Unwarmed managed fallback and concurrent Burst compilation produced
much slower measurements and are not presented as steady-state performance. A synchronous
Burst compilation pass temporarily blocked Editor tools; it completed, settings were restored,
and the final checks ran successfully. Unity was returned to Edit Mode afterward.

For the correction, an isolated Editor check with 500 enemies, including 10 Wizards and
10 launched Dashers, measured `WizardImpactZoneSystem` at **0.145ms mean / 0.279ms max**
over 300 updates after 50 warmups. The test includes the incoming Dasher sweep and empty
collision-event scheduling; it excludes actual collision events, zone creation, solver,
rendering and whole-frame cost. The result is in `Temp/WizardValidation/incoming-performance.txt`.

## Deviations and remaining validation

The follow-up correction intentionally changes cast-zone enemy effects and adds an incoming
impact trigger. No other accepted gameplay rule was intentionally changed. Routine implementation choices include
an additive shared disc/ring mesh, a separate continuous-flight ID alongside existing punch
launch sequences, and an ECS hit buffer for the managed player boundary. This preserves the
existing player/enemy architecture and damage ownership.

Manual visual/balance testing and player-build profiling remain verification work, not future
feature promises. Automated coverage does not establish gamepad feel, crowd readability,
all possible overlapping force combinations, or production performance on target hardware.

## Playtest

1. Select **17 Violet Court**. Check the 6-9m positioning, approach-sensitive casts, one-second
   telegraph, three-second zone, Dance loop, and readability when two violet zones overlap.
   In Wizard settings, set different Cast Radius and Impact Radius values, rebake/reload,
   and check that each disc, damage boundary and avoidance distance matches its own value.
2. Punch Wizards and launch other enemies into them. Check normal aim assist/preview,
   interruption, immediate impact zones and surviving zones after the Wizard dies. Re-punch
   a flying Wizard and confirm it cannot create another zone until a later genuine launch.
   Hit an active Wizard with a launched Baseline and Dasher; each should create an immediate
   detached zone, once per incoming flight.
3. Cross and re-enter zones, including while dashing or protected from an ordinary hit.
   Check immediate damage, independent stacking, the small default push and corner escape.
   Stand an enemy in a cast zone and confirm it takes no zone damage or push; repeat inside
   an impact zone and confirm the enemy takes its normal ticks and force.
4. Try all movement/force settings in a test wave: armor, elites, committed Dashers, exploders,
   recovering and zero-health launched enemies. Strong force should preserve an existing
   launch. Enemy/boss contact should create the intended special zone without zone damage
   to boss parts or puzzle objects; walls and puzzle objects alone must not trigger it.
5. Exhaust Baselines while a Wizard lives and verify delayed safe replacement. Kill the Wizard
   during the delay; pending supply should cancel. Finish both waves, wait out the last zone,
   then restart and switch levels to check clean encounter teardown.
