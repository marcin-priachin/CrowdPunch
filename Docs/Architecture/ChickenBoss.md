# Gauntlet 20: Chicken Run

Implements CHICKEN-001..008, PLAYER-003/004/005/009 and COMBAT-015. The traditional player,
camera and UI remain authoritative GameObjects. The Chicken and its shots are ECS-owned.

`ChickenBossSettings.asset` bakes immutable `ChickenTuning`; editing it requires rebaking.
It exposes all stage, aim, pause, body, projectile, damage and timing choices. Defaults are
single/paired/paired, interrupt-and-cast, position aim, 1800 health, 75 returned-shot damage,
0.8s wind-up, 1.5s minimum paired releases, 13m/s rushes (1.35 multiplier in stage three),
1.6s pauses (0.55 multiplier in stage three), 0.65s stagger and 1.15s damage protection.
Shots use 0.45m radius at y=1, 10m/s outgoing and 22m/s returns, 9s lifetime and five bounces.
Numbers target a 2-3 minute fight but do not enforce duration or establish final balance.

`CP20_01_Chicken_Crowd.asset` owns 3 Baselines, composition, safe spawn rectangles,
initial delay and three-second recycling. `EnemyWaveSequenceAuthoring.chickenBoss` uses
existing `BossCrowdSequence`/`BossCrowdMember`. Spawn and replenish gates now recognize
either living boss type. The level has a convex 36x36m floor and four solid perimeter rails,
with nature-kit visuals and no interior obstacles. Its authored center/half-size is the
projectile wall geometry and rush bound. Custom arenas must match those rectangular bounds;
this slice does not implement arbitrary interior-wall ricochets or obstacle navigation for bosses.

`ChickenAttackSystem` tracks fire-time aim, selects single/paired stages and commits a
bounded destination at release. Rush facing tracks the current player every fixed step while
the committed destination remains unchanged. The existing physics angular-velocity path applies
this orientation, and jog legs blend relative to actual owner facing. Pause and Cast also track
the current player. `ChickenMotionSystem` publishes signed idle yaw rate in `ChickenTurnState`;
Turn Left/Right samples (6/7) affect only masked leg bones during Pause. The pelvis, upper body
and equipment use a shared continuous Idle clock; turn poses are composed relative to the Idle
pelvis. Reversing or repeating a turn does not restart upper-body Idle. Cast and Sweep keep action priority.
The current turn step finishes before Idle, or repeats while turning continues. The turn clips
author 90-degree root yaw; export removes that yaw relative to the first sample so physics applies
facing exactly once. No GameObject transform or authored scale is changed by this correction.
Restart clears turn state and boss animation playback. Proximity only interrupts pauses. `ChickenMotionSystem`
is the sole normal velocity writer for the kinematic body; Unity Physics integrates it
and pushes ordinary crowd bodies aside. `ChickenPlayerImpactSystem` sweeps each rush once
against the player snapshot and calls the existing health/knockback bridge. During the stationary
Cast phase, overlap contact uses the same bridge and player damage protection, with outward push.
`ChickenCastTuning` bakes the full Cast Spell duration (1s) from settings populated by the model
builder. The cast plays full-body motion 5 without the jog layer and tracks the simulation timer.
At completion the AI writes the transient Teleport phase. `ChickenTeleportSystem` consumes it
after AI and before movement/physics build, selecting the farthest collider-inset arena corner
from the current player position. It changes position explicitly, preserves scale/height, clears
velocity, resets the contact sweep endpoints and returns to Pause. No rush or projectile is created
by proximity. Accepted damage or defeat changes phase and cancels the cast/teleport. Normal shot
rushes, paired spacing and the optional finish-pause response remain unchanged. Bosses carry
no Enemy, EnemyLaunchState, DamageRequest, RespawnRequest or ordinary movement components.

`ChickenProjectile` holds velocity, boss ownership, age, bounce count, launch ID and homing
lock. It has no physical collider: swept contact resolves pass-through damage rather than
allowing the solver to stop shots on ordinary enemies. `ChickenProjectileSystem` integrates
bounded segments, handling every remaining bounce within a fixed step. It clips the segment
at the nearest terminal player/returned-boss contact, then processes enemy overlaps along it.
Target lifetime records in `ChickenProjectileHit` prevent duplicate hits and admit reused
pooled enemies. Armor uses the shared hit resolver; ordinary launches use the shared transition,
damage resolver and PhysicsVelocity. Outgoing launches are Boss-owned; returns are Player-owned.
The existing propagation system inherits that ownership and COMBAT-015 keeps all chains dangerous.

