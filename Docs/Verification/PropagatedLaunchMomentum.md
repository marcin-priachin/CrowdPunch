# Propagated launch momentum

Requirements: COMBAT-003/004/011/012 and PLAYER-003/004. Verified 2026-11-02 with
Unity 6000.3.10f1 and Burst enabled.

## Diagnosis

The authoritative source visits each stored contact vertex over a full physics
step. Its movement velocity can therefore be much smaller than its incoming flight
velocity. A touching contact may first be resolved during the following remainder
step, after the preview has already reduced the source's outgoing velocity.
Unity Physics transferred that short segment's momentum, prematurely putting an
otherwise strong propagated launch below the 15 m/s useful-momentum threshold.
No obstacle was needed to reproduce the discrepancy.

Campaign level 8 uses the shared Baseline and Ranged profiles. Read-only inspection
confirmed both prefabs use 1kg bodies, zero linear damping, 2m-high/.5m-radius
capsules, .75 restitution (Maximum) and .05 friction (Minimum). A 90 m/s source and
different target spacing reproduced the timing problem in an isolated sphere probe
with the same mass, material and horizontal radius. Final tests use the prefab's
capsule dimensions and upright inertia constraint.

| Target Z | Initial probe's first propagated speed | Fixed capsule first speed |
| --- | ---: | ---: |
| 3.05m | 12.260 m/s | 78.750 m/s |
| 3.50m | 6.354 m/s | 78.750 m/s |
| 3.95m | 0.448 m/s | 78.750 m/s |
| 4.45m | 13.573 m/s | 78.750 m/s |

## Correction

The motor retains logical incoming velocity and the last crowd contact's incoming
velocity through its remainder step. AuthoritativeLaunchContactSystem runs inside
PhysicsSimulationGroup, after PhysicsCreateJacobiansGroup and before
PhysicsSolveAndIntegrateGroup. For an actual contact with an eligible unlaunched
dynamic enemy, it raises a weakened normal constraint velocity to the finite-mass
response calculated from incoming relative speed, original gameplay masses and
combined restitution. Stronger existing solver responses and depenetration survive.

Unity Physics still applies the impulse and produces collision events. The existing
collision system interprets its solved impulse for launch propagation and damage;
no hit is awarded from a forecast, no victim position is snapped, and no second
physics world is used. The directly punched source keeps its saved geometry.
Ordinary dynamic sources, already-launched victims, protected armor/Snake parts,
unlaunchable tiers and active Dasher dashes retain their existing response.

No minimum travel distance, minimum launch speed, recovery-time extension or saved
tuning change was introduced. A genuine 6 m/s weak hit produced 5.250 m/s, while
lateral .5m/.8m hits produced 77.256/60.298 m/s instead of flattening all hits to the
head-on speed. These are controlled probes, not campaign travel-distance promises.

## Verification

- 75 AuthoritativeLaunchTests pass, including contact-timing cases, a subsequent
  ordinary dynamic chain link, once-per-launch damage history, protected-body
  exclusions, source vertex accuracy and physics-stage ordering.
- 20 LaunchTrajectoryComparisonTests, 20 CommittedLaunchTests, 35
  LaunchedEnemyPlayerImpactTests, 10 RotatingCoverTests and 13 RicochetAimTests pass:
  173 tests in the final affected regression run. DirectLaunchTrajectoryTests also
  passed earlier in this change.
- Unity compilation and dotnet build Assembly-CSharp.csproj --no-restore pass with
  zero errors and the existing 318 package/reference/deprecation warnings.
- Warm full fixture updates with a touching crowd and one authoritative source
  measured 0.715ms at 100 bodies and 1.789ms at 1,000. These include real physics,
  gameplay systems and test assertions; they are not isolated hook overhead or
  target-hardware frame-time guarantees. OQ-001 remains open.

Level 8's configuration was inspected, but a manual level-8 playthrough was not
performed. Scene, prefab, material and tuning assets were not changed.
