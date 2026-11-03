# Dotted direct-launch trajectory preview

Current full-path behavior: [Authoritative launch path](AuthoritativeLaunchPath.md).
Directly punched bodies now follow the captured dotted geometry, with solid,
boss and physical-constraint interruption rules and dynamic release at the end.

Current commitment rules: [Committed launch guidance](CommittedLaunchGuidance.md).
The dots now supply fixed aiming intent and conditional expected rebounds when a
valid displayed snapshot is punched; they do not become a full path-following motor.

2026-11-02 amendment: [Launch trajectory consistency](LaunchTrajectoryConsistency.md)
supersedes the original step timing and contact-response limits below. The forecast
now uses the world's ECS fixed timestep, with duration and step budget as separate
caps (160 steps cover about 2.667s at 60Hz). It shares destruction/pass-through and
post-step rebound rules with runtime. Earlier verification below remains historical.

Requirements: PLAYER-003/004, INFO-005. Verified 2026-11-01 in the Agent B
worktree with Unity 6000.3.10f1.

## Protected-target contact accents (2026-11-03)

PLAYER-003 now suppresses ineffective contact accents for intact shells hit by
ordinary/spent Explosive bodies (SHELL-002/003), rolling or temporarily protected
Rolling bosses (ROLL-001), Dino (PILLAR-001), and protected Snake heads/inner
segments (SNAKE-002/004). Fresh Explosives against shells, exposed cores, vulnerable
Rolling states, exposed Snake tips/heads and detached foreign orbs retain accents.
The gates read current target protection; they do not forecast future boss phases.
Solid contacts remain in the forecast path even when their accent is suppressed.

Unity 6000.3.10f1 compilation passed. Editor suites passed: 36/36
`DirectLaunchTrajectoryTests` (including the 100/1,000-crowd cost probes) and 31/31
`RollingBossTests`. New swept-contact regressions cover protection expiry and newly
exposed Snake tips, and confirm the preview does not change shell durability.
`dotnet build Assembly-CSharp.csproj --no-restore` was blocked by denied access to
`AppData/Local/Microsoft SDKs`; Unity compilation is the compilation evidence.
Campaign-level visual playtesting was not performed for this change.

## Configuration

Open `Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset` in the Inspector.
Under Hit Indication, select **Dotted Trajectory** instead of **Arrows**. Arrows
is the default for newly created settings; the shared asset is currently set to
Dotted Trajectory. The dotted option replaces both direct and collision
arrows and covers only directly punched, launchable enemies. Its operation is
independent of arrow length, arrow colour and first-impact prediction distance.

Dotted Launch Trajectory exposes diameter (0.22m), spacing (0.65m), RGBA colour
(white, alpha 0.5), maximum forecast duration (8 seconds) and steps (160). Settings
are read live in Play Mode. Impact diameter (1.1m, zero to hide) controls a hollow comic burst marking predicted
enemy/objective contacts. Bursts share the trajectory colour and opacity, and dots
leave space around them. The trajectory ends at estimated launch recovery,
authored defeat bounds, or the forecast time cap. Zero damping can leave a body
flying at the cap; increasing the cap extends that estimate.

## Automated verification

- Unity compilation passed without new code errors.
- `DirectLaunchTrajectoryTests`: 17/17 passed, covering travel beyond arrow length,
  recovery, swept high-speed wall contact, rebound, momentum transfer, co-punched
  movement, triggers, homing/ground projection without gameplay mutation, defeat
  bounds, direct-only output, zero arrow lengths, direction commitment and switching
  back to arrows. Also checks enemy, barricade, track and upright-pillar contact accents, blocking
  scenery, triggers/broken objectives and accent clearing. Includes 100- and
  1,000-enemy cost probes.
- Existing `LaunchImpactPreviewTests`: 27/27 passed after extracting their existing
  collision sweep into the shared `CastStep` method.
- Existing `StableLaunchAimTests`: 15/15 passed.
- `git diff --check` passed for the changed source and documentation.
- `dotnet build Assembly-CSharp.csproj --no-restore` could not complete because
  access to Unity-generated `Temp/Bin` and `obj/Debug` outputs was denied. Unity
  compilation and Editor tests are the compilation evidence for this change.

Warm Editor cost probes measured approximately 7.202ms with 100 crowd bodies and
8.041ms with 1,000 crowd bodies, each tracing eight direct sources at the default
160-step budget over ten preview frames. These synthetic measurements cover the
estimator, not a player build or total game frame time. Final hardware acceptance
remains open under OQ-001.

## Render verification

