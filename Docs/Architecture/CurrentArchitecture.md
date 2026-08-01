# Crowd Punch — Current Architecture

Status: Repository snapshot  
Last inspected: 2026-08-01  
Unity: 6000.3.10f1

This document describes what exists now. It is not a desired future architecture and does not make prototype behavior into a design requirement.

## Architectural Shape

Crowd Punch uses a hybrid Unity architecture:

- The large player, input, camera, player health, and UI are GameObjects with MonoBehaviours.
- Enemies, crowd movement, enemy physics, combat requests, spawning, lifetime, and ECS presentation data are Entities.
- `PlayerEcsBridge` is the narrow runtime boundary between the GameObject player and ECS.
- Authoring components in the arena subscene are baked into runtime ECS data.

## Scenes

- `Assets/CrowdPunch/Scenes/Bootstrap.unity` — main GameObject scene and application bootstrap.
- `Assets/CrowdPunch/Scenes/Bootstrap/ArenaSubScene.unity` — arena authoring content baked into entities.

## Source Layout

All game code is under `Assets/CrowdPunch/Scripts`:

- `Mono` — GameObject player, camera, UI, and bridge registry.
- `Authoring` — inspector-facing baking inputs.
- `Bakers` — conversion from authoring objects to ECS components.
- `Components` — data-only ECS state and requests.
- `Systems` — initialization, bridge, AI, movement, combat, physics, lifetime, and presentation.
- `Groups` — explicit update phases.
- `Aspects` — one legacy learning aspect; new code should prefer direct component/query APIs.
- `Editor` — arena authoring inspector support.

There are currently no game-specific assembly definitions; scripts compile into Unity's generated assemblies.

## Ownership Boundary

| Concern | Current owner | Boundary/data |
|---|---|---|
| Input and player transform | GameObject | `PlayerController`, `PlayerPunch` |
| Player health and invincibility | GameObject | `PlayerHealth` |
| Camera | GameObject | `CameraFollow` |
| Scene bootstrap and UI | GameObject | `GameBootstrap`, UI MonoBehaviours |
| Player state visible to ECS | Bridge → ECS singleton | `PlayerSnapshot`, `PlayerHealthSnapshot` |
| Punch command visible to ECS | Bridge → enableable ECS request | `PunchRequest` |
| Enemy contact reported to player | ECS → bridge event | `EnemyContactHitReceived` |
| Enemy spawn and pooling | ECS | spawn settings and respawn systems |
| Enemy intent and movement | ECS | `DesiredMovement`, Unity Physics velocity |
| Enemy combat state | ECS | health, damage, impulse, recovery, death/respawn requests |

MonoBehaviours do not retain or query enemy entities. `PlayerBridgeRegistry` exposes the one active `PlayerEcsBridge` to the few managed systems that cross the boundary.

## Update Flow

### Initialization

`GameInitializationGroup` runs in Unity's initialization phase:

1. `BootstrapSystem` establishes singleton/runtime state.
2. `EnemySpawnSystem` instantiates the initial enemy pool.
3. `GameRestartSystem` handles managed restart coordination.

### Pre-Physics Simulation

`GamePrePhysicsGroup` runs inside `GameSimulationGroup` before `PhysicsSystemGroup`:

1. `PlayerBridgeSystem` copies the latest GameObject player snapshot, health, and punch request into ECS.
2. `EnemyChaseSystem` produces enemy movement intent.
3. `EnemyMovementSystem` steers Unity Physics velocity toward that intent.
4. `PunchDetectionSystem` finds enemies inside the punch volume and enables impulse, damage, recovery, and respawn-related requests.
5. `DamageApplicationSystem` applies enabled damage requests.
6. `ApplyImpulseSystem` adds gameplay impulse to `PhysicsVelocity`.
7. Unity Physics simulates motion and collisions.

Ordering between systems that share only a group should be made explicit when correctness depends on it. The attribute graph, not filename order, is authoritative.

### Post-Physics Simulation

`GamePostPhysicsGroup` runs after Unity Physics:

- `EnemyRecoverySystem` advances temporary knockback recovery.
- `PlayerContactDamageSystem` detects enemy proximity/contact and reports the closest accepted hit through the bridge.
- `EnemyEnemyCollisionRespawnSystem` currently propagates player-punch pooling intent through enemy collision events.
- `OutOfBoundsSystem` requests recovery for escaped enemies.
- `EnemyRespawnSystem` pools and respawns enemies.

### Presentation

`GamePresentationGroup` runs in Unity's presentation phase:

- `HealthBarPresentationSystem` updates ECS health-bar presentation data.
- `PresentationBridgeSystem` is the explicit ECS presentation bridge point.

The GDD now prohibits normal-enemy health UI, so the existing ECS `HealthBar` path should be treated as prototype/legacy behavior and reviewed when presentation is next changed.

## Transient State Pattern

Frequently toggled state is represented by enableable components to avoid archetype churn:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `DamageRequest`
- `DeathRequest`
- `RespawnRequest`

Systems get the entity for mixed singleton state through a non-enableable component such as `PlayerSnapshot` or `MatchState`, then inspect or toggle enableable state explicitly.

## Current Prototype Behavior Versus Design

The current implementation proves architecture and basic interactions, but several behaviors are placeholders:

- Punch detection uses a line/capsule-like distance test and immediately assigns impulse, damage, recovery, and respawn intent.
- Player movement and dash are transform-driven MonoBehaviour movement.
- Dash exists, but the accepted buffered dash-punch rule is not implemented yet.
- Enemy-enemy collision currently propagates respawn/pooling from a player-punched enemy; the final chain damage/effect grammar is unresolved.
- Enemy chasing and contact damage exist as prototype behavior.
- A player health bar exists and is consistent with the GDD; ECS enemy health-bar presentation data exists but conflicts with the no-normal-enemy-UI rule if displayed.
- The current scene is an arena sandbox, not the required route-based 15–20 minute run.

Do not preserve these details merely because they exist. Preserve the ownership boundary and system timing while evolving behavior toward accepted GDD rules.

## Packages Relevant To Runtime Architecture

- Unity Entities feature set (`com.unity.feature.ecs` 1.0.0)
- Input System 1.18.0
- Universal Render Pipeline 17.3.0
- Unity 6 built-in physics modules plus Unity Physics supplied through the ECS feature set

Package versions in `Packages/manifest.json` and `packages-lock.json` remain the source of truth.

## Verification Constraints

- Unity compilation, baking, scene wiring, and play-mode behavior are the final verification sources.
- A generated `Assembly-CSharp.csproj` may be stale until Unity refreshes assets.
- Physics changes require checking representative crowd density and profiling, not only single-enemy correctness.
- Changes to bridge fields, component ownership, system order, scenes, or package boundaries must update this document.
