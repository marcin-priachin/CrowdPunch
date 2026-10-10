# Crowd Punch â€” Current Architecture

Status: Repository snapshot  
Last inspected: 2026-10-10
Unity: 6000.3.10f1

This document describes what exists now. It is not a desired future architecture and does not make prototype behavior into a design requirement.

## Campaign Chapters 1-4 (2026-10-10)

This section supersedes the historical fixed-run/build-registration descriptions below.
Bootstrap now references `Data/Campaign/Campaign.asset`: 80 stable IDs, eight chapter names,
and forty available scene paths. `Scenes/Campaign/Campaign_01` through `_40` are editable additive
encounters with separate ECS SubScenes. Levels 4/6 use barricades, 7 uses rotating cover and
10 uses the copied Gatekeeper arena with unchanged geometry and campaign-owned settings.
Level 8 has a 100 x 100m floor and four finite waves of 40/60/76/94 enemies (270 total).
Shared ordinary profiles, original 23 gauntlets and legacy tuning are preserved.

Chapter 2 adds Exploder and Dasher combinations, a shell/core target in level 12, a barricade
in 13, bidirectional tracks in 16/19, rotating cover in 17 and the Chicken boss in 20.
Level 18 has a 100 x 100m floor and finite waves of 52/70/88/108 (318 total). The shell uses
the existing zero-surviving-Exploders pair replenishment; tracks use existing physical arrival
completion. No runtime objective or enemy ownership/system ordering changed.

Chapter 3 adds armor with ammunition safeguards (21/28), Elite support (23/28),
finite protected-point defense (24), permanent ground patches (25/26/28), periodic
patches (27/28), a narrow gate (22), track (26), cover target (29), and Rolling Blob (30).
Level 28 is 100 x 100m with 59/77/96/110 initial enemies including three total Elites
(342 total before existing supply/replenishment). Hazard cycles are 4s inactive,
1.5s warning and 2.5s active; level 28's periodic pair has a four-second offset.
The defense batches four Baselines every four seconds across 12/16/20-enemy waves,
with five-second inter-wave delays and first-breach failure. `CampaignChapterThreeBuilder`
authors these assets using existing ECS systems and copies the Rolling Blob arena
without geometry changes, with separate boss settings and six replenishing Baselines.
Campaign gates from level 13 onward are 2m wide; solid side terrain seals the exit
and sits behind the front of the gate so edge shots hit the gate before the rocks.

Chapter 4 introduces Wizards (31) and Trails (33), mixes defense attackers (36), and
copies the complete Dino/pillar arena (40). Levels 32/39 use tracks, 34 a four-hit narrow
gate plus periodic shortcut, 35 a four-hit rotating target with a Trail, and 37 a shell
with a permanent hazard in the outer corner. Level 32 rotates rail, block, socket and
markings together by -25 degrees. Level 38 has a 100 x 100m footprint and 70/86/102/120
initial enemies (378 total including two Elites), with Wizard and armor supply safeguards.
Wizard wave-clear encounters wait for persistent Wizard zones; Trails alone do not gate
completion. Level 36 explicitly disables replenishment and hazard completion gates;
its 18/21/23-enemy waves arrive in batches of up to four every three seconds. Dino retains
three successful pillar hits and two local Baselines per unconsumed pillar in addition
to eight general support enemies. Settings belong to the campaign, and shared mechanics,
ordinary profiles, system ordering and boss arena geometry remain unchanged.

The Chapter 4 multi-wave profile found hidden pooled bodies still generating contacts
at their common storage position. `EnemyRespawnSystem` now queues a shared-component
change through its local ECB when a body actually enters the pool: `PhysicsWorldIndex`
becomes `uint.MaxValue`, reserved here as an unsimulated pool index. Visible landing
responses stay in world 0. Safe respawn restores world 0 before the next physics build;
colliders, masses, ownership, corpse timing and replenishment delays are preserved.
The repository has one simulated physics world (0); adding another world must preserve
this reserved-index contract. This prevents accumulated earlier waves from generating
quadratic contacts below the arena, including costly Wizard collision-event scans.

`GauntletSequence` remains the sole scene lifecycle owner. It selects campaign paths or the
preserved legacy scene arrays, handles completion/failure/retry, places and heals the player,
and exposes menu state. `CampaignCatalog` owns authored identity/content availability;
`CampaignProgress` owns versioned local JSON completion, sequential unlocks, backup recovery
and atomic replacement. The normal save is `Application.persistentDataPath/campaign-v1.json`.
Replays cannot regress saves. Unavailable future chapters can be unlocked but never loaded;
Chapters 1-3 completion offer Continue into the next implemented chapter.
Chapter 4 completion unlocks Chapter 5, whose scenes remain unavailable.
Existing stable-ID saves need no migration.

`PauseMenu` routes campaign presentation to the narrow uGUI `CampaignMenu`, retaining its
existing input asset and EventSystem. Main/selection/confirmation/pause/failure/milestone pages
use controller-selectable buttons. Player movement/punch/look inputs are suspended while menu
or transition gates are active. `FeedbackTimeController` still owns those independent time gates.
No runtime MonoBehaviour queries enemy entities. Health binding occurs in Start because
Bootstrap may create PlayerHealth after the sequence's early Awake.

The existing Level Play window has Campaign and Legacy tabs. Campaign Editor previews permit
any implemented level and do not write the normal campaign save; legacy launches use Editor-only
scene loading without needing build registration or reading/writing campaign progress.
`CampaignBuildRegistration` keeps Bootstrap and available campaign scenes in build registration,
including after legacy recipes try to register their scenes. Validation/legacy scenes stay out.

`CampaignChapterOneBuilder` reuses the existing geometry/wave/environment authoring functions
with explicit campaign output folders. Its creation command refuses to overwrite an existing
catalog; subsequent tuning is performed directly on saved assets. It reloads asset references
after single-scene loads, which can unload cached Unity objects. Waves, floor meshes, objective
and boss settings belong to the campaign; ordinary profiles and nature materials remain shared.

`CampaignChapterTwoBuilder` also refuses to overwrite an existing batch. It copies the complete
shell/track objective visuals from legacy scenes and remaps cross-root socket references into
the new scenes, with separate settings. The Chicken main/subscene pair is copied with arena
transforms and collision meshes preserved. Its campaign crowd is six Baselines at a four-second
replacement delay. Build registration now contains Bootstrap plus forty encounters.

`CampaignProgress` skips disk writes for already saved completions and retries transient file
replacement locks with a bounded total delay of 70ms. Persistent I/O failures still surface in
the existing error path. Tests exercise a deliberately locked backup and replay backup stability.

`CampaignTests` covers progress persistence, corruption recovery, sequential unlocks, asset
references and the large-wave specification. `CampaignLifecycleCheck` is an explicit Editor-only
controlled probe using an isolated temporary save and injected defeats. It checks loaded ECS
ownership, all ten transitions, retry, chapter completion and replay. Its evidence is not a
human combat, duration or difficulty playtest. See `Docs/Verification/Chapter1.md` for results.
`CampaignChapterTwoTests` checks existing-save continuation, Chapter 3 availability, objective
references and the unchanged Chicken arena. `CampaignLifecycleCheck.StartChapterTwo()` starts
at Gatekeeper using a temporary save and checks Chapter 2 plus the chapter boundary. See
`Docs/Verification/Chapter2.md` for that batch's evidence and playtest limits.
`CampaignChapterThreeTests` covers continuation through level 30, mixed crowd counts,
defense and hazard wiring, navigation clearance, and preservation of the Rolling Blob arena.
`CampaignLifecycleCheck.StartChapterThree()` checks the Chicken-to-Chapter-3 boundary,
defense failure/retry and batching, hazard phases, track arrival, and chapter completion
using an isolated save. See `Docs/Verification/Chapter3.md` for results and the large-crowd
profiling that led to the bounded Elite staging search described below.
`CampaignChapterFourBuilder` authors levels 31-40 without overwriting existing encounters.
`CampaignChapterFourTests` covers save continuation, large-wave safeguards, navigation,
objective references, and identical Dino/pillar geometry. `StartChapterFour()` extends the
controlled lifecycle check through mixed defense, including final partial batches, and
checks the six pillar ammunition bodies. Evidence is in `Docs/Verification/Chapter4.md`.

## Architectural Shape

Crowd Punch uses a hybrid Unity architecture:

- The large player, input, camera, player health, and UI are GameObjects with MonoBehaviours.
- Enemies, crowd movement, enemy physics, combat requests, spawning, lifetime, and ECS presentation data are Entities.
- `PlayerEcsBridge` is the narrow runtime boundary between the GameObject player and ECS.
- Authoring components in the arena subscene are baked into runtime ECS data.

## Scenes

- `Assets/CrowdPunch/Scenes/Bootstrap.unity` â€” persistent GameObject scene and application bootstrap. Its `GameBootstrap` object owns the fixed `GauntletSequence`; it contains no arena SubScene.
- `Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_01.unity` â€” First Line, the first additive gauntlet, containing its player entry point, brief opening hint, light, and arena SubScene reference.
- `Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_01` through `Gauntlet_23` each contain their matching ECS SubScene. Twenty-three gauntlets are included after Bootstrap in Build Settings and in the Bootstrap level selector. The first ten remain ordinary encounters; gauntlet 11 is The Gatekeeper boss, gauntlet 17 introduces Wizards, gauntlet 18 introduces protected-point defense, gauntlet 19 introduces Trail enemies, gauntlet 20 introduces the Chicken boss, gauntlet 21 introduces the Rolling Blob, gauntlet 22 introduces Dino Pillars, and gauntlet 23 introduces fixed ground hazards. The separate navigation validation scenes remain outside progression.
- Authored gauntlet scenes load additively around Bootstrap. Each owns a `GauntletLevel` entry point and its own ECS SubScene containing layout collision, arena bounds, spawns, and waves.

## Source Layout

The Rolling Blob encounter is described in [Rolling boss ownership and lifecycle](RollingBoss.md).
Its separate dynamic, upright body uses Unity Physics and capsule sweeps against actual static
geometry for immediate obstacle redirects. Cycle stage is committed separately from health stage;
hits never rewrite surviving action timers. Three boss types now share bounded wave ownership,
health-bar presentation, sideways body rebounds, completion and restart integration.

The Chicken encounter is described in [Chicken boss ownership and lifecycle](ChickenBoss.md).
Its separate kinematic body, manual swept bouncing projectiles and combined hit resolver do not
enter ordinary Enemy lifecycle queries. Both bosses share bounded wave ownership/replenishment,
the health-bar bridge and level completion while retaining separate attack/damage state.

The first boss is described in [Boss encounter ownership and lifecycle](BossEncounter.md).
Its separate head/hand components never enter ordinary Enemy queries. The dedicated baked boss
settings asset and one supporting EnemyWaveSettings wave own tuning; focused coordination,
physics motion, collision, replenishment, reset and presentation systems implement the encounter.

Gauntlet 11 uses `BossRoundArenaBuilder` (**Crowd Punch > Levels > Apply Round Boss Arena**), also called by the boss rebuild recipe. A grass disk and 64 overlapping rock perimeter colliders enclose a 19m court; the head follows a 20m circular route and protrudes inward. The existing player collision bridge blocks traversal behind the head. The supporting wave spawn rectangle is inscribed inside the court. Shared nature materials and the `rock_largeA` / `ground_grass` meshes reuse the earlier visual recipe. No runtime system or ownership change is needed.

The ten gauntlet SubScenes use nature-kit environment visuals authored by
`Scripts/Editor/GauntletNatureEnvironment.cs` (**Crowd Punch > Levels > Apply Nature Kit Environment**).
It fits `rock_largeA`, `path_stone`, `ground_pathTile`, and `ground_grass` from
`Models/kenney_nature-kit` to the existing perimeter, lanes, entry aprons, and backdrop.
Repeated pieces are combined into one visual mesh per original renderer under
`Data/GauntletLayouts/Nature`; shared, instanced URP materials live in `Materials/Nature`.
Custom floor polygons retain their vertices with separate grass-top and stone-side materials.
Original transforms, floor collision meshes, perimeter colliders, bounds, and encounter
authoring are unchanged (LOOP-002, VISION-004). This adds no runtime system or collider.
Reapplying preserves existing material edits. After using **Rebuild Ten Gauntlets**, reapply
the nature environment; that older recipe still creates the original blockout visuals.
The kit selection implements the requested environment replacement without closing the
broader art-direction question OQ-015.

All game code is under `Assets/CrowdPunch/Scripts`:

- `Mono` â€” GameObject player, camera, UI, and bridge registry.
- `Authoring` â€” inspector-facing baking inputs.
- `Configuration` â€” reusable ScriptableObject tuning consumed by scene-facing MonoBehaviours and bakers.
- `Bakers` â€” conversion from authoring objects to ECS components.
- `Components` â€” data-only ECS state and requests.
- `Systems` â€” initialization, bridge, AI, movement, combat, physics, lifetime, and presentation.
- `Groups` â€” explicit update phases.
- `Aspects` â€” one legacy learning aspect; new code should prefer direct component/query APIs.
- `Editor` â€” arena authoring inspector support.

There are currently no game-specific assembly definitions; scripts compile into Unity's generated assemblies.

`LevelPlayWindow` (**Window > Crowd Punch > Level Play**, also **Crowd Punch > Levels > Level Play Window**)
reads Bootstrap's authored `GauntletSequence` in an isolated preview scene and starts Editor Play Mode
from a selected gauntlet. It temporarily sets `EditorSceneManager.playModeStartScene` to Bootstrap;
an editor-only, one-shot `SessionState` selection is consumed by `GauntletSequence.Start` before
the usual first-level load. Normal additive loading, restart and onward progression remain owned
by the sequence (LOOP-002/006). The previous Play Mode start scene is restored on returning to Edit Mode.
The chosen level is remembered in local Editor preferences; no scene asset or build gameplay changes.

## Ownership Boundary

`EnemyFacingSystem` runs in `GamePostPhysicsGroup` after respawn processing and the Dasher
rotation lock. Active and Recovering enemies face the available player's horizontal position
(INFO-004), with yaw angular velocity cleared and position/linear velocity preserved.
Active Dashers normally face the player, but a Dasher in its committed `Dashing` phase faces
its locked dash direction. Launched enemies
instead face current horizontal physics velocity, including after collisions and homing,
even when the player is unavailable. Defeated rotations are untouched. Enabled respawn requests and coincident horizontal
positions skip facing. The resulting root rotation feeds enemy-local animation blending.

`Enemy.prefab` nests the humanoid `Models/BaseEnemy/BaseEnemy.prefab`, fitted to its existing
physics capsule. `EnemyMovement.controller` remains an editor-side animation source: runtime
enemies do not create GameObject Animators. `EnemyAnimationSampling` evaluates its idle and
eight directional poses (including mirrored diagonals), plus the `Flying.fbx` state, at 32 normalized phases per motion.
The generated `Animation/EnemyMovement.bytes` contains durations and 88 root-relative skin
matrices per pose. Rebuild it with **Crowd Punch > Animation > Rebuild Enemy Samples** after
editing the controller, clips, avatar, or mesh. The baker rejects stale source hashes and
bone ordering; it converts the samples into a shared blob with no runtime UnityEngine references.

`EnemyAnimationAuthoring` belongs to the skinned renderer. Its baker stores the owning enemy
entity, shared pose blob, blend response, and per-instance playback state there.
`EnemyAnimationSystem` runs a Burst parallel job in `GamePresentationGroup`, which precedes
Entities Graphics' `DeformationsInPresentation` group. It reads `DesiredMovement` in enemy-local
space, normalizes by `EnemyMovementSettings.MoveSpeed`, damps the blend, and interpolates idle,
the two surrounding movement directions, and neighboring sample frames into `SkinMatrix`.
Cycles start at entity-specific phases to avoid synchronized crowds. Matrix blending is a
sampled locomotion approximation, not a general runtime Animator-controller interpreter.

`EnemyBaseline.prefab` uses `Models/UltimateMonsters/Big/Orc.fbx` through that sampled
GPU-skinning path. Its `EnemyAnimationProfile.Baseline` loops `Idle` while stationary and
`Walk` while moving, holds a sideways `Idle` pose while launched, and plays `Death` once when
defeated. Generated instanced materials live under `Materials/Enemies/Baseline`; mesh-specific
CPA3 samples, the controller, prefab, materials, and `EnemySpawnSettings` reference can be rebuilt
through **Crowd Punch > Enemies > Rebuild Baseline Prefab** (ENEMY-012, INFO-004).
All enemy prefab builders enable `Collider.providesContacts`; the baked physics collider must
raise collision events for `EnemyLaunchCollisionSystem` to propagate launched state (COMBAT-002/003).

`EnemyDasher.prefab` uses the same sampled GPU-skinning path with the generic-rigged
`Models/UltimateMonsters/Flying/Dragon.fbx`. Its `EnemyAnimationProfile.Dasher` selects a
narrow state mapping: `Flying_Idle` loops while stationary, `Fast_Flying` loops while moving,
and `Death` plays once for `Defeated`. Intentional dashes and the shared `Launched` phase both
hold the first sampled frame of `Fast_Flying`, leaving dash direction and launch physics under
their existing ECS owners (ENEMY-005/007/008). The generated `EnemyDasherMovement.bytes`,
controller, prefab, and skinning material can be rebuilt with **Crowd Punch > Enemies > Rebuild
Dasher Prefab**. `EnemyDasherSpawnSettings` references that prefab directly.

