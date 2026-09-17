# Navigation validation - 2026-09-14

Implemented and checked in Unity 6000.3.10f1. This is an Editor validation slice, not a standalone performance certification. OQ-001 (target hardware, crowd size, and frame-rate target) remains unresolved.

## Open and tune

Open **Crowd Punch > Navigation > Open Validation Arena**, then enter Play mode. The entry scene is `Assets/CrowdPunch/Scenes/NavigationValidation/NavigationValidationBootstrap.unity`; it loads `NavigationValidationArena.unity` and its baked `NavigationValidationGeometry.unity` SubScene. The original Bootstrap and ten gauntlets retain their order; the two validation scenes are appended to Build Settings.

The 48 x 40 m arena contains an open southern area, two short blocks with broad alternate routes, an L, a 3 m clearance-sensitive passage, and a sealed northwest pocket. Its first mixed wave has 78 enemies (64 baseline, six ranged, four explosive, four Dashers); the second adds one Elite for 79. Existing combat profile assets supply their tuning. Greybox blocking silhouettes match authored rectangular colliders.

The ready settings asset is `Assets/CrowdPunch/Data/Settings/NavigationSettings.asset`, referenced by the geometry arena authoring. Change asset settings outside Play mode and rebake/reload. Placement units belong to each obstacle; cell size belongs to navigation. These controls do not resize one another. Keep the participation anchor in the region containing the playable entry point.

Use **Crowd Punch > Navigation > Inspect** for the live assistance comparison and optional Scene view overlay. Select each clearance class to inspect blocked cells/regions, goals, pending requests, routes, and current waypoints. Counters include queued/active searches, expanded nodes, route failures, resource limits, replans, discarded stale results, rejected spawns, and sampled navigation CPU time. No debug data is added to the game HUD.

Start tuning with clearance and silhouette correctness. Increase the global budget only when queue latency is excessive; increase per-search limits only when resource-limit counters justify it. Destination threshold, cooldown, and staggering control chasing-goal churn. Stuck duration should outlast temporary crowd compression. A coarse grid or large radius class intentionally rejects some physically narrow routes. See [architecture and defaults](../Architecture/Navigation.md).

## Automated checks actually run

`dotnet build Assembly-CSharp.csproj --no-restore`: **0 errors**, 141 existing warnings, including Unity package assembly conflicts, deprecated ColliderAspect/IAspect use, and an unused legacy aspect field. Unity also compiled and ran the baked scenes and tests.

The final `dotnet build Assembly-CSharp-Editor.csproj --no-restore` also completed with **0 errors** and 127 package-reference warnings across the incremental build. `git diff --check` passed. The final Play-mode Console query returned no errors or exceptions. Unity was left out of Play mode with the validation Bootstrap open; temporary probe entities, settings overrides, and disabled systems were discarded with the Play-mode world.

The Unity EditMode suite completed **101 tests: 100 passed, one failed**. All **12 navigation tests passed**. The remaining failure is `CombatFeedbackTests.Loop006PauseDuringFreezeNeverCapturesZeroAsResumeScale`: the existing EditMode test calls `SendMessage("Awake")` and Unity emits `ShouldRunBehaviour()` at line 35. No navigation assertion failed. This unrelated feedback test has not been suppressed or changed. The suite is therefore not reported as fully green.

Focused coverage in `Assets/CrowdPunch/Tests/Editor/NavigationTests.cs` and `NavigationSystemTests.cs`:

| Concern | Verified |
|---|---|
| Coordinates, obstacle rasterisation | World XZ origin, cell centres, bounds exclusion, blocked footprint |
| Clearance and corners (COMBAT-017/018) | Different radii, inset edges, safe edges, no diagonal cutting |
| Routing | Reachable detour, disconnected region rejection, clearance-safe smoothing |
| Bounded work | Search continuation, round-robin expansion, FIFO admission with a one-node global budget, resource limit distinct from unreachable |
| Tactical changes | Destination versions, stale entity/result rejection, disabled legacy steering |
| Lifecycle (LOOP-006) | Pooling, grid unload/reload, reset/version invalidation, restart from actual position |
| Terrain steering | High-speed braking without reducing every movement step to a crawl |
| Behavior goals (COMBAT-016/ENEMY-009) | Stable distributed alternatives and exact-setup failure cooldown |

