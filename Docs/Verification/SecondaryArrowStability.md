# Secondary arrow stability and bounded forecast

Date: 2026-10-31. Requirements: PLAYER-003/004, COMBAT-003/004, INFO-005.

## Implemented behavior

Secondary arrows confirm a consistent source, victim, follow-up and outgoing direction
before appearing. Small angular changes are smoothed. Major changes fade the old
direction out and require confirmation before the new direction fades in. Missing,
blocked, protected or conflicting predictions hide immediately. Pooling, re-punches,
player/bridge replacement, level unload, preview reset and long sampling gaps clear
stability history. The primary arrow and its click/display commitment are preserved.

The first-impact forecast follows existing limited homing over bounded short steps,
uses relative casts for moving crowd bodies and replacement velocities for co-punched
bodies, and evaluates follow-up targets at estimated impact time. It accounts for
linear damping, known ground locks and timed armor protection at arrival. Competing
sources resolve by earliest impact, then impulse and stable entity identity. Near-time
conflicting directions suppress the affected secondary arrow. Confidence multiplies
its opacity. Shared propagation-direction math keeps the existing gameplay rule.

## Exposed tuning

Select `Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset`. Changes are copied
live through the existing player bridge; no scene/prefab regeneration is required.
Existing direct/collision length, width and RGBA controls remain independent.

| Inspector group / setting | Saved default | Effect |
| --- | ---: | --- |
| Collision Arrow Stability / Confirmation Time | 0.10 s | Consistent prediction required before display; zero skips delay. |
| Stable Angle Degrees | 8 degrees | Angular tolerance for confirmation and small-change smoothing. |
| Smoothing Time | 0.06 s | Small-jitter smoothing; zero follows immediately. |
| Fade In Time | 0.06 s | Fade after confirmation; zero appears immediately. |
| Fade Out Time | 0.04 s | Fade on a major valid direction change; invalid predictions still hide immediately. |
| Minimum Opacity | 0.35 | Lowest-confidence multiplier; authored colour alpha also applies. Set to one to disable confidence dimming. |
| Collision Forecast Budget / Forecast Seconds | 0.25 s | Short forecast horizon, clamped to 0.02-0.5 s and also limited by prediction distance. |
| Forecast Steps | 6 | Cast steps per source, clamped to 1-12. More steps increase CPU cost. |
| Competition Window | 0.025 s | Arrival-time separation treated as competing; zero disables conflict suppression. |
| Conflict Angle Degrees | 20 degrees | Competing directions beyond this difference suppress the arrow. |

Raise confirmation time to reject longer flashes; lower it for faster feedback.
Increase smoothing time to reduce small jitter, accepting more visual lag. Increase
the angle tolerance if gradual direction changes repeatedly hide the arrow. Reduce
forecast steps first when measuring crowd CPU cost, then shorten the horizon if needed.
The horizon is intentionally short: a source with no predicted impact inside it has
no secondary arrow even if a longer straight-line cast would find a distant enemy.

## Verification

Unity 6000.3.10f1 imported the new scripts and metadata and compiled successfully.
`dotnet build Assembly-CSharp.csproj --no-restore -v quiet` passed with zero errors
and 288 existing package/version/deprecated-API warnings on the final build.

301 checks passed across the targeted Editor suites:

| Suite | Passed |
| --- | ---: |
| CollisionPreviewStabilityTests | 11 |
| LaunchImpactPreviewTests | 27 |
| StableLaunchAimTests | 15 |
| SnakeEnemyTests | 52 |
| ArmoredEnemyTests | 20 |
| RollingBossTests | 31 |
| ChickenBossTests | 41 |
| BarricadeTests | 12 |
| RotatingCoverTests | 10 |
| LinkedPairTests | 23 |
| PlayerPunchCooldownTests | 12 |
| EnemyLaunchHomingTests | 2 |
| LaunchedEnemyPlayerImpactTests | 35 |
| DasherObstacleRedirectTests | 2 |
| DeliberateShotTests | 8 |

New checks cover brief wrong directions, fixed-anchor confirmation during continuous
drift, small-jitter smoothing, major-change fading/reconfirmation, confidence opacity,
pooling/re-punch identity, preview reset/unload, deterministic source competition,
bounded horizon/steps, curved homing, moving follow-ups at impact time, known ground
constraints, timed armor and live setting copies. Existing real Unity Physics contact
tests still verify guidance cutoff and forward-cone propagation behavior.

An isolated Editor preview render used the real arrow meshes, saved colours and
CombatFeedback shader at opacity multipliers 1, 0.35 and 0.1. Visual inspection
confirmed independent dimming and preservation of the direct arrow; the shader's
default multiplier is one. No campaign scene was changed by this check.

Synthetic Editor profiling used 100 measured updates after warmup, eight punch sources,
real broadphase/collider queries, aim plus both previews, and the saved six-step budget:
4.350 ms/update at 100 bodies and 7.031 ms/update at 1,000. Simulation and rendering
are excluded. These are diagnostic measurements; OQ-001 target-hardware acceptance
remains open.

## Limits and playtest follow-up

No manual campaign playthrough or target-hardware player build was performed. Tune
confirmation and smoothing while moving through dense crowds and approaching exposed
Snake tips. Confirm the readability/latency tradeoff at representative crowd sizes.

This remains a short first-impact estimate. It does not clone the physics solver,
predict future AI decisions, angular collider motion, full falling/grounding behavior,
friction/restitution, simultaneous-contact impulse distribution, linked-pair swing,
environmental rebounds or recursive chains. It never commits secondary gameplay
targets or changes collision, damage, movement or launch settings.