The existing punch system redirects shots in its exact normal volume and confirms the normal
cooldown once. Aim locks use the same ray-first, angular-fallback rules; enemy targeting and homing
also recognize a living Chicken. Presentation publishes the same short initial direction preview.
Redirection replaces speed and resets age, bounces, hit history and launch ID. Homing uses the
shared horizontal rotation function and turn rate; a wall reflection clears its target.

`ChickenCollisionSystem` gathers real solver body contacts, admits player-owned flights using
the shared impulse damage curve (and Dasher boss tuning), and requests contact detonations.
`ExplosionResolutionSystem` also queues Chicken hits for any in-range Exploder regardless of
launch state/owner. `ChickenDamageResolution.Queue` merges equal source/launch/lifetime events
using maximum damage. After blasts and shots, `ChickenDamageSystem` consumes histories even
during protection, applies health/stage changes once, cancels attacks and stops velocity on a hit.
History cleanup removes destroyed sources and obsolete launches/lifetimes. This gives an Exploder
impact plus its associated same-step blast one combined maximum hit, without copying Gatekeeper rules.

`BossHeadBounceSystem` now recognizes both the Gatekeeper head and Chicken body. After the
current-step launched-body/player impact check, it deflects a launched body's player-bound
solver rebound sideways, preserving horizontal speed, vertical motion, launch damage and ownership.
It clears homing toward the contacted boss and leaves already-safe rebounds intact (CHICKEN-004).

`ChickenProjectileCleanupSystem` removes surviving shots after same-step defeat or owner unload.
Completion uses Chicken defeat without checking survivors. `ChickenEncounterReset` restores
health, stage, pose, timers, velocity and history and destroys shots during shared soft reset.
Scene reload restores baking and the shared wave reset destroys/reallocates its bounded crowd.

The boss uses `Prefabs/Enemies/Goblin Giant Wizard Variant.prefab`, preserving all authored
transform scales. `ChickenModelBuilder` samples the Giant pack's Humanoid Idle, Run Forward In
Place, Sweep Attack, Take Damage, Die, Cast Spell, Turn Left and Turn Right through the supplied Giant avatar. Body, shoulders, hat and wand
share the existing CPA3 ECS skinning path; rigid equipment is converted to single-bone skinning.
Only translation aligns the model with the collider floor. Dedicated materials preserve source
textures under `CrowdPunch/Cel/Enemy` and are excluded from monster-atlas refreshes.

`ChickenAnimationSystem` plays phase animations without procedural squash, recoil or shaking.
`ChickenSkinPose` interpolates rotations separately to retain authored bone scales. The shared
CPJ1 directional jog samples replace only leg matrices below the sampled pelvis. Upper-body Sweep
and attached equipment retain their original pose while the boss rushes. Jog weight follows local
physics velocity, and stagger/defeat bypass the leg layer. No animation writes physics transforms.
`ChickenPresentationSystem` keeps the boss material multiplier neutral; shots retain their
orange/magenta dangerous ownership colors. The elite health-bar canvas remains the sole boss bar.

`Wind Up` sets the full Sweep duration. `Projectile Launch Frame` is a zero-based source frame,
clamped against `Firing Animation Last Frame` (30, derived from the supplied clip). The default
18/30 releases at 0.48s of the 0.8s Sweep, then immediately commits the existing rush. Upper-body
follow-through continues during movement. Paired-shot spacing still uses actual releases; any
extra spacing wait holds frame zero. The simulation clock and presentation use the same release
phase, including frame zero/last-frame settings. Soft reset clears the release sequence.

**Crowd Punch > Bosses > Apply Goblin Chicken Model** migrates saved Campaign 20, Campaign 60
and Gauntlet 20 without rebuilding their arenas, waves or lighting. The generic boss appearance
and level builders call the same model recipe. See `Docs/Verification/GoblinChicken.md`.

**Crowd Punch > Levels > Build Chicken Boss Gauntlet 20** reproduces only this level and appends
it to Bootstrap/build/selector. It preserves existing dedicated boss and wave tuning, model
GUID/importer and earlier levels. See [validation and remaining playtests](../Validation/ChickenBoss.md).
