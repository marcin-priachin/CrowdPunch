# CrowdPunch ECS Skeleton

## Folder Structure

```text
Assets/CrowdPunch/Scripts
  Authoring
  Bakers
  Components
  Systems
    Initialization
    InputBridge
    AI
    Combat
    Movement
    Physics
    Lifetime
    Presentation
    Groups
  Aspects
  Utilities
  Mono
    Player
    Camera
    UI
```

`Authoring` and `Bakers` are separate so learners can see the conversion boundary clearly. `Components` are pure ECS data. `Systems` are grouped by responsibility and update phase. `Mono` contains the traditional player, camera, and scene bootstrap code. The project stays hybrid: MonoBehaviours own the large player, ECS owns enemy simulation.

## Source Files

| File | Responsibility |
| --- | --- |
| `Authoring/EnemyAuthoring.cs` | Marker that causes an enemy prefab to bake the common ECS component layout. |
| `Authoring/SpawnerAuthoring.cs` | Inspector-facing enemy prefab and spawn counts. |
| `Authoring/AuthoredEnemyGroupAuthoring.cs` | Organizational parent for explicitly placed combat groups. |
| `Authoring/AuthoredEnemySpawnPointAuthoring.cs` | One scene-positioned enemy using an existing spawn profile. |
| `Authoring/EnemyWaveSequenceAuthoring.cs` | Ordered wave assets plus sequence-wide deterministic placement settings. |
| `Authoring/ArenaAuthoring.cs` | Inspector-facing arena bounds. |
| `Authoring/GameSettingsAuthoring.cs` | Inspector-facing match bootstrap settings. |
| `Bakers/EnemyBaker.cs` | Creates the common ECS component layout that spawn profiles populate per instance. |
| `Bakers/SpawnerBaker.cs` | Converts spawner authoring data to `SpawnSettings`. |
| `Bakers/EnemySpawnProfileBaking.cs` | Shared conversion from `EnemySpawnSettings` to an ECS profile. |
| `Bakers/AuthoredEnemySpawnPointBaker.cs` | Converts one child point and exact world position into an authored spawn request. |
| `Bakers/EnemyWaveSequenceBaker.cs` | Flattens separate wave assets and reusable profiles into ECS sequence buffers. |
| `Bakers/ArenaBaker.cs` | Converts arena authoring data to `ArenaBounds`. |
| `Bakers/GameSettingsBaker.cs` | Creates match, player snapshot, and punch singleton data. |
| `Components/Enemy.cs` | Enemy tag component. |
| `Components/Health.cs` | Current and maximum ECS health value. |
| `Components/DamageRequest.cs` | Enableable pending damage request. |
| `Components/DeathRequest.cs` | Enableable marker for zero-health entities. |
| `Components/HealthBar.cs` | Presentation-facing normalized health bar value. |
| `Components/PlayerSnapshot.cs` | ECS-readable player state from MonoBehaviour code. |
| `Components/PlayerHealthSnapshot.cs` | ECS-readable player health state from MonoBehaviour code. |
| `Components/PunchRequest.cs` | Enableable one-frame punch request. |
| `Components/EnemyMovementSettings.cs` | Enemy movement, surround, and separation tuning data. |
| `Components/EnemySeparationDistance.cs` | Per-enemy preferred spacing selected from the authored range at spawn. |
| `Components/EnemyContactDamageSettings.cs` | Enemy touch damage, push, and player invincibility tuning data. |
| `Components/DesiredMovement.cs` | AI-produced movement intent. |
| `Components/ExternalImpulse.cs` | Enableable pending physics impulse. |
| `Components/KnockbackRecovery.cs` | Enableable temporary recovery state. |
| `Components/MatchState.cs` | Match-level singleton state. |
| `Components/ArenaBounds.cs` | Play-area bounds. |
| `Components/EnemySpawnProfile.cs` | Shared baked prefab, archetype, respawn, and archetype-tuning profile. |
| `Components/EnemyWaveSequence.cs` | Wave definitions, runtime progress, completion state, ranges, profiles, and per-enemy ownership. |
| `Components/SpawnSettings.cs` | Legacy random-radius batch configuration. |
| `Components/AuthoredEnemySpawnPoint.cs` | One exact-position initial spawn request. |
| `Components/AuthoredEnemyInitialPosition.cs` | Per-enemy authored position restored by full restart. |
| `Components/RandomEnemySpawnRegion.cs` | Per-enemy legacy region used for randomized restart placement. |
| `Components/RespawnRequest.cs` | Enableable timed pool/respawn state for enemies. |
| `Systems/Groups/*.cs` | Custom update phase boundaries. |
| `Systems/Initialization/BootstrapSystem.cs` | Validates and initializes ECS match state. |
| `Systems/Initialization/EnemySpawnSystem.cs` | Owns initial crowd creation. |
| `Systems/Initialization/EnemySpawnInitialization.cs` | Shared ECS-owned initialization for one enemy from either workflow. |
| `Systems/Initialization/EnemyWaveSpawnSystem.cs` | Owns wave delay/cadence, deterministic weighted selection, safe placement, and progression. |
| `Systems/InputBridge/PlayerBridgeSystem.cs` | Copies player bridge data into ECS components. |
| `Systems/AI/EnemyChaseSystem.cs` | Produces enemy chase movement intent. |
| `Systems/Movement/EnemyMovementSystem.cs` | Applies enemy movement intent to Unity Physics velocity. |
| `Systems/Combat/PunchDetectionSystem.cs` | Detects enemies affected by punches. |
| `Systems/Combat/DamageApplicationSystem.cs` | Applies pending damage requests to ECS health values. |
| `Systems/Combat/PlayerContactDamageSystem.cs` | Detects ECS enemy overlap against the player snapshot and reports contact hits through the player bridge. |
| `Systems/Combat/EnemyLaunchCollisionSystem.cs` | Propagates launched state through qualifying solver-resolved enemy impacts. |
| `Systems/Physics/ApplyImpulseSystem.cs` | Applies gameplay impulses before physics simulation. |
| `Systems/Physics/EnemyRecoverySystem.cs` | Times enemy knockback recovery and returns control to movement. |
| `Systems/Lifetime/OutOfBoundsSystem.cs` | Marks enemies outside arena bounds. |
| `Systems/Lifetime/EnemyRespawnSystem.cs` | Resets enemies marked for respawn. |
| `Systems/Lifetime/EnemyWaveDefeatCountSystem.cs` | Counts each authoritative wave-owned defeat once before pooling. |
| `Systems/Presentation/PresentationBridgeSystem.cs` | Publishes ECS state to presentation-only consumers. |
| `Systems/Presentation/HealthBarPresentationSystem.cs` | Derives normalized ECS health bar values. |
| `Aspects/EnemyMovementAspect.cs` | Groups movement components commonly queried together. |
| `Mono/Player/PlayerEcsBridge.cs` | Dedicated player-to-ECS bridge surface and active bridge registry. |
| `Mono/Player/PlayerController.cs` | Traditional GameObject player movement shell. |
| `Mono/Player/PlayerHealth.cs` | Traditional GameObject player health value and ECS health publishing. |
| `Mono/Player/PlayerPunch.cs` | Traditional GameObject punch input shell. |
| `Mono/Camera/CameraFollow.cs` | Traditional camera follow shell. |
| `Mono/UI/GameBootstrap.cs` | Scene-level hybrid bridge wiring and game canvas prefab bootstrap. |
| `Mono/UI/PlayerHealthBar.cs` | Scene UI binding for the GameObject-owned player health bar. |
| `Mono/UI/RestartGameButton.cs` | Game UI restart action and process-local restart request bridge. |
| `Resources/GameCanvas.prefab` | Reusable game UI canvas prefab with the player health bar. |
| `Systems/Initialization/GameRestartSystem.cs` | Resets ECS-owned enemy state for restart without reloading SubScenes. |

