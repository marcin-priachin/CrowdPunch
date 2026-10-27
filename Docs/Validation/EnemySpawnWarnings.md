# Enemy Spawn Warnings

Requirement: INFO-006. Safe placement also preserves GROUND-006.

All shared enemy creation and pooled-return paths reserve a position and show disturbed
earth for one simulation second before enabling the prefab hierarchy. A cracked mound
heaves while faceted dirt clods tumble out and small cartoon dust puffs drift from its rim.
Wave and replacement accounting includes pending reservations. Soil uses one immutable,
GPU-animated instanced mesh; dust reuses the shared SporeCloud material. No scene-specific
setup is required. This implements INFO-006 in the VISION-006 art direction.

## Digging presentation verified 2026-10-27

- Unity 6000.3.10f1 imported the material, shader and scripts and compiled successfully.
  The soil shader reports supported and GPU instancing is enabled on the material.
- Final `dotnet build Assembly-CSharp.csproj --no-restore -m:1` passed with zero errors;
  existing package assembly conflicts, deprecated API and legacy-aspect warnings remain.
- Rendered and inspected the mound, cracks, clods and existing cartoon dust at .15/.55/.9s
  with a Unity preview camera. Confirmed instanced soil rendering in Campaign_01 Play Mode
  using temporary warning-only entities; stopped Play Mode afterwards. This is a controlled
  presentation check, not a full encounter playtest.
- Isolated real presentation-system checks include 500 Disabled reservations, an elevated
  arena floor, overdue warnings that continue digging, and cancellation clearing submitted
  instances. Also exercised 1100 warnings across the 1023-instance batch boundary. No changes
  to activation timing, ground clearance or enemy emergence animation.
- 500 simultaneous warnings, 10 warmup and 60 measured updates: approximately 2.8-3.1 ms/update
  CPU and zero managed bytes/update after warmup. Soil shares 474 vertices in one instanced
  draw; 3000 dust quads share a second draw. Measurements exclude GPU cost, live crowd
  physics, baking and camera rendering. OQ-001 hardware/FPS acceptance remains unresolved.
- Shared sprite-mesh disposal now uses DestroyImmediate in Edit Mode, so isolated ECS-world
  verification also releases its dust mesh cleanly.

## Verified 2026-10-12

- Unity 6000.3.10f1 imported the new scripts and metadata and compiled successfully.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` passed with zero errors.
  Existing package assembly conflicts and deprecated aspect warnings remain.
- Isolated Editor ECS-world checks: root and linked child remain Disabled at .5 seconds;
  both enable at 1 second, and the warning component is removed.
- Player overlap holds activation; an active ground-hazard patch holds activation;
  removing the obstruction lets the same reserved point activate.
- A pending wave enemy with a stale run generation is destroyed.
- 500 simultaneous warnings, 10 warmup and 120 measured updates in an isolated Editor
  world: warning processing averaged .314 ms/update; cached ring presentation averaged
  .057 ms/update. The shader reports supported. These are CPU-only microbenchmarks,
  excluding crowd physics, baking, camera rendering and GPU cost. Geometry rebuilds
  when reservations change, rather than on every pulse.
- Diff whitespace check passed.

## Remaining playtest checks

Confirm dirt/dust contrast from the gameplay camera in bright and dark campaign arenas;
verify wave, boss/pillar replenishment and retry/unload behavior during live gameplay.
These isolated checks do not establish full-scene baking, balance or GPU performance.
