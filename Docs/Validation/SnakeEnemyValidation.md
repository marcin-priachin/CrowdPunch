# Snake Enemy Validation - 2026-10-13

## Birb appearance update - 2026-10-15

SNAKE-001/006 now use `Models/UltimateMonsters/Blob/Birb.fbx`. The saved Snake
prefab, controller and sampled animation data are rebuilt for Birb. Physics,
health, pursuit speed, six-segment tails and campaign/legacy wiring are retained.
The obsolete unreferenced Yeti animation samples are removed.

Birb's atlas body swatch and the tail use matching blue, stored in linear space
in SnakeSettings. The existing gold weak-point color moves from the exposed tip
to the head after tail exhaustion. A generated body-only mask and DOTS-instanced
`SnakeBodyColor` retain white face details and the red beak in both states.
Applying the shared monster atlas or rebuilding Snake restores this material setup.

Verification: all 35 Snake EditMode cases pass, including both primitive/textured
color progression through all six tips and the exposed head, atlas mask coverage,
saved Birb prefab/material references and matching tail colors. Unity compilation
passes. A serial `dotnet build Assembly-CSharp.csproj --no-restore -m:1` passes
with zero errors and existing package warnings.

Follow-up fixed two issues missed by the initial isolated preview: the builder
saved the terminal Death skeleton, which pointed the beak upwards, and recorded
the controller dependency hash before its import completed. That stale hash made
the animation baker reject playback, also omitting the renderer's color hook.
The builder now imports the saved controller synchronously before sampling and
restores forward-facing Idle before saving the prefab. A regression checks the
saved sample hash and Idle skeleton against the assets used by baking.

A fresh campaign level 61 run confirms one animated Birb head, six orb bodies
and the renderer-owned SnakeBodyColor component. The beak faces forward in the
game view. Six sequential injected tip launches, each followed by an ordinary
game frame, reduce AttachedCount from six to zero. The normal severing and
presentation updates keep the protected head blue and switch its body to gold
after the final cut, preserving face/eye/beak details. No presentation or severing
system was manually updated for this check. The live Unity Console has zero
errors/warnings. This is controlled integration verification, not a balance playtest.

The following sections record the earlier Yeti implementation and historical tuning.

Implementation: SNAKE-001..007; PLAYER-003/004/005/009; COMBAT-001/003/004/010/011/012/014/015; LOOP-002.

Playable through **Window > Crowd Punch > Level Play > Legacy > 24 Cut The Tail**.
The original finite-wave recipe was twelve Baselines and two Yeti snakes. The wave
asset remains authoritative for user-tuned composition. Only head/Baseline roots
belong to encounter defeat accounting; orb entities do not. Gauntlet_24 is absent from
EditorBuildSettings and campaign catalog assets were not edited.

## Settings

`Assets/CrowdPunch/Data/Settings/SnakeSettings.asset` holds explosion behavior, tail tuning
and cyan/gold protected/vulnerable colors. LaunchAndSever remains selected. Head protection
now lasts until the tail is empty; AlwaysVulnerable was removed. Provisional initial tuning:
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

## Speed adjustment obstacle fix (13 October; current verification)

At the user's 6 m/s pursuit setting, the acceleration motor could retain sideways
velocity after navigation changed direction. The protected kinematic head entered
solid geometry, where the solver could not push it out and navigation could not
find a physical escape. Controlled baked-arena routes reproduced this: the head
stopped at (4.659569, -3.504058) beside the second routing obstacle and remained
intersecting it for 845 of 900 simulated steps.

The active-head motor now checks its actual next horizontal step against navigation
clearance before physics, stopping residual momentum before penetration. It retains
physical-radius escape from conservative corner margins and outside-grid recovery.
No transform teleporting, launched/recovering momentum change or settings change was
introduced; the authored pursuit speed remains 6 m/s.

Unity compilation and all 32 snake + 42 navigation/ground-hazard navigation tests passed.
New regressions cover 3.1/6/12 m/s obstacle/bounds steps, round-corner/bounds escape,
the real head motor preserving height/position and unaffected launched-head momentum.
Eight baked-arena start/goal approaches, each advanced through 900 actual Unity Physics
steps at .02 seconds, now produced zero head penetrations at 6 m/s; every case approached
within .18 m of its target. The same eight approaches at 3.1 and 12 m/s also produced
zero penetrations. These extra speeds check obstacle safety; they do not establish
contact timing, turn-radius balance or manual controller feel at arbitrary speeds.
Injected trajectories/positions were discarded on exiting Play Mode.

The baked-grid crowd probe included 65 heads, 390 orbs and 200 unrelated bodies.
With each head given 6 m/s horizontal velocity before every sample, the combined
tail motor/head guard averaged .073172 ms per completed frozen update across
20 warmups + 120 samples, with zero calling-thread managed allocation. Reset writes,
rendering and whole-frame performance are excluded. Raw report:
`Temp/SnakeValidation/head-guard-performance.txt`.

