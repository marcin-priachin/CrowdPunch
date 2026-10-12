# Snake Enemy Validation - 2026-10-12

Implementation: SNAKE-001..007; PLAYER-003/004/005/009; COMBAT-001/003/004/010/011/012/014/015; LOOP-002.

Playable through **Window > Crowd Punch > Level Play > Legacy > 24 Cut The Tail**.
The finite wave is twelve Baselines and two Yeti snakes. Only the fourteen enemy roots
belong to encounter defeat accounting; orb entities do not. Gauntlet_24 is absent from
EditorBuildSettings and campaign catalog assets were not edited.

## Settings

`Assets/CrowdPunch/Data/Settings/SnakeSettings.asset` holds both agreed behavior modes
with ProtectedWhileTailRemains and LaunchAndSever selected. Provisional initial tuning:
six orbs, 0.6m radius, 1.5m spacing, 0.5m wave amplitude, 0.7 cycles/second, 3.1m/s pursuit,
8% player-health contact damage. The original head-health default was 90; tune it through
`SnakeEnemySpawnSettings.asset > Max Health`. Orb radius is baked into runtime
transform scale and contact/navigation radius. Reload/rebake after settings edits.

Wave counts/regions remain independently editable in
`Assets/CrowdPunch/Data/Settings/Waves/Progression/CP24_01_Snakes_And_Ammunition.asset`.
The explicit Build Snake Gauntlet 24 command recreates the provisional encounter recipe.
Existing profile health is preserved when rebuilding. The duplicate health field in
SnakeSettings was removed after it silently overwrote a user's 0.1 Max Health tuning.
Head health now comes solely from its spawn profile; constructing or severing a tail
does not reset it. Zero health still defers defeat until the launched body stops,
following COMBAT-011; another punch is unnecessary to defeat it and extends that launch.

## Performed verification

- Unity 6000.3.10f1 compiled the feature without C# errors. Existing deprecated aspect
  warnings remain outside this slice.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -p:BuildProjectReferences=false
  -p:BuildInParallel=false` passed with zero errors and 18 existing package/aspect
  warnings, using Unity's already-built package assemblies. The default generated
  solution build exited unsuccessfully without compiler diagnostics. A serial full
  package build emitted existing assembly-version conflicts and was stopped after
  the gameplay assembly check passed. Unity compilation remains authoritative.
- All 15 SnakeEnemyTests cases passed: path corner sampling, any-owner middle cuts,
  simultaneous cut ordering, attached damage immunity, protected-punch cooldown,
  final-cut exposure/no regrowth, AlwaysVulnerable launching, both explosion modes,
  spatial/permanent clearance, launch-end destruction and stale encounter cleanup.
- All 57 targeted existing regressions passed: 20 ArmoredEnemyTests,
  35 LaunchedEnemyPlayerImpactTests and two EnemyLaunchHomingTests.
- The real legacy scene baked and ran with two heads, twelve orbs and twelve Baselines.
  Frame/time counters advanced. Game-view inspection showed the Yeti heads, spherical
  physical tails and two routing obstacles.
- SnakeEncounterPlayCheck exercised a real Unity Physics body collision with injected
  positions/velocity: a Boss-owned source cut a middle orb, preserved the two-orb prefix
  and ownership, and the launched orb spatially cleared its original snake. A committed
  return shot then physically cut its original remaining tail, leaving the head alive.
- Actual legacy retry restored both full tails, 90 health, twelve Baselines, and a new
  encounter entity identity. Injected head/Baseline defeat exercised actual accounting
  and final legacy completion with no remaining attached tail entities.

The live check isolates incidental crowd contacts and sets an explicit return homing
target. It injects defeat for completion verification. These are controlled integration
checks, not a human combat/balance playtest. Temporary changes exist only in Play Mode.
Raw output is generated at `Temp/SnakeValidation/live-check.txt` by the repeatable
Editor harness; no validation state is stored in authored scene/settings assets.

## Performance probe

SnakeCrowdPerformanceCapture created an isolated ECS world with 64 heads, 384 orbs,
200 unrelated enemies and 128 path samples per head. Twenty warmups and 120 completed
updates per system on this Editor measured:

| Work | Mean milliseconds/update | Calling-thread managed allocation/120 updates |
| --- | ---: | ---: |
| Path-following tail motor | 0.117030 | 0 bytes |
| Sever batch scan | 0.643864 | 0 bytes |
| 64 uncleared detached-orb spatial checks | 0.022118 | 0 bytes |

Physics, rendering and simulation time were not advanced in this microbenchmark.
It measures the new systems at an expanded crowd size, not full-game frame rate,
collision-event throughput or target-hardware acceptance. OQ-001 remains unresolved.

## Remaining human checks

Health fix verification: all 17 SnakeEnemyTests cases passed, including profile Max
Health 0.1 requiring one punch and Max Health 40 requiring two 20-damage punches after
exposure. Unity compilation and the gameplay assembly build passed. Controlled
Gauntlet_24 runtime inspection confirmed both heads spawn at the user's 0.1 health.
The regression tests reduce the exposed head to zero with one 20-damage punch and
confirm defeat at launch end without a second punch. They also confirm tail updates
do not heal it.
The actual Gauntlet_24 ECS world was also checked while paused: after an injected
first-orb cut, one 20-damage PunchDetectionSystem/DamageApplicationSystem update
changed head health from 0.1 to zero. After stopping its velocity, EnemyRecoverySystem
changed Launched to Defeated without another punch. No authored state was changed.

Tune contact pressure, health, orb spacing/wave silhouette and wave counts through
normal controller/mouse play. Check feel and readability while steering around the
obstacles, dash punching, using Exploders and combining several moving snakes.
The shared dash-punch and aim-preview paths are retained; no manual controller playtest
or player build was performed in this task.