## ECS Feature Choices

`ISystem` is used for systems because it keeps systems unmanaged and Burst-friendly. `SystemAPI` is named in system TODOs as the intended query and singleton access path because it is the current Entities 1.x style.

`IEnableableComponent` is used for `PunchRequest`, `ExternalImpulse`, `KnockbackRecovery`, and `RespawnRequest` because these are transient states. Enabling or disabling a component avoids structural changes when state frequently turns on and off.

Custom system groups make frame order explicit: bridge input, compute intent, apply impulses, simulate physics, reconcile state, then present results. This is clearer for learning than relying on default update order.

Initial enemy creation supports two coexisting authoring workflows. `SpawnerAuthoring` remains the random circular batch tool. An `AuthoredEnemyGroupAuthoring` parent organizes child `AuthoredEnemySpawnPointAuthoring` objects, each of which creates exactly one enemy at its baked world-space transform from an existing `EnemySpawnSettings` asset. Both bakers produce `EnemySpawnProfile`, and `EnemySpawnInitialization` exclusively owns the shared prefab, common movement/health/contact tuning, separation, respawn-policy, archetype component, material, ranged, explosive, and Dasher setup. `EnemyAuthoring` only establishes the prefab component layout; it does not own tuning. Invalid authored points are omitted independently and do not affect valid points or random spawners.

Wave spawning is a third coexisting workflow. Separate `EnemyWaveSettings` assets define totals, weighted existing profiles, positive-area world-space rectangles, delays, and all-at-once or batched cadence; `EnemyWaveSequenceAuthoring` supplies their order. Baking flattens these assets into unmanaged buffers and declares asset/prefab dependencies. Profile selection is weighted and deterministic, while rectangle selection is proportional to area followed by uniform sampling. Placement uses prefab-collider-derived conservative clearance, player distance, Unity Physics occupied-space queries, same-update candidate separation, and bounded later-update retries.

