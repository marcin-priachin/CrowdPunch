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
| `Components/PlayerSnapshot.cs` | ECS-readable player state from MonoBehaviour code. |
| `Components/PunchRequest.cs` | Enableable one-frame punch request. |
| `Components/EnemyMovementSettings.cs` | Enemy movement tuning data. |
| `Components/DesiredMovement.cs` | AI-produced movement intent. |
| `Components/ExternalImpulse.cs` | Enableable pending physics impulse. |
| `Components/KnockbackRecovery.cs` | Enableable temporary recovery state. |
| `Components/MatchState.cs` | Match-level singleton state. |
| `Components/ArenaBounds.cs` | Play-area bounds. |
| `Components/SpawnSettings.cs` | Enemy spawn configuration. |
| `Components/RespawnRequest.cs` | Enableable request to return an enemy to valid space. |
| `Systems/Groups/*.cs` | Custom update phase boundaries. |
| `Systems/Initialization/BootstrapSystem.cs` | Validates and initializes ECS match state. |
| `Systems/Initialization/EnemySpawnSystem.cs` | Owns initial crowd creation. |
| `Systems/InputBridge/PlayerBridgeSystem.cs` | Copies player bridge data into ECS components. |
| `Systems/AI/EnemyChaseSystem.cs` | Produces enemy chase movement intent. |
| `Systems/Movement/EnemyMovementSystem.cs` | Applies enemy movement intent to Unity Physics velocity. |
| `Systems/Combat/PunchDetectionSystem.cs` | Detects enemies affected by punches. |
| `Systems/Physics/ApplyImpulseSystem.cs` | Applies gameplay impulses before physics simulation. |
| `Systems/Physics/EnemyRecoverySystem.cs` | Times enemy knockback recovery and returns control to movement. |
| `Systems/Lifetime/OutOfBoundsSystem.cs` | Marks enemies outside arena bounds. |
| `Systems/Lifetime/EnemyRespawnSystem.cs` | Resets enemies marked for respawn. |
| `Systems/Presentation/PresentationBridgeSystem.cs` | Publishes ECS state to presentation-only consumers. |
| `Aspects/EnemyMovementAspect.cs` | Groups movement components commonly queried together. |
| `Mono/Player/PlayerEcsBridge.cs` | Dedicated player-to-ECS bridge surface and active bridge registry. |
| `Mono/Player/PlayerController.cs` | Traditional GameObject player movement shell. |
| `Mono/Player/PlayerPunch.cs` | Traditional GameObject punch input shell. |
| `Mono/Camera/CameraFollow.cs` | Traditional camera follow shell. |
| `Mono/UI/GameBootstrap.cs` | Scene-level hybrid bridge wiring shell. |

## ECS Feature Choices

`ISystem` is used for systems because it keeps systems unmanaged and Burst-friendly. `SystemAPI` is named in system TODOs as the intended query and singleton access path because it is the current Entities 1.x style.

`IEnableableComponent` is used for `PunchRequest`, `ExternalImpulse`, `KnockbackRecovery`, and `RespawnRequest` because these are transient states. Enabling or disabling a component avoids structural changes when state frequently turns on and off.

Custom system groups make frame order explicit: bridge input, compute intent, apply impulses, simulate physics, reconcile state, then present results. This is clearer for learning than relying on default update order.

Enemy movement is split into intent and application. `EnemyChaseSystem` chooses whether each enemy wanders or charges and writes `DesiredMovement`; `EnemyMovementSystem` consumes that data and steers `PhysicsVelocity` toward the desired velocity instead of overwriting it instantly. This lets collision impulses survive long enough to affect other enemies. It also locks pitch and roll through `PhysicsMass.InverseInertia` so enemies remain upright while Unity Physics owns collision and position integration.

Punching follows the same data pipeline. `PlayerPunch` publishes a `PunchRequest` through the bridge; `PunchDetectionSystem` tests enemy positions against the request volume and enables `ExternalImpulse`; `ApplyImpulseSystem` adds that value to `PhysicsVelocity`; `EnemyRecoverySystem` keeps movement from immediately overriding the knockback.

The aspect is intentionally small. It is useful when several systems repeatedly touch the same movement components. A plain `SystemAPI.Query` is still preferable for one-off queries.

Bakers are used instead of runtime setup MonoBehaviours because baking is the normal Entities 1.x conversion path from scene authoring to ECS data. `TransformUsageFlags` are explicit so each baked entity declares whether it needs runtime transform data.

`EntityCommandBuffer` is used by spawning because deferred structural changes are the correct extension point for entity instantiation. Combat still calls it out as the future path for enable/disable-heavy workflows inside jobs.

Blob assets are not introduced yet because the current settings are tiny scalar values. They become useful later for shared immutable data such as ability tables, animation event windows, or spawn waves.

## Extension Points

Animation can be added through a presentation-facing component or bridge system that reads ECS state and drives Animator parameters or Entities Graphics material properties.

Health can be added as `Health`, `DamageRequest`, and `DeathRequest` components, with damage calculation in Combat and cleanup in Lifetime.

Abilities can be modeled as data components plus enableable request components. If ability definitions grow, move static definitions into blob assets.

VFX can be spawned from presentation events produced after simulation, keeping visual effects separate from combat rules.

Audio and UI should read summarized presentation or match-state data rather than querying enemy entities directly.