`DasherTelegraphBridgeSystem` publishes each preparing Dasher's presentation ID, position,
locked aim, and normalized preparation progress through `PlayerEcsBridge`. `CombatFeedback`
owns a bounded `DasherTelegraphParticlePool` of the generated `Prefabs/Feedback/DasherTelegraph`
effect. Each active effect follows its preparing Dasher and scales toward dash commitment;
leaving `Preparing`, interruption, pooling, restart, or suspended feedback clears it without
changing gameplay state or storing an ECS entity in a MonoBehaviour (ENEMY-005).

Physics transforms and velocities are read-only to animation. Launched bodies play Flying
from its first frame on launch entry or a changed launch sequence (including re-punch),
clamp at the final frame, and apply visual pitch from vertical versus horizontal velocity.
Their collider remains upright; yaw comes from `EnemyFacingSystem`. At zero velocity,
the last facing is retained. Sampled bounds include all possible flight pitches.
On launch end, Recovering and Defeated bodies play `Falling Flat Impact.fbx` once and hold
its endpoint (COMBAT-010/011). Living enemies fit the impact into their remaining recovery
window before resuming locomotion; a re-punch immediately restarts Flying. The landing
clip imports vertical root motion into the pose, so the body lowers during the fall.
Sampling raises any below-floor retargeted pose to the model ground plane using the same
four-weight skin matrices used by ECS, and includes that offset in the sampled bounds.
This moves only the visual body; physics position and collider remain unchanged. The height
regression verifies all 32 poses stay above the model floor, finish grounded and lower the
body by more than 0.8 model units.
The landing pose does not inherit flight pitch. The visual post-baking system puts `EnemyLandingAnimation.Duration`
on the owner. Defeat processing uses that duration as `RespawnRequest.PoolNotBefore` for
previously launched bodies, so a stopped body is not pooled before its impact can play.
Pooled enemies reset playback and skip pose updates. Sample format CPA3 contains eleven motions;
Flying and impact endpoints are sampled explicitly, while locomotion wraps between samples.
Impact validation: ten animation cases and a lifetime check pass, covering recovery/defeat
impact playback, endpoint hold, re-punch interruption, and pooling before/after the visibility
deadline. A live defeated enemy showed advancing impact playback at phase 0.506, eleven
baked motions, finite skin positions and `IsPooled = 0`. The C# build has zero errors.
Flying validation: eight animation regression cases and seven facing cases pass, including
re-punch restarts, endpoint hold, flight pitch, redirected velocity and unavailable player.
A live launched enemy reported ten baked motions, advancing Flying playback, finite skin
matrices and a facing/velocity dot product of 1. The C# build passes with no errors.
`EnemyAnimationVisualBakingSystem` connects the additional skinned rendering entities to
`EnemyVisualOwner` and the existing body-color feedback (INFO-004). `EnemySkinning.shadergraph`
retains the Sidekick surface shading, adds compute deformation, and multiplies the result by
the ECS `_BaseColor` override. Sampled motion bounds include a small padding for culling.
The graph uses full precision: the compute deformation buffer index must remain a float
for the package's DOTS-instancing lookup; inheriting the Sidekick graph's half precision
produces an invalid DOTS shader variant.
`EnemyStaticShapeMesh` resolves the BaseEnemy model's fixed blend-shape proportions into
`Animation/EnemySkinningMesh.asset` during the same rebuild command. The enemy prefab uses
this generated mesh with its original bone weights, bind poses, topology and surface data.
The source model remains the input on every rebuild, so shape deltas cannot accumulate.
The current source uses two blend-shape frames at weights 0 and 100; generation rejects
unsupported active frame layouts. The renderer explicitly uses four bone influences so
editor bounds sampling does not depend on the selected quality tier.
Runtime enemies need only bone deformation, avoiding the GPU blend-shape path that produced
intermittent out-of-range vertices on the current Direct3D12 setup. Proportions are fixed;
future animated facial/body morphs would require a separately validated runtime path.
This uses the installed Entities Graphics experimental deformation path; GPU work and mesh
cost still need profiling against the unresolved crowd/hardware targets in OQ-001.

Validation (2026-09-10): six isolated ECS regression checks cover looping/interpolation,
enemy-relative direction, partial-speed strafing, launch/recovery/defeat without physics
writes, pooling/reuse, and missing owners. Unity Play Mode confirms the baked renderer,
body-color ownership, animated poses and shadows. An isolated 500-enemy/88-bone CPU playback
measurement averaged 0.30 ms per update over 300 updates after warmup on an i9-14900KF
(RTX 4090); this includes scheduling/completion but excludes GPU rendering and gameplay.
A separate live rendering check reached 518 animated instances, with 518 valid distinct
owners and body-color renderers, independent phases, and no shader errors. This is a
rendering/ownership smoke check, not an end-to-end crowd frame-rate benchmark.

Glitch validation (2026-09-11, Direct3D12): before the static-shape mesh change, a clean
120-frame GPU bounds probe found 48 frames exceeding four local units (maximum 15.63).
After rebuilding, both the single-enemy and 64-enemy checks completed 300 frames with
zero non-finite or oversized vertices (maximum absolute coordinate 1.77 and 1.83).
The mesh regression compares CPU-skinned positions and normals with the original model
within 0.0001, and checks unchanged bone weights, bind poses and topology. It and the six
ECS playback checks pass. The C# build completes with zero errors and package warnings.

`PlayerModel.prefab` owns a humanoid Animator with `Animation/PlayerMovement.controller` and
`PlayerMovementAnimation`. Its `MoveX`/`MoveZ` directional blend tree uses fighting idle and
eight jog directions from the seven imported clips; the left diagonal clips are mirrored for
the right diagonals. All movement clips loop with root translation and rotation baked into
the pose, and root motion is disabled. `PlayerController.LocomotionVelocity` publishes only
commanded movement (including the committed dash displacement), excluding external knockback.
The presentation component reads it relative to player facing, normalizes by authored move
speed, clamps to the jog blend radius, and damps the Animator parameters. Disable and player
reset clear the published velocity. The model discovers its parent controller at runtime,
so the Bootstrap prefab instance inherits animation support without additional scene wiring.
This preserves PLAYER-002 camera-forward facing and PLAYER-005 independent dash/punch timing.
Jogging during dash is the current presentation fallback because no dash clip is supplied.
Animation follows movement intent, so pressing against a blocking wall still plays locomotion.
No ECS ownership, physics ordering, or open art-direction decision (OQ-015) changes.

`PlayerPunchAnimation` drives the always-weighted `Upper Body Punch` override layer using
`PlayerUpperBody.mask` (torso, head, arms, and fingers; root and legs remain locomotion-owned).
The non-looping `Cross Punch` clip is sampled through the state's `PunchTime` motion-time
parameter. Frames 0-22 follow `PlayerPunch.CooldownProgress`, with frame 22 held when ready.
`PlayerPunch.PunchStarted` fires only for accepted requests and starts playback from frame 22
through the end at the Inspector's `punchSpeedMultiplier` (default 1). Strike playback takes
precedence over cooldown posing; after the final frame it returns to the current cooldown
pose, or directly to frame 22 on a miss/completed cooldown. Another accepted punch restarts
the strike even if the previous visual has not finished. `PunchStateReset` cancels visual
playback on gameplay reset/disable. Animation never delays hits, spends cooldown, or changes
dash movement (PLAYER-005, PLAYER-009). The shared cooldown property also drives hand feedback.
`PlayerPunchCooldownFeedback` finds the model's `hand_r` bone, scales it from the authored
cooldown multiplier to the ready multiplier, and creates two child particle systems at runtime.
The looping aura increases its emission and size during recovery before changing to the ready
color; a separate burst flashes once when recovery completes. It restores the animated bone
scale before each Animator evaluation so the presentation cannot accumulate scale drift.
`PlayerPunchAnimation` also exposes `readyYaw` and `punchYaw` offsets in degrees (both default
to zero). Cooldown frames 0-22 interpolate from punch yaw to ready yaw; frame 22 holds ready
yaw, and strike frames 22-34 interpolate back to punch yaw at the existing playback speed.
Punch yaw then holds from frame 34 through the rest of the strike clip.
After Animator evaluation, `LateUpdate` offsets the humanoid spine around the model's up
axis, carrying the torso, head, and arms without rotating the root or legs. The previous
offset is restored before the next animation evaluation and on disable to prevent drift.
This is presentation only: camera-forward facing and gameplay punch direction stay unchanged
(PLAYER-002, PLAYER-004, PLAYER-005).

| Concern | Current owner | Boundary/data |
|---|---|---|
| Input, player transform, and punch | GameObject | `PlayerController` owns movement and committed dash timing; `PlayerPunch` owns immediate attack input and hit-confirmed cooldown; `PlayerPunchCooldownFeedback` presents recovery on `hand_r`. Punching does not interrupt a dash. |
| Player health and invincibility | GameObject | `PlayerHealth`; `PlayerHitAnimation` plays Hit To Body on accepted damage; invulnerability remains health-owned |
| Camera | GameObject | `CameraFollow` |
| Scene bootstrap and UI | GameObject | `GameBootstrap`, UI MonoBehaviours |
| Fixed gauntlet sequence and additive scene lifecycle | GameObject | `GauntletSequence`, `GauntletLevel` |
| Player state visible to ECS | Bridge â†’ ECS singleton | `PlayerSnapshot` (including collision-resolved velocity), `PlayerHealthSnapshot` |
| Punch command visible to ECS | Bridge â†’ enableable ECS request | `PunchRequest` |
| Enemy contact reported to player | ECS â†’ bridge event | `EnemyContactHitReceived` |
| Enemy initial spawn, waves, and pooling | Authoring â†’ baked profile â†’ ECS | random `SpawnSettings`, authored spawn points, ordered wave buffers, shared initialization, and respawn systems |
| Enemy tactical intent, navigation, and movement | ECS | `NavigationIntent`, cached `NavigationPathState` / waypoint buffer, resolved `DesiredMovement`, Unity Physics velocity |
| Static solid geometry and navigation grid | Authoring / baking | `SolidObstacleAuthoring`, `NavigationArenaAuthoring`, immutable clearance/region blob; no dynamic crowd occupancy |
| Enemy archetypes and attacks/effects | ECS | `EnemyArchetype`, ranged state, and explosive settings/state |
| Ranged projectile trajectory and lifetime | ECS | `RangedProjectile`, velocity-led fixed fire-time start/target, ECS transform evaluation |
| Enemy combat state | ECS | health, damage, impulse, explicit launch lifecycle, death/respawn requests |
| Punch trajectory preview | ECS â†’ bridge â†’ GameObject | `PresentationBridgeSystem`, `PlayerEcsBridge`, `PunchTrajectoryPreview` |
| Punch cooldown feedback | GameObject | `PlayerPunchCooldownFeedback` scales `hand_r` and owns the generated cooldown-aura and ready-flash particles; tuning lives in `PlayerPunchSettings` |
| Enemy body feedback | ECS rendering | Baked `EnemyVisualOwner` connects root/child renderers to gameplay state; `EnemyReadabilitySystem` writes body color. `DasherPresentationSystem` retains ownership of Dasher color/shape. |
| Temporary normal health and elite health UI | ECS â†’ presentation registry â†’ Canvas | `EnemyHealthBarVisibility`, `EnemyHealthBarBridgeSystem`, `EnemyHealthBarCanvasRegistry`, `EnemyHealthBarCanvas`; no repeated normal launch/recovery text, and zero-health normal bars are suppressed. |

The same presentation registry and pooled Canvas now draw one shield icon per remaining Armored armor stage.
The canvas never queries enemy entities, and Armored does not publish health bars.

MonoBehaviours do not retain or query enemy entities. `PlayerBridgeRegistry` exposes the one active `PlayerEcsBridge` to the few managed systems that cross the boundary.

## Update Flow

### Initialization

`GameInitializationGroup` runs in Unity's initialization phase:

1. `BootstrapSystem` establishes singleton/runtime state.
2. `EnemySpawnSystem` instantiates the initial enemy pool.
3. `GameRestartSystem` handles managed restart coordination.

### Gauntlet Level Flow

`GauntletSequence` belongs to the persistent Bootstrap scene and loads one configured gauntlet scene additively at a time. A gauntlet scene owns its presentation layout, one `GauntletLevel` marker with an authored player entry point, and an ECS SubScene for level-specific collision and encounter data. The transition pauses scaled simulation, unloads the previous scene and its baked entities, loads the next scene, places the GameObject player at the authored entry point, and requests the established ECS restart reset. It never queries or retains enemy entities.

`GauntletCompletionSystem` runs in `GamePresentationGroup` and reports through the narrow `GauntletCompletionRegistry`. Ordinary gauntlets require every loaded wave sequence to complete; an empty loading interval cannot advance. When a boss is loaded, its defeat state is authoritative and supporting-wave completion cannot win early. The Bootstrap flow consumes one completion signal and loads the next scene (LOOP-002/006). Gauntlet 10 advances to The Gatekeeper; its defeat advances to Gauntlet_12 (Crack the Shell). Chicken defeat advances to Gauntlet_21 (Rolling Blob), and its defeat advances to Gauntlet_22 (Dino Pillars). Only the final authored gauntlet sets `GauntletSequence.RunComplete`. The existing pause menu presents Run Complete, Play Again, and twenty-two selectable levels (BOSS-007, CHICKEN-006, ROLL-006, PILLAR-006).

`GauntletLevel` owns optional opening-hint text alongside its entry transform. `GauntletSequence` publishes that text and an entry counter to the existing `PauseMenu`, which shows a ten-second hint in levels 1-2 and a two-column selection grid in the menu. This is GameObject presentation metadata, not an enemy bridge. The authored display names are separate from scene-loading names. The existing restart button now delegates through `GameBootstrap` to `RestartCurrentLevel` when a gauntlet is active, resetting final-completion state and reusing the additive reload path. Legacy scenes still use soft restart.

### Initial Spawn Workflows

The arena supports two independent initial-layout inputs, and a SubScene may contain either or both:

- `SpawnerAuthoring` bakes the established `SpawnSettings` random-radius batch. Its center, radius, count, deterministic random sequence, and reusable `EnemySpawnSettings` asset retain their existing meaning.
- `AuthoredEnemyGroupAuthoring` is an organizational parent. Each child `AuthoredEnemySpawnPointAuthoring` references one existing `EnemySpawnSettings` asset and bakes one `AuthoredEnemySpawnPoint` with the child's exact world-space position, including Y. Each point bakes independently, so a null settings asset or prefab suppresses only that point.

Both inputs bake the same `EnemySpawnProfile`. `EnemySpawnSystem` resolves either a random position or an authored position and delegates prefab instantiation, common movement/health/contact tuning, separation selection, respawn policy, archetype identity, material override, and Baseline/Ranged/Explosive/Dasher component setup to `EnemySpawnInitialization`. `EnemyAuthoring` is only the prefab marker that bakes the common component layout; `EnemySpawnSettings` is the single tuning source for each spawned variant. Shared approach and retreat speeds serve both ranged positioning and Dasher positioning instead of maintaining duplicate Dasher values. Runtime behavior is always selected through `EnemyArchetypeKind`, never names. The initial spawn system has no `SpawnSettings` requirement, so authored-only scenes work. It consumes processed source components through an ECB, allowing newly loaded scenes to initialize once without respawning previous inputs.

Wave spawning is a third, independently selectable workflow and does not gate either initial-spawn workflow. Each `EnemyWaveSettings` ScriptableObject contains one wave's total count, weighted references to existing `EnemySpawnSettings` profiles with optional guaranteed minimum counts, positive-area world-space XZ rectangles, pre-wave delay, per-wave activation mode and timed-activation duration, and `AllAtOnce` or `Batched` cadence. Guaranteed normal allocations are interleaved in authored profile order and distributed proportionally over the complete wave; weighted selection fills the intervening slots. Minimums whose sum exceeds the wave's normal total make the wave invalid. `EnemyWaveSequenceAuthoring` holds the ordered wave references plus the deterministic seed, minimum player distance, and bounded placement retry count. Its baker declares dependencies on the sequence, wave assets, profiles, and prefabs, then flattens immutable definitions into ECS buffers; runtime components contain no Unity object references.

`ArenaAuthoring` bakes two independent world-space volumes. `ArenaBounds` is the enemy spacing/distribution area consumed by AI, movement containment, pooling, and edge respawn. `EnemyDefeatBounds` is consumed only by `OutOfBoundsSystem`; leaving it requests pooling for respawning enemies or terminal defeat for fixed wave enemies. Each volume has its own authored center offset and size, while an unset defeat size falls back to the spacing size for existing scenes.

`EnemyWaveSpawnSystem` owns timing, deterministic weighted profile allocation, and candidate sampling. Each wave selects whether its next wave activates after all current-wave enemies are defeated, all current-and-previous wave enemies from the sequence's current run are defeated, or its authored duration has elapsed once spawning finishes; current-wave defeat gating remains the default. The sequence maintains a running undefeated-owned-enemy count so cumulative gating does not require a crowd-wide query. Rectangles are selected proportionally to area and sampled uniformly, so equal valid world area has approximately equal probability. A conservative spherical clearance is derived from each prefab's authored collider bounds. Every candidate must remain inside its rectangle, clear the GameObject player's published position/radius and configured extra separation, and produce no Unity Physics point-distance hit against enemies or blocking geometry. Candidates spawned in the same update are also checked against one another. Attempts are bounded; blocked enemies stay pending, retry on later updates without restarting the pre-wave delay, and emit only throttled diagnostics.

