# Crowd Punch — Current Architecture

Status: Repository snapshot  
Last inspected: 2026-08-03
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
| Input, player transform, and dash-punch coordination | GameObject | `PlayerController` owns dash timing/cancellation; `PlayerPunch` owns attack input, buffering, and cooldown |
| Player health and invincibility | GameObject | `PlayerHealth` |
| Camera | GameObject | `CameraFollow` |
| Scene bootstrap and UI | GameObject | `GameBootstrap`, UI MonoBehaviours |
| Player state visible to ECS | Bridge → ECS singleton | `PlayerSnapshot`, `PlayerHealthSnapshot` |
| Punch command visible to ECS | Bridge → enableable ECS request | `PunchRequest` |
| Enemy contact reported to player | ECS → bridge event | `EnemyContactHitReceived` |
| Enemy initial spawn, waves, and pooling | Authoring → baked profile → ECS | random `SpawnSettings`, authored spawn points, ordered wave buffers, shared initialization, and respawn systems |
| Enemy intent and movement | ECS | `DesiredMovement`, Unity Physics velocity |
| Enemy archetypes and attacks/effects | ECS | `EnemyArchetype`, ranged state, and explosive settings/state |
| Ranged projectile trajectory and lifetime | ECS | `RangedProjectile`, fixed fire-time start/target, ECS transform evaluation |
| Enemy combat state | ECS | health, damage, impulse, explicit launch lifecycle, death/respawn requests |
| Punch trajectory preview | ECS → bridge → GameObject | `PresentationBridgeSystem`, `PlayerEcsBridge`, `PunchTrajectoryPreview` |
| Committed punch-area feedback | GameObject | `PlayerPunch` triggers `PunchAreaFeedback` from the same origin, direction, radius, and range published to ECS |
| Temporary enemy health and non-active state UI | ECS → presentation registry → Canvas | `EnemyHealthBarVisibility`, `EnemyLaunchState`, `EnemyHealthBarBridgeSystem`, `EnemyHealthBarCanvasRegistry`, `EnemyHealthBarCanvas` |

MonoBehaviours do not retain or query enemy entities. `PlayerBridgeRegistry` exposes the one active `PlayerEcsBridge` to the few managed systems that cross the boundary.

## Update Flow

### Initialization

`GameInitializationGroup` runs in Unity's initialization phase:

1. `BootstrapSystem` establishes singleton/runtime state.
2. `EnemySpawnSystem` instantiates the initial enemy pool.
3. `GameRestartSystem` handles managed restart coordination.

### Initial Spawn Workflows

The arena supports two independent initial-layout inputs, and a SubScene may contain either or both:

- `SpawnerAuthoring` bakes the established `SpawnSettings` random-radius batch. Its center, radius, count, deterministic random sequence, and reusable `EnemySpawnSettings` asset retain their existing meaning.
- `AuthoredEnemyGroupAuthoring` is an organizational parent. Each child `AuthoredEnemySpawnPointAuthoring` references one existing `EnemySpawnSettings` asset and bakes one `AuthoredEnemySpawnPoint` with the child's exact world-space position, including Y. Each point bakes independently, so a null settings asset or prefab suppresses only that point.

Both inputs bake the same `EnemySpawnProfile`. `EnemySpawnSystem` resolves either a random position or an authored position and delegates prefab instantiation, common movement/health/contact tuning, separation selection, respawn policy, archetype identity, material override, and Baseline/Ranged/Explosive/Dasher component setup to `EnemySpawnInitialization`. `EnemyAuthoring` is only the prefab marker that bakes the common component layout; `EnemySpawnSettings` is the single tuning source for each spawned variant. Shared approach and retreat speeds serve both ranged positioning and Dasher positioning instead of maintaining duplicate Dasher values. Runtime behavior is always selected through `EnemyArchetypeKind`, never names. The one-shot spawn system has no `SpawnSettings` requirement, so authored-only scenes work and initialization cannot repeat on restart.

