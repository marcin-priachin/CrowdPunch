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
| `Authoring/EnemyAuthoring.cs` | Inspector-facing enemy movement settings. |
| `Authoring/SpawnerAuthoring.cs` | Inspector-facing enemy prefab and spawn counts. |
| `Authoring/ArenaAuthoring.cs` | Inspector-facing arena bounds. |
| `Authoring/GameSettingsAuthoring.cs` | Inspector-facing match bootstrap settings. |
| `Bakers/EnemyBaker.cs` | Converts enemy authoring data to enemy ECS components. |
| `Bakers/SpawnerBaker.cs` | Converts spawner authoring data to `SpawnSettings`. |
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
| `Components/EnemyMovementSettings.cs` | Enemy movement tuning data. |
| `Components/EnemyContactDamageSettings.cs` | Enemy touch damage, push, and player invincibility tuning data. |
| `Components/DesiredMovement.cs` | AI-produced movement intent. |
| `Components/ExternalImpulse.cs` | Enableable pending physics impulse. |
| `Components/KnockbackRecovery.cs` | Enableable temporary recovery state. |
| `Components/MatchState.cs` | Match-level singleton state. |
| `Components/ArenaBounds.cs` | Play-area bounds. |
| `Components/SpawnSettings.cs` | Enemy spawn configuration. |
| `Components/RespawnRequest.cs` | Enableable timed pool/respawn state for enemies. |
| `Systems/Groups/*.cs` | Custom update phase boundaries. |
| `Systems/Initialization/BootstrapSystem.cs` | Validates and initializes ECS match state. |
| `Systems/Initialization/EnemySpawnSystem.cs` | Owns initial crowd creation. |
| `Systems/InputBridge/PlayerBridgeSystem.cs` | Copies player bridge data into ECS components. |
| `Systems/AI/EnemyChaseSystem.cs` | Produces enemy chase movement intent. |
| `Systems/Movement/EnemyMovementSystem.cs` | Applies enemy movement intent to Unity Physics velocity. |
| `Systems/Combat/PunchDetectionSystem.cs` | Detects enemies affected by punches. |
| `Systems/Combat/DamageApplicationSystem.cs` | Applies pending damage requests to ECS health values. |
| `Systems/Combat/PlayerContactDamageSystem.cs` | Detects ECS enemy overlap against the player snapshot and reports contact hits through the player bridge. |
| `Systems/Combat/EnemyEnemyCollisionRespawnSystem.cs` | Detects high-speed enemy-enemy physics collisions and requests delayed respawn. |
| `Systems/Physics/ApplyImpulseSystem.cs` | Applies gameplay impulses before physics simulation. |
| `Systems/Physics/EnemyRecoverySystem.cs` | Times enemy knockback recovery and returns control to movement. |
| `Systems/Lifetime/OutOfBoundsSystem.cs` | Marks enemies outside arena bounds. |
| `Systems/Lifetime/EnemyRespawnSystem.cs` | Resets enemies marked for respawn. |
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

Enemy movement is split into intent and application. `EnemyChaseSystem` chooses whether each enemy wanders or charges and writes `DesiredMovement`; `EnemyMovementSystem` consumes that data and steers `PhysicsVelocity` toward the desired velocity instead of overwriting it instantly. This lets collision impulses survive long enough to affect other enemies. It also locks pitch and roll through `PhysicsMass.InverseInertia` so enemies remain upright while Unity Physics owns collision and position integration.

Punching follows the same data pipeline. `PlayerPunch` publishes a `PunchRequest` through the bridge; `PunchDetectionSystem` tests enemy positions against the request volume and enables `ExternalImpulse` and `DamageRequest`; `DamageApplicationSystem` applies the damage to `Health`; `ApplyImpulseSystem` adds the impulse value to `PhysicsVelocity`; `EnemyRecoverySystem` keeps movement from immediately overriding the knockback.

Player health remains GameObject-owned through `PlayerHealth` because the large player is outside ECS. `PlayerEcsBridge` publishes it into `PlayerHealthSnapshot` for ECS-readable presentation or later gameplay systems. Enemy health is ECS-owned through `Health`, with `HealthBarPresentationSystem` deriving a normalized `HealthBar` value so visual bars can consume presentation data without MonoBehaviours querying enemy entities.

Enemy contact damage is detected in ECS by comparing enemy transforms with `PlayerSnapshot`. `PlayerContactDamageSystem` sends only the resulting hit event through `PlayerEcsBridge`; `PlayerHealth` owns damage and the invincibility timer, while `PlayerController` owns the GameObject knockback motion.

Enemy-enemy collision respawn stays ECS-owned. `EnemyEnemyCollisionRespawnSystem` listens to Unity Physics collision events after simulation and enables `RespawnRequest` when enemy relative velocity exceeds the gameplay threshold. `EnemyRespawnSystem` treats enabled `RespawnRequest` as a short pool state, keeps the enemy inert off-arena, then returns it after the delay to a random arena edge that avoids the current player snapshot.

Restart is a soft reset because the Bootstrap scene contains an auto-loaded SubScene. UI restart code should not reload the active Unity scene during play mode; `GameBootstrap` resets Mono-owned player state and `GameRestartSystem` resets ECS-owned enemy state.

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
