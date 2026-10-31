# Stable launch aiming - 2026-10-31

Implemented the user's selected option 2. Requirements: PLAYER-003/004,
COMBAT-003/012/014, INFO-005. Input-specific OQ-017 remains unresolved; both input
schemes use the same controls. No option-3 collision direction bias was introduced.

## Saved tuning

`Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset`:

| Setting | Initial value |
| --- | --- |
| Assist range | 22 metres |
| Acquire / release angle | 12 / 16 degrees |
| Challenger persistence | 0.12 seconds |
| Fallback distance weight | 0.35 |

`Assets/CrowdPunch/Data/Settings/GameRuntimeSettings.asset`, baked by
`GameSettingsBaker`:

| Setting | Initial value |
| --- | --- |
| Movement lead | up to 0.2 seconds / 2 metres |
| Homing turn rate | 45 degrees/second |
| Homing duration | up to 0.25 seconds per launch |
| Homing flight cone | 20 degrees |
| Propagation correction | 6 metre radius / 20 degree forward cone |

The existing user-authored arrow lengths, widths and RGBA colours were retained.
Range zero disables target selection; switch delay zero allows immediate switches;
distance weight zero restores alignment-only fallback scoring. Lead time or distance
zero disables movement lead. Homing turn rate or duration zero disables guidance.
Propagation radius or angle zero disables chain correction.

## Behavior and integration

- A challenger must remain preferred for the delay before replacing a valid lock.
  The wider release cone prevents acquire-boundary flicker. Invalid/out-of-range
  locks can be replaced immediately. The hit volume is unchanged.
- A scalar revision connects a click to the last displayed per-source ECS direction
  and target. The snapshot is validated at resolution; an absent or invalid snapshot
  falls back to current aim. A pending click holds its presentation snapshot until
  the fixed step consumes it. Pause/clear invalidates the revision. Mono stores no
  enemy entities, and a hidden direct arrow creates no displayed-direction snapshot.
- Shared lead math estimates horizontal travel time and clamps extrapolation in
  time/distance. The release cone bounds direct aiming. Dasher lead uses its authored
  normalized launch speed; ordinary bodies use punch strength as velocity change.
- Homing preserves horizontal speed and vertical velocity, without reacquiring a
  different target in flight. It ends on timeout, cone departure, invalidity,
  ordinary obstruction or meaningful contact. Solver contacts use the propagation
  impulse threshold (minimum 0.01) and reject floor-like normals. Rebounds, Dasher
  swept impacts and hybrid player-hit paths clear guidance through their existing
  contact paths. Re-punch opens a fresh budget. Dasher normalization/capture runs
  before homing. Existing ownership, damage, armor and recovery remain authoritative.
- Propagation selects only within the forward cone/radius, checks obstruction and
  caps the led correction angle. Geometry/direction rejection runs before expensive
  candidate eligibility/obstruction work. Rotating cover/enclosures preserve their
  existing acquisition exception; guidance cannot steer through their obstruction.
- Collision preview uses expanded broadphase bounds (including collider extents)
  and relative casts for moving enemies. It extrapolates for the bounded motion
  horizon, then holds the projected position. Stationary scene geometry retains
  the ordinary collider-world cast. Duplicate victims skip redundant correction.
- Returned Chicken projectiles share direction commitment, bounded lead and a short
  guidance budget. Body contact/wall bounce clears guidance while existing projectile
  pass-through and damage behavior remain intact.

## Verification performed

Unity 6000.3.10f1 imported the new scripts/metadata and compiled without new errors.
`dotnet build Assembly-CSharp.csproj --no-restore -v quiet` passed: zero errors,
318 existing package/version/deprecated-API warnings. No scene or prefab regeneration
was required; the shared punch/runtime settings retain their existing GUID bindings.

273 relevant checks passed across these Editor suites:

| Suite | Passed |
| --- | ---: |
| StableLaunchAimTests | 15 |
| LaunchImpactPreviewTests | 18 |
| SnakeEnemyTests | 44 |
| ArmoredEnemyTests | 20 |
| DeliberateShotTests | 8 |
| RollingBossTests | 31 |
| ChickenBossTests | 41 |
| BarricadeTests | 12 |
| RotatingCoverTests | 10 |
| LinkedPairTests | 23 |
| PlayerPunchCooldownTests | 12 |
| EnemyLaunchHomingTests | 2 |
| LaunchedEnemyPlayerImpactTests | 35 |
| DasherObstacleRedirectTests | 2 |

New behavioral tests cover persistent challengers, hysteresis boundaries, nearby
fallback preference, bounded/disabled lead, click/display agreement despite a changed
current lock, invalid snapshots, obstruction, guidance duration and speed/Y
preservation. Real Unity Physics contacts verify incoming guidance ends while the
victim still launches with inherited ownership, accepting a 12 degree chain target
and rejecting 60/180 degree targets. Moving-target preview tests cover bodies entering
and leaving the flight corridor. Existing Dasher armor and hybrid player impact tests
now also assert guidance cutoff without changing their damage expectations.

Final synthetic Editor profile: 100 measured updates after warmup, eight punch sources,
real broadphase/collider queries, aim plus both previews: 2.672 ms/update at 100 bodies
and 4.740 ms/update at 1,000. Simulation steps and rendering are excluded. These are
diagnostic measurements, not target-hardware frame-rate acceptance under OQ-001.

## Remaining limits

No manual campaign playthrough or target-hardware player build was performed. The
settings are conservative starting points for a gameplay feel check. The second
arrow remains an estimate: it does not simulate curved homing, future AI decisions,
falling/grounding, friction/restitution, multi-contact solver distribution, linked-pair
swing or a recursive chain. Guidance improves consistency without guaranteeing hits.

Intermittent Pipeline socket disconnects during script reload were retried; completed
results above were checked. Historical Sidekick editor-window errors and old failed
development-test logs are unrelated to the final compilation state.