Wave spawning is a third, independently selectable workflow and does not gate either initial-spawn workflow. Each `EnemyWaveSettings` ScriptableObject contains one wave's total count, weighted references to existing `EnemySpawnSettings` profiles, positive-area world-space XZ rectangles, pre-wave delay, and `AllAtOnce` or `Batched` cadence. `EnemyWaveSequenceAuthoring` holds only the ordered wave references plus sequence-wide deterministic seed, minimum player distance, and bounded placement retry count. Its baker declares dependencies on the sequence, wave assets, profiles, and prefabs, then flattens immutable definitions into ECS buffers; runtime components contain no Unity object references.

`EnemyWaveSpawnSystem` owns timing, deterministic weighted profile allocation, and candidate sampling. Rectangles are selected proportionally to area and sampled uniformly, so equal valid world area has approximately equal probability. A conservative spherical clearance is derived from each prefab's authored collider bounds. Every candidate must remain inside its rectangle, clear the GameObject player's published position/radius and configured extra separation, and produce no Unity Physics point-distance hit against enemies or blocking geometry. Candidates spawned in the same update are also checked against one another. Attempts are bounded; blocked enemies stay pending, retry on later updates without restarting the pre-wave delay, and emit only throttled diagnostics.

Every wave instance still goes through `EnemySpawnInitialization`, preserving prefab/archetype identity, health, state, presentation overrides, projectile data, separation randomization, and archetype-specific setup. It additionally receives `EnemyWaveOwnership` with sequence entity, run generation, and wave index. `EnemyWaveDefeatCountSystem` observes the authoritative `EnemyLaunchState.Defeated` transition before pooling, marks each ownership record once, and increments only its owning active wave. Legacy and authored enemies therefore cannot advance a wave. Wave instances explicitly bake their per-instance `EnemyRespawnSettings.Enabled` override to false, so they pool after defeat but never perform continuous arena-edge respawning; source assets and non-wave instances keep their existing policy.

Only one wave is active per sequence. Progress waits for both the configured successful-spawn count and the exact owned-defeat count, then begins the next asset's pre-wave delay on a later update. Zero-count waves progress safely. An invalid nonempty wave enters an inspectable stopped state without affecting other spawners. Empty sequences and completed final waves enable `EnemyWaveEncounterComplete` and never loop.

Each random enemy owns `RandomEnemySpawnRegion`; each authored enemy instead owns `AuthoredEnemyInitialPosition`. A full `GameRestartSystem` reset randomizes legacy enemies inside their own regions and restores authored enemies to their exact initial positions without archetype matching. It also clears velocity, health/damage/launch state, collision histories, ranged attack and positioning state, Dasher action state and hit history, explosive state, movement intent, and enableable transient data. Normal defeat pooling is unchanged: `EnemyRespawnSettings` still controls whether an enemy returns, and enabled enemies respawn at an arena edge rather than at an authored point. Disabled enemies stay pooled until full restart.

### Pre-Physics Simulation

`GamePrePhysicsGroup` runs as a direct child of `SimulationSystemGroup` before `PhysicsSystemGroup`:

1. `PlayerBridgeSystem` copies the latest GameObject player snapshot, health, and punch request into ECS.
2. `EnemyChaseSystem` produces enemy movement intent, blending each enemy's spawn-selected local separation distance into both wandering and charging. Close active enemies hold deterministic slots on authored surround rings, then use per-enemy randomized intervals to make brief, speed-scaled contact attempts toward the player. Contact attempts retain separation with an independently authored weight so they can commit more strongly without disabling crowd avoidance or physical collisions. `EnemyContactAttemptState` owns that runtime cadence; its distance, interval range, duration, speed multiplier, and separation weight are baked from `EnemySpawnSettings`.
3. `RangedEnemyPositioningSystem` replaces baseline intent only for ranged enemies, selecting approach, hold, or retreat and blending the same active-crowd separation input.
4. `EnemyMovementSystem` steers Unity Physics velocity toward that intent and continues to reject non-`Active` enemies.
5. `PunchDetectionSystem` finds living active, launched, or recovering enemies inside the punch volume, starts a fresh `Launched` sequence with the current punch data, and enables impulse and damage requests (COMBAT-014).
6. `DamageApplicationSystem` applies enabled damage requests, clamps health, and resolves immediate defeat or records launch-deferred defeat.
7. `RangedEnemyAttackSystem` evaluates ranged state after punch and damage resolution, cancels invalid wind-ups, and instantiates a projectile when a valid wind-up completes.
8. `ApplyImpulseSystem` adds gameplay impulse to `PhysicsVelocity`.
9. Unity Physics simulates motion and collisions.