A controlled Play Mode world published two directly punched body paths (162 copied
segments). One body rebounded from a surface and the other deflected on a crowd
body; indirectly struck bodies originated no paths. The renderer generated 64
circular dots in one mesh (576 vertices, 512 triangles). The Game View capture was
visually inspected for world-space dots, path bends and absence of arrow geometry.
The test used increased dot alpha for visibility. Play Mode was stopped and the
shared asset retained Arrows as its default. Scratch capture assets were removed.
Entering Play Mode also logged an unrelated invalid `Synty.SidekickCharacters.ToolDownloader`
window. The first screenshot request rejected a destination outside the project;
capturing within the project succeeded. Neither prevented the render check.

## Contact-accent render check (2026-11-01)

The controlled Play Mode preview was repeated with one enemy contact and one
barricade objective contact. It published exactly two accents on the two source
paths. A Game View capture was visually inspected: each accent is a hollow, flat,
eight-spike comic burst in the trajectory RGBA, with a gap in the dots underneath.
The burst lies on the source trajectory at estimated contact, oriented by incoming
travel. The shared asset saved 1.1m impact diameter and retained its existing Dotted
Trajectory selection.
The transient scene was discarded on exiting Play Mode. No trajectory, physics,
damage or objective response is changed by adding the accents.

## Ground projection and occlusion (2026-11-01)

Trajectory dots, contact bursts and arrows now project onto the authored arena
floor (`ArenaGroundPlane.SurfaceHeight`), with 0.015m clearance to avoid z-fighting.
The shader uses normal `ZTest LEqual` occlusion, allowing opaque characters and
objects to cover the indicators. Body-height samples remain unchanged for launch
forecasting and direction commitment; spacing and accent gaps use XZ distances.
Standalone scenes without an authored ground singleton use a 0m floor fallback.

Unity compilation and all 17 trajectory tests passed. The bridge integration check
used a -0.75m floor with source samples still at 0m. A controlled Play Mode render
used a -1m floor and original source samples at +0.5m: every one of the 604 dotted
mesh vertices, including the two impact bursts, lay at -0.985m. A Game View capture
was visually inspected with an opaque block and capsule placed across the paths;
both covered the paths, with no dots drawn over their surfaces. Play Mode was
stopped and scratch capture assets were removed.

## Raised rotating-objective floor (2026-11-03, PLAYER-003, COVER-004)

The saved Campaign_07 turntable and its art recipe now declare a circular,
presentation-only floor surface at 0.13m above the arena plane. The ECS presentation
bridge copies this region without entity identities. Dots and contact accents
clear its top within their footprints; rigid arrows clear crossed floor regions.
Indicators outside it retain the base arena height. Scene-depth occlusion and
body-height forecast/committed path samples remain unchanged.

Unity compilation and all 18 `DirectLaunchTrajectoryTests` passed. Regression
checks cover entering/leaving the raised region, contact accents, crossing arrows,
unmodified forecast heights, bridge copying, surface removal and preview reset.
The C# build passed with existing package/reference and deprecated-API warnings.
A controlled before/after camera render reproduced hidden dots on a raised blue
disc, then showed dots and the contact burst above it while an opaque block still
occluded the path. This was a controlled rendering check, not a campaign playtest.

Follow-up: audited every saved scene containing rotating-cover authoring. Added
floor regions to campaign 17/29/35/44/53/66/72 and legacy gauntlets 14/30; campaign
7 already had one, and finale 81 has no raised plinth. The generic campaign and
legacy cover builders now author the regions on creation. A saved-scene regression
opens every rotating-objective scene in isolation and checks all ten floor regions
against their actual mesh heights and footprints. Unity compilation and all 37
current trajectory cases passed, including this inventory check.

## Forecast limits

This is an advisory direct-body estimate, not a cloned Unity Physics solver or a
committed collision plan. It uses the same launch direction and broadphase collider
sweeps as the existing preview, estimates homing, ground constraints, damping,
gravity, linear momentum transfer, restitution and friction, and stops at the
current defeat bounds. Other enemies receive the existing bounded movement lead.
After a collision, a struck crowd body is considered to depart rather than being
resolved repeatedly at its snapshot position. Its later chain reactions and
recollisions are not simulated.

Angular contact effects, linked-pair swing, special boss redirection, destructible
state changes, source composite-material differences, later crowd motion and
multi-contact solver distribution can change the actual path. At most four
contacts are processed per step. Dots are spaced along the copied polyline; extreme
total lengths reduce their density to keep geometry near 16,000 dots. Human
campaign readability and performance checks remain useful before enabling this
option by default.