Raw controlled results and a repeatable paused-Editor eval body are in
`Temp/SnakeValidation/speed-results.json` and `speed-probe.cs` (Gauntlet_24, one active
snake). No player build was performed. The generated command-line build remains
limited by missing package-reference DLLs; Unity compilation/tests are authoritative.

## External bounce and head obstruction (13 October; preceding verification)

Detached orbs ignore their original head and every sibling, including detached siblings,
until a meaningful external collision/rebound. Distance and floor support do not enable
owner collisions. Solver events and swept barricade/rotating-cover rebounds both enable
return collisions. Owner targeting/homing obey the same exemption. Player punches cannot
pass through the owner head's current collider; blocked parts lose their aim lock and
direction preview. Side/tail approaches restore eligibility, including detached repunches.

Unity compilation passed. All 27 SnakeEnemyTests cases and 101 targeted shared regressions
passed (20 armor, 35 player impact, 2 homing, 12 barricade, 10 rotating cover, 22 track).
The new cases cover sibling exclusion, distance/floor non-rearming, both swept rebound
systems, head-blocked punches/repunches, aim-lock/arrow clearing and current head pose/scale.

Controlled real-physics Play Mode checks passed using the user's current one-snake,
zero-Baseline wave: protected middle impact, Boss-owned tip launch, an unbounced return
passing through the original tip without severing, actual external obstacle collision
enabling owner collisions, and a return shot launching the next tip. Retry restored six
orbs, profile health and authored counts; root defeat completed the level without tail
objectives. Temporary injected bodies/trajectories were discarded on leaving Play Mode.

The expanded baked-arena collision probe used 64 heads + 384 orbs + 200 unrelated bodies,
including 64 launched tips, with 20 warmups and 120 samples. Reset poses/launch state before
each sample produced up to 47 externally armed orbs. Physics build/simulation including
contact filtering averaged 1.142838 ms/completed step; the collision-event interpretation
system including bounce arming averaged 0.405208 ms/completed update. These exclude reset
writes, rendering and full gameplay; they do not establish target-hardware frame rate.
The independent tail/sever probe averaged 0.116608 / 0.226542 ms respectively, with zero
calling-thread managed allocation. The old spatial-clearance scan was removed.

Raw reports: `Temp/SnakeValidation/live-check.txt`, `collision-performance.txt` and
`performance.txt`. Repeat collision profiling with `SnakeCrowdPerformanceCapture.CaptureCollisions`
while paused in Gauntlet_24, then exit Play Mode to discard its stress fixtures.
The .NET check again failed with CS0006 missing generated package DLLs in Temp/Bin/Debug;
Unity's compilation and executed tests are authoritative. No manual controller playtest
or player build was performed.

## Tip-only revision (13 October; preceding verification)

Only the last attached orb accepts launches or participates in aim assistance and direction
previews. Gold identifies the current tip, then the head once no attached orbs remain;
protected parts and detached projectiles are cyan. Punches/explosions resolve the current
tip before the predecessor becomes eligible, preserving every unhit predecessor.

All 21 current SnakeEnemyTests cases passed: tip-only punches in either iteration order,
any-owner tip launches, single-tip explosions and the Ignore mode, progressive head
exposure, aim eligibility, real presentation-bridge direction previews, renderer color
handover, missing-tip cleanup, low-health preservation and the existing lifecycle checks.
All 57 targeted armor/player-impact/homing regressions passed. Unity compilation passed.

Controlled real-physics Play Mode validation passed: a Boss-owned body striking a
protected middle orb left the tail intact; its subsequent tip impact detached only
that orb and preserved ownership. The detached orb cleared its original snake, then
a return shot launched the now-exposed predecessor without exposing the head early.
Retry restored full tails, profile health and current authored wave counts; injected
root defeat exercised final completion. The harness uses a temporary independent
projectile so it also works with the user's current two-snake, zero-Baseline composition.
Game-view inspection confirmed gold tip/cyan protected parts. These are controlled
integration checks with injected trajectories, not a human balance/controller playtest.

The current 64-head/384-orb/200-unrelated-enemy microbenchmark measured 0.246825 ms tail
motor, 0.347757 ms tip-resolution scan and 0.046522 ms for 64 clearance checks per completed
update, with zero calling-thread managed allocations. These include 20 warmups and 120
samples per system, exclude physics/rendering and do not establish whole-frame performance.

The command-line build attempt failed on missing generated package-reference DLLs under
Temp/Bin/Debug (CS0006); Unity's Editor compilation and executed tests remain authoritative.

## Original verification (12 October; hit rules superseded above)

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