Ordering between systems that share only a group should be made explicit when correctness depends on it. The attribute graph, not filename order, is authoritative.

### Post-Physics Simulation

`GamePostPhysicsGroup` runs as a direct child of `SimulationSystemGroup` after `PhysicsSystemGroup`:

- `EnemyLaunchCollisionSystem` interprets solver-resolved enemy impacts, resolves launch propagation first, and independently queues eligible impulse-scaled collision damage without rewriting velocity.
- `ExplosiveCollisionTriggerSystem` requests an explosive detonation when either participant in an enemy collision is `Launched`; `ExplosionResolutionSystem` then resolves explosion overlap chains to a same-frame fixed point before recovery.
- `RangedProjectileSystem` evaluates each fixed trajectory, performs a swept player-radius hit check, forwards one accepted hit through `PlayerEcsBridge`, and destroys the projectile on hit, after falling below its authored world-space minimum altitude, or on expiry. It does not apply arena-bound cleanup because the unconstrained GameObject player can currently provide a valid target outside `ArenaBounds`.
- `EnemyRecoverySystem` advances living `Launched` enemies through low-momentum dwell and `Recovering` back to `Active`; a zero-health launched enemy enters `Defeated` directly when launch ends.
- `PlayerContactDamageSystem` detects enemy proximity/contact and reports the closest accepted hit through the bridge.
- `OutOfBoundsSystem` requests recovery for escaped enemies.
- `DefeatedEnemyLifecycleSystem` converts the one-shot defeat marker into the existing respawn request.
- `EnemyRespawnSystem` brakes, pools, resets, and respawns defeated or otherwise invalid enemies.

### Presentation

`GamePresentationGroup` runs in Unity's presentation phase:

- `HealthBarPresentationSystem` updates ECS health-bar presentation data and expires one-second post-damage visibility.
- `EnemyHealthBarBridgeSystem` publishes enemy position, health, and launch-phase snapshots while post-damage health visibility is enabled or the enemy is not `Active`.
- `PresentationBridgeSystem` is the explicit ECS presentation bridge point.
- `PresentationBridgeSystem` selects enemies currently inside the live punch volume and publishes their initial launch segments through `PlayerEcsBridge`; `PunchTrajectoryPreview` renders those segments as pooled semitransparent world-space lines.

Normal-enemy health bars are transient damage feedback only. `EnemyHealthBarCanvas` uses the same pooled screen-space view to label transient `Launched`, `Recovering`, and `Defeated` phases, and independently respects the scene-facing health-bar and state-label options configured on `GameBootstrap`. It hides the view when neither enabled channel has content; the Canvas never queries or stores enemy entities.

## Transient State Pattern

Frequently toggled state is represented by enableable components to avoid archetype churn:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `DamageRequest`
- `DeathRequest`
- `RespawnRequest`
- `EnemyHealthBarVisibility`

Enemy lifecycle is represented by the non-enableable `EnemyLaunchState` component because every enemy is always in exactly one of `Active`, `Launched`, `Recovering`, or `Defeated`. Its phase, last launch cause, and propagated-launch count remain visible in the Entities inspector. Any launched enemy, including one at zero health with deferred defeat, can be punched again; a recovering enemy must still be alive. The transition increments its launch sequence and resets its cause, damage, recovery timing, propagated-launch count, and last propagation impulse to the new launch. `Health` exposes current and maximum health, while `EnemyDamageState` records the last applied damage and whether zero-health defeat is currently deferred for development inspection.

`DamageApplicationSystem` is the explicit pre-physics health stage after punch detection. Punch detection establishes `Launched` before damage is evaluated, so a same-frame lethal launching punch deterministically defers defeat and still receives its impulse. After physics and collision propagation, `EnemyRecoverySystem` chooses either normal recovery for a living projectile or direct defeat for a zero-health projectile. `DeathRequest` is enabled only on the transition to `Defeated`, making the lifetime handoff idempotent; `DefeatedEnemyLifecycleSystem` consumes it once and enables `RespawnRequest`.

