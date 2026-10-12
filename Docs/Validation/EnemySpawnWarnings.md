# Enemy Spawn Warnings

Requirement: INFO-006. Safe placement also preserves GROUND-006.

All shared enemy creation and pooled-return paths reserve a position and show a pulsing
amber ground ring for one simulation second before enabling the prefab hierarchy. Wave
and replacement accounting includes pending reservations. Rings use the existing shared
WizardZone material and one cached mesh draw. No scene-specific setup is required.

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

Confirm ring contrast from the gameplay camera in bright and dark campaign arenas;
verify wave, boss/pillar replenishment and retry/unload behavior during live gameplay.
These isolated checks do not establish full-scene baking, balance or GPU performance.