Wave enemies use the same `EnemySpawnInitialization` path, then receive sequence/run/wave ownership. Their per-instance respawn policy is disabled without changing the reused profile asset. The post-physics defeat counter observes `EnemyLaunchState.Defeated` exactly once before pooling and ignores every non-wave enemy. A sequence advances only after all configured instances spawned and all owned instances were defeated; final completion enables queryable `EnemyWaveEncounterComplete`. Full restart destroys prior-run wave entities, increments generation, resets seed/counters/completion, and restarts wave zero's delay. Legacy random and authored enemies retain their existing restart and respawn behavior.

Enemy movement is split into intent and application. Each enemy receives a deterministic random `EnemySeparationDistance` from the prefab's authored range when spawned. `EnemyChaseSystem` chooses whether each enemy wanders or charges and writes `DesiredMovement`; active enemies blend their selected short-range separation from nearby active enemies into both behaviors, and charging enemies also choose deterministic slots around the player. `EnemyMovementSystem` consumes that data and steers `PhysicsVelocity` toward the desired velocity instead of overwriting it instantly. This lets collision impulses survive long enough to affect other enemies. It also locks pitch and roll through `PhysicsMass.InverseInertia` so enemies remain upright while Unity Physics owns collision and position integration.

Punching follows the same data pipeline. `PlayerPunch` publishes a `PunchRequest` through the bridge; `PunchDetectionSystem` tests active enemy positions against the request volume, transitions hits to `Launched`, and enables `ExternalImpulse` and `DamageRequest`; `DamageApplicationSystem` applies damage to `Health`; `ApplyImpulseSystem` adds the impulse to `PhysicsVelocity`. Post-physics collision interpretation propagates launched state while leaving collision velocity resolution to Unity Physics, and recovery advances `Launched` through `Recovering` to `Active`.

Player health remains GameObject-owned through `PlayerHealth` because the large player is outside ECS. `PlayerEcsBridge` publishes it into `PlayerHealthSnapshot` for ECS-readable presentation or later gameplay systems. Enemy health is ECS-owned through `Health`, with `HealthBarPresentationSystem` deriving a normalized `HealthBar` value so visual bars can consume presentation data without MonoBehaviours querying enemy entities.

Enemy contact damage is detected in ECS by comparing enemy transforms with `PlayerSnapshot`. `PlayerContactDamageSystem` sends only the resulting hit event through `PlayerEcsBridge`; `PlayerHealth` owns damage and the invincibility timer, while `PlayerController` owns the GameObject knockback motion.

Enemy launch collision stays ECS-owned. `EnemyLaunchCollisionSystem` listens to Unity Physics collision events after simulation and propagates only from a `Launched` source to an `Active` or `Recovering` target above the authored solver-estimated impulse threshold. Unity Physics owns the resulting velocity; gameplay does not add a second synthetic transfer. Collision no longer requests pooling. `RespawnRequest` remains available for explicit lifecycle cleanup, though out-of-bounds detection is still a TODO.

Restart is a soft reset because the Bootstrap scene contains an auto-loaded SubScene. UI restart code should not reload the active Unity scene during play mode; `GameBootstrap` resets Mono-owned player state and `GameRestartSystem` resets ECS-owned enemy state. Random enemies own `RandomEnemySpawnRegion` and are randomized inside that same region on restart. Authored enemies own `AuthoredEnemyInitialPosition` and return exactly there. Normal post-defeat pooling remains independent: `EnemyRespawnSettings.RespawnEnabled` is honored and enabled enemies return at an arena edge, while disabled enemies remain pooled until full restart.

The aspect is intentionally small. It is useful when several systems repeatedly touch the same movement components. A plain `SystemAPI.Query` is still preferable for one-off queries.

Bakers are used instead of runtime setup MonoBehaviours because baking is the normal Entities 1.x conversion path from scene authoring to ECS data. `TransformUsageFlags` are explicit so each baked entity declares whether it needs runtime transform data.

`EntityCommandBuffer` is used by spawning because deferred structural changes are the correct extension point for entity instantiation. Combat still calls it out as the future path for enable/disable-heavy workflows inside jobs.

Blob assets are not introduced yet because the current settings are tiny scalar values. They become useful later for shared immutable data such as ability tables, animation event windows, or spawn waves.

## Extension Points

Animation can be added through a presentation-facing component or bridge system that reads ECS state and drives Animator parameters or Entities Graphics material properties.

Health uses `Health`, `DamageRequest`, and `DeathRequest` components, with damage calculation in Combat and cleanup in Lifetime. Health bar visuals should consume `PlayerHealth` for the GameObject player or ECS presentation data such as `HealthBar` for enemies.

Abilities can be modeled as data components plus enableable request components. If ability definitions grow, move static definitions into blob assets.

VFX can be spawned from presentation events produced after simulation, keeping visual effects separate from combat rules.

Audio and UI should read summarized presentation or match-state data rather than querying enemy entities directly.