Every wave instance still goes through `EnemySpawnInitialization`, preserving prefab/archetype identity, health, state, presentation overrides, projectile data, separation randomization, and archetype-specific setup. It additionally receives `EnemyWaveOwnership` with sequence entity, run generation, and wave index. `EnemyWaveDefeatCountSystem` observes terminal `EnemyLaunchState.Defeated` or an enabled pooling request after bounds handling and before pooling, marks each current-run ownership record once, decrements the sequence's cumulative undefeated count, and increments the current-wave defeat count when applicable. Legacy and authored enemies therefore cannot advance a wave. Wave instances explicitly bake their per-instance `EnemyRespawnSettings.Enabled` override to false, so they pool after defeat but never perform continuous arena-edge respawning; source assets and non-wave instances keep their existing policy.

Only one wave is spawned at a time per sequence. Progress first waits for the configured successful-spawn count, then applies the current wave asset's activation mode: exact current-wave defeat count, cumulative undefeated count, or its duration. The next asset's pre-wave delay begins after that condition. Timed waves may overlap previously spawned survivors. Zero-count waves progress safely. An invalid nonempty wave enters an inspectable stopped state without affecting other spawners. Empty sequences and completed final waves enable `EnemyWaveEncounterComplete` and never loop.

Each random enemy owns `RandomEnemySpawnRegion`; each authored enemy instead owns `AuthoredEnemyInitialPosition`. A full `GameRestartSystem` reset randomizes legacy enemies inside their own regions and restores authored enemies to their exact initial positions without archetype matching. It also clears velocity, health/damage/launch state, collision histories, ranged attack and positioning state, Dasher action state and hit history, explosive state, movement intent, and enableable transient data. Normal defeat pooling is unchanged: `EnemyRespawnSettings` still controls whether an enemy returns, and enabled enemies respawn at an arena edge rather than at an authored point. Disabled enemies stay pooled until full restart.

### Pre-Physics Simulation

`EnemyGroundConstraint` owns each enemy's cached floor height, landed lock, and player-punch snap request (COMBAT-018). `EnemyGroundConstraintSystem` runs last before physics, finds a horizontal static floor beneath an uninitialized enemy using the enemy collider filter, and accounts for the baked collider's bottom offset. It ignores dynamic bodies so crowds cannot become stacked ground planes. Initial falling remains physical until the collider bottom reaches the floor; a valid player punch requests immediate grounding before simulation. `EnemyGroundReconciliationSystem` runs first after physics and projects grounded Y position and velocity back onto that plane, preserving solver-produced XZ momentum. This is an explicit vertical constraint, not normal movement integration. Pooling requests are excluded, and restart/edge respawn clear the cached state. Floor lookup is cached after success; this slice assumes the existing flat, static arenas and does not implement traversable slopes or changing floor heights.

`GamePrePhysicsGroup` runs as a direct child of `FixedStepSimulationSystemGroup` before `PhysicsSystemGroup`:

1. `PlayerBridgeSystem` copies the latest GameObject player snapshot, health, and punch request into ECS.
2. `EnemyChaseSystem` produces explicit tactical destinations and a separate separation vector for navigation, alongside the legacy blended movement intent used when assistance is disabled. `EnemyCrowdPressureSettings` supplies a global cap, and the closest active baseline or explosive enemies up to that cap receive pressure assignments. Every other ordinary melee enemy owns a stable low-discrepancy coverage slot across the inset `ArenaBounds` rectangle and moves back toward that slot when released from pressure, preserving arena-wide interior launch opportunities rather than roaming between edge-heavy waypoints (COMBAT-016). Slot restoration uses normal movement speed while an enemy is materially displaced and reduces, but does not disable, separation influence until it nears the assigned area; this prevents crowd repulsion from overpowering interior coverage. An active explosive inside its authored contact-attempt range bypasses the pressure cap, continuously targets the player, and suppresses active-enemy separation until it leaves range or ceases to be active, ensuring crowd avoidance cannot prevent its intended collision (ENEMY-010). If that direct player target lies beyond the navigation spacing grid but the complete route is clear of authored static obstacles, `EnemyNavigationSystem` preserves the direct physics pursuit instead of resolving the goal back to the grid edge; this keeps COMBAT-017's distribution volume separate from physically reachable arena space. Other assigned enemies approach deterministic surround-ring slots, using normal speed outside their authored charge distance and the charge multiplier inside it. When a slot is compressed by the inset arena bounds, the system samples deterministic golden-angle alternatives and selects the candidate that preserves the most intended radius. Assigned enemies use per-enemy randomized intervals to make brief, speed-scaled contact attempts toward the player. Each enemy uses its spawn-selected default separation distance and weight unless its profile's override array contains an entry for the nearby enemy's explicit archetype, and ordinary contact attempts retain separation with an independently authored weight. Ranged, Dasher, and elite systems retain their later archetype-specific overrides.
3. `RangedEnemyPositioningSystem`, `DasherDecisionSystem`, `ElitePunchSystem`, and `EliteCrowdSupportSystem` make their existing tactical overrides. Each intent writer publishes an explicit `NavigationIntent` destination, arrival behavior, mode, and separate local-separation vector alongside legacy `DesiredMovement`. Destinations are never reconstructed from a blended direction.
4. `WizardCastSystem` advances cast state after the player bridge and wave spawning. `WizardPositioningSystem` writes its dedicated distance-band intent, and `WizardHazardAvoidanceSystem` adds local zone avoidance after the other tactical writers. `EnemyNavigationSystem` runs after these intent writers and before both movement systems. It resolves clearance-valid direct travel or a budgeted cached route, then applies terrain-safe local steering to `DesiredMovement`. `EnemyMovementSystem` remains the normal Unity Physics velocity owner and rejects non-`Active` enemies. `DasherMovementSystem` retains committed straight-dash velocity ownership. See [Static obstacles and navigation](Navigation.md) for scheduler, settings, lifecycle, and inspection details.
5. `PunchAimAssistSystem` maintains an ECS-owned target lock for each punchable enemy in the live punch volume. A physics ray from the source enemy along player facing replaces the lock when it hits another eligible enemy inside the configured range and horizontal-angle limit; misses retain the lock only while it remains within those limits, and the smallest-angle rule supplies an initial in-cone fallback. `PunchDetectionSystem` uses that locked direction for each hit, clears existing linear and angular velocity before a normal player launch in any eligible state, starts a fresh `Launched` sequence with the current punch data, and enables impulse and damage requests (PLAYER-003, PLAYER-004, COMBAT-014).
6. `DamageApplicationSystem` applies enabled damage requests, clamps health, and resolves immediate defeat or records launch-deferred defeat.
7. `RangedEnemyAttackSystem` evaluates ranged state after punch and damage resolution, cancels invalid wind-ups, predicts a fire-time intercept from the collision-resolved player velocity and configured lead multiplier, and instantiates a projectile when a valid wind-up completes. The projectile locks that target and does not home (ENEMY-002, ENEMY-003).
8. `ApplyImpulseSystem` adds gameplay impulse to `PhysicsVelocity`.
9. Unity Physics simulates motion and collisions.

Ordering between systems that share only a group should be made explicit when correctness depends on it. The attribute graph, not filename order, is authoritative.

### Post-Physics Simulation

`GamePostPhysicsGroup` runs as a direct child of `FixedStepSimulationSystemGroup` after `PhysicsSystemGroup`:

- `EnemyLaunchCollisionSystem` interprets solver-resolved enemy impacts, resolves launch propagation first, applies configured smallest-angle direction correction to newly propagated horizontal velocity while preserving its solver-produced speed and vertical velocity, retains the selected candidate as that launch's homing target, and independently queues eligible impulse-scaled collision damage.
- `WizardImpactZoneSystem` observes qualifying physics contacts before collision damage and also sweeps against the Mono player's snapshot. It also checks launched Dasher sweeps because their enemy solver contacts are disabled. A launched Wizard's first qualifying contact or an active/recovering Wizard hit by a launched enemy creates an immediate fixed zone; incoming source/continuous-flight history deduplicates repeated contacts. `WizardZoneSystem` resolves zone membership and player ticks for both kinds; only impact zones query and affect enemies. It runs after launched-player impacts and explosions, before recovery. `WizardPlayerHitSystem` delivers its ECS hit buffer through the player bridge before recovery; per-zone player protection remains ECS-owned.
- `EnemyLaunchHomingSystem` runs before physics after gameplay impulses. A launched body with a player aim-assist or propagation target turns its horizontal velocity toward the still-living active/recovering target by the configured maximum degrees per second while preserving horizontal speed and vertical velocity (COMBAT-012, PLAYER-004).
- `ExplosiveCollisionTriggerSystem` requests an explosive detonation when either participant in an enemy collision is `Launched`; `ExplosionResolutionSystem` then resolves explosion overlap chains to a same-frame fixed point before recovery.
- `RangedProjectileSystem` evaluates each fixed trajectory, performs a swept player-radius hit check, forwards one accepted hit through `PlayerEcsBridge`, and destroys the projectile on hit, after falling below its authored world-space minimum altitude, or on expiry. It does not apply arena-bound cleanup because the unconstrained GameObject player can currently provide a valid target outside `ArenaBounds`.
- `EnemyRecoverySystem` advances living `Launched` enemies through low-momentum dwell and `Recovering` back to `Active`; a zero-health launched enemy enters `Defeated` directly when launch ends.
- `PlayerContactDamageSystem` uses full three-dimensional enemy/player proximity and reports the closest accepted hit through the bridge, so entities above or below the player cannot produce planar-only contact damage.
- `OutOfBoundsSystem` compares enemy positions with the separate baked three-dimensional `EnemyDefeatBounds`. Escaped enemies whose profile allows respawning enter the existing pool immediately; fixed wave enemies instead enter terminal `Defeated` state with zero health so their ownership is counted and they cannot block cumulative encounter completion from outside the authored arena.
- `DefeatedEnemyLifecycleSystem` converts the one-shot defeat marker into the existing respawn request.
- `EnemyRespawnSystem` brakes, pools, resets, and respawns defeated or otherwise invalid enemies.

### Presentation

`GamePresentationGroup` runs in Unity's presentation phase:

- `HealthBarPresentationSystem` updates ECS health-bar presentation data and expires one-second post-damage visibility.
- `EnemyHealthBarBridgeSystem` publishes living normal enemy health while post-damage visibility is enabled, and elite health while alive.
- `EnemyReadabilitySystem` maps archetype and non-launch gameplay feedback to body color through baked renderer ownership. Entering `Launched` preserves the archetype body color; movement and animation communicate launch state. Dasher presentation remains specialized and keeps its launched shape override without a launched color override.
- `PresentationBridgeSystem` is the explicit ECS presentation bridge point.
- `PresentationBridgeSystem` selects enemies currently inside the live punch volume, reads the same ECS-owned aim-assist target lock used by punch detection, and publishes their initial launch segments through `PlayerEcsBridge`; `PunchTrajectoryPreview` renders those segments as pooled semitransparent world-space lines.

Normal-enemy health bars are transient damage feedback only. The bridge no longer publishes normal launch/recovery labels or exhausted-health normal bars. `EnemyHealthBarCanvas` retains pooled views and never queries or stores enemy entities. The legacy state-label rendering option remains available in the canvas, but receives no normal labels from this iteration's bridge.

## Transient State Pattern

Frequently toggled state is represented by enableable components to avoid archetype churn:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `DamageRequest`
- `DeathRequest`
- `RespawnRequest`
- `EnemyHealthBarVisibility`

Enemy lifecycle is represented by the non-enableable `EnemyLaunchState` component because every enemy is always in exactly one of `Active`, `Launched`, `Recovering`, or `Defeated`. Its phase, last launch cause, explicit `Player`/`Enemy` launch owner, and propagated-launch count remain visible in the Entities inspector. Any launched enemy, including one at zero health with deferred defeat, can be punched again; a recovering enemy must still be alive. Before applying a normal player-punch impulse in any eligible state, `PunchResolution` clears the body's linear and angular `PhysicsVelocity`, so old momentum cannot skew the previewed direction. Elite knockback remains additive. This experiment extends the original launched-body replacement rule to Active and Recovering bodies. The transition then increments its launch sequence and resets its cause, owner, damage, recovery timing, propagated-launch count, and last propagation impulse to the new launch. A player punch therefore replaces enemy ownership rather than leaving stale player-threat state on the body. `Health` exposes current and maximum health, while `EnemyDamageState` records the last applied damage and whether zero-health defeat is currently deferred for development inspection.

`DamageApplicationSystem` is the explicit pre-physics health stage after punch detection. Punch detection establishes `Launched` before damage is evaluated, so a same-frame lethal launching punch deterministically defers defeat and still receives its impulse. After physics and collision propagation, `EnemyRecoverySystem` chooses either normal recovery for a living projectile or direct defeat for a zero-health projectile. `DeathRequest` is enabled only on the transition to `Defeated`, making the lifetime handoff idempotent; `DefeatedEnemyLifecycleSystem` consumes it once and enables `RespawnRequest`.

Collision damage is queued post-physics into the target's existing `DamageRequest` and applied during the next pre-physics damage stage. `EnemyLaunchState.LaunchDamage` carries the originating punch damage through every propagated launch; `EnemyCollisionDamage` is the shared rule that converts estimated impulse into the configured multiplier of that value up to its cap. `EnemyLaunchState.LaunchSequence` identifies each continuous launch. Each target's `CollisionDamageHistory` buffer suppresses repeat damage from the same source sequence; `CollisionDamageHistoryCleanupSystem` removes entries when the source leaves that launch. Propagation and collision damage have independent impulse thresholds. Only launched-to-active/recovering impacts are damage-eligible, and propagation copies launch ownership before damage is queued so a lethal propagated target defers defeat without losing provenance.

`LaunchedEnemyPlayerImpactSystem` handles the hybrid boundary after physics and before recovery/contact damage. Because the GameObject player is outside the ECS collision world, it performs a swept enemy-to-player radius test and estimates impact impulse from the body's post-physics speed and dynamic mass. Every launched body qualifies regardless of launch ownership. Damage uses the same `EnemyCollisionDamage` threshold and curve as enemy targets, and `PlayerImpactLaunchSequence` permits at most one player impact per continuous source launch. Simultaneous qualifying bodies consume their launch impact and the strongest damage is sent through `PlayerEcsBridge`; normal player invulnerability remains MonoBehaviour-owned. Explosion and other independent player-damage rules remain separate.

`GameSettingsAuthoring` reads reusable `GameRuntimeSettings` ScriptableObject data and bakes `EnemyLaunchSettings` as scene-level singleton configuration. Its provisional sandbox tuning includes independent propagation/damage impulse thresholds, propagated-launch aim-correction radius, launched-body homing degrees per second, base/per-impulse/maximum collision-damage multipliers, useful-momentum threshold and dwell, and recovery duration. Player movement and punch settings assets own their respective tuning and Input System asset/action selection, while `EnemySpawnSettings` owns the enemy prefab, common movement, health, contact behavior, archetype tuning, initial crowd tuning, and whether enemies from that spawn profile return after pooling. Every profile retains a default separation-distance range and weight, plus an optional array of target-archetype entries containing their own range and weight. Baking randomizes the default and configured target-specific distances per enemy; movement selects the matching entry from the nearby enemy's `EnemyArchetype`, falling back to the defaults. Duplicate target entries are warned about in the custom Inspector and resolve last-entry-wins during baking. The baked per-enemy `EnemyRespawnSettings` preserves that policy when multiple spawners use different profiles; disabled respawning leaves defeated enemies pooled until a game restart. Scene MonoBehaviours retain only scene-instance wiring such as bridges, cameras, and origin transforms.

Full restart destroys all old wave-owned instances, increments each sequence run generation, clears counters and completion, restores its initial random seed, and lets the first wave start its full pre-wave delay again. Destroying the old generation prevents stale pooled entities or defeat observations from affecting the new run and avoids double-spawning. Legacy random and authored enemies continue through their existing in-place reset semantics.

## Ranged Enemy Archetype

`EnemySpawnSettings.Archetype` explicitly selects `Baseline` or `Ranged`; no prefab, scene-object, or presentation name participates in runtime identification. Every spawned enemy receives `EnemyArchetype`. A ranged selection additionally receives the baked `RangedEnemySettings`, `RangedPositioningState`, and `RangedAttackState`. The existing `EnemySpawnSettings.asset` remains baseline. `RangedEnemySpawnSettings.asset` is used by a second ordinary `SpawnerAuthoring` in the arena subscene to add five ranged enemies without changing the 500-enemy baseline batch.

`EnemyRanged.prefab` uses `Models/UltimateMonsters/Big/Alien.fbx` through the sampled GPU-skinning path.
Its `EnemyAnimationProfile.Ranged` loops `Idle` while stationary and `Walk` while moving, plays `Wave`
from the beginning during ranged wind-up, uses a sideways `Idle` pose while launched, returns to locomotion
during recovery, and plays `Death` after entering `Defeated`. Generated instanced materials live under
`Materials/Enemies/Ranged`; mesh-specific CPA3 sample blobs, the controller, prefab, materials, and the
`RangedEnemySpawnSettings` prefab reference can be rebuilt through **Crowd Punch > Enemies > Rebuild Ranged Prefab**.

All ranged numerical settings are provisional and live on the ranged spawn settings asset: preferred minimum/maximum distance, engagement range, approach/retreat speed, initial delay and per-instance variation, wind-up, base cooldown and per-shot cooldown variation, damage, player invulnerability duration, projectile speed, horizontal aim-spread radius, fire-time target Y offset, arc height, minimum world-space altitude, lifetime, and radius. Independent per-enemy cadence plus initial and per-shot timing variation is the first-pass multi-attacker control; there is no global simultaneous-attack cap.

