# Baseline crowd tactics verification

Date: 2026-10-28. Rules: COMBAT-016, ENEMY-012, VISION-002, BARRICADE-008, ENEMY-010.

## Implemented behavior and settings

Baseline pressure uses approximately 60% direct pursuit and two interception groups sharing
the remaining 40%. Closest participants pursue; remaining participants are divided by their
position relative to player movement. Interceptors follow frozen lateral/interception
destinations, then close in on arrival or timeout. Direction changes do not continuously
retarget the maneuver. Lost members do not immediately receive replacements. Small crowds
pursue together when two meaningful interception groups cannot be formed. Active contact
commitments take priority across replanning.

Game Runtime Settings defaults:

| Setting | Value |
| --- | --- |
| Crowd Participation | Regional; Whole Arena also selectable |
| Pursuit Share | 0.6 |
| Crowd Engagement Radius | 45 m |
| Crowd Disengagement Margin | 10 m |
| Interception Prediction Seconds | 1.25 s |
| Maximum Interception Prediction Distance | 16 m |
| Interception Flank Distance | 10 m |
| Interception Commitment Seconds | 4 s |
| Interception Close In Seconds | 2 s |
| Crowd Group Radius | 4 m |
| Reserve Group Count | 6 |
| Minimum Interception Group Size | 4 |
| Maximum Approaching Explosives | 30, migrated from the former shared cap |

Regional participation uses hysteresis at planning boundaries; engaged members may remain
within 55 m before returning to reserve. Reserve neighbors gather at the closest shared
area with stable individual offsets. Targets are clamped to arena/spawn-room territory;
navigation resolves static obstacles. Settings are baked snapshots: allow SubScene rebaking
and restart after Inspector edits. This does not add a gameplay settings menu.

## Automated verification

Unity 6000.3.10f1 compiled runtime/editor scripts successfully. The final
`dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero errors and
318 existing package/reference/deprecation warnings. The initial sandboxed attempt was
unable to read Windows SDK metadata; the build passed with approved SDK access.

90 targeted Editor tests passed:

- EnemyCrowdTacticsTests: 16. Includes the default asset, actual planner/chase split,
  frozen targets after player direction changes, arrival/timeout and closing interval,
  Regional hysteresis and Whole Arena selection, small populations, launch/recovery/
  defeat exclusions, pooling eligibility, special-archetype exclusions, reserve clustering,
  bounded dash-speed prediction, committed contact attacks, and interception routing
  around an obstacle without writing physics transforms.
- BarricadeRoomMovementTests: 23. Uses the new planner before chase and verifies territory,
  player room transitions, preserved movement/launch physics and special-archetype rules.
- EnemyArenaDistributionTests: 13. Includes preserved Explosive proximity/separation rules.
- ProtectedPointTests: 17.
- NavigationSystemTests: 13.
- DeliberateShotTests: 8.

The room-distribution tests now start bodies across their territory when checking coverage;
neighbors intentionally gather at common areas instead of receiving unrelated uniform slots.

## Live Editor integration

Previewed Campaign 08 through Bootstrap using the existing EditorPreview path, without
writing campaign completion. Baked enemies had EnemyCrowdAssignment and baked settings
contained Regional participation and a 0.6 pursuit share. The initial 35 m regional radius
left only a small attacking population in this 100 x 100 m arena; the saved default was
raised to 45 m after comparing body distances. Concentrated distant reserves were inspected
in the Game view. A subsequent preview used the final settings.

A temporary runtime-only switch to Whole Arena produced the following advancing samples:

| Elapsed real time | Frame | Active Baselines | Pursuit | Interception | Reserve | Bodies moving since previous sample |
| --- | --- | --- | --- | --- | --- | --- |
| 0.50 s | 961 | 39 | 23 | 16 | 0 | First sample |
| 1.01 s | 975 | 39 | 23 | 16 | 0 | 39 |
| 2.57 s | 1015 | 40 | 23 | 16 | 1 | 39 |
| 4.63 s | 1077 | 40 | 39 | 0 | 1 | 38 |
| 5.15 s | 1097 | 40 | 39 | 0 | 1 | 39 |

The additional active body waited in reserve until a later plan rather than immediately
filling a group. The interception group transitioned to pursuit after its maneuver.
The probe restored Regional mode; all probe changes were discarded on exiting Play Mode.
Temporary samples/screenshots are under Temp and are not product assets. The original
Campaign 09 Editor scene was restored. The previously present Synty Sidekick downloader
window error recurred on Play Mode entry; no new gameplay-system errors were observed.

## Isolated planner profiling

30 warmups and 200 measured Editor samples per case, 100 x 100 m synthetic arena.
Whole Arena mode; steady updates measured separately from full replans. These timings
exclude existing chase/separation, navigation, physics and rendering and are not FPS claims.

| Bodies | Steady mean / maximum | Replan mean / maximum |
| --- | --- | --- |
| 148 | 0.0194 / 0.0261 ms | 0.0334 / 0.0771 ms |
| 512 | 0.0614 / 0.0837 ms | 0.1102 / 0.2410 ms |
| 2,000 | 0.2300 / 0.3392 ms | 0.5855 / 1.2853 ms |

## Remaining playtest work

Assess escape-route pressure while moving, turning, dashing and launching through groups
in representative mixed waves, arena edges and dense obstacle layouts. Check whether
the four-second maneuver allows enough travel and whether the two-second closing interval
rewards opened gaps. The proposed participation distances and group spacing remain tuning;
these checks establish integration and behavior, not a finished difficulty balance.
OQ-001 target hardware/FPS requirements remain unresolved.