Gauntlet test setup/teardown was adjusted to restore an empty captured Editor setup safely and to assert the original eleven build entries plus the two dedicated validation scenes. The ten-level progression assertions pass.

## Play-mode checks actually performed

These probes used baked scene entities, the existing bridge, and real Unity Physics. Where specified, AI systems were temporarily disabled or runtime entity state was set through Editor tooling to isolate a behavior; no combat settings assets were changed.

- **Routing:** isolated baseline bodies traversed the short block, L, and passage to their explicit goals. Example final XZ positions were (-7.855, 0.872) for goal (-8, 1), (-2.136, 5.889) for goal (-2, 6), and (11.222, 16.883) for goal (11.5, 17). Routes returned to direct mode. A separate body stopped near another body occupying its shared goal, demonstrating that dynamic crowd congestion remains distinct from terrain reachability.
- **Clearance and participation:** the passage accepted 0.5 m and 0.85 m bodies and rejected the 1.5 m class. The pocket rejected spawning. A final 200-active-body snapshot had quadrant counts 54/50/44/52, 184 of 184 coverage resolved goals in valid participating space, and zero bodies in the pocket (COMBAT-016). This is a measured snapshot, not a statistical distribution guarantee.
- **Elite:** with existing reservation/staging and punch systems active, an Elite routed around the west block, entered windup at about 3.35 simulated seconds, and launched its reserved normal target. Exact unreachable setup cancellation is covered separately by the ECS test. The isolated role report's later Dasher displacement included Elite interaction; the committed-dash claim below uses a separate isolated probe (ENEMY-009).
- **Ranged:** a ranged body starting 3 m from the player near the east wall moved along the wall, reached 12.68 m separation inside its authored distance band, and held the same position for the remaining approximately four seconds. It did not endlessly push outward (ENEMY-001).
- **Dasher:** an isolated committed dash at x=8 toward the east short block had zero sampled lateral displacement while Dashing, stopped at z=-0.4493, and entered Recovering. Navigation did not bend the dash (ENEMY-005).
- **Launched body:** directly updating navigation preserved a launched body's velocity exactly. Live sampling observed Launched then recovery to Active; the body remained on the approach side of the wall. Existing physics response, including its reflected motion, was retained. This probe does not establish environment-feedback audio/visual quality or add impact damage.
- **GameObject player:** the existing collision bridge resolved a full straight sweep from (-8, .5, -9) toward (-8, .5, 1) to z=-5.7701. A diagonal corner sweep resolved to approximately (-11.5445, .5, -5.5445). These exercise the long-displacement path also used by dash movement; a separate human input-driven dash session was not performed.
- **Restart:** `GauntletSequence.RestartCurrentLevel()` unloaded the old grid and enemy handles, exposed an empty-grid interval, and loaded a new grid Entity version. The fresh queue, active searches, and stale-result counters were zero. Physics and crowd state resumed in the reloaded arena (LOOP-006).
- **Wave completion:** after another fresh restart, all 78 first-wave enemies and all 79 second-wave enemies (including one Elite) spawned. Editor tooling marked owned enemies Defeated and enabled their existing death request; normal lifetime/ownership counting advanced both waves and produced `RunComplete=True`. This verifies spawn/count/transition plumbing without claiming a human combat clear.
- **Scene presentation:** an overhead capture was inspected for footprints, open routes, the L, passage, sealed pocket, and distributed bodies. The greybox image is included in the evidence folder.

These checks cover directed cases. Long-duration emergent corner congestion, every possible multi-Elite arrangement, visual/audio environment feedback, and human play feel still need a normal playtest. No claim of exhaustive gameplay verification is made.

## Measured costs and allocations

Final-code measurements used 78 and 200 active mixed bodies in the validation geometry. Explosive damage was temporarily disabled and health/state reset to keep the population stable; the 200-body case adds baseline instances and redistributes positions. Existing profile separation rules remain intact. Editor tooling and rendering are present, so these numbers are useful comparisons rather than hardware-independent limits.

**Completed system updates:** 20 warmups, then 120 updates per system/mode with tracked jobs completed. Physics and simulation time do not advance inside this microbenchmark. Times include the named AI system's tactical logic, neighbour collection/scans, scheduling, and completion, so they conservatively include more than separation alone.

