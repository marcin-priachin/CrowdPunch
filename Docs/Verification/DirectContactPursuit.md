# Timed direct-contact pursuers

Date: 2026-10-30. Requirements: COMBAT-016, ENEMY-012.

## Configuration and behavior

Game Runtime Settings now exposes the following baked fields under Baseline Direct Contact Pursuit:

| Setting | Starting value |
| --- | --- |
| Minimum Direct Contact Pursuers | 2 |
| Maximum Direct Contact Pursuers | 4 |
| Direct Contact Pursuer Resample Seconds | 3 s |

The first eligible population triggers an immediate draw. A deterministic ECS random stream
uniformly samples the inclusive count again after each interval; consecutive draws may repeat.
The sampled target is capped by the active eligible population. Membership continuously follows
the closest eligible bodies; distance changes and vacancies do not reroll the count.
Shrinking the target releases the farther selected members. Both endpoints zero disables
the extra subset; equal endpoints give a fixed count. Reversed/negative endpoints are
normalized. The inclusive sampler also handles int.MaxValue without overflow.

Selected Baselines directly target the player's current position between their ordinary
contact attempts, overriding surrounding, interception or reserve destinations in both
participation modes. They retain contact range/cooldown/wind-up, committed directions,
local separation, obstacle navigation and physics. Other Baselines retain ordinary tactics
and can still attempt contact: this count is not a global attack cap.

The enableable DirectContactPursuit tag is baked disabled and reset on pooling. Selection
excludes other archetypes, inactive/pooled bodies, Snakes, off-room members and still-bound
pillar groups. Protected-point selection takes precedence; an unavailable player or empty
population resets sampling so a new draw occurs when eligible bodies return.

System flow: PlayerBridgeSystem -> EnemyCrowdTacticsSystem -> DirectContactPursuitSystem ->
EnemySurroundSystem -> EnemyChaseSystem -> existing navigation/movement/physics. There are
no runtime structural changes, Mono enemy queries or additional HUD elements.

## Closest-membership follow-up (2026-10-30)

Membership now continuously follows the closest eligible Baselines by horizontal distance,
with entity index resolving equal-distance ties. Closer newcomers, player movement and
enemy movement can replace farther selected members before the next count draw. Only the
target count remains fixed until its resampling interval; distance changes never reroll it.
Existing committed contact attacks still finish even when their direct-pursuit tag is lost.

The selector checks the farthest selected member against the nearest unselected member.
When this boundary is correctly ordered and the count is correct, no sort or tag writes
are needed. Otherwise distance sorting chooses the nearest subset and only changed flags
are written. Previous membership has no ranking preference.

Unity compilation and the C# build passed (zero errors, 318 existing warnings). 84 targeted
Editor checks passed: DirectContactPursuitTests 19, EnemyCrowdTacticsTests 16,
EnemySurroundTests 18, BarricadeRoomMovementTests 23 and DeliberateShotTests 8. The new
regressions specifically cover closer newcomers, player displacement, enemy movement and
equal-distance tie stability. The count/timer, lifecycle and commitment tests also passed.

Final isolated timings after Play Mode warm-up, 30 warmups and 200 samples per case:

| Active bodies | Steady mean / max | Selection/refill mean / max |
| --- | --- | --- |
| 148 | 0.0197 / 0.0409 ms | 0.0429 / 0.0607 ms |
| 512 | 0.0640 / 0.1130 ms | 0.0837 / 0.1862 ms |
| 2,000 | 0.2463 / 0.4113 ms | 0.3530 / 0.5678 ms |

These exclude other gameplay/physics/rendering and are not game frame-rate guarantees.
The original integration and verification record below predates this membership correction.

## Original verification

- Unity 6000.3.10f1 compilation passed. The C# build passed with zero errors and 318 existing
  package/reference/deprecation warnings, using approved Windows SDK metadata access.
- 125 targeted Editor tests passed: DirectContactPursuitTests 17, EnemySurroundTests 18,
  EnemyCrowdTacticsTests 16, BarricadeRoomMovementTests 23, EnemyArenaDistributionTests 13,
  ProtectedPointTests 17, NavigationSystemTests 13, DeliberateShotTests 8.
- New checks cover timed/inclusive resampling and held counts, stable nearest assignments,
  vacancy replacement, population limits and new arrivals, zero/fixed/reversed/large ranges,
  all tactical-role overrides, normal wind-up and committed direction after deselection,
  unavailable player/protected-point suppression, and archetype/room exclusions.
- Existing tactics/room fixtures also run the new system with a zero range to verify that
  disabling direct pursuit preserves their established behavior.

## Live baked integration

Campaign 08 was previewed through Bootstrap using EditorPreview, without writing campaign
progress. The baked singleton contained minimum 2, maximum 4 and interval 3 s; runtime enemy
roots carried the new enableable tag. A temporary observer reset player health to keep
the preview active, then discarded those changes on leaving Play Mode.

| ECS time | Selected | In wind-up | Other selected members targeting player |
| --- | --- | --- | --- |
| 31.28 s | 2 | 0 | 2 |
| 33.28 s | 2 | 1 | 1 |
| 34.29 s | 2 | 0 | 2 |
| 36.87 s | 2 | 2 | 0 |
| 37.30 s | 3 | 2 | 1 |
| 37.74 s | 3 | 0 | 3 |
| 39.84 s | 3 | 0 | 3 |

The repeated count of two across consecutive draws and later change to three are valid
random outcomes. Wind-up pauses were retained; all selected members outside wind-up targeted
the player directly. No new gameplay errors appeared; the existing Synty Sidekick window
error recurred on Play Mode entry. The original Campaign 09 scene was restored afterward.

## Isolated selector profiling

30 warmups, 200 measured samples per case. Selection/refill timings force missing assignments;
resetting flags is outside the measured call. Results exclude tactics, surround, navigation,
physics and rendering, and do not establish game frame rate or OQ-001 hardware targets.

| Active bodies | Steady mean / max | Selection/refill mean / max |
| --- | --- | --- |
| 148 | 0.0193 / 0.0442 ms | 0.0255 / 0.0541 ms |
| 512 | 0.0626 / 0.1259 ms | 0.0920 / 0.1689 ms |
| 2,000 | 0.2575 / 0.4336 ms | 0.3929 / 0.7753 ms |

The 2-4/3-second starting balance remains playtest tuning. Inspector changes require the
normal SubScene rebake/restart; the feature does not make shared ScriptableObjects live ECS data.
