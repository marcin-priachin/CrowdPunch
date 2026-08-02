# Crowd Punch — Current Architecture

Status: Repository snapshot  
Last inspected: 2026-08-02
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
- `Configuration` — reusable ScriptableObject tuning consumed by scene-facing MonoBehaviours and bakers.
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
| Input, player transform, and dash-punch coordination | GameObject | `PlayerController` owns dash timing/cancellation; `PlayerPunch` owns attack input/buffering |
| Player health and invincibility | GameObject | `PlayerHealth` |
| Camera | GameObject | `CameraFollow` |
| Scene bootstrap and UI | GameObject | `GameBootstrap`, UI MonoBehaviours |
| Player state visible to ECS | Bridge → ECS singleton | `PlayerSnapshot`, `PlayerHealthSnapshot` |
| Punch command visible to ECS | Bridge → enableable ECS request | `PunchRequest` |
| Enemy contact reported to player | ECS → bridge event | `EnemyContactHitReceived` |
| Enemy spawn and pooling | ECS | spawn settings and respawn systems |
| Enemy intent and movement | ECS | `DesiredMovement`, Unity Physics velocity |
| Enemy combat state | ECS | health, damage, impulse, explicit launch lifecycle, death/respawn requests |
| Punch trajectory preview | ECS → bridge → GameObject | `PresentationBridgeSystem`, `PlayerEcsBridge`, `PunchTrajectoryPreview` |
| Committed punch-area feedback | GameObject | `PlayerPunch` triggers `PunchAreaFeedback` from the same origin, direction, radius, and range published to ECS |
| Temporary enemy health bars | ECS → presentation registry → Canvas | `EnemyHealthBarVisibility`, `EnemyHealthBarBridgeSystem`, `EnemyHealthBarCanvasRegistry`, `EnemyHealthBarCanvas` |

MonoBehaviours do not retain or query enemy entities. `PlayerBridgeRegistry` exposes the one active `PlayerEcsBridge` to the few managed systems that cross the boundary.

## Update Flow

### Initialization

`GameInitializationGroup` runs in Unity's initialization phase:

1. `BootstrapSystem` establishes singleton/runtime state.
2. `EnemySpawnSystem` instantiates the initial enemy pool.
3. `GameRestartSystem` handles managed restart coordination.

### Pre-Physics Simulation

`GamePrePhysicsGroup` runs as a direct child of `SimulationSystemGroup` before `PhysicsSystemGroup`:

1. `PlayerBridgeSystem` copies the latest GameObject player snapshot, health, and punch request into ECS.
2. `EnemyChaseSystem` produces enemy movement intent.
3. `EnemyMovementSystem` steers Unity Physics velocity toward that intent.
4. `PunchDetectionSystem` finds active enemies inside the punch volume, transitions them to `Launched`, and enables impulse and damage requests.
5. `DamageApplicationSystem` applies enabled damage requests, clamps health, and resolves immediate defeat or records launch-deferred defeat.
6. `ApplyImpulseSystem` adds gameplay impulse to `PhysicsVelocity`.
7. Unity Physics simulates motion and collisions.

Ordering between systems that share only a group should be made explicit when correctness depends on it. The attribute graph, not filename order, is authoritative.

### Post-Physics Simulation

`GamePostPhysicsGroup` runs as a direct child of `SimulationSystemGroup` after `PhysicsSystemGroup`:

- `EnemyLaunchCollisionSystem` interprets solver-resolved enemy impacts, resolves launch propagation first, and independently queues eligible impulse-scaled collision damage without rewriting velocity.
- `EnemyRecoverySystem` advances living `Launched` enemies through low-momentum dwell and `Recovering` back to `Active`; a zero-health launched enemy enters `Defeated` directly when launch ends.
- `PlayerContactDamageSystem` detects enemy proximity/contact and reports the closest accepted hit through the bridge.
- `OutOfBoundsSystem` requests recovery for escaped enemies.
- `DefeatedEnemyLifecycleSystem` converts the one-shot defeat marker into the existing respawn request.
- `EnemyRespawnSystem` brakes, pools, resets, and respawns defeated or otherwise invalid enemies.

### Presentation

`GamePresentationGroup` runs in Unity's presentation phase:

