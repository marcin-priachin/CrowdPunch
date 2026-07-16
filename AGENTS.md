# CrowdPunch Agent Guide

This repository is a Unity 6 learning project for hybrid DOTS architecture. The goal is to keep a traditional GameObject player and simulate many small enemies with Entities 1.x and Unity Physics.

Future agents should preserve the architecture first. Do not turn this into a finished game by collapsing responsibilities into large scripts.

## Project Intent

- Unity version target: Unity 6.
- ECS packages: Entities 1.x, Unity Physics, Burst, Mathematics.
- Hybrid boundary: the large player, camera, and UI stay as MonoBehaviours; enemies are ECS entities.
- Learning goal: code should clearly demonstrate ECS concepts and update flow.
- Avoid packages not already part of the architecture: Netcode, Havok, CharacterController package, and third-party gameplay libraries.

## Source Layout

Keep source under `Assets/CrowdPunch/Scripts`:

```text
Authoring/          Inspector-facing MonoBehaviours used for baking.
Bakers/             Baker<TAuthoring> classes only.
Components/         Pure ECS data: IComponentData, IEnableableComponent tags/state.
Systems/
  Groups/           Custom update groups.
  Initialization/   Bootstrapping and initial entity creation.
  InputBridge/      Mono-to-ECS bridge systems.
  AI/               Intent decisions, not physics integration.
  Movement/         Convert intent into physics-friendly movement.
  Combat/           Gameplay hit detection and combat requests.
  Physics/          Gameplay writes to physics components before/after simulation.
  Lifetime/         Bounds, respawn, cleanup.
  Presentation/     ECS-to-presentation bridge points.
Aspects/            Only when repeated query bundles justify them.
Utilities/          Avoid unless a utility has a narrow, named responsibility.
Mono/
  Player/           Player GameObject controls and ECS bridge.
  Camera/           Camera MonoBehaviours.
  UI/               Scene-level UI/bootstrap MonoBehaviours.
```

One file should have one responsibility. Avoid classes named `Manager`, `Helper`, `Utils`, `Common`, `Base`, or similarly vague names.

## Hybrid Boundary Rules

- MonoBehaviours must not directly query or store enemy entities.
- MonoBehaviours communicate with ECS through dedicated bridge components such as `PlayerEcsBridge`.
- ECS systems should read bridge data through a bridge system, then write ECS data such as `PlayerSnapshot` and `PunchRequest`.
- The GameObject player owns input, player visuals, and player transform.
- ECS owns enemy spawning, movement, combat reaction, physics velocity, lifetime, and enemy presentation data.
- If a new MonoBehaviour needs ECS data, add a narrow bridge or presentation component rather than querying entities from the MonoBehaviour.

## ECS Coding Conventions

- Prefer `ISystem` over `SystemBase`.
- Prefer `SystemAPI` for singleton access and queries.
- Use `[BurstCompile]` on systems and jobs that can be Burst-compatible.
- Use `partial struct` for ECS systems and `IJobEntity` jobs.
- Use `readonly` where data is not mutated.
- Keep component structs small and data-only.
- Do not put UnityEngine object references in runtime ECS components.
- Use `EntityCommandBuffer` for structural changes such as instantiate, destroy, add/remove components, and entity creation during simulation.
- Prefer enableable components for frequent on/off transient state.
- Do not use `GetSingletonEntity<T>()` with enableable component types. If a singleton entity has enableable components, get the entity through a non-enableable singleton component on the same entity, such as `PlayerSnapshot` or `MatchState`.
- Do not use deprecated `IAspect` for new code. Existing aspect code is kept only as a learning artifact; prefer direct component/query APIs unless a repeated query bundle clearly improves readability.

## Enableable Component Use

Use `IEnableableComponent` for transient state that frequently toggles without changing archetypes:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `RespawnRequest`

When querying enableable components:

- Use normal queries when only enabled instances should be processed.
- Use `EntityQueryOptions.IgnoreComponentEnabledState` only when a system must see both enabled and disabled entities.
- Be explicit when enabling or disabling state. Avoid relying on structural add/remove for short-lived state.

## System Ordering

Preserve the custom groups and their intent:

- `GameInitializationGroup`: create and validate initial ECS state.
- `GameSimulationGroup`: gameplay simulation wrapper.
- `GamePrePhysicsGroup`: input bridge, AI, movement intent, combat detection, and impulse writes before physics.
- `GamePostPhysicsGroup`: recovery, bounds, respawn, and post-physics cleanup.
- `GamePresentationGroup`: copy ECS results to presentation-only systems.