| Active bodies | Assistance | Chase + separation ms | Ranged + separation ms | Dasher + separation ms | Navigation ms |
|---:|:---:|---:|---:|---:|---:|
| 78 | On | 0.04008 | 0.01492 | 0.01470 | 0.01339 |
| 78 | Off | 0.04482 | 0.01227 | 0.01196 | 0.00401 |
| 200 | On | 0.04238 | 0.01953 | 0.02081 | 0.02405 |
| 200 | Off | 0.06127 | 0.01434 | 0.01329 | 0.00699 |

All four measured systems reported **zero managed bytes on the calling thread over 120 updates** in both modes. This does not imply zero native allocations: existing separation collections use temporary native storage, route buffers can grow to their bounded capacity, and search scratch allocates on grid initialization. The 1,920-cell/four-slot node+heap payload is 215,040 bytes plus slot/queue/allocator overhead. Sampled route-buffer capacity before the final captures was 8,184 bytes at 78 bodies and 8,664 bytes at 200 bodies; it can grow as more routes are requested.

**Live frame sampling:** 180 rendered frames per mode after 20 warmups, with physics running. `CrowdPunch.Navigation` records CPU time across that frame's navigation updates. `GC.Alloc` below covers the whole Editor frame and cannot be attributed to navigation.

| Active bodies | Assistance | Navigation mean ms | Navigation max ms | Whole-Editor GC mean B/frame |
|---:|:---:|---:|---:|---:|
| 78 | On | 0.02995 | 0.1019 | 8,040 |
| 78 | Off | 0.01113 | 0.0307 | 8,157 |
| 200 | On | 0.09720 | 0.1993 | 8,175 |
| 200 | Off | 0.01554 | 0.0547 | 8,292 |

The separation-containing systems together measured about 0.083 ms/update at 200 bodies with assistance enabled. They exceed the isolated navigation microbenchmark but did not constitute a material absolute crowd bottleneck in this representative Editor workload. The existing scans were retained to preserve per-archetype behavior; no shared spatial index is justified by these measurements alone. Their quadratic scaling remains a limitation at larger crowds and must be reassessed when OQ-001 establishes a target. No standalone build, target-device frame-rate, or broad 500+ crowd performance claim is made.

## Combat boundaries and remaining limitations

Ranged projectiles retain their explicit swept-player hit test and collider filter excluding Default geometry: they **pass through obstacles**. Explosions retain radial overlap behavior without cover/occlusion. Launch homing has no terrain line-of-sight test and can steer toward an obstructed target; physical bodies still collide with the wall. These are existing combat rules, explicitly left unchanged by this slice. Wall-impact damage, bounce rewards, and hazard damage were not added.

Navigation supports one flat, static, axis-aligned arena grid at a time, explicit rectangular solids, and three conservative radius classes. It does not support moving/destructible obstacles, slopes, runtime rebaking, procedural layout generation, or hazard costs. Obstacles outside the navigation authoring subtree can still physically collide but will not be represented in this grid; keep participating solids under the arena authoring. Invalid authoring stops an impossible wave with a diagnostic rather than silently dropping required enemies.

Evidence is retained in [NavigationEvidence](NavigationEvidence/): TestRunner XML, final microbenchmarks/live samples, targeted role/physics/restart/coverage reports, and the arena overview. `Library/NavigationValidation` contains the local build log and working captures. The report distinguishes observations from outstanding manual validation so a passing C# build is not mistaken for complete gameplay proof.

## Automatic participation anchor follow-up - 2026-09-17

Baking now derives the default anchor from the world XZ spacing-bounds centre, falling back to the nearest cell centre clear for all configured radius classes. Enable Override Participation Anchor only when a different connected region is intended. Stored coordinates are ignored while the override is off; invalid explicit points are reported without relocation.

All 16 navigation EditMode tests passed, including four new cases for translated bounds, blocked-centre fallback, explicit region selection/invalid override, and insufficient clearance. Runtime C# build passed with zero errors and 140 existing warnings. In Gauntlet 01 Play mode, the baked anchor was (0, 0), valid for all three classes; its configured one-enemy wave spawned and entered AwaitingActivation. The Console contained no errors or exceptions. The original SubScene view was restored afterward. Test output: `NavigationEvidence/anchor-tests.xml`.
