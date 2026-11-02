# Direct and ricochet aim assistance

2026-11-02 follow-up: [Launch trajectory consistency](LaunchTrajectoryConsistency.md)
adds fixed-step timing, post-physics rebound guidance, shared destruction/safety
decisions and measured forecast-versus-runtime checks. Its current horizon/cost
measurements supersede the original forecast timing and cost figures below.

Requirements: PLAYER-003/004, COMBAT-012, INFO-005. Verified 2026-11-02 in
Unity 6000.3.10f1 in the current CrowdPunch checkout.

## Behavior and settings

Direct punches retain stable target selection, movement lead and displayed-direction
commitment. Player-owned launched enemy bodies can now acquire a new eligible target
along their outgoing velocity after a meaningful horizontal scenery rebound, an
intact barricade rebound or rotating-cover reflection. The incoming lock ends first.
No target, blocked targets and disabled correction retain the physical outgoing path.
New guidance does not start another launch, refresh damage eligibility, change
ownership, or change horizontal speed/Y velocity. Boss safety deflections retain
their existing behavior.

The shared GameRuntimeSettings asset already saves a 6m propagation correction
radius, 20-degree maximum angle and 0.25s guidance duration. These settings now also
apply to rebound assistance; radius/angle zero disables correction and turn rate or
duration zero disables ongoing homing. No asset tuning or scene wiring is required.

The dotted trajectory uses the same target selection/correction after its estimated
rebounds and continues bounded homing. It uses authored barricade and rotating-cover
reflection multipliers/directions. Target contact accents follow the resulting path.
Arrow mode retains its initial-direction/first-enemy-impact scope.

## Verification

- Unity script compilation passed.
- `dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors and
  318 existing package/reference/deprecation warnings on the final build. The initial sandboxed run
  could not read the installed Windows SDK; the permitted build completed.
- `RicochetAimTests`: 13 passed, covering actual Unity Physics wall contact,
  barricade/cover rebound buffers, new target acquisition, speed/height and launch
  identity preservation, one-time correction, invalid/blocked/behind targets,
  zero range/angle, and read-only dotted prediction that hits an off-axis target
  only when rebound assistance is enabled.
- Existing `DirectLaunchTrajectoryTests`: 17 passed.
- Existing `StableLaunchAimTests`: 15 passed, including assisted direct-launch
  preview/click agreement and bounded homing.
- Existing `LaunchImpactPreviewTests`: 27 passed.
- Existing `BarricadeTests`: 12 passed; `RotatingCoverTests`: 10 passed.

Warm synthetic Editor probes at 100 and 1,000 crowd bodies measured 28.352ms and
24.762ms respectively for eight repeated 8-second/160-step assisted rebound
forecasts. A full runtime mailbox scan plus one rebound measured 0.117ms and
0.350ms/update respectively. These are isolated CPU probes, not total frame time
or target-hardware acceptance. The forecast fixture has long, low-damping flights;
its repeated identical source is a cost probe, not eight distinct simulated bodies.
OQ-001 remains unresolved.

The forecast remains advisory: later crowd motion, angular solver effects, linked
pair motion, specialized boss responses and changing/destructible geometry can
change contacts. Existing package/Pipeline transport logs predate this verification.
No campaign playthrough or new visual capture was performed for this change.