- `HealthBarPresentationSystem` updates ECS health-bar presentation data and expires one-second post-damage visibility.
- `EnemyHealthBarBridgeSystem` publishes only currently visible enemy position/health snapshots to the registered screen-space Canvas.
- `PresentationBridgeSystem` is the explicit ECS presentation bridge point.
- `PresentationBridgeSystem` selects enemies currently inside the live punch volume and publishes their initial launch segments through `PlayerEcsBridge`; `PunchTrajectoryPreview` renders those segments as pooled semitransparent world-space lines.

Normal-enemy health bars are transient damage feedback only. `EnemyHealthBarCanvas` pools screen-space bar objects and hides each one after ECS disables `EnemyHealthBarVisibility`; the Canvas never queries or stores enemy entities.

## Transient State Pattern

Frequently toggled state is represented by enableable components to avoid archetype churn:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `DamageRequest`
- `DeathRequest`
- `RespawnRequest`
- `EnemyHealthBarVisibility`

Enemy lifecycle is represented by the non-enableable `EnemyLaunchState` component because every enemy is always in exactly one of `Active`, `Launched`, `Recovering`, or `Defeated`. Its phase, last launch cause, and propagated-launch count remain visible in the Entities inspector. `Health` exposes current and maximum health, while `EnemyDamageState` records the last applied damage and whether zero-health defeat is currently deferred for development inspection.

`DamageApplicationSystem` is the explicit pre-physics health stage after punch detection. Punch detection establishes `Launched` before damage is evaluated, so a same-frame lethal launching punch deterministically defers defeat and still receives its impulse. After physics and collision propagation, `EnemyRecoverySystem` chooses either normal recovery for a living projectile or direct defeat for a zero-health projectile. `DeathRequest` is enabled only on the transition to `Defeated`, making the lifetime handoff idempotent; `DefeatedEnemyLifecycleSystem` consumes it once and enables `RespawnRequest`.

Collision damage is queued post-physics into the target's existing `DamageRequest` and applied during the next pre-physics damage stage. `EnemyLaunchState.LaunchDamage` carries the originating normal or dash punch damage through every propagated launch; collision impulse selects a configured multiplier of that value up to a cap. `EnemyLaunchState.LaunchSequence` identifies each continuous launch. Each target's `CollisionDamageHistory` buffer suppresses repeat damage from the same source sequence; `CollisionDamageHistoryCleanupSystem` removes entries when the source leaves that launch. Propagation and collision damage have independent impulse thresholds. Only launched-to-active/recovering impacts are damage-eligible, and propagation is written before damage is queued so a lethal propagated target defers defeat.

`GameSettingsAuthoring` reads reusable `GameRuntimeSettings` ScriptableObject data and bakes `EnemyLaunchSettings` as scene-level singleton configuration. Its provisional sandbox tuning includes independent propagation/damage impulse thresholds, base/per-impulse/maximum collision-damage multipliers, useful-momentum threshold and dwell, and recovery duration. Player movement and punch settings assets own normal and dash punch damage as well as their Input System asset/action selection, while `EnemySpawnSettings` owns the enemy prefab and initial crowd tuning. Scene MonoBehaviours retain only scene-instance wiring such as bridges, cameras, and origin transforms.

Systems get the entity for mixed singleton state through a non-enableable component such as `PlayerSnapshot` or `MatchState`, then inspect or toggle enableable state explicitly.

## Current Prototype Behavior Versus Design

The current implementation proves architecture and basic interactions, but several behaviors are placeholders:

- Punch detection uses a line/capsule-like distance test and independently assigns impulse, damage, and launched state. Enemy collision damage is also independently thresholded rather than inferred from propagation.
- Player movement and dash are transform-driven MonoBehaviour movement.
- Dash-punch coordination stays on the player GameObject: `PlayerPunch` buffers an early press, while `PlayerController` reports normalized progress from its existing dash timer and ends dash movement when the punch is consumed at or after the configured `0.5` midpoint. Dash punches select independently configured damage and launch strength, then use the ordinary bridge and ECS punch pipeline.
- A launched enemy, including a zero-health enemy with deferred defeat, can propagate launched state to and independently damage an active or recovering enemy when Unity Physics reports solver-estimated contact impulse above the respective authored thresholds. Unity Physics exclusively owns velocities; gameplay adds no synthetic transfer. One source launch damages each target at most once but may damage multiple targets. Defeated enemies and launched-versus-launched pairs are ineligible. The final effect grammar remains unresolved.
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
