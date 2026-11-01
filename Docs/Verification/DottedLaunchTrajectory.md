# Dotted direct-launch trajectory preview

Requirements: PLAYER-003/004, INFO-005. Verified 2026-11-01 in the Agent B
worktree with Unity 6000.3.10f1.

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
