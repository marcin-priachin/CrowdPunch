# Authoritative dotted launch path

Requirements: PLAYER-003/004, COMBAT-012/014, INFO-005. Verified 2026-11-02 with
Unity 6000.3.10f1 in the current CrowdPunch checkout.

## Result

A punch consuming a valid displayed dotted snapshot now drives its directly hit
body through the exact saved body-height polyline. It does not chase later target
motion or accept additional source deflection from the crowd. The last shown
revision is copied into a separate active buffer; later previews cannot replace it.
Removed targets do not invalidate that geometry. The preview is still ground-
projected visually, while the authoritative data retains original physics height.

`PhysicsMassOverride` temporarily makes the source kinematic for Unity Physics.
The motor writes velocity; Unity Physics writes its transform and physically pushes
dynamic victims. The original `PhysicsMass` component is unchanged, including mass
used by gameplay impact calculations. Collision damage, propagation, ownership,
armor and hit histories continue to require actual contacts. Infinite solver mass
means actual transferred impulses can differ from the old finite-mass source.
There is no second solver, scripted predicted damage or post-step position snap.

Each saved bend is visited before the next segment. Contact vertices may consume
an extra fixed step, so timing can quantize relative to the forecast. Expected
confirmed contacts permit their recorded turns. Unexpected solids interrupt and
restore dynamic physics before crossing the obstacle. Current rotating-cover/track
poses and live boss poses/linear motion are checked explicitly. A live sweep retains touching endpoints with a 2mm
margin and publishes actual contact identity/point/normal, full incoming flight
velocity and an impulse estimate from original gameplay mass. Objective systems
consume it before physics; Gatekeeper, Chicken and Rolling collision systems merge
it with solver events for the same pair, avoiding zero-impulse kinematic/static hits.
Damage is still routed through the established objective/boss rules and histories.
Unexpected contacts resolve their hit before release. Boss protection, ownership
changes and safety, plus tether and ground constraints, retain priority. A missing
victim does not remove a saved bend: the captured geometry owns source motion.

The existing duration/step budget is unchanged: 160 steps at default 60Hz capture
about 2.667 seconds, with an 8-second duration upper bound. Contact/endpoint storage
is bounded at 2,001 points. Every displayed endpoint now stops linear/angular launch
velocity (and Dasher preserved velocity), including a budget-limited endpoint.
Ordinary recovery/deferred defeat follows release; recovery is deferred while the
motor owns the path. The renderer includes a terminal dot unless a contact accent
already covers it. No hidden final-velocity coasting extends beyond the line.
Re-punch replaces the path, and reset/respawn clear it and restore mass overrides.
Arrow mode retains initial-point guidance; indirect launches remain dynamic.

The shared punch asset is already set to Dotted Trajectory. `EnemyBaker` supplies
the new disabled component, override and buffers to enemy prefabs through normal
Unity rebaking; no serialized scene wiring or saved tuning was changed.

## Verification

Rotating-target correction: the latest focused runs passed 60 AuthoritativeLaunchTests,
10 RotatingCoverTests, 17 DirectLaunchTrajectoryTests and 27 LaunchImpactPreviewTests
(114 total). Unity compilation and the C# build pass with zero errors; the build
retains 318 package/reference/deprecation warnings.

The new real-physics cases reproduce the baked 36-panel compound, central target
and stationary CoverEnclosure cylinder. Before the fix, both open-window forecast
cases failed by recording the walking cylinder as a flight contact. The forecast
and motor now ignore that cylinder while ordinary walking physics still blocks it.
Tests verify normal/Exploder core hits through the opening, shield returns without
core damage or shield detonation, a cover closing after the preview, and a return
shot using the player's impact-time position. The moving-player return test also
failed before the finish-system correction. Matching return directions retain the
saved path; a changed live return releases it with its real reflected velocity.
No scene, prefab or tuning asset changes were needed. Campaign playtesting was not
performed for this correction.