`RangedEnemyPositioningSystem` owns the ranged approach/hold/retreat decision. It reuses the baseline active-enemy separation input and writes `NavigationIntent` plus legacy `DesiredMovement`; reachable distance-band goals preserve approach/retreat/hold semantics, and `EnemyNavigationSystem` resolves final steering before movement; `EnemyMovementSystem` remains the velocity owner and its `Active` gate prevents ranged steering from overwriting launch or recovery velocity. `RangedAttackState` exposes eligibility, lifecycle phase, remaining time, emitted count, and cancelled-wind-up count for Entities inspection. Attack evaluation runs after punch and damage application, so same-frame launch or defeat cancels before emission. Pooling resets both attack and positioning state; already-fired projectiles have no shooter reference and remain independent.

`RangedProjectileSystem` uses a deterministic parametric path. At fire time, a deterministic per-enemy/per-shot point inside the authored horizontal spread radius and the authored target Y offset are added to the sampled player position; the time to that point is derived from horizontal distance divided by authored speed. Horizontal/world-space position is `lerp(start, fireTimeTarget, t)` and vertical readability adds `4 * arcHeight * t * (1 - t)`. The system does not clamp `t` at `1`, so after crossing the sampled aim point the projectile continues at the same horizontal speed and along the descending parabola until hit, minimum-altitude cleanup, or expiry. The target is never updated after firing. The prefab is a yellow grey-box sphere with a baked kinematic collider on the `RangedProjectile` layer. Its collider explicitly excludes the Default layer used by enemies and the arena, so it produces no enemy collision events or reactions; player contact is checked against the ECS player snapshot because the player remains a GameObject outside the ECS physics world.

Projectile damage calls `PlayerEcsBridge.ReceiveEnemyHit`, which converts the configured amount for the existing `PlayerHealth` event pipeline. `PlayerHealth` remains authoritative for invulnerability, health clamping, damage acceptance, and death. Projectile simulation is data-only and its baked mesh/material is presentation. There was no compatible projectile pool, so this first pass uses command-buffer instantiation and destruction; this should be revisited only if profiling at representative projectile counts shows structural-change cost is material.

Systems get the entity for mixed singleton state through a non-enableable component such as `PlayerSnapshot` or `MatchState`, then inspect or toggle enableable state explicitly.

## Explosive Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Explosive`. `EnemyExplosive.prefab` uses the generic-rigged
`Models/UltimateMonsters/Blob/GreenBlob.fbx`; both skinned renderers receive generated instanced materials and
mesh-specific CPA3 sample blobs. Its `EnemyAnimationProfile.Explosive` loops `Idle` while stationary and `Walk`
while moving, holds the first `Walk` frame while launched, returns to locomotion during recovery, and plays `Death`
only after entering `Defeated`. Rebuild the controller, samples, materials, and prefab through **Crowd Punch > Enemies
> Rebuild Explosive Prefab**. `ExplosiveEnemySpawnSettings.asset` references this prefab directly.

The spawn system uses the prefab's common movement components, adds `ExplosiveEnemySettings`, `ExplosiveEnemyState`,
and an enableable `ExplosiveDetonationRequest`, then overrides the material base color to orange for gameplay
readability. `ExplosiveEnemySpawnSettings.asset` and the arena's ordinary `SpawnerAuthoring` follow the same profile
pattern as the ranged archetype.

`ExplosiveCollisionTriggerSystem` reads Unity Physics collision events after simulation. It requests the explosive participant when either enemy was already in the authoritative `Launched` phase, without an impact threshold. `ExplosionResolutionSystem` also requests an active explosive whose baseline contact radius overlaps the player. `PlayerContactDamageSystem` excludes explosive archetypes so this contact produces the explosion instead of ordinary melee contact damage.

Explosion resolution marks `ExplosiveEnemyState.HasExploded` before applying any effects, making multiple contacts and overlapping blasts idempotent. Each blast uses a constant radius check with no falloff, accumulates the existing `DamageRequest` and `ExternalImpulse`, and calls the same `EnemyLaunchTransition` used by punches and collision propagation. Explosion-launched enemies therefore carry explosion damage as their launch-chain damage and follow normal collision propagation, deferred defeat, recovery, and pooling rules. Newly overlapped explosives enable their own detonation request; the resolver repeats until no request remains, allowing legitimate overlap chains to complete in the same post-physics frame. The source explosive transitions directly to `Defeated` and enters the established death/respawn handoff.

The existing player bridge receives explosion damage through the normal `PlayerHealth`/invulnerability event and publishes a separate presentation-only event. Player/elite knockback is tuned independently from normal-enemy knockback; boss knockback is baked as future-compatible configuration but is not consumed because bosses do not exist. `ExplosionFeedback` renders a short-lived expanding grey-box sphere without exposing ECS entities to MonoBehaviours. Its dedicated transparent URP shader is loaded from `Resources`, keeping the shader as an explicit Windows player-build dependency instead of relying on a runtime `Shader.Find` lookup that may be stripped.

## Current Prototype Behavior Versus Design

## Pause And Level Selection

`PauseMenu` is a scene-level MonoBehaviour UI boundary created on the persistent game canvas by `GameBootstrap`. It reads
the shared `Assets/InputSystem_Actions.inputactions` `Game/Pause` action, toggles `Time.timeScale`, and owns no ECS data.
It also owns cursor capture: active gameplay locks and hides the cursor, while pausing releases and shows it for menu
interaction. The component reapplies the current cursor state when enabled and when application focus returns (PLAYER-011).
Its runtime-built menu restores selection to Resume when opened so the Input System UI module can navigate and submit with
a gamepad. Level buttons are populated from the fixed `GauntletSequence`; selecting any entry calls the same additive
level-loading path, and selecting the active entry therefore unloads and reloads that gauntlet. The loader continues to
reactivate the GameObject player, restore full health, reset player movement and punch state, place it at the authored
entry point, and request the established ECS restart handoff. The
old always-visible restart button is disabled; restart is now represented by selecting the active level in the pause menu.

## Elite Projectile Punch

Elite spawn profiles now bake `ElitePunchSettings` and initialize an inspectable `ElitePunchState`; no ordinary enemy
receives attack state. The explicit phases are `InitialDelay`, `SelectingTarget`, `Repositioning`, optional `WindUp`, and
`Cooldown`. Each new attempt advances the elite's stored ECS random stream for its inspectable tactic state, while actual
projectile selection scans the eligible set and always chooses the closest active normal enemy with a deterministic
entity-index tie break. It re-evaluates that choice on the existing retarget interval during setup, so a newly closer enemy
replaces the reservation without requiring per-frame selection.
Retargeting continues while the projectile is staging; only setup timeout pauses during that wait (ENEMY-009).

Only active, normal-tier, health-bearing, non-defeated, non-pooled targets qualify for a coordinated shot. The legacy elite
search range and target-to-player distance fields do not filter this nearest-enemy rule. Spawn order affects only the
entity-index tie break when squared distances are exactly equal.

During setup, the desired position is behind the projectile relative to the live player, on the elite's movement plane.
`ElitePunchSystem` runs after `EnemyChaseSystem` and before `EnemyNavigationSystem` and `EnemyMovementSystem`, making its tactical intent override
explicit while leaving velocity integration in the existing movement system. It turns the elite toward the live
target-to-player launch direction at the profile's existing `TurnSpeed`; ordinary chase facing is not used as a hidden
attack prerequisite. Search eligibility uses the dedicated active, recovering, and already-launched switches, while final
punch-effect eligibility is independently rechecked immediately before execution. Setup speed uses the common movement
acceleration and braking values to calculate an arrival speed from remaining distance, allowing fast traversal while
decelerating into the authored position tolerance. Outside that tolerance, the requested speed is never allowed below the
selected target's horizontal speed plus the relative speed needed to cross the tolerance under the same braking model;
this prevents a moving target from creating a permanent follow gap. Position, facing, the exact shared punch
volume, target/player displacement, phase eligibility, reservation ownership, setup timeout, and entity existence are
revalidated through repositioning and wind-up. Zero wind-up executes on the first valid update; invalid wind-up returns to
setup instead of firing. `TelegraphActive` is presentation-only state and is never required for execution.

`PunchResolution` is the focused common resolver used by both player and elite punches. It owns capsule-like forward/radius
evaluation, position-weighted impulse direction, Dasher interruption, `ExternalImpulse`, `DamageRequest` accumulation, and
the authoritative `EnemyLaunchTransition`. Elite-launched normal enemies carry the explicit `ElitePunch` cause and retain
the existing launch sequence, collision history, collision damage, propagation, deferred-defeat, and recovery pipeline.
The elite can affect only its selected projectile or every eligible normal-tier enemy in the exact volume. Optional direct
player contact uses the same ECS-to-Mono hit bridge and remains distinct from later launched-body collision damage.

Every spawned enemy carries a small reservation record. An elite writes ownership only while setting up, and releases it
on execution or cancellation; stale owners and defeated/pooled targets are cleared independently each update. Shared
reservations are an authored policy. Restart and pooling reset attack state, random state, target references, telegraph
state, and reservations, while wave-owned destruction continues to remove the entire old generation. Elite tier gating is
unchanged, so elites remain damageable and knockback-responsive but never enter `Launched`; normal replenishment remains
owned by the existing wave counters.

`EliteCrowdSupportSystem` runs after elite target selection and before movement integration. For each active elite, its
selected projectile (or the closest active normal before selection) first tests its current location as a staging point.
The test excludes static-world ray obstruction, occupied target space, and non-defeated enemies inside the finite route
from the elite to its eventual behind-projectile position. If blocked, the projectile first checks two deterministic rings
of eight nearby candidates, preferring lateral displacement. If those local samples cannot provide full elite clearance,
it searches navigation-cell rings outward until it finds a reachable staging cell with a direct, body-clear elite approach.
The projectile may route around static terrain to reach that distant cell; the elite remains stationary until it arrives.
This lets a selected enemy beside a long obstacle move far enough away for an unobstructed aim and approach. It requests
zero speed when its current approach lane is clear or no sampled alternative is available; only a verified clear current
lane publishes `IsStaged`. With navigation enabled, staging also checks clearance-class-inflated static geometry and arena
bounds for the projectile path and elite approach, plus navigation anchors at both destinations. A centre ray alone does
not prove that an elite can reach the behind-projectile point. The target reservation publishes `IsStaged`; while it is false,
`ElitePunchSystem` requests zero elite movement and does not spend setup timeout, preventing two moving goals from chasing
one another. The expanding search checks at most 32 in-bounds cells per elite per update,
with a 256-step limit for skipping out-of-bounds cells. `EliteStagingSearch` stores the
outward cursor and a candidate in system-owned managed scratch, keyed by the versioned
elite entity. A candidate is revalidated against current bodies and terrain before reuse.
The search resets when its projectile or arena owner changes, or a participant moves
more than four metres from its search origin; an exhausted search retries after half a
second. Inactive elites and an unavailable player discard cached searches. This bounds
the long-obstacle fallback in the 100 x 100m campaign arenas without changing staging
clearance or publishing a pending search as a ready shot. Sampling and clearance reuse
the elite's existing crowd-corridor radius and position tolerance. Other active normal enemies in the finite
projectile-to-player corridor override chase intent with lateral movement toward the nearest side.
Launched, recovering, defeated, disabled, and pooled enemies are excluded. When several elites are active, each normal
supports its nearest active elite with entity index as the deterministic equal-distance tie break. The support layer only
writes explicit `NavigationIntent` and legacy `DesiredMovement`; navigation resolves terrain steering and physics velocity remains owned by `EnemyMovementSystem`.
If a projectile recovers outside navigation spacing bounds, support first requests a clear interior reentry point and keeps
it unstaged. Navigation's existing slow escape movement permits that ingress while checking the complete segment against
inflated static obstacles; it does not teleport enemies or change physical arena/defeat bounds (COMBAT-017).
Projectile staging also uses the existing body-side approach calculation around the waiting elite, using their combined
navigation-agent radii. Terrain clearance checks the waypoint and tries the opposite side when needed, preventing a
staging destination from driving the projectile directly into its stationary owner.

After executing a punch, `ElitePunchSystem` clears the launched target and retains the authored cooldown. During that
cooldown it continuously finds the next closest eligible active normal and uses the same behind-projectile destination and
arrival-speed calculation as setup. It does not reserve or punch that enemy until cooldown ends. This prevents ordinary
player-chase intent from visually interrupting the coordinated sequence between consecutive shots.
Failed speculative cooldown routes cannot restart cooldown before target selection. Selection and retargeting publish a
hold intent to retire the previous route through `EnemyNavigationSystem`; only a failed reserved, staged approach cancels
the current attempt into cooldown. This prevents the Gauntlet_07 failure loop from suppressing all subsequent reservations.

Both cooldown approach and reserved-target setup pass their desired direction through the same collision-avoidance helper.
When the direct segment to the behind-projectile destination crosses the target's punch-radius clearance, the helper chooses
a deterministic side waypoint around the target; once the target no longer blocks that segment, movement returns directly
to the desired position. Unity Physics remains responsible for actual collision response.

Gauntlet_07 stall verification (2026-09-20): before the fix, the live elite remained at attack sequence two while failed
speculative routes repeatedly restarted cooldown. After the fixes, a 75-second Editor observation recorded eight distinct
elite launches with 23 peak active enemies, and the longer state trace reached attempt 19. The idle player's health was
restored for observation and earlier normal-only waves were cleared with injected damage; this was a behavior check, not
a balance test. Crowd-blocked staging can still pause while waiting for a clear lane or a closer eligible projectile.
ProfilerRecorder samples in that run averaged 0.143 ms for elite punch and 0.521 ms for crowd support (maxima 3.160/5.890 ms);
these are local Editor system costs, not standalone performance guarantees. Regression coverage includes failed-cooldown
retry, retargeting during staging, actual setup failure, body-clear staging, blocked fallback readiness, projectile bounds
reentry, and obstacle rejection during reentry. Build and regression artifacts are under `Temp/elite-*` and
`Temp/EliteRegressionResults.xml`.

The subsequent paused recurrence exposed an additional corner case: projectile 337 at (-6.421355, 9.660804) was
physically clear of the obstacle spanning (-6, 10) to (-3, 13), but inside its square-inflated navigation corner.
`TryEscape` now checks a swept circle of the actual body radius against rectangle faces and corners. This lets a round
body leave the conservative corner margin while still rejecting paths that cross or touch physical obstacle clearance.
Ordinary paths keep square inflation and the escape destination must still be a class-valid interior cell. Regression
coverage uses the captured coordinates, checks nonzero ECS movement intent, and rejects intersecting/tangent sweeps.
The runtime/geometry suites passed 48 tests after this correction. A fresh Gauntlet_07 replay restored the captured player,
elite, projectile and crowd positions with fresh wave health/velocity state: the projectile left the corner and four direct
elite launches were recorded within 15 seconds. The replay was paused after observation. Captures and replay evidence are
in `Temp/elite-paused-recurrence.txt`, `Temp/elite-paused-diagnosis.txt`, `Temp/elite-corner-replay.txt`, and
`Temp/EliteCornerRegressionResults.xml`; the replay is a targeted reproduction, not a restored save of the original run.

## Elite Enemy And Elite-Gated Waves

`Elite` is appended after the four original serialized `EnemyArchetype` values. Baking maps it to the explicit
`EnemyArchetypeKind.Elite` runtime identity and adds `EnemyTier { Elite }`; neither combat nor presentation infers elite
status from prefab, asset, or object names. Elite profiles reuse `EnemySpawnSettings` for their prefab, health, ordinary
melee movement/contact behavior, and all common tuning. Their `KnockbackResponse` uses the existing `PlayerElite` tier.

`EliteEnemy.prefab` uses `Models/UltimateMonsters/Big/Yeti.fbx` through the sampled GPU-skinning path.
Its `EnemyAnimationProfile.Elite` loops `Idle` while stationary and `Walk` while moving, plays `Punch`
during the authoritative elite wind-up, restarts `HitReact` when health decreases, and gives `Death`
priority on defeat. Generated instanced materials live under `Materials/Enemies/Elite`; mesh-specific
CPA3 sample blobs, the controller, prefab, materials, and `EliteEnemySpawnSettings` reference can be rebuilt
through **Crowd Punch > Enemies > Rebuild Elite Prefab** (ENEMY-009, INFO-004).

All enemy damage continues through `DamageRequest` and the shared health resolver; `DamageApplicationSystem`
handles pre-physics requests and Wizard zones resolve their ticks before post-physics recovery. Punches, launched-body collision damage,
explosions, and launched Dashers can therefore defeat an elite normally. `EnemyLaunchTransition` is gated by `EnemyTier`:
normal targets enter the shared `Launched` lifecycle, while elite targets receive the applicable existing elite-tier
impulse without changing launch phase. This preserves normal deferred-defeat and future boss-tier behavior.
Player punches additionally carry `PlayerPunchSettings.EliteKnockbackMultiplier` through the existing Mono-to-ECS punch
bridge. It scales normal- and dash-punch strength only for `EnemyTier.Elite`; punch damage and all non-punch impulse paths
remain unchanged. The default multiplier is `1`, preserving existing tuning until explicitly authored.

`EnemyHealthBarPolicy` is the per-entity presentation rule. Normal enemies retain temporary damage bars and existing
state labels. Elites use `AlwaysWhileAlive`; `EnemyHealthBarBridgeSystem` explicitly bypasses the canvas's global normal
health-bar option for that policy, publishes no normal launch-state label, and withdraws the pooled view on defeat,
respawn ineligibility, or loss of presentation eligibility.

