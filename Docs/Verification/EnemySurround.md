# Occupancy-aware Baseline surround

## Refresh work optimisation (2026-11-13)

Scope: approved optimisation 4 only, preserving COMBAT-016 and ENEMY-012.
`EnemySurroundSystem` now caches each eligible actor's clearance class, current
navigation region, territory limits and ring dimensions for one refresh. A candidate's
offset is reused through availability, lane evaluation and assignment. Lane occupancy
queries skip cells already covered by the preceding sample's neighbourhood; the original
sample positions, full-segment distance test, self exclusion and endpoint rules remain.
Reservations are still checked against the evolving reserved map. There is no cross-refresh
cache, altered refresh interval, new search budget or change to candidate/ring order,
sorting, tie breaks, retention, promotion, contact attacks, physics or presentation.
Other proposed optimisations were left untouched.

### Behaviour verification

Before editing the planner, captured assignment fingerprints covering the assigned flag,
slot ID and exact offset bits for 96 mixed actors over eight updates, with and without a
navigation grid. Both sequences match after the change. They cover different radii/ring
profiles, unsupported clearance radii, disconnected navigation regions, reservations,
committed actors, direct pursuers, reserves, pooling/recovery, a launched vacancy, small
and large player movement, suspension/resume and arena-edge placement.

Three additional occupancy cases compare the optimized lane query with an independent
body-first exhaustive oracle: 256 queries each at cell sizes 1, 2.5 and 4 (768 comparisons).
They cover zero-length checks, axial/diagonal segments, negative coordinates, exact cell
boundaries, differing body/query radii and self exclusion.

Unity 6000.3.10f1 loaded the new planner and tests with no compilation failure. All
134 focused Editor cases passed:

| Suite | Passed |
| --- | ---: |
| EnemySurroundTests | 27 |
| EnemyCrowdTacticsTests | 16 |
| DirectContactPursuitTests | 19 |
| BarricadeRoomMovementTests | 23 |
| EnemyArenaDistributionTests | 13 |
| ProtectedPointTests | 17 |
| NavigationSystemTests | 19 |

`dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero errors and
309 existing/package warnings using installed-SDK access. `git diff --check` passed.
An initial native-list write compilation issue was fixed; results obtained before the
new assembly loaded were discarded. The final run explicitly verified that Unity had
loaded the optimized actor geometry and occupancy test. No scene or settings assets changed.

### Same-session planner cost comparison

Synthetic 100 x 100 m arena, deterministic body positions, 30 warmups and 50 measured
updates per workload. Physics, chase, navigation-system execution and rendering are
excluded; the navigation variant includes a static obstacle grid used by the planner.
The initial post-reload run included asynchronous Burst compilation and was discarded.
The final measurements below use warmed compiled code, compared with the captured
pre-change planner on the same Editor machine.

| Bodies | Navigation grid | Refresh mean before / after | Fresh allocation mean before / after |
| --- | --- | --- | --- |
| 148 | No | 1.7599 / 1.3530 ms | 2.0025 / 1.3345 ms |
| 512 | No | 9.1695 / 7.4866 ms | 8.9753 / 6.9415 ms |
| 148 | Yes | 2.3263 / 1.7054 ms | 2.6220 / 1.5655 ms |
| 512 | Yes | 10.7712 / 7.9252 ms | 10.4342 / 7.6960 ms |

At 512 bodies, warmed refresh means were about 18% lower without the grid and 26% lower
with it. Steady-update means were 0.0873/0.0851 ms without/with the grid, compared with
0.0893/0.0903 ms before. The 148-body no-grid steady probe had an Editor outlier (1.8051 ms
maximum), so its mean is not useful as an optimisation comparison. These samples are not
whole-frame FPS, target-hardware acceptance under OQ-001, or a new balance/visual playtest.
Raw local outputs are `Temp/SurroundOptimisationBefore.txt` and
`Temp/SurroundOptimisationAfterWarm.txt`.

## Contact eligibility follow-up (2026-10-30)

This follow-up supersedes the arrival/clear-lane prerequisites described in the original
2026-10-29 verification record below. Pursuing Baselines now advance ordinary contact
cadence whenever within Contact Attempt Distance, even if queued or still travelling to
a reserved position. Clear lanes remain a positioning preference; they do not veto a
wind-up. Cooldown, wind-up, committed direction/duration, separation and physics still apply.
No wave, level geometry, damage, speed or spawn-cadence tuning was changed in this follow-up.

Removed the unused attack-eligibility flag from EnemySurroundAssignment. Renamed Surround
Arrival Distance to Surround Movement Refresh Distance: its remaining responsibility is
triggering an early occupancy refresh after player movement. Serialized values migrate via
FormerlySerializedAs; the current saved value of 3 m and Whole Arena selection were verified
in Unity after migration.

Verification: Unity compilation and the C# build passed (zero errors, 318 existing warnings).
108 targeted Editor tests passed: EnemySurroundTests 18; EnemyCrowdTacticsTests 16;
BarricadeRoomMovementTests 23; EnemyArenaDistributionTests 13; ProtectedPointTests 17;
NavigationSystemTests 13; DeliberateShotTests 8. New checks execute surround -> chase and
verify wind-up before slot arrival, wind-up/committed charge despite a crowd blocker in
both open space and a corner, and retained range/cooldown requirements. Movement-only
formation tests explicitly set a cooldown so they test positioning between attacks.
Existing committed-direction, lifecycle, interception and room tests also passed.

These checks establish relaxed eligibility, not that every attempt will land or that corner
camping has been eliminated. No additional live balance playtest was performed for this
follow-up; the live samples below describe the earlier surround implementation.

## Original verification record (2026-10-29)

Date: 2026-10-29. Requirements: COMBAT-016, ENEMY-012, VISION-002.

## Change

`EnemySurroundSystem` runs after tactics and before chase. Active pursuing Baselines reserve
distinct player-relative positions rather than using entity-index angles. Closest bodies
get first access to inner positions; complete inner rings precede outer waiting rings.
Small crowds also use full-circle spacing. Valid reservations persist. A blocked approach
causes a search for another open angle, with clear lanes preferred in the same ring and
nearest distance deciding between equivalent candidates. A single pursuer can search the
whole inner circle when another archetype blocks its original approach.

The system uses spatial hashes for physical occupancy and goal reservations, plus static
navigation clearance/connected-region checks when a navigation grid exists. Candidate
generation is bounded. Invalid boundary/obstacle points are skipped rather than collapsed
onto common clamped targets. Overflow with no available point holds and retries.

Chase uses short arc waypoints to change sides without aiming through the player. New
contact attempts require arrival at the reserved point and a clear physical approach.
Existing wind-ups and direction-committed charges continue. Reserve/interception roles,
protected-point selection, active pillar groups and special archetypes keep their owners.
Launch/recovery/defeat and pooling invalidate surround reservations. Outer bodies may fill
freed inner positions on subsequent occupancy updates.

New baked settings in Game Runtime Settings:

| Setting | Initial value |
| --- | --- |
| Surround Reassignment Seconds | 0.5 s |
| Surround Arrival Distance | 1.25 m |

Substantial player displacement can trigger occupancy refresh sooner. Offsets follow the
current player position between refreshes. Existing user settings were preserved, including
Whole Arena, Flank Distance 24 and Group Radius 16. Baseline movement/contact tuning was not
edited. Slot spacing is at least the leading profile's Separation Distance Min/body diameter;
Surround Distance sets each profile's inner radius and larger Ring Spacing widens outer rings.

## Automated checks

- Unity 6000.3.10f1 compilation passed.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed: zero errors,
  318 existing package/reference/deprecation warnings. Build used approved Windows SDK access.
- 105 targeted Editor tests passed: EnemySurroundTests 15; EnemyCrowdTacticsTests 16;
  BarricadeRoomMovementTests 23; EnemyArenaDistributionTests 13; ProtectedPointTests 17;
  NavigationSystemTests 13; DeliberateShotTests 8.
- Tactics and room movement fixtures now include the surround component/system, checking
  the real tactics -> surround -> chase sequence rather than only the earlier behavior.
- Surround tests cover one-sided queues, unique positions/full-circle inner coverage,
  nearest free side and arc routing, stable reservations/player translation, clear arrival
  before contact, a newly blocked approach, promotion into a launched body's opening,
  lifecycle exclusions, interception preservation, committed attacks, arena edges and
  static-obstacle rejection.
- The defaults test creates default settings independently of the saved editable asset,
  allowing user-selected participation and tuning without a false regression failure.

## Live Editor integration

Previewed Campaign 08 through Bootstrap with EditorPreview enabled, avoiding campaign-save
writes. The baked shared settings retained Whole Arena. Observed distinct reservations,
normal advancing positions through physics, preserved interception roles and ordinary
contact charges. Temporary health resets kept the observation run active; these were
discarded on exiting Play Mode. No runtime testing changes were saved to scenes/assets.

Selected live samples from the final 24-second observer:

| Real time | ECS time | Frame | Valid positions | Waiting without a point | Charging | Positions changed since prior sample |
| --- | --- | --- | --- | --- | --- | --- |
| 16.4 s | 46.65 s | 3077 | 31 | 0 | 2 | 19 |
| 17.4 s | 47.67 s | 3115 | 31 | 0 | 4 | 31 |
| 20.5 s | 50.49 s | 3300 | 24 | 0 | 2 | 24 |
| 22.5 s | 52.37 s | 3424 | 40 | 0 | 3 | 32 |
| 23.5 s | 53.38 s | 3487 | 40 | 0 | 2 | 40 |
| 24.5 s | 54.24 s | 3535 | 24 | 0 | 3 | 24 |

At 23.5 seconds, nearby bodies occupied all eight angular sectors with counts
7, 4, 5, 5, 3, 4, 8, 2. Participation/committed attacks explain the changing number of
pursuers as the tactics planner alternates pursuit and interception. A subsequent snapshot
confirmed 24 valid positions and 24 unique slot IDs among the 40 roots, with no allocation
failures. This establishes distributed physical movement, not perfectly uniform body
positions every frame: travel, attacks, collisions and static obstacles remain meaningful.

Temporary captures and observer data live under Temp. The original Campaign 09 Editor
scene was restored. Existing Synty Sidekick window errors and transient Pipeline transport
errors occurred around Editor transitions; no new gameplay-system errors were observed.

## Isolated performance

Synthetic 100 x 100 m arena, all Baselines pursuing; 30 warmups. Steady updates use
200 samples, retained/fresh allocations 50 samples each. Every actor obtained a position.
Timings exclude the static grid, navigation, chase, physics and rendering; these are
planner costs in this Editor session, not frame-rate claims or target-hardware guarantees.

| Bodies | Steady mean / max | Occupancy refresh mean / max | Fresh allocation mean / max |
| --- | --- | --- | --- |
| 148 | 0.0274 / 0.1036 ms | 1.0327 / 1.4369 ms | 1.9217 / 2.9674 ms |
| 512 | 0.0752 / 0.1275 ms | 6.5490 / 9.1117 ms | 5.8283 / 6.3263 ms |

Occupancy work normally runs twice per second, with earlier refresh after player movement.
Dense larger crowds and obstacle-heavy arenas still need representative player/hardware
profiling under OQ-001. Human playtesting should check pressure while moving/dashing,
whether the 1.25 m arrival tolerance feels responsive, and how quickly opened sides refill.