For enemy simulation, keep the flow data-oriented:

1. `PlayerBridgeSystem` copies Mono player state into ECS.
2. `EnemyChaseSystem` writes `DesiredMovement`.
3. `EnemyMovementSystem` steers `PhysicsVelocity` toward intent.
4. `PunchDetectionSystem` converts a player punch request into `ExternalImpulse`.
5. `ApplyImpulseSystem` writes impulse into `PhysicsVelocity`.
6. Unity Physics simulates collisions.
7. `EnemyRecoverySystem`, lifetime systems, and presentation systems react after physics.

## Unity Physics Rules

- ECS enemies use Unity Physics components, not GameObject runtime `Rigidbody` components.
- Authoring prefabs may contain `Rigidbody` and collider components so baking can create ECS physics components.
- After baking, tune runtime behavior through ECS components such as `PhysicsVelocity`, `PhysicsMass`, `PhysicsDamping`, and collider data.
- Do not modify `LocalTransform.Position` for physics-driven enemies during normal movement. Use `PhysicsVelocity`.
- Do not overwrite horizontal velocity every frame unless intentionally building a kinematic motor. Steering toward target velocity preserves collision response.
- Keep enemy upright in ECS by adjusting `PhysicsMass.InverseInertia` or equivalent ECS physics data, not by expecting GameObject Rigidbody constraints to control baked entities.
- For collision problems, check collider filters, mass, damping, solver/timestep settings, and whether any system is overwriting physics state after the solver.

## Authoring And Baking

- Authoring MonoBehaviours expose inspector data only.
- Bakers convert authoring data into ECS components.
- Use current Entities baking APIs: `Baker<TAuthoring>`, `GetEntity(TransformUsageFlags...)`, `AddComponent`, `SetComponentEnabled`, and `GetEntity(authoringPrefab, TransformUsageFlags.Dynamic)` for prefab references.
- Declare transform usage explicitly. Use dynamic transform usage for moving enemies and prefab entities that need runtime transforms.
- Do not use runtime MonoBehaviours to populate ECS data that belongs in baking unless the data genuinely changes at runtime.

## MonoBehaviour Rules

- Keep MonoBehaviours thin and scene-facing.
- `PlayerController` handles player GameObject movement input.
- `PlayerPunch` handles punch input and writes punch requests to `PlayerEcsBridge`.
- `CameraFollow` follows the player transform only.
- `GameBootstrap` wires scene-level bridge references through `PlayerBridgeRegistry`.
- Do not add enemy logic, entity queries, or ECS spawning logic to MonoBehaviour player scripts.

## Naming And Style

- Namespace code under `CrowdPunch` and its existing subnamespaces.
- Use clear names that describe domain responsibility.
- Avoid inheritance. Prefer composition.
- Avoid large utility classes.
- XML documentation is only needed for public APIs and inspector-facing public properties where it adds clarity.
- Comments should explain non-obvious ECS or physics reasoning. Avoid comments that restate the code.
- Keep code ASCII unless the edited file already uses non-ASCII or there is a clear Unity-facing reason.

## Common Pitfalls Already Seen

- Entities visible in the Entities Hierarchy do not guarantee GameObjects will appear in the regular Hierarchy. Subscene content is baked into entities.
- GameObject Rigidbody constraints do not necessarily control the baked ECS runtime entity. Check baked `PhysicsMass` and runtime systems.
- If collisions appear to do nothing, inspect systems that write `PhysicsVelocity` every frame.
- Enableable singletons need careful access. Do not call `GetSingletonEntity()` on queries containing enableable component types.
- Unity-generated `.csproj` files can be stale until Unity refreshes them. A command-line `dotnet build` is useful but Unity compilation is the final source of truth.

## Verification

When changing code:

- Run `dotnet build Assembly-CSharp.csproj --no-restore` when practical.
- Expect existing Unity package warnings in this project; report errors and new warnings that matter.
- If adding new files and the project file is stale, tell the user to let Unity refresh assets and confirm in the Unity Console.
- For scene/prefab setup issues, inspect serialized scene/prefab files when possible, but clearly separate code problems from Unity Editor configuration problems.

## Design Bias

Prefer simple, explicit ECS data flow over clever abstractions. This project exists to teach architecture, so each system should make ownership and timing obvious.