`EnemyWaveSettings` retains `totalEnemyCount` and its weighted normal list and adds an ordered fixed-count elite list.
The baker rejects Elite profiles from the weighted pool, rejects non-Elite fixed entries, declares dependencies on the
wave, profile, and prefab assets, and flattens valid elite entries into `EnemyWaveEliteProfile`. Each wave definition stores
separate normal and elite profile ranges plus total configured elite count; runtime buffers contain no Unity object
references.

`EnemyWaveSpawnSystem` consumes the ordered elite counts before making any weighted normal selection. Failed elite safe
placement remains at the head of the queue. Elites and normals share rectangles, deterministic RNG, player clearance,
physics overlap checks, bounded retries, batch capacity, batch interval, and warning throttling; a batch may finish the
elite queue and use remaining capacity for normals.

Elites and normals both count toward the wave's initial spawn budget. Once every configured elite and normal has spawned,
the wave begins its authored activation policy: `AllEnemiesDefeated` waits for every owned enemy from the wave, while
`DurationElapsed` starts its duration immediately. Normals in an elite wave use the existing pool to replenish while at
least one same-wave elite remains non-defeated. A returning normal clears its observed defeat and restores the sequence's
undefeated/current-wave counters; once the last elite is defeated, pooled normals remain pooled and the surviving crowd
can be cleared normally (ENEMY-011). Elites never replenish.

`EnemyWaveOwnership` records sequence, wave, generation, and idempotent defeat observation. Restart
increments the generation, destroys each old wave-owned root individually so its complete prefab `LinkedEntityGroup` is
removed safely, clears spawn/defeat counts and elite cursors, restores the
initial seed, and re-enters initialization so the first-wave delay is applied again. Legacy random and authored enemies
retain their existing in-place restart and pooling behavior.

## Dasher Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Dasher`. The existing enemy prefab is instantiated as a variant with
`DasherSettings`, `DasherState`, and a per-action `DasherHitHistory` buffer. Its explicit phases are `Positioning`,
`Preparing`, `Dashing`, and `Recovering`; the shared `EnemyLaunchState` remains authoritative for punches, launched
collision chains, recovery, defeat, and pooling. A punch or defeat therefore interrupts every Dasher phase, and pooling
resets both state and hit history.

The decision system maintains an authored distance band and evaluates the configured corridor policy only when entering
preparation. Preparation faces the live player and either stops immediately or brakes using normal movement. Direction is
resampled and locked when the telegraph expires. Dash movement writes the locked horizontal velocity until maximum travel.
After a static-obstacle collision, `DasherObstacleRedirectSystem` adopts the solver's horizontal result as the new locked
direction and restores authored dash speed; a stopped head-on result reflects the incoming direction so the body leaves
the contact instead of remaining embedded. Maximum distance accumulates the redirected path length rather than measuring
straight-line displacement from the start. Player hits are limited by a per-dash flag; enemy hits use a source,
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
- Player movement and dash remain transform-driven MonoBehaviour movement. `PlayerController` submits each intended horizontal displacement through `PlayerEcsBridge`; `PlayerObstacleCollisionSystem` first resolves any shallow overlap, then sphere-casts the configured player radius against baked non-enemy geometry, resolves one wall-slide pass, and returns the corrected position to the controller. Zero-distance contacts permit motion away from their surface so corners cannot trap the player. This keeps SubScene obstacle collision inside the ECS physics world without allowing the MonoBehaviour to query or retain entities.
- Dash punches start immediately through the ordinary punch pipeline, use the same tuning as standing punches, and do not alter committed dash movement (PLAYER-005). Misses do not start cooldown (PLAYER-009).
- A launched enemy, including a zero-health enemy with deferred defeat, can propagate launched state to and independently damage an active or recovering enemy when Unity Physics reports solver-estimated contact impulse above the respective authored thresholds. Unity Physics supplies the transferred velocity; gameplay may rotate newly propagated horizontal velocity toward the smallest-angle eligible target inside the configured correction radius without changing its magnitude or vertical component. One source launch damages each target at most once but may damage multiple targets. Defeated enemies and launched-versus-launched pairs are ineligible. The final effect grammar remains unresolved.
- Enemy chasing and contact damage exist as prototype behavior.
- A player health bar exists. Living damaged normals may display temporary health per INFO-001; body presentation communicates normal launch/recovery state. Elites retain persistent health bars.
- Ten compact gauntlets are authored through the additive lifecycle, replacing the original four encounters. Exact normal compositions, finite timed overlap, and cumulative clear gates bound active populations to 30. Gauntlets 7 and 10 contain one replenishing elite each. The complete boss-ended MVP run remains unfinished.

Do not preserve these details merely because they exist. Preserve the ownership boundary and system timing while evolving behavior toward accepted GDD rules.

## Packages Relevant To Runtime Architecture

- Unity Entities feature set (`com.unity.feature.ecs` 1.0.0)
- Input System 1.18.0
- Universal Render Pipeline 17.3.0
- Unity 6 built-in physics modules plus Unity Physics supplied through the ECS feature set

Package versions in `Packages/manifest.json` and `packages-lock.json` remain the source of truth.

## Player Punch Cooldown Confirmation

Player punch containment uses a horizontal rectangle defined by the authored range and radius: forward distance from
zero to range, and lateral distance up to radius. Height is checked independently against radius, so shorter enemies
do not lose horizontal reach. Detection, aim-assist membership, and trajectory preview all use `PunchResolution.Contains`
with the player-punch cause (PLAYER-003). Elite punches retain their circular cross-section.

`PunchDetectionSystem` records resolution and whether any `PunchResolution.TryApply` call succeeded on the existing
`PunchRequest`, then disables the request. `PresentationBridgeSystem` reads that result through the non-enableable
`PlayerSnapshot` singleton and sends the request sequence and hit flag through `PlayerEcsBridge.PunchResolved`.
This result is delivered even when there are no enemies or trajectory preview is disabled. `PlayerPunch` waits for its
matching result before accepting another request and starts its cooldown only on a hit (PLAYER-009). A miss leaves the
punch ready; a multi-target hit starts one cooldown. Resetting or disabling the punch clears its pending state, so stale
results cannot apply cooldown to a later request. Enemy detection remains ECS-owned and Burst-compatible.

## Deliberate Shot Experiment (2026-09-06)

See [design diagnosis, alternatives, tuning and playtest protocol](../Design/DeliberateShotsIteration.md).
The GDD remains the accepted baseline; this document records the implemented experiment.

- `EnemyContactDamageSettings.AttemptWindUpDuration` is authored in the existing spawn profile. Baseline
  `EnemyContactAttemptState` now also owns `IsWindingUp` and `CommittedDirection`. `EnemyContactCommitment`
  handles prepare/commit/cancel/cadence without physics or presentation writes. `EnemyChaseSystem` emits
  zero intent during preparation and direction-committed intent during the finite attempt. Committed
  attempts retain a pressure slot until they finish; otherwise the closest eligible enemies receive it.
  Launch/recovery interrupts the attempt; pooled reset uses the existing whole-state reset.
  Elite staging and corridor-clearance overrides also cancel contact commitment so body warnings match actual intent.
  Setting wind-up to zero selects the previous immediate pursuit behavior. Explosive, ranged, Dasher
  and elite overrides retain their ownership and timing.
- `EnemyVisualBaker : Baker<MeshRenderer>` discovers an `EnemyAuthoring` ancestor through dependency-aware
  baker access. It adds `EnemyVisualOwner` and a URP color override to its **own** renderer entity. A parent
  baker must not add components to a child's primary entity. Owner references remap during prefab
  instantiation, so both ordinary root meshes and the elite child mesh follow their correct gameplay entity.
- `EnemyReadabilitySystem` is a Burst-compatible parallel presentation job over those renderers, with
  read-only gameplay lookups. It writes body color only. Existing Dasher presentation still owns its
  specialized color and shape. Ranged/explosive silhouettes are set once in spawn initialization using
  `PostTransformMatrix`; physics collider geometry is unchanged. No per-enemy GameObject is added.
- The health bridge now publishes normal bars only for living recently damaged enemies. It does not
  publish normal launch/recovery labels. Elite health policy remains always-visible while alive.
- `PunchResolution` now clears linear/angular velocity before all eligible **normal player launches**,
  including Active and Recovering bodies, so incoming momentum cannot skew the previewed direction
  (PLAYER-004, COMBAT-014). This intentionally revises the prior Active first-punch momentum rule;
  elite additive knockback and enemy-originated punch semantics remain unchanged.
  The punch pipeline, cooldown acknowledgement, bridge data and physics phase ordering are unchanged.
  Arrowheads are local preview presentation only. Authored assist is 18 m / 12 degrees;
  propagation correction and homing are disabled by their existing zero settings. The collision system
  avoids allocating a full correction-candidate snapshot when correction is disabled.
- The shared authored impulse damage curve now caps at three times originating damage, including
  player impacts. This improves durable-target payoff without a separate scoring or reward system.
  Gauntlet 4 supplies 48 mixed normals plus one replenishing elite. Gauntlet 3's 200-enemy wave is unchanged.

## Verification Constraints

The ten-gauntlet content uses 39 inspector-editable wave assets under
`Data/Settings/Waves/Progression`, unchanged enemy profiles, and one existing wave sequence per
SubScene. No spawning schema was added. Every recovery break and final wave uses cumulative
clear; timed activation only assembles finite cohorts inside a stage. Elite cohorts drain
before the next stage. See [the progression and validation record](../Design/TenGauntletProgression.md).

`GauntletProgressionBuilder` is an explicit Editor authoring recipe, not a runtime loader.
Rebuilding replaces generated content, so ordinary tuning should edit the saved wave assets
and scenes. Floor MeshColliders use convex baking, producing one solid hull rather than
triangle-mesh collision edges that can deflect grounded enemies inside the court
(VISION-004, COMBAT-018). Floor outlines and heights are unchanged. Low perimeter rails bake into Unity Physics; rail collision
faces extend above the visible mesh to intersect the elevated hybrid player sphere cast.
Movement bounds are inset from the perimeter and defeat bounds permit limited launch travel
outside the court before terminal defeat. There are no internal navigation obstacles. A
non-colliding presentation backdrop below each floor occludes pooled bodies that would
otherwise be visible beneath compact courts from the orbit camera.

Four lifecycle corrections support the sequence without changing enemy profiles or attacks:
`GameRestartSystem` destroys independent fired `RangedProjectile` roots and linked children on
restart; `EliteWaveReplenishmentSystem` also checks the ownership record's terminal
`DefeatCounted` flag, because pooling restores an elite's launch phase to Active for generic
pool bookkeeping. A pooled defeated elite must not re-enable its normal cohort (ENEMY-011).
`EnemyWaveDefeatCountSystem` explicitly runs after `OutOfBoundsSystem` and also counts
enabled pooling requests, including normals that leave defeat bounds while replenishment is
enabled and therefore bypass terminal launch state. The normal respawn path already reverses
that ownership count on return. An out-of-bounds pooled normal now permits completion if its
elite dies before it returns, instead of leaving a permanently positive undefeated count.

`GamePrePhysicsGroup` observes `GameRestartRegistry.Sequence` and a rebuild request from
`GameRestartSystem` after actual cleanup. Either skips one gameplay fixed-step update so the
following `PhysicsSystemGroup` rebuilds its collision world before pre-physics queries resume.
Those queries otherwise read the previous step's world, which may reference collider blobs
released by additive teardown or pooled-Dasher cleanup. This is a restart-only warmup;
ordinary system order and combat/physics tuning are unchanged. The cleanup notification is
needed because the new SubScene's `MatchState` can become available after the Mono request.

`GauntletProgressionTests` checks authored references, geometry, bounded compositions, and
isolated ECS lifecycle regressions. `GauntletSequenceSmokeCheck` is an explicit Editor-only
probe that keeps an idle player alive and injects `DamageRequest` to clear stages. Its logs
and gameplay-camera captures go to `Temp/GauntletValidation`; it does not measure player
skill, intended clear times, or balance.

- Unity compilation, baking, scene wiring, and play-mode behavior are the final verification sources.
- A generated `Assembly-CSharp.csproj` may be stale until Unity refreshes assets.
- Physics changes require checking representative crowd density and profiling, not only single-enemy correctness.
- Changes to bridge fields, component ownership, system order, scenes, or package boundaries must update this document.

## Player Hit Reaction

GameBootstrap adds PlayerHitAnimation to the player root instead of renderer blinking.
It listens to PlayerHealth.DamageAccepted and triggers the masked Hit Reaction Animator
layer above the punch layer. Hit To Body plays once in place, with a short entry/exit blend.
The component releases the layer weight to zero after playback and on disable, so locomotion
and the current punch pose resume. Punch spine yaw fades out with reaction weight and returns
afterward. Rejected invulnerable hits do not restart the clip. Damage, invincibility duration,
movement, and punch eligibility remain unchanged (PLAYER-002, PLAYER-005, PLAYER-009).
An accepted PlayerPunch.PunchStarted immediately cancels the hit reaction, clears its pending trigger, and sets its layer weight to zero so the punch pose and yaw regain priority. Cooldown-rejected punch input does not interrupt the reaction (PLAYER-005, PLAYER-009).


## Combat Feedback Pass (2026-09-12)

This presentation pass reinforces VISION-004/005, COMBAT-001/003/012 and INFO-004.
It changes no attack eligibility, damage, launch impulse, targeting, dash movement, enemy
archetype, or wave configuration. OQ-001 (target hardware/performance), OQ-015 and OQ-016
remain open; the restrained defaults are tunable presentation choices, not new gameplay rules.
No audio, HUD, decals, bloom or third-party dependencies were added.

### Event ownership and ordering

- `EnemyBaker` adds `EnemyImpactFeedback`, a fixed-size strongest-pending mailbox per enemy.
  `PunchResolution` records only successful applications. `EnemyLaunchCollisionSystem`
  records eligible launched-body contacts and launched-body/world contacts using the existing
  collision stream. `DasherEnemyImpactSystem` records its existing deduplicated swept hits.
  These producers write data only; they never reference UnityEngine presentation objects.
- `ImpactVelocityCaptureSystem` runs last in GamePrePhysicsGroup and records pre-solver
  velocity. Ordinary collision strength uses relative speed along the contact normal and
  solver-estimated impulse. When momentum is received and transferred within the same physics
  step, impulse times source inverse mass supplies a transferred delta-velocity estimate.
  World impacts use incoming normal speed. Dasher hits use preserved launch velocity.
- `EnemyLaunchState.FeedbackChainDepth` starts at one for player launches, increments from
  the source on ordinary and Dasher propagation, and is capped at 64. A re-punch begins a
  new depth-one chain. This is presentation metadata, independent of damage and existing
  PropagatedLaunchCount. Only player-owned depth escalates presentation.
- `CombatFeedbackBridgeSystem` runs in GamePresentationGroup after the existing punch result
  bridge, before the final visual composition. It consumes pending mailboxes, filters weak
  contacts, starts short visual envelopes, and publishes bounded `CombatFeedbackMessage`
  values through `PlayerEcsBridge`. It also sends bounded launched-trail samples. Mono receives
  opaque visual keys and launch sequences, never enemy Entities or an EntityManager.
- `EnemyImpactVisualSystem` runs after role colors, Dasher shape and enemy animation. It
  composes flash with their current color and applies direction-based deformation to renderer
  LocalToWorld only. Each renderer caches its current undeformed matrix and last output;
  every deformation is rebuilt from that baseline. LocalTransform, authored scale,
  PostTransformMatrix, physics collider geometry and sampled skin matrices remain untouched.
  Both MeshRenderer baking and additional skinned render entities receive this state.
  The old hardcoded health-bar-timer flash was removed so there is one enemy impact flash.

### Scene presentation and configuration

Bootstrap's GameBootstrap references
`Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset` and automatically installs
`FeedbackTimeController` and `CombatFeedback` on the bootstrap object. The latter uses
existing PunchResolved, DamageAccepted, DashStarted/Ended and PunchStateReset events.
It prewarms `ImpactParticlePool` (eight instances per effect by default) and
`LaunchedTrailPool` (48 short TrailRenderers by default), attaches `CombatCameraFeedback`
to the main camera, and prepares `PlayerDamageFlash`. No manual scene plumbing is required.
Missing particle prefab references deliberately select built-in placeholders using the
included Resources shader. Optional custom prefabs should be finite particle bursts.

All effect tuning except punch-pose timing lives on CombatFeedbackSettings:

| Inspector group | Controls |
| --- | --- |
| Time | Punch freeze (45 ms), exceptional depth (4), enabled toggle, scale (0.22), duration (95 ms), 1.5 s retrigger interval |
| Impact significance | Speed range (2-16 m/s), impulse minimum (1.5), environment minimum normal speed (4 m/s), contact interval (120 ms), single-hit ceiling and capped chain gain |
| Camera | Punch/damage/collision/exceptional directional kick, shake, decay, maximum displacement, significance threshold, collision interval and distance falloff |
| Particles | Seven optional effect prefabs, pool capacity, per-frame impact budget (8), scale range, placeholder count, dash emission interval |
| Launched trails | Capacity, minimum speed, 140 ms length in time, width and color |
| Visual-only enemy deformation / flash | Squash amount/duration/threshold, flash duration and distinct enemy/player strength/color |
| Dash camera | FOV delta (5.5 degrees) and transition time (75 ms) |