The earlier gate/boss integration runs passed all 52 AuthoritativeLaunchTests and all 17
DirectLaunchTrajectoryTests with Burst enabled. DinoPillarTests passed 29 of 31;
the two supply-count cases still expect 1/4 members and receive zero. Those fixtures
have no authoritative-path state and exercise unchanged crowd supply/placement.
The actual pillar toppling, pass-through and boss-damage checks pass.

A broader run completed 1,907 tests: 1,809 passed and 98 failed. It took about
22 minutes and remained busy after cancellation was requested. Failures include
scene/asset expectations (room counts, authored transforms, materials and clearance),
animation/presentation, the two supply cases above, and two errors caused by test-
service requests timing out while Unity was busy. No pre-change full-suite baseline
was taken, so this is not a claim that every broad failure predates this work.
The failure names/messages are saved in Temp/LaunchObjectives-wide-summary.json.
These unrelated systems/assets were not changed to make the audit green.

The prior committed guidance, comparison, aim, collision/player-impact, barricade,
shell, cover, track, Chicken, Rolling and Finale suites passed in that broader run.
The original feature pass was 249 tests. Expanded full-path coverage includes:

- Real Unity Physics integration at every scheduled vertex, within 1mm, for free
  flight, a full rebound and an enemy impact. Original gameplay mass remains intact.
- Real victim launch propagation with player ownership and one collision-damage
  history entry while the source remains on the saved path.
- A snapshot remaining usable after its target disappears, with Burst enabled.
- A new wall/boss interrupting the motor without crossing the collider.
- A cover moving into the route before the collision-world pose refreshes.
- Normal recovery and zero-health deferred defeat after a stopped path.
- Re-punch replacement, end/reset restoration of dynamic mass behavior and clearing
  the active/displayed buffers.
- Full gameplay pre/post group ordering, with motor before authored sweeps and
  finish before recovery.
- Synthetic cost probes at 100 and 1,000 bodies.
- Gate damage once per launch, destruction/pass-through and damage after ricochet;
  moving a predicted gate away does not award a hit.
- Closed shell protection, exposed core damage, track advancement, rotating-cover
  reflection, pillar toppling/pass-through and Finale anchor completion.
- Gatekeeper head damage, hand stagger and active-hand ownership, Chicken damage,
  Rolling damage/protection/ownership and Finale downed/protected windows. Direct
  Dino body damage remains forbidden; pillars retain their existing boss-hit route.
- Moving bosses can escape a saved contact using either a newly authored pose or
  velocity; a stale collision-world pose does not award damage. Fixed-player boss
  rebounds keep the saved path, while later player movement can trigger safety.
- Incoming flight speed survives a shortened final contact segment. Dasher boss
  damage and ordinary explosive detonation requests keep their established rules.
- Gates, cores, pillars and bosses moving into a previously clear saved path still
  receive their real contact before the motor releases.
- Free and reflected paths end at the final copied line point within 1mm and remain
  there through later physics steps; a budget-limited launch does not coast onward.

The existing `CommittedLaunchTests` and `LaunchTrajectoryComparisonTests` retain
coverage of the no-full-path/point guidance fallback. The new suite exercises the
full-path data and production motor rather than comparing the estimator to itself.

Unity compilation and `dotnet build Assembly-CSharp.csproj --no-restore` passed with
zero errors and 318 existing package/reference/deprecation warnings. No new package
or dependency was added. `git diff --check` passed.

Final warm Editor probes measured one direct-source preview at 2.326ms / 3.521ms with
100 / 1,000 bodies. Motor/finish processing measured 0.134ms / 0.122ms per update,
excluding physics. The motor probe supplies the scheduled transform in its fixture
to isolate CPU work; correctness cases use actual Unity Physics integration.
These are isolated synthetic measurements, not total frame time or a reliable
before/after benchmark. OQ-001 hardware/FPS acceptance remains open.

No campaign playthrough or new visual capture was performed. Tests construct the
same component layout as the baker and execute real physics, but do not establish
campaign-wide rebaking/scene validation. Moving geometry, linked-pair constraints,
boss contacts and changes to the launch can interrupt authority as described above;
target motion still determines whether a real hit occurs. Input-specific tuning
remains unresolved under OQ-017.
