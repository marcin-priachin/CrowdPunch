# Gauntlet 20: Chicken Run

Implements CHICKEN-001..008, PLAYER-003/004/005/009 and COMBAT-015. The traditional player,
camera and UI remain authoritative GameObjects. The Chicken and its shots are ECS-owned.

`ChickenBossSettings.asset` bakes immutable `ChickenTuning`; editing it requires rebaking.
It exposes all stage, aim, pause, body, projectile, damage and timing choices. Defaults are
single/paired/paired, interrupt-and-flee, position aim, 1800 health, 75 returned-shot damage,
0.8s wind-up, 1.5s minimum paired releases, 13m/s rushes (1.35 multiplier in stage three),
1.6s pauses (0.55 multiplier in stage three), 0.65s stagger and 1.15s damage protection.
Shots use 0.45m radius at y=1, 10m/s outgoing and 22m/s returns, 9s lifetime and five bounces.
Numbers target a 2-3 minute fight but do not enforce duration or establish final balance.

`CP20_01_Chicken_Crowd.asset` owns 12 Baselines, composition, safe spawn rectangles,
initial delay and three-second recycling. `EnemyWaveSequenceAuthoring.chickenBoss` uses
existing `BossCrowdSequence`/`BossCrowdMember`. Spawn and replenish gates now recognize
either living boss type. The level has a convex 36x36m floor and four solid perimeter rails,
with nature-kit visuals and no interior obstacles. Its authored center/half-size is the
projectile wall geometry and rush bound. Custom arenas must match those rectangular bounds;
this slice does not implement arbitrary interior-wall ricochets or obstacle navigation for bosses.

`ChickenAttackSystem` tracks fire-time aim, selects single/paired stages and commits a
bounded destination at release. Proximity only interrupts pauses. `ChickenMotionSystem`
is the sole normal velocity writer for the kinematic body; Unity Physics integrates it
and pushes ordinary crowd bodies aside. `ChickenPlayerImpactSystem` sweeps each rush once
against the player snapshot and calls the existing health/knockback bridge. Bosses carry
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

The supplied Chicken.fbx has a Generic mesh and Idle/Walk/Bite_Front/HitRecieve/Death clips.
The recipe samples these into the existing CPA3 skin matrices; `EnemyAnimationBaker` now accepts
an ordinary enemy or Chicken owner. The common visual baking system assigns Chicken render
children to `ChickenVisualOwner`, excluding ordinary deformation/readability. `ChickenAnimationSystem`
plays the appropriate phase, with root motion disabled. `ChickenPresentationSystem` shows amber
wind-up, red rush, bright stagger and pulsing cyan protection. Shots are orange or magenta;
both remain visibly dangerous. The existing elite health-bar canvas shows one boss bar.

**Crowd Punch > Levels > Build Chicken Boss Gauntlet 20** reproduces only this level and appends
it to Bootstrap/build/selector. It preserves existing dedicated boss and wave tuning, model
GUID/importer and earlier levels. See [validation and remaining playtests](../Validation/ChickenBoss.md).
