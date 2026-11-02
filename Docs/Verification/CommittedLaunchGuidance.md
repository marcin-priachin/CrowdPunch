# Committed aim points and expected rebounds

Superseded for full dotted snapshots by [Authoritative launch path](AuthoritativeLaunchPath.md).
The initial-point/arrow fallback retains the behavior described below.

Requirements: PLAYER-003/004, COMBAT-012/014, INFO-005. Verified 2026-11-02 in the
current CrowdPunch checkout with Unity 6000.3.10f1.

## Behavior

Player punches consuming a valid displayed snapshot now copy its fixed world-space
aim point and expected rebound intent into ECS-owned flight state. Limited homing
uses that point instead of chasing the target's later position. A moving enemy can
escape the committed shot. The last shown revision, not a newly computed solution
after the click, supplies the snapshot. Source hit-volume/eligibility checks remain.

In dotted mode the estimator uses fixed aim points and saves up to eight expected
rebounds. Each records a surface, normal, outgoing direction and optional fixed
follow-up point/target generation. A real contact must match the next surface and
a normal within 20 degrees. The saved direction must remain outward and within the
existing propagation correction angle of the solved outgoing velocity. Correction
preserves actual horizontal speed and vertical velocity; it does not restore lost
momentum, alter damage histories or start another launch. Current boss player-safety
deflections have priority and can reject an otherwise matching commitment.

Unexpected meaningful contacts (including enemies/player and qualifying Dasher
swept hits), distinct simultaneous contact surfaces, blocked point routes,
invalid/reused targets, missing/broken expected surfaces, expiry and incompatible
corrections cancel the remaining plan. Cancellation leaves physical velocity intact
and prevents automatic target reacquisition for that launch. The enableable flight
component stays enabled as a cancelled latch until lifecycle/sequence replacement.
Re-punch replaces it. Restart/respawn clear both stored buffers and disable it.

Arrow mode commits only its displayed initial aim point; it does not secretly plan
unshown rebounds. Launches without a valid displayed snapshot and indirect propagated
launches retain their existing ordinary assistance. Chicken projectiles retain
their prior behavior and generation-aware displayed-direction validation.

## Integration and limits

`EnemyBaker` adds disabled `CommittedLaunchFlight` and empty displayed/active rebound
buffers to enemy prefabs. These are separate: aiming at a flying body again cannot
overwrite its active shot. `PresentationBridgeSystem` populates displayed data;
`PunchDetectionSystem`/`PunchResolution` copy it after the ordinary launch transition.
No enemy entities cross the Mono bridge. Scene/prefab wiring and saved tuning values
do not change; normal Unity rebaking incorporates the new baker data.

`CommittedLaunchValidationSystem` runs after impulse application and before homing.
`CommittedLaunchReboundSystem` runs after collision interpretation, Dasher impacts
and boss safety, before ordinary ricochet assistance and recovery. Matching uses
actual physics collision events and the existing authored swept contact pipeline.
No target is selected afresh during a committed rebound.

The existing 45 degrees/s, 0.25s guidance window and 20-degree flight cone constrain
point steering. Existing propagation radius/angle (saved 6m/20 degrees) constrain
rebound assistance; zero radius/angle disables rebound alignment. Compatibility is
a named 20-degree normal tolerance and an eight-rebound storage limit. The existing
fixed-step duration/work budget bounds plan lifetime (about 2.667s with 160 steps
at 60Hz); arrows use the guidance window. No corridor/full-path follower, position
teleport, speed increase, committed indirect collision chain or second solver is
introduced.

## Verification

236 relevant Editor tests passed:

| Suite | Passed |
| --- | ---: |
| CommittedLaunchTests | 20 |
| LaunchTrajectoryComparisonTests | 20 |
| StableLaunchAimTests | 15 |
| DirectLaunchTrajectoryTests | 17 |
| RicochetAimTests | 13 |
| LaunchImpactPreviewTests | 27 |
| LaunchedEnemyPlayerImpactTests | 35 |
| DeliberateShotTests | 8 |
| ChickenBossTests | 41 |
| EnemyLaunchHomingTests | 2 |
| BarricadeTests | 12 |
| RotatingCoverTests | 10 |
| ShellTargetTests | 5 |
| FinaleTests | 11 |

The new suite verifies pending-click preservation when current selection changes,
fixed aim despite later target motion, independent displayed/active buffers, actual
expected-contact application, speed/Y velocity and launch identity preservation,
wrong surface/normal, excessive turn, invalid generation/health, obstruction,
multiple contacts, expiry, ended lifecycle, re-punch replacement and reset. It also
sorts the custom groups and verifies the new systems' relative order. Two crowd
probes are included in the 20 tests.

The comparison harness now commits the recorded forecast before running the real
ECS punch, homing, physics, contact and rebound systems. In its one-second fixtures:

- Moving-target pre-contact position error is below 0.1mm at reported precision.
  After impact, maximum error is 134.2mm and outgoing-angle difference 1.597 degrees.
  The moving victim changes the physical contact response; freezing aim does not
  freeze the victim or make it collide at its forecast position. The fixture checks
  pre-impact guidance separately from a 150mm whole-flight regression bound.
- Assisted wall-bounce outgoing-angle difference is now 0.000 degrees at reported
  precision (previously 0.818 degrees). Maximum position error remains 57.8mm, RMS
  20.9mm, with a one-step first-contact timing difference.
- Stationary contact remains within 2.0mm. The largest tested position error remains
  281.4mm for boss-head deflection. Free flight/damping and destroying-hit pass-through
  retain their previous tight bounds. Multiple-contact corners keep their physical
  solution and cancel commitments.

Unity compilation and `dotnet build Assembly-CSharp.csproj --no-restore` passed:
zero build errors, 318 existing package/reference/deprecation warnings. No campaign
playthrough or new render capture was performed. The tests exercise real physics
with fixture entities; they are not a claim of campaign-wide baking/playtest coverage.

Warm synthetic Editor probes at 100/1,000 bodies measured one direct-source preview
at 8.406/6.292ms per update. Validation/homing/contact processing for one planned
source among those crowds measured 0.141/0.585ms per update. These are isolated
Editor costs, not total frame time or a before/after benchmark. Long previews and
many simultaneous sources remain costly; OQ-001 target-hardware acceptance is open.

Saved directions/points improve committed intent rather than guarantee exact body
positions or hits. Real solver contacts, moving/destructible geometry, linked-pair
constraints, target motion, special mechanics and forecast approximations can still
cause cancellation or different physical outcomes. OQ-017 input-specific tuning is
unchanged.