Base speed intensity is normalized over the configured speed range. A single-impact ceiling
(default 0.7) leaves room for chains: multiply by `min(maximumChainMultiplier,
1 + max(0, depth - 1) * chainGainPerDepth)` and clamp the result to [0,1]. Strong standalone,
small-chain and exceptional events therefore remain distinguishable even at high speeds.
The impulse minimum independently rejects insignificant solver contacts. Feedback cooldowns
apply only to accepted presentation impacts; weak floor contacts do not suppress later hits.

Ordinary collisions use solver average contact positions. Punches use the struck enemy's
position because the existing volume attack has no fist contact manifold; Dasher swept
contacts use the midpoint of the overlapping bodies. Player damage estimates contact from
player radius and accepted push direction, falling back to facing when no direction exists.
These approximations do not change hit geometry or manufacture gameplay collision data.

### Restoration and readability

Punch freeze is driven once by the existing hit-confirmed PunchResolved event (PLAYER-009),
not by input or per-target particles. `PlayerPunchAnimation.ConfirmContactPose` evaluates
its existing time-parameter Animator layer at the configurable contact frame before freezing.
The existing animation component exposes strike speed, follow-through speed, contact frame
and a confirmation-pose toggle. Fist/arm bone scaling was deliberately omitted because the
humanoid rig is retargeted; the existing animation/upper-body yaw system remains authoritative.

FeedbackTimeController centrally composes hit-stop, exceptional slow motion, menu pause and
additive transition gates. Hit-stop wins while active; an unexpired slow-motion window then
continues, followed by the captured baseline. Deadlines use unscaled time. Pause and transition
cancel temporary effects and never capture a transient zero scale as their resume value.
The fixed timestep and Unity Physics integration are unchanged; gameplay timers and movement
remain on scaled time, while feedback restoration continues on real time. Reset, disable,
death and restart cancel temporary state. PauseMenu and GauntletSequence retain separate gates.

CameraFollow removes the previous translation before its normal follow solve and composes
new feedback afterward. Rotation is never shaken, preserving camera-forward punch aiming.
FOV is baseline plus one smoothed offset, with dash end/interruption and reset restoring it.
Collision camera responses are distance attenuated and rate limited. Exceptional events require
player ownership, configured depth, sufficient strength and the global retrigger interval.

Enemy flashes reuse ECS material properties. Sidekick's player shader lacks a general body
tint: PlayerDamageFlash briefly appends one shared transparent overlay to the existing renderers
and restores their preallocated original material arrays. It does not instantiate per-body
materials. Trails and fallback particles use one shared vertex-color material. Authored particle
prefabs share a separate URP unlit alpha-blended material and soft-disc texture. Trail samples are limited
to launched bodies above minimum speed; unobserved, pooled, reused or new-launch slots clear
before release/reassignment. Particle exhaustion drops cosmetic work. The bridge allocates no
per-frame enemy arrays. Restart and pooling explicitly reset mailboxes as well as launch state.

The existing ExplosionFeedback sphere presentation now prewarms a bounded reusable pool
(default 16, configurable on that component) and clears on reset/disable/transition. Its sphere
appearance and authored explosion duration/radius are unchanged. Its shader is also reused
for the player overlay. This removes the previous instantiate/destroy cycle per explosion.


### Verification of this pass

- Runtime and Editor assemblies compile with zero errors. Existing package assembly-conflict,
  obsolete ColliderAspect and unused legacy-aspect warnings remain.
- Six EditMode regressions passed in Unity: time overlap/expiry; menu and transition ownership;
  re-punch ownership/depth reset; bounded mailbox priority/contact throttling; repeated renderer
  deformation preserving physics transforms and exact authored-scale restoration; trail release
  and capacity reuse. Tests are in `Tests/Editor/CombatFeedbackTests.cs`.
- A real six-body arranged line produced one confirmed punch, 14 impact messages, depth three,
  37 peak particles and six active trails. The successful punch reached timeScale zero and
  restored to one. An empty-volume smoke request produced no freeze or effects.
- A temporary 200-body crowd in Gauntlet 3 (created only in Play mode, without changing wave
  assets) produced a real chain reaching depth 11: 154 impact messages over four seconds,
  77 peak particles and 46 active trails. Exceptional slow motion was observed and scale
  restored to one. Particle/trail object count remained 104, the prewarmed budget. The later
  pooled explosion implementation adds a separate fixed budget of 16 existing-style spheres.
- An Editor ProfilerRecorder sample at that crowd size measured CombatFeedbackBridgeSystem
  at 0.270 ms mean and 0.333 ms maximum across 180 samples. This measures the bridge in this
  local Editor run, not total rendering/physics cost, standalone-build performance or a
  supported hardware target. OQ-001 remains unresolved. A gameplay-camera capture was inspected
  at `Temp/CombatFeedback-chain.png` for crowd visibility; it is not a final-art approval.
- Real Input System dash presses, including an early disable/interruption followed by a second
  dash, produced two starts/two ends. FOV peaked at 62.837 degrees from a 60-degree baseline and
  restored to 60.00006 degrees. Player hit overlays appeared on the model and all 31 inspected
  renderer material arrays restored. Background crowd attacks continued during that test.
- Editor script-reload testing exposed managed-pool loss and camera destruction-order hazards.
  Pools now reconstruct when Unity retains scene references across reload, and teardown checks
  Unity object validity before touching the camera. These are presentation lifecycle fixes.

No manual Unity Editor setup is required for Bootstrap or the shipped gauntlets. The seven
particle slots now reference authored prefabs in `Assets/CrowdPunch/Prefabs/Feedback`.
Punch phase controls live on the existing PlayerPunchAnimation component on the player model;
they preserve PLAYER-005/009 gameplay timing and do not require rig changes.

- Final lifecycle verification after the fixes: a script reload retained exactly 56 particle
  systems and 48 trails with settings rebound and timeScale one. Repeated explosion requests
  used the same 16 spheres and all expired. Reset left zero trail vertices/emitting slots,
  timeScale one and FOV exactly 60 degrees. Play mode was stopped and the temporary stress
  crowd was discarded. The pre-existing Synty downloader/disabled Animator Editor warnings
  are unrelated to feedback gameplay.


### Authored combat particles

`CombatFeedbackSettings.asset` assigns PunchImpact, EnemyImpact, PlayerDamage,
EnvironmentImpact, DashStart, DashMovement and DashEnd. Each prefab has a directional
fleck burst and a soft-wisp child, sharing `Materials/Feedback/CombatParticles.mat` and
`ParticleSoftDisc.asset`. Warm punch flecks, pale enemy impacts, coral damage and tan dust
distinguish event types; pale cyan dash effects stay short. This supports VISION-004/005
and INFO-004 without changing damage, launch, dash or chain rules.

The existing ImpactParticlePool instantiates eight copies per effect (56 roots, 112 particle
systems). Each activation emits 5-11 particles, with a maximum authored lifetime of 0.32
seconds. No looping emission, collision modules, lights, shadows or per-instance materials
are used. World-space simulation preserves emitted particles during movement, and hierarchy
scaling retains the existing impact-intensity control. All systems use AlwaysSimulate so
offscreen effects expire and release their pool slots.

Tune burst count, lifetime, color, shape and size on each prefab's root and SoftWisps child.
Global scale, pool size and impact limits remain on CombatFeedbackSettings. No scene edits
or additional runtime components are required.

Verification: all seven authored burst counts, particle expiry, replay and recursive clear
passed in Unity. A rendered preview was inspected at `Temp/CombatParticlePrefabs.png`.
Live bridge requests activated seven pool slots, all expired within one second, then all
seven replayed with the same 112 systems. Reset left zero live systems. The existing Synty
Sidekick downloader Editor-window error remains unrelated to these assets.


### Cartoon feedback tuning (2026-09-14)

The user's requested exaggeration strengthens silhouette changes and directional bursts
while preserving VISION-004/005, INFO-004 and PLAYER-005/009. This is presentation tuning,
not a resolution of the broader art/camera questions OQ-015/016.

- Punch freeze is 45 ms; exceptional chains use 95 ms at scale 0.22, still gated at depth
  four and by the existing 1.5-second retrigger interval.
- Punch/damage/collision/exceptional kick strengths are 0.17/0.28/0.085/0.22 metres.
  Displacement is capped at 0.34 metres, with 100 ms decay and no camera rotation.
  Collision impulses remain strength/distance gated, now spaced at least 220 ms apart.
- Squash rises from 0.13 to 0.27 with 160 ms recovery; hit flashes last 65 ms and have
  maximum strength 0.9. Existing velocity/intensity multipliers still temper ordinary hits.
- Launched trails are 0.24 metres wide before velocity scaling, with alpha 0.58 and only
  140 ms history. The minimum speed rises to 4 m/s to exclude sluggish bodies.
- All seven particle prefabs have larger, more saturated flecks, slightly longer finite
  bursts and restrained low-alpha wisps. Global scale is 0.7-1.45. Particle count remains
  5-11 per event; the bridge emits at most eight effects per frame instead of twelve.
  Dash movement emits at 55 ms intervals; pool capacity and shared materials are unchanged.
- Chain gain rises to 0.18 per depth, capped at 1.8, retaining the single-impact ceiling
  of 0.7. Strong singles, short chains and exceptional chains remain distinct.
- PlayerModel's existing punch layer uses strike speed 1.85, follow-through speed 0.85
  and torso yaw from +50 to -42 degrees. Contact frame, gameplay timing, root facing
  and all rig scales are unchanged. Dash FOV is baseline plus 5.5 degrees with 75 ms smoothing.

Configuration lives in CombatFeedbackSettings.asset, the seven Feedback prefabs, and
PlayerModel's PlayerPunchAnimation. Script defaults match the new central tuning.
Build passed with zero errors and existing package/legacy warnings. All six existing
feedback regressions passed in Unity. Particle preview and a gameplay-camera saturation
capture were inspected. Sending 140 presentation requests filled the fixed 56 root slots
without increasing the 112 particle systems; all expired and reset cleared every system.
Overlapping configured hit-stop/slow motion restored timeScale to one. This saturation
check verifies presentation capacity, not a new whole-crowd performance benchmark.
Real Input System dash verification produced two starts/two ends, including an early disable/interruption. FOV peaked at 65.37495 degrees and restored exactly to the 60-degree baseline. Play mode was stopped without saving temporary scene state.

## Static Obstacles And Navigation

The dedicated validation arena adds baked rectangular solids, class-based clearance and reachable regions, explicit tactical intent, a shared budgeted A* scheduler, and optional editor diagnostics. The original ten-level progression is unchanged. See [Navigation ownership, settings, and system order](Navigation.md) and [performed validation and profiling](../Validation/NavigationValidation.md). Projectile cover, explosion occlusion, and terrain-aware homing are not introduced: existing projectiles pass static solids, explosions remain radial, and launched-body homing may select an obstructed target while physical wall collision remains active.

