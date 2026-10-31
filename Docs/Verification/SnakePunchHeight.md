# Low Snake tip punch eligibility - 2026-10-31

Requirements: SNAKE-002/006, PLAYER-003/004.

The reported failure was reproduced with the baked legacy Gauntlet_24 Snake.
Its head grounds at Y=0 and the saved tail offset places the tip centre at Y=-0.7.
Bootstrap's punch origin is Y=0.5. The 1.2m centre gap exceeds the saved 1.15m
vertical tolerance, so the old centre-only check hid the arrow and rejected the
punch even from a clear tail-side approach. The tip's authored 0.8m sphere overlaps
that vertical band. The owner-head occlusion check correctly returned clear.

`PunchResolution.Contains(EntityManager, Entity, ...)` now tests vertical overlap
using `SnakeSegment.Radius`, delegating the unchanged horizontal footprint/range to
the existing geometry test. Enemy punch resolution, detection, aim-source selection
and presentation use the same target-aware overload. Only Snake segments gain body
height overlap; ordinary-enemy/head centre membership and elite geometry stay as
before. Protection and owner-head obstruction remain separate mandatory gates.
No prefab, settings, collider or movement changes were needed.

Verification:

- The new authored-height regression failed before the fix (missing arrow from a
  clear approach) and passed afterward. It exercises aim, displayed-direction
  commitment, punch detection, severing and impulse application; the next tip becomes
  previewable while protected predecessors remain active. Its through-head variant
  remains blocked.
- Boundary cases reject vertically separated bodies, excess horizontal width,
  targets beyond range and targets behind the punch origin.
- SnakeEnemyTests: 52/52; StableLaunchAimTests: 15/15;
  LaunchImpactPreviewTests: 18/18 passed.
- Unity 6000.3.10f1 compiled successfully. Required command-line build passed with
  zero errors and the existing 318 package/deprecation warnings.
- Controlled Play Mode check in the actual baked legacy Snake level, using a
  tail-side injected punch origin at Y=0.5 and the saved 1.15m radius: tip at Y=-0.7
  displayed an arrow; punch detached it, changed tail count 6 -> 5, and produced
  horizontal speed 90.00001. This was a scripted check, not a manual playthrough.
  Play Mode was stopped afterward; runtime setup was discarded and Bootstrap restored.

The earlier Snake tests covered protection/occlusion with same-height fixtures and
therefore missed this authored-height case. The new tests retain the production
height mismatch explicitly.