Collision damage is queued post-physics into the target's existing `DamageRequest` and applied during the next pre-physics damage stage. `EnemyLaunchState.LaunchDamage` carries the originating normal or dash punch damage through every propagated launch; collision impulse selects a configured multiplier of that value up to a cap. `EnemyLaunchState.LaunchSequence` identifies each continuous launch. Each target's `CollisionDamageHistory` buffer suppresses repeat damage from the same source sequence; `CollisionDamageHistoryCleanupSystem` removes entries when the source leaves that launch. Propagation and collision damage have independent impulse thresholds. Only launched-to-active/recovering impacts are damage-eligible, and propagation is written before damage is queued so a lethal propagated target defers defeat.

`GameSettingsAuthoring` reads reusable `GameRuntimeSettings` ScriptableObject data and bakes `EnemyLaunchSettings` as scene-level singleton configuration. Its provisional sandbox tuning includes independent propagation/damage impulse thresholds, base/per-impulse/maximum collision-damage multipliers, useful-momentum threshold and dwell, and recovery duration. Player movement and punch settings assets own normal and dash punch damage as well as their Input System asset/action selection, while `EnemySpawnSettings` owns the enemy prefab, common movement, health, contact behavior, archetype tuning, initial crowd tuning, and whether enemies from that spawn profile return after pooling. The baked per-enemy `EnemyRespawnSettings` preserves that policy when multiple spawners use different profiles; disabled respawning leaves defeated enemies pooled until a game restart. Scene MonoBehaviours retain only scene-instance wiring such as bridges, cameras, and origin transforms.

Full restart destroys all old wave-owned instances, increments each sequence run generation, clears counters and completion, restores its initial random seed, and lets the first wave start its full pre-wave delay again. Destroying the old generation prevents stale pooled entities or defeat observations from affecting the new run and avoids double-spawning. Legacy random and authored enemies continue through their existing in-place reset semantics.

## Ranged Enemy Archetype

`EnemySpawnSettings.Archetype` explicitly selects `Baseline` or `Ranged`; no prefab, scene-object, or presentation name participates in runtime identification. Every spawned enemy receives `EnemyArchetype`. A ranged selection additionally receives the baked `RangedEnemySettings`, `RangedPositioningState`, and `RangedAttackState`. The existing `EnemySpawnSettings.asset` remains baseline. `RangedEnemySpawnSettings.asset` is used by a second ordinary `SpawnerAuthoring` in the arena subscene to add five ranged enemies without changing the 500-enemy baseline batch.

All ranged numerical settings are provisional and live on the ranged spawn settings asset: preferred minimum/maximum distance, engagement range, approach/retreat speed, initial delay and per-instance variation, wind-up, base cooldown and per-shot cooldown variation, damage, player invulnerability duration, projectile speed, horizontal aim-spread radius, fire-time target Y offset, arc height, minimum world-space altitude, lifetime, and radius. Independent per-enemy cadence plus initial and per-shot timing variation is the first-pass multi-attacker control; there is no global simultaneous-attack cap.

`RangedEnemyPositioningSystem` owns the ranged approach/hold/retreat decision. It reuses the baseline active-enemy separation input and writes only `DesiredMovement`; `EnemyMovementSystem` remains the velocity owner and its `Active` gate prevents ranged steering from overwriting launch or recovery velocity. `RangedAttackState` exposes eligibility, lifecycle phase, remaining time, emitted count, and cancelled-wind-up count for Entities inspection. Attack evaluation runs after punch and damage application, so same-frame launch or defeat cancels before emission. Pooling resets both attack and positioning state; already-fired projectiles have no shooter reference and remain independent.

