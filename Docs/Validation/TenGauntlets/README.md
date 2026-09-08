# Ten-gauntlet validation

Validated in Unity 6000.3.10f1 on 2026-09-08. The [design record](../../Design/TenGauntletProgression.md) contains the progression, layout decisions, provisional duration targets, and first-playtest tuning priorities.

## Executed checks

- [Editor assertions](editor-tests.txt): 16/16 passed, invoked directly through a Unity Editor command. These inspect the serialized scenes and assets and exercise isolated ECS restart, completion, and elite pooling cases. They were not run through the Test Runner UI.
- [Full sequence](lifecycle.txt): all ten additive levels and 39 waves baked/loaded and spawned to their exact configured counts. The final run includes the restart physics warmup correction. The probe restored an idle player's health and cleared stages through injected `DamageRequest`; it did not bypass wave activation or completion. Both elite stages reused a defeated normal entity, reversed its defeat count on return, and stopped replenishment for eight observed seconds after elite defeat. No leaked ownership, stuck wave, escaped player, or runtime exception was observed in this run.
- [Input check](input-check.txt): activated the actual Play Again menu button, then used a virtual gamepad to drive the existing Move/Look/Punch/Dash actions through levels 1-2. Enemy positions informed the input driver's aim, but enemy damage came from the ordinary combat pipeline. Player health was restored without resetting normal invulnerability. This demonstrates functional controls and propagated launches, not novice learnability or unassisted survival.
- [Retry check](retry.txt): induced player death while a ranged projectile existed, then invoked the normal bootstrap retry. Also restarted the finale with a live Dasher. Inspected old roots and linked children, projectile cleanup, restored player entry/health, fresh wave counters, and completion reset.
- [Asset audit](asset-audit.txt): 115 scene dependencies, 382 serialized GUID references, no unresolved references, and no duplicate GUIDs among 301 CrowdPunch metadata files. The original four scene and SubScene identities were retained. Git comparison against `6500b7c` found no changes in existing enemy/projectile prefabs, non-wave settings, AI/combat/movement/physics systems, player code, or camera code.
- Compilation: `dotnet build Assembly-CSharp.csproj --no-restore -v:q` and `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q` both completed with zero errors. Each reported 119 existing package assembly-resolution warnings (`MSB3277`, including System.Net.Http/Unity AI dependencies). Unity refreshed/compiled the code and executed the checks. No standalone player build was produced.

## Population and profiling

| Level | Peak active bodies | Retained roots at final clear |
|---|---:|---:|
| 01 | 10 | 19 |
| 02 | 14 | 26 |
| 03 | 14 | 21 |
| 04 | 17 | 28 |
| 05 | 19 | 32 |
| 06 | 22 | 40 |
| 07 | 23 | 43 |
| 08 | 24 | 44 |
| 09 | 28 | 52 |
| 10 | 30 | 82 |

[ProfilerRecorder samples](profiling.txt) cover levels 7-10 in the final lifecycle run. Median wave-spawn system marker time was 0.064-0.074 ms; the largest sampled marker was 5.33 ms at the level 7 spawn. Defeat-accounting medians were 0.016-0.028 ms, with a 0.314 ms maximum. The selected elite-replenishment marker produced no positive samples, so no timing claim is made for it. These are Editor marker samples, not whole-system job-completion costs or a target-hardware benchmark. Editor frame medians near 16.7 ms include pacing, tools, and Editor overhead. Retained roots include pooled enemies; they are not the complete entity count with linked render children and projectiles.

## Visual inspection and limits

Gameplay-camera captures: [First Line](FirstLine.png), [Side Step](SideStep.png), [finale](Finale.png). Game-view captures include the [opening prompt](OpeningHint.png) and [completion menu](RunComplete.png). Inspected readable perimeter geometry, floor lanes, enemy silhouettes, pooling occlusion, and menu fit. Low visible rails retain taller collision faces for the existing player sphere cast. A collider-free backdrop hides pooled bodies below the floors.

The continuous convex layouts avoid pathfinding and projectile-cover assumptions. All authored spawn-region corners and movement-bound corners pass clearance checks; all waves spawned during the run. This does not exhaustively test every player camping position or camera angle. Full unassisted play, novice comprehension, deliberate versus arbitrary shot efficiency in every encounter, target duration, and the difficulty curve remain human playtest work. Keep the provisional tuning within the existing enemy profiles and population budget.

To repeat the content/lifecycle probe, save scenes and use **Crowd Punch > Levels > Run Sequence Lifecycle Smoke Check** from Edit mode. It enters Play mode from Bootstrap and writes logs/captures to `Temp/GauntletValidation`. It intentionally injects damage and restores player health; exit Play mode before normal playtesting. The separate input, retry, and profiler probes were temporary Editor commands, with their results preserved here.
