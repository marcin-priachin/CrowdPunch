# Gauntlet 21: Rolling Blob

Implements ROLL-001..007, COMBAT-007/012/014/015/018, PLAYER-003/004/005 and LOOP-006.
The player/camera/UI remain GameObjects; boss/crowd/contacts and presentation data are ECS-owned.

`RollingBossSettings.asset` bakes immutable `RollingTuning`. Actual defaults: 1800 health,
66%/33% stage thresholds, 3s opening, 4/3.2/2.5s pauses, 1.2s wind-up, 5s rolls,
9/11/13m/s speeds, .75s hit protection. Bounce-count mode uses four bounces and a 9s
safeguard. Contact uses 14 player damage/push, .8s contact/protection; crowd damage is 8,
launch speed 16m/s and resistance push 7m/s. These are provisional tuning, not final balance.
Six modes default to collision-time re-aim, duration ending, Player-only body damage,
rolling-only player danger, every-living-state targeting, and roll-direction crowd launches.

`RollingCycleSystem` commits pause/wind-up/roll timers. `Stage` changes on threshold-clamped
damage; `CycleStage` updates only when the next pause starts. Wind-up tracks player position;
rolling direction is fixed between redirects. `RollingMotionSystem` writes motor velocity
and uses a shape-matched capsule sweep into the previous step's static collision world.
Entering lateral normals redirect; outgoing contacts are ignored. Collision-time re-aim
requires an outward component, preventing a player across the wall from repeatedly pulling
the boss into it. The dynamic 250kg collider stays upright through zero inverse inertia;
post-physics vertical projection constrains only height. No boss enters ordinary Enemy queries.

`RollingCollisionSystem` gathers solver boss/body events before ordinary launch propagation.
Incoming body damage uses the shared impulse curve and optional Dasher boss damage. Rolling
begins a fresh Boss-owned launch with normal transition data/velocity, including reclaimed
player bodies; contact history suppresses sustained-contact repeats and accounts for pooled
lifetimes. Protected armor receives push only; elites receive damage and push without launch.
Passive contact does not alter ownership. Shared launch propagation inherits Boss ownership;
all owners remain dangerous to the player. `BossHeadBounceSystem` also recognizes the Blob,
preserving solver speed/vertical motion, deflecting player-bound rebounds and clearing its homing lock.

`ExplosionResolutionSystem` queues any-source blasts using source/launch/lifetime identity.
`RollingDamageResolution` combines simultaneous impact/blast using maximum damage.
`RollingDamageSystem` consumes launch histories even during rolling/protection, clamps damage
at the next health threshold and leaves surviving phase/timer/speed unchanged. New launches or
pooled lifetime generations renew eligibility; histories of destroyed/obsolete sources are removed.
Direct punches cannot enter the boss resolver because the boss has no ordinary punch components.
Aim-assist and homing use the same configurable living/vulnerable eligibility.

`RollingPlayerImpactSystem` sweeps the boss path against the player snapshot and publishes
damage/knockback through `PlayerEcsBridge`, with a contact interval. Pause/wind-up are solid
and harmless by default. Boss defeat immediately stops velocity/replenishment and reports
completion once; supporting survivors do not gate it. Shared restart destroys/reallocates
the bounded crowd; `RollingEncounterReset` restores health, pose, stage/timers and hit histories.
Scene selection/unload removes baked boss and supporting entities.

`CP21_01_Rolling_Crowd.asset` owns three Baselines and three-second replenishment.
The 40x40m arena contains three well-spaced `SolidObstacleAuthoring` rectangles under the
navigation arena. Broad side/bottom spawn regions exclude them. Their ECS solid colliders
and immutable navigation footprints share the same authored dimensions.

The supplied `Assets/Spike Cute Series/Prefabs/Spike.prefab` provides the visual with a constant
uniform scene scale fitted to the existing hitbox footprint. `RollingSpikeBuilder` measures the
idle/spin samples, preserves proportions and aligns the visual with the collider floor. Runtime
never resizes it. The builder applies it to legacy Gauntlet 21 and campaign 30/70. Dedicated
`CrowdPunch/Cel/Enemy` materials preserve the Spike texture. The CPA3 samples contain Idle,
Hide and Spin Attack In Place, reversed Hide to Idle for wind-up, Take Damage, Die and forward
Hide to Idle for recovery. Sampled floor corrections use translation only, including defeat.
`RollingAnimationSystem` uses `RollingAnimationState` and accepted-hit sequences to select these
clips without changing combat timers or physics. Quaternion interpolation preserves authored
bone scale. The procedural roll, squash and material-color presentation system are removed.
The existing elite-style Canvas publishes one boss bar; there is no countdown UI.

**Crowd Punch > Levels > Build Rolling Blob Gauntlet 21** reproduces this level, generated
samples and its Bootstrap/build/selector entry while preserving dedicated tuning and earlier
levels. The shared boss sampling method also serves Chicken; Chicken gameplay is unchanged.
See [validation](../Validation/RollingBoss.md) for automated evidence and remaining playtesting.