`RangedProjectileSystem` uses a deterministic parametric path. At fire time, a deterministic per-enemy/per-shot point inside the authored horizontal spread radius and the authored target Y offset are added to the sampled player position; the time to that point is derived from horizontal distance divided by authored speed. Horizontal/world-space position is `lerp(start, fireTimeTarget, t)` and vertical readability adds `4 * arcHeight * t * (1 - t)`. The system does not clamp `t` at `1`, so after crossing the sampled aim point the projectile continues at the same horizontal speed and along the descending parabola until hit, minimum-altitude cleanup, or expiry. The target is never updated after firing. The prefab is a yellow grey-box sphere with a baked kinematic collider on the `RangedProjectile` layer. Its collider explicitly excludes the Default layer used by enemies and the arena, so it produces no enemy collision events or reactions; player contact is checked against the ECS player snapshot because the player remains a GameObject outside the ECS physics world.

Projectile damage calls `PlayerEcsBridge.ReceiveEnemyHit`, which converts the configured amount for the existing `PlayerHealth` event pipeline. `PlayerHealth` remains authoritative for invulnerability, health clamping, damage acceptance, and death. Projectile simulation is data-only and its baked mesh/material is presentation. There was no compatible projectile pool, so this first pass uses command-buffer instantiation and destruction; this should be revisited only if profiling at representative projectile counts shows structural-change cost is material.

Systems get the entity for mixed singleton state through a non-enableable component such as `PlayerSnapshot` or `MatchState`, then inspect or toggle enableable state explicitly.

## Explosive Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Explosive`. The spawn system reuses the baseline enemy prefab and movement components, adds `ExplosiveEnemySettings`, `ExplosiveEnemyState`, and an enableable `ExplosiveDetonationRequest`, then overrides the material base color to orange for grey-box readability. `ExplosiveEnemySpawnSettings.asset` and the arena's ordinary `SpawnerAuthoring` follow the same profile pattern as the ranged archetype.

`ExplosiveCollisionTriggerSystem` reads Unity Physics collision events after simulation. It requests the explosive participant when either enemy was already in the authoritative `Launched` phase, without an impact threshold. `ExplosionResolutionSystem` also requests an active explosive whose baseline contact radius overlaps the player. `PlayerContactDamageSystem` excludes explosive archetypes so this contact produces the explosion instead of ordinary melee contact damage.

Explosion resolution marks `ExplosiveEnemyState.HasExploded` before applying any effects, making multiple contacts and overlapping blasts idempotent. Each blast uses a constant radius check with no falloff, accumulates the existing `DamageRequest` and `ExternalImpulse`, and calls the same `EnemyLaunchTransition` used by punches and collision propagation. Explosion-launched enemies therefore carry explosion damage as their launch-chain damage and follow normal collision propagation, deferred defeat, recovery, and pooling rules. Newly overlapped explosives enable their own detonation request; the resolver repeats until no request remains, allowing legitimate overlap chains to complete in the same post-physics frame. The source explosive transitions directly to `Defeated` and enters the established death/respawn handoff.

The existing player bridge receives explosion damage through the normal `PlayerHealth`/invulnerability event and publishes a separate presentation-only event. Player/elite knockback is tuned independently from normal-enemy knockback; boss knockback is baked as future-compatible configuration but is not consumed because bosses do not exist. `ExplosionFeedback` renders a short-lived expanding grey-box sphere without exposing ECS entities to MonoBehaviours.

## Current Prototype Behavior Versus Design

## Dasher Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Dasher`. The existing enemy prefab is instantiated as a variant with
`DasherSettings`, `DasherState`, and a per-action `DasherHitHistory` buffer. Its explicit phases are `Positioning`,
`Preparing`, `Dashing`, and `Recovering`; the shared `EnemyLaunchState` remains authoritative for punches, launched
collision chains, recovery, defeat, and pooling. A punch or defeat therefore interrupts every Dasher phase, and pooling
resets both state and hit history.

The decision system maintains an authored distance band and evaluates the configured corridor policy only when entering
preparation. Preparation faces the live player and either stops immediately or brakes using normal movement. Direction is
resampled and locked when the telegraph expires. Dash movement writes the locked horizontal velocity until maximum travel
or an obstacle reduces it below the stop threshold. Player hits are limited by a per-dash flag; enemy hits use a source,
target, and action-sequence history. Both use the existing player bridge, `DamageRequest`, `ExternalImpulse`, and
`EnemyLaunchTransition` pipelines.