Navigation participation anchors now default to the clearance-valid spacing-bounds centre (or nearest valid cell), with an optional explicit override for disconnected layouts. See [anchor selection](Navigation.md#spawning-and-lifecycle). This is baked configuration, not a per-frame search.


## Armored And Gauntlet_12 (ENEMY-014/015)

Armored appends serialized archetype value 5 and stays Normal tier. EnemySpawnSettings owns
three armor timing/recoil values; shared profile baking and spawn
initialization add EnemyArmorSettings, EnemyArmor and ArmorHitHistory only to this archetype.
Recovery leaves armor alone. Pooling and restart reset it; wave reload destroys old owned roots.
Armored never publishes an individual health bar. The existing health-bar presentation bridge
publishes its remaining armor count to the pooled screen-space Canvas instead. The Canvas displays
one shield icon per stage, hides the indicator on break, defeat or pooling, and reuses views across spawns.
GameBootstrap keeps Canvas ownership; MonoBehaviour UI does not query enemy entities.

ArmorHitResolution is shared by solver body impacts, swept launched-Dasher impacts and explosions.
Per-target history stores source entity and launch sequence, sharing identity between an explosive
body and its blast. A protected contact is consumed so sustained contact cannot become a late hit.
History cleanup follows current source launches, retaining exploded sources until pooling/reset.
It does not accumulate across source lifetimes. Two accepted shield hits write a modest planar recoil and a
stagger deadline; chase and movement both honor the deadline. The second hit removes the last shield
without launching or damaging health. Subsequent eligible hits use the existing source-specific launch
and damage pipeline. DamageApplicationSystem blocks health damage through the post-hit protection window.
Player punch detection reports connection separately from PunchResolution's gameplay result.
Preview filters protected sources; assist/homing candidate eligibility deliberately retains them.
Elite selection, stale/area punch resolution and direct boss scattering reject protected targets.

EnemyChaseSystem uses its existing local-separation neighborhood and writes direct player pursuit
for Armored without allocating pressure slots. Navigation remains the path owner. Armored uses
MoveSpeed * ChargeSpeedMultiplier; stagger never lets normal movement overwrite recoil velocity.
EnemyReadabilitySystem uses one stable Armored tint at every stage; EnemyImpactVisualSystem still
composes transient flashes over it. The shield indicator alone communicates the stage count.
EnemyAnimationProfile.Armored reuses CPA3 sampling: Idle/Run, HitReact on armor-hit sequence changes,
a sideways Idle flight pose, Jump_Land recovery and Death defeat. Physics never depends on the poses.
EnemyArmoredPrefabBuilder regenerates the Orc_Skull prefab, controller, materials and samples.

Wave settings optionally reference a Baseline armoredAmmunitionProfile. Its baked profile is used
only after the wave's original pending spawn queue drains. ArmoredAmmunitionSupply checks owned
current-generation bodies at most four times per second and uses the wave's existing ranges,
physics clearance, player distance and navigation checks. One supplemental root replaces the previous
counted supplemental root, keeping live allocation bounded. Each addition increments cumulative
undefeated count and a per-wave supplemental budget; ordinary defeat counting decrements/increments
the established counters. Wave completion includes that budget. Restart resets it. No MonoBehaviour
queries enemies and no alternate runtime spawning framework is introduced.

GauntletProgressionBuilder.BuildArmored authors only Gauntlet_12 and its two waves (1+6, then 3+12
Armored+Baseline), navigation, opening hint and Bootstrap/build-list registration. Nature-kit rendering
uses the existing recipe. Rebuilding the original ten or boss preserves later sequence entries.
Default armor tuning: 0.30 s stagger, 3 m/s planar recoil, 0.25 s hit protection.
The stable body tint and shield icons are presentation constants.

Scene, physics, regression and crowd-cost evidence is recorded in [Armored validation](../Validation/Armored.md).

## Barricade And Gauntlet_13 (BARRICADE-001..005)

Gauntlet_13, "Break Through", follows the unchanged Gauntlet_12. The main scene supplies the
entry and nonblocking opening hint. Its SubScene contains a 28 x 36 m court, one solid
28 x 4 x 1.2 m barricade at z=11, and a green exit at z=15. It reuses the nature environment,
existing Baseline/Explosive prefabs, wave placement and pooling. The Bootstrap selector and
build list now continue to Gauntlet_14. The authored sequence is not a decision about the
final game's total length.

`BarricadeAuthoring` and `BarricadeBaker` bake shared hit-count state, collider references,
exit geometry and tuning from `Data/Settings/BarricadeSettings.asset`. The collider is a
dedicated static Unity Physics box; changing durability never changes an enemy's archetype.
The baker owns immutable intact and zero-filter collider blobs, swapping the component's
reference on destruction instead of mutating a shared collider. No enemy health component
or health-bar presentation is attached to the barricade.

`BarricadeImpactSystem` runs last in pre-physics, after launch homing and ground constraint.
Swept AABBs cheaply reject distant bodies; remaining launches cast their actual collider
through the upcoming fixed-step displacement in the existing collision world. The closest
blocking lateral hit is authoritative, so a nearer enemy or solid is not shot through.
`BarricadeHitResolution` counts source entity + launch sequence once. Owner masks accept
Player/Enemy/Boss/unowned launches by default. Independent radial explosions also use this
resolution and the closest point on the box; an explosive impact requests the existing
explosion pipeline, sharing its identity with the blast. History expires on pooling,
destruction or a changed source launch sequence, and restart clears it.

Destroying hits swap the collider before the physics world rebuild and preserve incoming
velocity. Intact hits queue a `BarricadeRebound` buffer entry; `BarricadeReboundSystem` runs
after physics and before ordinary launch propagation and explosions. It reflects horizontal
incoming velocity at the configured multiplier, clears the homing lock, and corrects only
normal penetration if a fast discrete step tunneled through the contact plane. It does not
perform normal enemy movement. A wall broken by another body in that step cancels pending
rebounds, letting the bodies continue through. Ordinary collisions, damage, ownership and
recovery remain under their existing systems.

The existing punch detection tests overlap against the box surface and returns its existing
connection result for cooldown, with no barricade damage. The barricade is an additional
candidate for the existing persistent punch lock, propagated correction and homing. Selection
still uses target centers and the same ranges, angles, ray replacement and tie-breaking rules.
The existing initial-direction preview follows that lock; it does not predict rebound.

An optional `BarricadeCrowdSequence` link on `EnemyWaveSequenceAuthoring` requires one wave
and excludes a boss owner. Initial allocation remains `EnemyWaveSpawnSystem`; its finite
allocation never advances by kills for this objective. Spawned roots carry
`BarricadeCrowdMember`. Replenishment enables the existing `EnemyRespawnSystem` while the wall
is intact, reusing the wave's safe placement routine (currently named `BossCrowdPlacement`),
authored ranges and ownership counters. The saved wave contains 14 Baselines and 2 Explosives;
composition and the 16-root bound are editable in `CP13_01_Barricade_Crowd.asset`. Pool delay is
2 seconds after the ordinary defeat/pooling animation. Destruction stops the pending initial
queue and pending respawns; it never changes a surviving enemy's attack or health state.

`GauntletCompletionSystem` reports this objective only when durability is zero and the
available player's snapshot enters the 2 m exit radius. It ignores surviving crowd count and
uses the existing additive transition/completion registry. `GameRestartSystem` restores
durability, collider and buffers; level reload removes the wave-owned crowd as before.

`BarricadeVisualAuthoring`/baking and `BarricadePresentationSystem` own presentation-only
entities: solid plate/ribs, one then two crack rows, configurable flash and shrinking/scattering
debris. They never change the collision geometry. Impacts use the existing player presentation
bridge and particle pool, without a new HUD. Defaults are 0.85 rebound, 0.18 s flash and 0.65 s
debris. `GauntletProgressionBuilder.BuildBarricade` authors only level 13 and its assets;
rebuilding resets that level's recipe while preserving existing barricade tuning.

See [Barricade validation](../Validation/Barricade.md) for actual checks and remaining playtests.

## Rotating Cover And Gauntlet_14 (COVER-001..005)

Gauntlet_14, "Return to Sender", is saved after Gauntlet_13 in Bootstrap and Build Settings.
Its 34 x 34 m court surrounds a central barricade target and a 5 m radius rotating cover.
`RotatingTargetSettings.asset` supplies four required hits (covered targets clamp to at least two),
the existing ownership mask, target rebound and damage feedback. `RotatingCoverSettings.asset`
supplies a 75 degree opening, 35 degrees/second default continuous rotation, rotate/pause and
reversal modes, optional pause/acceleration on accepted hits, and a 1.0 reflection multiplier.
Settings are baked: edit assets, let Unity rebake, then reload the level. Wave composition and
placement remain in `CP14_01_Rotating_Cover_Crowd.asset` (14 Baselines, 2 Explosives).

`RotatingCoverBaker` builds one immutable compound arc collider from 36 overlapping boxes.
The ECS root rotates before the existing `BarricadeImpactSystem`. `CoverGeometry` supplies
the identical panel geometry to baking and presentation; panels explicitly request nonuniform
scale transform data. A separate stationary cylinder is query-visible to the existing player
collision bridge. Its manually owned transforms survive baking. `CoverEnclosureContactSystem`
runs between Unity Physics contact creation and Jacobian creation and disables cylinder
contacts only for `Launched` bodies. Walking/recovering bodies and the player remain blocked;
ordinary enemy-enemy contacts and the shield/target colliders are unaffected. Navigation bakes
a conservative square footprint around the enclosure through the existing grid baker, so walking
routes and safe wave placement do not enter it. The low plinth marks the walking exclusion.

`BarricadeImpactSystem` includes cover bounds in its existing broadphase rejection and queries
the current cover pose directly, since the collision world available before physics contains
the previous pose. Launched sweeps ignore the stationary enclosure; nearer enemies and ordinary
solids still block shots. Shield hits queue `CoverReflection` instead of target damage or explosion.
`CoverReflectionSystem` runs after physics, before launch propagation/explosions. It corrects
normal penetration for tunneled impacts, redirects incoming planar speed toward the player
snapshot sampled for that impact step, applies the multiplier and clears homing. It preserves
launch sequence, ownership, damage history and recovery timers. No launch-distance component
was added: the user explicitly selected preserving the existing damping/momentum model.

The original target hit resolver, history, crack/flash/debris presentation and explosive request
pipeline are reused. For a covered target only, blast damage requires an eligible launched
source inside the enclosure; outside blasts cannot damage it, even with a large radius. Target
impact and explosion share source plus launch sequence and count once. Aim-assist rays ignore
cover/enclosure but otherwise retain existing target selection; fallback selection, homing and
the short direction preview still accept the intact target. No preview predicts shield motion.

`Barricade.CompleteOnDestruction` selects immediate completion for this target; Gauntlet_13
still requires its exit. The existing barricade link to wave allocation/pooling supplies bounded
replenishment using the shared boss/elite-era safe placement infrastructure. Target destruction
stops pending allocation/respawns, and completion uses the existing registry/level flow.
Restart clears pending cover reflections and restores angle, rotation timers and observed-hit
state alongside target durability/history; scene reload removes the encounter and owned crowd.

`GauntletProgressionBuilder.BuildRotatingCover` authors only Gauntlet_14 and registers it without
rewriting earlier level content. Rebuilding preserves existing target/cover tuning assets but
reapplies the wave/layout recipe. The plinth is generated from geometry settings by that recipe.
No new player control, enemy archetype, health bar, progression system or launch-range model exists.
Balance, final art and run-duration tuning remain future playtesting work. See
[Rotating cover validation](../Validation/RotatingCover.md) for measured checks and limitations.

## Shell And Gauntlet_15 (SHELL-001..006)

Gauntlet_15, "Crack the Shell", follows Gauntlet_14 in Bootstrap, selection and Build Settings.
The saved 30 x 30 m court contains one central stationary target, a navigation footprint and
four surrounding spawn regions. Initial allocation is 12 Baselines and 2 Explosives, bounded
to 14 roots by the existing wave/pool infrastructure. Earlier level content is preserved.

`ShellTargetAuthoring`/`ShellTargetBaker` add independent shell/core state to a `BarricadeAuthoring`
solid. `ShellTargetSettings.asset` owns three required explosions, five core health (the current
Baseline profile's maximum at creation), and a two-second exploder replacement delay.
Core health is independently editable; later Baseline tuning does not silently overwrite it.
`ShellSolidSettings.asset` reuses rebound, Baseline pool delay, flash and debris tuning. Its
hit count is only the solid's alive/completed flag; shell hits and core damage never pass through
the ordinary barricade hit-count resolver. Wave population and regions live in
`CP15_01_Shell_Crowd.asset`. Settings changes require rebaking/reloading as elsewhere.

The existing pre-physics swept `BarricadeImpactSystem` branches to `ShellHitResolution` for
shell targets. The same nearest-blocker cast, collision geometry, rebound queue and post-physics
penetration correction are reused. Core body damage uses `EnemyCollisionDamage.Calculate`,
with incoming normal speed divided by inverse mass estimating impact impulse against the static
target. `BarricadeHitHistory` deduplicates core impacts by entity and launch sequence. Shell-only
contacts do not spend core impact eligibility. Exploder contact still requests the ordinary
post-physics explosion pipeline. That pipeline resolves every detonation once; shell targets
receive blast damage independently of body-impact history. A single blast resolves either a shell
hit or core damage, so the breaking blast cannot leak through. A subsequent blast in the same
fixed step is a fresh attack and can damage the exposed core.

`PunchDetectionSystem` reuses the solid overlap/cooldown confirmation, dispatching blocked shell
punches or normal core damage. Neither phase has enemy movement, health bars, or the enemy
launch lifecycle. The static collider remains unchanged across exposure. Core death alone marks
the shared solid destroyed and lets `GauntletCompletionSystem` report immediate completion.
Existing persistent aim selection, propagated correction, homing and short initial-direction
preview accept the same solid in both phases. No bounce prediction was added. Navigation bakes
the target footprint through `NavigationArenaBaker`; Mono player collision reads the normal ECS
collision world through its established bridge.

`BarricadeCrowdReplenishmentSystem` retains Baseline replenishment until core death.
`ShellExploderReplenishmentSystem` runs after explosions and before respawn. Once initial wave
allocation finishes, it starts a delay only when no living exploder remains, then releases both
existing pooled slots. Per-member pending flags prevent an already returned slot being issued
again while its partner waits for safe placement. Defeat animation, pooling and safe placement
remain owned by `EnemyRespawnSystem`; those can extend the visible replacement delay. Exposure
cancels pending replacements without changing survivors. No additional crowd entities are allocated.

`ShellPresentationSystem` animates shell plates and core scars through the existing visual baking
data and a `ShellVisual` marker. Shell cracks progress with explosion damage; core tint/scars
progress with health loss. Shell break scatters/shrinks the armor, revealing turquoise core geometry.
Blocked punches flash blue at low impact intensity; successful hits flash gold with the existing
environment impact particles. There are no new UI bars. The opening hint uses `GauntletLevel`.

`GameRestartSystem` resets shell/core health, replacement timers, flash state and break time alongside
the existing collider/history reset and wave-root cleanup. Scene reload restores authored state.
`GauntletProgressionBuilder.BuildShell` rebuilds only level 15, preserving existing tuning assets
and earlier sequence entries. Balance, final geometry/art/audio and whole-game performance targets
remain future work. See [Shell validation](../Validation/Shell.md) for verification and playtests.

## Knock Into Place And Gauntlet_16 (TRACK-001..005)

Gauntlet_16 follows the shell level in Bootstrap, selection and Build Settings. Its open 32 x 34m
arena contains one orange block on a 10m rail from z=-5 to z=5, side arrows and a blue socket.
Two objective-owned wave sequences reuse the existing bounded pool: 12 Baselines begin after
two seconds and two Explosives join after ten seconds. Both replenish until physical docking.
Previous level content is unchanged. `GauntletProgressionBuilder.BuildTrack` rebuilds only level 16
and its assets; it preserves existing tuning assets and reapplies geometry/wave recipes.

`TrackObjectAuthoring`/`TrackObjectBaker` add track settings, motion state and an infinite-mass
kinematic body to the existing barricade solid. `TrackObjectSettings.asset` owns five net hits,
0.35-second slide duration, launch eligibility and optional push damage. `TrackSolidSettings.asset`
owns the existing rebound, replenishment and impact-flash tuning. The solid's one-hit flag is only
an unfinished/completed marker; impacts never decrement it. The collider remains intact after docking.

`BarricadeImpactSystem` retains its launched-body broadphase and nearest-blocker casts, including
relative sweeps against the current moving track pose. Ordinary rebound and Explosive detonation
are unchanged. `TrackObjectHitResolution` converts signed track direction into a clamped integer
destination. Impact and explosion share the existing source/launch history. `ExplosionResolutionSystem`
dispatches in-range blasts using the vector from blast origin to current object position. The
common cleanup bounds history by source lifetime/relaunch. Direct punches skip the track target
without confirming cooldown by themselves. Existing aim selection, propagated aim correction,
homing and short trajectory preview accept its barricade target data until docking.

After swept impact detection, `TrackObjectMotionSystem` writes kinematic velocity toward a smoothstep
pose. A new hit restarts easing from the actual position toward the updated destination; no hit
waits for an earlier step to finish. Unity Physics integrates the body. In post-physics,
`TrackCharacterPushSystem` resolves swept overlap for non-launched characters by testing free
side positions and then both ends against solid geometry. Ordinary enemy locomotion remains
velocity-owned; these writes are obstacle penetration corrections. Launched bodies retain the
shared rebound path. The authored track has clearance on all sides; arbitrary enclosed custom
tracks are not validated. Optional damage queues existing `DamageRequest` or player health events,
deduplicated by character across a continuous slide. `TrackPushContactSystem` preserves pre-physics
swept contact eligibility so successful solver separation cannot erase optional damage. Mid-slide
retargets preserve the damage history; stationary/cancelled motion cannot deal push damage.

Player corrections pass through `PlayerEcsBridge.ReceiveObstacleDisplacement` to `PlayerController`,
which owns the GameObject transform. The system also refreshes the ECS player snapshot for later
post-physics consumers. Neither player MonoBehaviour queries or stores enemies. `TrackObjectArrivalSystem`
then removes numerical rail drift, stops velocity, locks at the physical endpoint and marks the
shared objective complete. Existing replenishment and completion systems stop supply and report the
win without survivor cleanup. `TrackSocketPresentationSystem` colors socket pieces green on locking.

`TrackNavigationSystem` owns a runtime replacement for the immutable baked navigation grid. It adds
the current block AABB and rebuilds after a quarter-cell displacement or a movement-state change.
Moving footprints include that threshold as padding. Existing navigation detects a changed blob,
cancels stale searches and revalidates paths; safe spawn checks use the same footprint. The system
disposes only its own blobs, restores the original when the object disappears, and handles scene
unload. This intentionally bounded single-object implementation avoids changing global navigation
ownership. The system is Burst compiled; its `CrowdPunch.TrackNavigation` profiler marker includes
rebuild work. A warm Editor check at the 14-root cap measured 0.234ms mean / 0.242ms maximum
over 30 forced footprint rebuilds. Managed fallback before asynchronous Burst compilation measured
about 17ms; these are isolated rebuild timings, not whole-frame or player-build guarantees.

`GameRestartSystem` resets pose, destination, timers, velocity and push history alongside shared
hit history and crowd teardown. Scene reload restores the baked state. The three unresolved
eligibility/damage edge cases are exposed as provisional options, with defaults and alternatives
documented in `OpenQuestions.md`; no wider design question is resolved by these choices.
See [Track validation](../Validation/TrackObject.md) for evidence, limitations and playtests.

## Protected Point And Gauntlet_18 (PROTECT-001..004)

Implemented: Gauntlet_18, "Hold the Line", follows the Wizard level in Bootstrap, level
selection and Build Settings. The 32 x 96m rectangular floor has a noncolliding turquoise
8 x 4m ground zone at z=-46. Spawn rectangles occupy the opposite end, z=37..45.
`ProtectedPointAuthoring` and its Baker attach the objective and selection buffer to the
existing wave-sequence entity. `ProtectedPointSettings.asset` exposes threshold (one) and
new-attack selection cap (three). Zone coordinates/size are authored in the SubScene.
The three CP18 wave assets own exact composition, batches and cadence; puzzle ammunition
supply and persistent-hazard gating are disabled. The first wave has a three-second entry delay.

`ProtectedPointPrioritySystem` runs after player bridging and wave spawning, before chase
and Wizard casting. It maintains the closest eligible in-range roots in a small sorted
`ProtectedPointAttacker` buffer, filtered by wave owner and run generation. Distances use XZ;
ties use entity index. Melee range is the larger of contact-attempt range and physical contact
reach; Ranged/Wizard use engagement range and Dashers use their preparation band. Staggered
Armored enemies cannot take slots. Selection is O(enemies * configured cap), with no all-pairs query.

`EnemyChaseSystem` reuses separation and writes the zone destination for unselected enemies.
Selected melee retains normal contact cadence; committed Baseline attempts finish. Ranged
and Wizard positioning holds selected attackers in place and otherwise preserves zone intent;
their attack systems gate new wind-ups/casts. Dasher positioning keeps zone intent until
selected, after which existing preparation, dash and recovery own movement. Existing navigation
and physics velocity steering are reused. Facing follows movement while advancing. Contact
damage and Explosive player-contact detonation obey selection; launched impacts and area
effects retain ordinary resolution. Committed attacks may finish after losing selection.

Last in post-physics, after recovery, bounds and lifetime handling, `ProtectedPointBreachSystem` checks Active root centres
against the inclusive XZ rectangle. Launched and recovering bodies can be moved back out
before becoming Active. A breach decrements shared undefeated accounting, increments the
current wave's resolved count, and ECB-destroys the root and linked visuals immediately.
Moving Wizard zones are removed with their owner; detached zones keep their existing lifetime.
No death explosion or replacement is generated. After removal, the managed system requests
the existing one-step pre-physics rebuild guard because destroyed Dashers can own unique
colliders still referenced by the preceding collision world. Spawn progression stops at the breach threshold.

`GauntletCompletionSystem` checks failure before completion and reports it once through
`GauntletFailureRegistry`. `GauntletSequence.RunFailed` drives the existing pause menu's
"PROTECTED ZONE BREACHED" / "Retry Level" result. MonoBehaviours never inspect enemy entities.
Scene retry restores authored state; `GameRestartSystem` also resets breach/selection state
alongside its existing wave teardown. Level transitions consume stale failure signals.

**Crowd Punch > Levels > Build Protected Point Gauntlet 18** rebuilds only this level and
registers it, preserving the dedicated threshold/cap settings while reapplying the layout and
wave recipe. Earlier scenes and shared archetype tuning are preserved. Difficulty, final art,
full-run duration and camera suitability for this longer court remain future validation work.
See [Protected point validation](../Validation/ProtectedPoint.md) for measured checks and playtests.

## Wizard And Gauntlet_17 (WIZARD-001..007)

Wizard is an explicit sixth standard `EnemyArchetypeKind`. `WizardEnemySpawnSettings`
bakes `WizardSettings` through the shared spawn profile, and `EnemySpawnInitialization`
adds its dedicated `WizardCastState`. Normal contact damage excludes Wizards. Punches,
aim assist, preview, health, launch, recovery and pooling use the existing enemy systems.
Wizard movement tuning overrides its movement components; shared Ranged tuning is not used.

`WizardCastSystem` owns cooldown, probability checks, telegraph and active duration. It
supports `WizardSettings.CastWheneverInRange`, default false, to bypass probability and
check interval after cooldown while retaining player availability and engagement range gates. It
creates a first-class `WizardZone` entity with an ECB-remapped reference in the cast state.
`WizardPositioningSystem` owns range-band decisions and optional casting stops. It writes
intent only. `WizardHazardAvoidanceSystem` adds separation from other Wizards' reserved
radii and independent zones before navigation; stopped casters and committed Dashers
retain their intentional movement ownership. Existing navigation and physics steering
remain responsible for movement. `EnemyFacingSystem` applies Wizard turn tuning while
facing the player; launched facing still follows velocity.

A zone stores its source, baked settings, explicit Cast/Impact kind, position, expiry, follow/active flags and scene/
wave ownership. Its `WizardZoneTarget` buffer stores independent entry/tick clocks and
player protection per target. `WizardSettings.CastRadius` and `ImpactRadius` are separate
Inspector fields. `WizardZone.Radius` selects the radius by kind for damage membership,
ground visuals and avoidance. Casting probability and caster reservations use Cast Radius.
Membership is refreshed every fixed step, including between
damage ticks; leaving removes the record. Physics broadphase candidates are expanded for
body travel since broadphase construction, then filtered by exact XZ radius plus the
target's physical radius. Cast zones check the player only. Impact zones exclude the source,
armor stages, bosses and non-enemy puzzle objects from enemy effects. Impact-zone force writes post-physics velocity; strong force uses the shared launch
transition only when starting a new normal-enemy flight. A dashing Dasher receives damage
without force, and an elite receives force without launching.

`EnemyDamageResolution` is the shared health/request resolver used by pre-physics
`DamageApplicationSystem` and same-step zone ticks before recovery. It preserves deferred
defeat, armor protection and one-shot death requests. No explosive detonation request is
created by zone damage. `WizardPlayerHit` is a singleton buffer written by the Burst zone
system and drained by managed `WizardPlayerHitSystem`. `PlayerEcsBridge.WizardHitReceived`
delivers damage and impulse to `PlayerHealth`, bypassing global invulnerability while
retaining the existing accepted-damage/knockback path. MonoBehaviours never hold enemies.

`EnemyLaunchState.ContinuousFlight` advances on a genuine entry into `Launched`.
`LaunchSequence` still advances for re-punches as before. `WizardImpactZoneSystem` consumes
one special impact per launched Wizard continuous flight. It also creates the same zone
when a launched enemy strikes an active/recovering Wizard; `WizardIncomingImpactHistory`
allows one zone per incoming source flight per Wizard and resets on Wizard pool/restart.
An incoming enemy preserves its monotonic `ContinuousFlight` number across pooling, so
the next genuine launch of the same entity remains distinguishable from the earlier one.
Moving zones are cancelled by launch or defeat; fixed zones keep their own timer through
source death, pooling, recovery and recasting. Scene/run ownership and `GameRestartSystem`
remove old zones on unload/reset. Respawn and restart reset cast state.

`EnemyWizard.prefab` uses Blob/Wizard.fbx and the established sampled ECS skinning path.
`EnemyAnimationProfile.Wizard` selects Idle, Walk, looping Dance for both cast phases,
the established launched pose and Death. Root motion is disabled. Rebuild its controller,
samples, materials, prefab and settings reference with **Crowd Punch > Enemies > Rebuild
Wizard Prefab**. `WizardZonePresentationSystem` draws a shared procedural disc using
`Resources/WizardZone.mat` and the additive URP `WizardZone.shader`; the full radius and
outer ring pulse during telegraph and brighten while active. The Wizard's body remains
violet through shared readability data. No extra HUD element is introduced.

Wave assets expose optional Wizard Baseline supply, supply delay, persistent-zone waiting
and an alive-Wizard cap (zero means unlimited). These features default off for existing
waves. Supply reuses existing safe placement, ownership and wave-count accounting and
cancels a pending replacement when the last living Wizard dies. Weighted selection
rerolls eligible non-Wizard profiles at the cap and reserves capacity for outstanding
guaranteed Wizard allocations; invalid minimum/cap combinations are rejected in baking.

**Crowd Punch > Levels > Build Wizard Gauntlet 17** builds Gauntlet_17, its matching SubScene,
two wave assets and progression references. It introduces 6 Baselines + 1 Wizard, then
12 Baselines + 2 Wizards in an open clipped court. Both waves enable delayed Baseline
supply and wait for defeated enemies plus expired zones; Wizards themselves are finite.
See [Wizard validation](../Validation/Wizard.md) for checks and remaining playtests.

## Trail Enemy And Gauntlet_19 (TRAIL-001..007)

Trail appends archetype value 7, stays Normal tier, and uses Baseline health. Its spawn profile
references `Data/Settings/Enemies/TrailEnemySettings.asset`, a dedicated `TrailEnemySettings`
ScriptableObject. Shared profile baking explicitly depends on that asset; spawning adds
`TrailSettings` and `TrailEmitter`. Per-archetype separation overrides support Trail. Contact
damage excludes it. The health-bar presentation bridge never publishes a Trail health bar.
Ordinary punch, preview, homing, collision propagation and recovery paths are reused.

`TrailCirclingSystem` runs after chase and elite support, before hazard avoidance/navigation.
It writes a short tangential travel goal plus radial correction, preserves crowd separation,
reverses periodically and respects an elite's projectile reservation. Navigation owns obstacle
handling and `EnemyMovementSystem` owns velocity steering. No normal enemy transform movement
or separate physics motor is introduced.

Post-physics `TrailExpirySystem` removes expired or unloaded encounter data. `TrailEmissionSystem`
runs after ground reconciliation/explosions and before recovery. It samples actual horizontal
travel in Active/Launched only, emits capsule sections using an ECB, and projects endpoints onto
upward-facing static surfaces below the body. Section spacing is bounded by width to keep paths
continuous. Stationary/recovering/defeated states break the emission anchor. Each section snapshots
width, damage, timing, color, immunity/avoidance, launch ownership and chain depth.

All baked enemies have `EnemyLifetime`, incremented at pooling and restart. A detached `TrailSource`
stores source entity + lifetime, scene/sequence/run/wave ownership, latest expiry and a
`TrailDamageTarget` buffer. Sections share this record across normal/launch transitions. Source
death, pooling and reuse never remove or mutate old sections. The record outlives its enemy and
expires when its last section does. `TrailWaveCleanupSystem` runs after defeat counting and removes
sections and source clocks immediately once a fully spawned finite encounter has no undefeated enemies.
It does not mistake empty spawn-batch gaps for wave clearance or interrupt objective-owned replenishing crowds.
Scene/run invalidation and `GameRestartSystem` remove both
records and sections, plus pending player hits.

`TrailDamageSystem` runs after emission and Wizard-zone damage, before recovery. It snapshots eligible
enemies' current post-physics positions, radius, lifetime and armor state into a temporary 4m spatial
grid; exact capsule overlap uses XZ and target radius. Each target enters one grid cell, so broadphase
candidate enumeration cannot duplicate a target. This avoids repeated EntityManager lookups per section
and does not depend on pre-integration physics broadphase positions.
Armor stages/protection, defeated/pooled enemies, bosses and non-enemy objects are excluded. One
source/target-lifetime timer survives exit/reentry and prevents overlapping or adjacent sections
from multiplying damage. Player uses a separate entry in that same source buffer with interval
at least the trail-specific protection duration; independent source records stack. Damage uses
the shared resolver with no velocity writes, launch transition or explosive request. Earlier
pending damage resolves separately before trail damage so it cannot acquire trail credit.

`EnemyDamageState` records last damage and lethal damage source lifetime, ownership and chain
depth. Lethal trail credit survives deferred defeat and later source reuse without altering the
target's launch ownership. The repository has no numeric score/reward economy; these fields retain
player kill/chain attribution for the existing/future consumers without inventing progression.
`TrailPlayerHitSystem` drains an ECS hit buffer into `PlayerEcsBridge.TrailDamageReceived`.
`PlayerHealth` accepts damage independently from ordinary invulnerability, with zero impulse.

`TrailAvoidanceSystem` runs after other hazard decisions and before navigation. A Burst parallel
job filters damage eligibility, cheaply rejects distant section bounds, and adds the strongest
local capsule repulsion to movement/navigation separation. It ignores launched bodies, committed
Dashers and explicitly anchored/stopped behavior. The None mode leaves intent alone. It never
makes trails solid or changes launched-body collision physics.

`EnemyTrailPrefabBuilder` samples Fish Idle/Walk/Death into the established CPA3 skinning path,
using the common Baseline animation state mapping and a stable green body tint. Root motion is
disabled and the physical capsule provides collision contacts. `TrailPresentationSystem` batches
all ground capsules into one reusable mesh/draw, with green normal and orange launched colors.
Each retains full width while alpha fades through the last 35% of its lifetime.

**Crowd Punch > Levels > Build Trail Gauntlet 19** authors only Slippery Circuit: an open 32 x 34m
clipped court, 6 Baselines + 1 Trail then 12 Baselines + 2 Trails. Waves use guaranteed counts,
no ammunition supply, and no Trail expiry gate. Trail cleanup on defeat lets wave advancement and final
completion proceed without waiting; the optional Wizard-zone hazard gate remains unchanged.
Bootstrap/build/selector append 19 after Hold the Line.
Starting tuning is 1.2m/5s/4 damage normal, 2m/6s/8 damage launched, .75s ticks, .35s player
protection, 8m circling distance and 4s reversals. See [Trail validation](../Validation/Trail.md).

## Dino Pillars And Gauntlet_22 (PILLAR-001..006)

`DinoBossAuthoring` and `FallingPillarAuthoring` bake the boss and exactly three scene-owned
pillars. `DinoBossSettings.asset` is the dedicated tuning source; `DinoTuning` and
`PillarTuning` contain only ECS data and immutable collider blobs. The boss owns `DinoBoss`,
`Health` measured in required pillar hits, dynamic Unity Physics capsule/mass/velocity and
no ordinary enemy launch components. Ordinary punch/explosion/body-damage paths therefore
cannot launch or damage it. `PillarDamageResolution` alone changes boss health and stagger.

`DinoCycleSystem` advances chase/warning/burst/stagger before physics. `DinoMotionSystem`
turns toward the player, uses capsule sweeps and surface tangents around static obstacles,
and steers horizontal velocity with bounded acceleration. Burst turning is slower;
warning retains normal chase. `DinoContactSystem` locks only the ground axis after physics,
publishes cooldown-limited player contacts through the existing bridge, and gives ordinary
crowd bodies outward velocity without damage or launch-state changes.

`PillarToppleSystem` runs after launch homing and grounding, sweeping Player-owned launched
enemy colliders before the physics build. A nearer blocking contact remains authoritative.
Toppling locks direction at impact, clears per-fall histories, reserves the triggering
body's pass-through contact so launch mode cannot steal its launch, and swaps the solid box for
an immutable zero-filter box before the solver. It never changes the triggering body.
`PillarFallMotionSystem` rotates the scripted pillar around its base with an accelerating
fall. `PillarFallContactSystem` runs after physics and before recovery/replenishment. It
collects enemy contacts in a Burst job by subdividing the rotating box arc and target
motion with conservative capsule padding,
records each enemy once per fall and separate player/boss flags, and resolves shared enemy
damage/deferred defeat. The alternate launch response starts explicit `EnvironmentImpact`
launches owned by `Environment`; normal propagation inherits this ownership. These launches
remain dangerous to the player and fail the Player-only pillar trigger.

`PillarRegenerationSystem` hides fallen geometry after its brief linger. Successful pillars
stay consumed; misses wait for their delay and player/boss clearance. Ordinary occupants
receive outward velocity; collision is restored only after they actually clear the space.
This avoids teleporting physics-driven enemies into an upright obstacle.

Upright pillars join aim-assist candidates and launch homing. Falling/waiting/consumed
pillars and immune Dino are ineligible. `DinoAnimationSystem` uses generated Dino Walk,
Run, HitReact and Death samples through the established GPU skinning path. Presentation
colors distinguish warning, burst, stagger, impact and regeneration; no ground marker or
additional HUD is created. `EnemyHealthBarBridgeSystem` reuses the existing boss bar.

`BossCrowdSequence`, spawning and replenishment now recognize Dino as an owner. The
Gauntlet_22 wave asset supplies eight Baselines with a three-second respawn delay.
`DinoTuning.EnemiesPerPillar` and `PillarCrowdRadius` additionally reserve two local
Baseline bodies per unconsumed pillar, default radius 4m. `PillarCrowdSupplySystem` uses the
wave's baked Baseline profile and normal ECB spawn initialization; each fixed slot owns
one `PillarCrowdMember`, `BossCrowdMember` and `EnemyWaveOwnership`. It counts pooled or
launched occupants too, so firing bodies cannot create unlimited replacements.
`PillarCrowdPositioningSystem` runs after ordinary chase intent and before navigation,
preserves ordinary crowd goals, speeds, contact cadence and separation when they fit within
the pillar radius. Outside goals are clipped to that circle; displaced Active bodies return
to its nearest inner edge. Small arc waypoints avoid crossing through the upright shaft.
Local bodies participate in the ordinary crowd's chase-pressure allocation. The shared
`EnemyMovementSystem` removes outward radial motor velocity at the local boundary, keeping
tangential and inward movement. Launched/Recovering motion is untouched; consumed pillars
release the constraint along with the local replenishment policy.
`BossCrowdPlacement` safely respawns these bodies in their pillar sector instead of the
general wave regions, with the wave's existing respawn delay. Space blocked by the player,
boss or crowd retries later. Consumed pillars release survivors to normal AI and disable
their respawn. Existing wave-owned reset and unloading remove every local body; resetting
the sequence recreates exactly the configured slot count.
`GauntletCompletionSystem` uses Dino defeat as authoritative regardless of crowd survivors.
`DinoEncounterReset` restores health, timers, velocity, all pillar colliders/transforms,
hit histories and sampled playback; additive unloading removes their SubScene entities.
The authored sequence and Build Settings append Dino Pillars after Rolling Blob.
Rebuild via **Crowd Punch > Levels > Build Dino Pillars Gauntlet 22**; existing boss/wave
settings are preserved. See [Dino pillar validation](../Validation/DinoPillars.md).

## Fixed Ground Hazards And Gauntlet_23 (GROUND-001..007)

`GroundHazardAuthoring` bakes XZ circle or yaw-oriented rectangle footprints,
per-patch damage/tick tuning, periodic durations/offset and first-wave introduction.
Dimensions are explicit world metres; transform scale and height do not resize damage.
`GroundHazardPolicyAuthoring` adds scene policy to the wave sequence from the dedicated
`Data/Settings/GroundHazardSettings.asset`: Active-only avoidance and Wait Safely by default.
Warning-and-Active and Cross As Last Resort remain exposed alternatives.

Pre-physics `GroundHazardCycleSystem` runs before wave spawning, resetting patch epochs
when sequence wave index or run generation changes. Epochs begin at the wave's pre-spawn
delay. Later-wave patches are introduced then; introduced inactive patches stay visible.
Spawn capture evaluates authored offsets when initialization/reset/advancement precedes
the cycle system, preventing a one-update unsafe placement gap. Initial random, wave,
shared spawn creation, soft restart and pooled respawn paths reject warning/active
footprints independently of avoidance policy. Failed placement retains existing retries.

`GroundHazardNavigationSystem` runs after terrain navigation and before both enemy motors.
It retains the immutable terrain grid and adds a dynamic edge mask per clearance class,
rebuilt by a Burst parallel job only when hazardous membership changes. It does not
rebuild solid connectivity, create collision geometry or alter physics velocity. A Burst
`IJobEntity` writes voluntary `DesiredMovement`, using a bounded FIFO and separate
`GroundHazardRoute`/`GroundHazardWaypoint` data. Shared A* accepts optional edge masks;
terrain callers keep previous behavior. Hazard searches use the existing navigation
asset's expansion, slot, queue and path limits, with a separate budget from terrain.
Radius-inclusive sweeps validate edges, shortcuts and braking probes. Activation invalidates
cached routes/searches. Caught Active bodies sample 32 analytical footprint exits,
preserving static clearance and other patches. Armor does not exempt avoidance. Launch,
recovery, committed lunges/Dashers, stationary casters and reserved elite projectiles
preserve existing movement ownership.

Wait Safely brakes while routes are pending/unavailable. Cross As Last Resort keeps
terrain's original movement only after an exhaustive unreachable result. Expansion/path
limits and invalid anchors remain safe waits. Native scratch survives inactive cycles
and is disposed on arena unload/world teardown. Pooling/restart invalidate versions and
buffers; scene-owned roots disappear on unload.

Post-physics `GroundHazardDamageSystem` runs after Trail damage and before recovery. Each
baked enemy owns one `GroundHazardDamageClock`, shared across all patches and keyed by
`EnemyLifetime`; exit, overlap and activation never clear it. Simultaneous overlaps select
the highest-damage patch, with the longer interval breaking ties. Shared damage resolution
preserves armor, pending-damage attribution and launched deferred defeat, without impulse
or explosive requests. Current Player-owned launched victims receive Player lethal credit;
other hazard damage uses Environment ownership. Existing launch ownership stays intact.

One player clock and `GroundHazardPlayerHit` singleton buffer feed the dedicated
`GroundHazardPlayerHitSystem`/`PlayerEcsBridge.GroundHazardDamageReceived` event.
`PlayerHealth` accepts damage through the existing damage-only path, bypassing ordinary
invulnerability and retaining dash/movement. MonoBehaviours do not query enemies.
Restart clears pending hits/clocks and patch epochs. `GroundHazardPresentationSystem`
draws matching footprints with a shared procedural mesh/material: slate inactive,
full-area pulsing amber warning and bright red active. No additional HUD is created.

**Crowd Punch > Levels > Build Ground Hazards Gauntlet 23** authors Hot Footing after Dino
Pillars: a nature-kit 32 x 34m clipped court, two permanent patches and two periodic patches
introduced in wave two. Editable waves contain 12 and 20 finite Baselines, with no supply
or persistent-hazard completion gate. Broad side corridors and safe spawn ranges remain.
Initial tuning is 12 damage every .75s; periodic timing is 3s inactive / 1.5s warning /
2.5s active, with 0s/2s offsets. See [Ground hazard validation](../Validation/GroundHazards.md).