Enemy prefab colliders bake on the dedicated `Enemy` Unity Physics category. Each Dasher receives a unique runtime collider
with package-owned cleanup data. While intentionally dashing or in the shared `Launched` phase,
`DasherColliderModeSystem` removes the enemy category from that collider's mask, so enemy contacts never reach the solver
and therefore cannot deflect, displace, spin, or slow the Dasher. The solid filter is restored on leaving those phases.
Static geometry stays in the collision mask and remains solver-owned.

Dash commitment also stores a yaw-only `LockedRotation`. Player punch launch stores the punch direction immediately;
other launch sources capture facing from launch velocity on their first pre-physics update. The rotation is reapplied before
and after simulation while dashing or launched, and yaw angular velocity is cleared after simulation, preventing visible
solver-induced turning without disabling static collision response.

On each new shared `LaunchSequence`, `DasherVelocityCaptureSystem` preserves the incoming launch direction but normalizes
the horizontal launch speed to the Dasher's authored `DashSpeed`. This occurs once per launch, so subsequent static-geometry
response can still slow or stop the Dasher normally.

Because solver-free enemy pairs do not emit collision events, `DasherEnemyImpactSystem` performs a swept ECS overlap from
the Dasher's pre-step position to its post-step position using the existing enemy contact radii. It retains per-action,
per-target deduplication for player-launched Dasher impacts and writes `DamageRequest`, `ExternalImpulse`, and
`EnemyLaunchTransition` state. Intentional dashes are ignored by this enemy-impact path, so they pass through enemies
without damage, knockback, launch state, or other gameplay effects.
The sweep also prevents fast dashes from tunnelling through gameplay impacts. When a player-launched Dasher overlaps an
unexploded explosive enemy, this same contact path queues `ExplosiveDetonationRequest` before `ExplosionResolutionSystem`;
an intentional enemy dash does not trigger that launched-body interaction.

Launched-Dasher knockback direction blends its horizontal travel direction with the horizontal direction from the Dasher
to the struck target. The authored `LaunchedImpactPositionWeight` controls the blend: `0` produces a straight-ahead push
and `1` pushes directly toward the side on which the target was struck.

Launched-Dasher impacts have independent normal-enemy, elite, and boss tuning. `KnockbackResponse` provides those existing
three conceptual tiers without introducing elite or boss behavior: ordinary enemies are always momentum-transparent,
while elite and boss momentum preservation is authored independently. Static geometry remains solver-owned. Grey-box
feedback uses the existing per-entity material colour and post-transform overrides: warning pulse, bright elongated
dash/launched motion streak, distinct resting silhouette, and dark recovery. This stays entirely inside ECS rendering;
the current presentation architecture has no entity-linked GameObject trail renderer.

The current implementation proves architecture and basic interactions, but several behaviors are placeholders:

- Punch detection uses a line/capsule-like distance test and independently assigns impulse, damage, and launched state. Enemy collision damage is also independently thresholded rather than inferred from propagation.
- Player movement and dash are transform-driven MonoBehaviour movement.
- Dash-punch coordination stays on the player GameObject: `PlayerPunch` buffers an early press, while `PlayerController` reports normalized progress from its existing dash timer and ends dash movement when the punch is consumed at or after the configured `0.5` midpoint. Dash punches select independently configured damage and launch strength, then use the ordinary bridge and ECS punch pipeline.
- A launched enemy, including a zero-health enemy with deferred defeat, can propagate launched state to and independently damage an active or recovering enemy when Unity Physics reports solver-estimated contact impulse above the respective authored thresholds. Unity Physics exclusively owns velocities; gameplay adds no synthetic transfer. One source launch damages each target at most once but may damage multiple targets. Defeated enemies and launched-versus-launched pairs are ineligible. The final effect grammar remains unresolved.
- Enemy chasing and contact damage exist as prototype behavior.
- A player health bar exists. Normal-enemy health is displayed temporarily after damage per `INFO-001`, while non-active launch phases share that pooled view for transient state feedback.
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
