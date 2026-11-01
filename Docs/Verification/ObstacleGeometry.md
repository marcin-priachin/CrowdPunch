# Obstacle collision surfaces

2026-11-01. Requirements: VISION-004, VISION-005, VISION-006, COMBAT-018.

The Level 8 rock mismatch came from `SolidObstacleBaker` substituting the authored
navigation rectangle for the visible rock. Perimeter rocks also retained oversized,
elevated BoxColliders after their original cube visuals were replaced.

Static solid obstacles now bake their visible child triangle surfaces, including
concave cutouts and disconnected pieces. Child transforms and mirrored scale are
included. Shadow-only and disabled renderers do not add invisible collision.
All 894 perimeter BoxColliders were replaced with nonconvex MeshColliders sharing
the rendered meshes. Builders produce the corrected configuration on regeneration.
The player's movement sweep is anchored to the arena floor rather than its elevated
visual root, so visible low rocks still contain the player.

The initial audit inspected all 230 scene files under `Assets/CrowdPunch/Scenes`:

| Content | Solid obstacle authorings | Perimeter colliders corrected |
| --- | ---: | ---: |
| Campaign 1-81 | 201 | 666 |
| Legacy Gauntlets 1-31 | 61 | 228 |
| Navigation validation and other scenes | 14 | 0 |

Only perimeter collider components and their GameObject component references changed
in the 112 saved gameplay SubScenes. A serialized-document comparison against HEAD
verified all other scene data remains identical, including transforms, mesh/material
references, objective configuration, navigation data and lighting references. Floor
colliders retain their original convex surfaces. No lighting rebake is needed.

Validation performed in Unity 6000.3.10f1:

- Unity compilation completed with no C# errors.
- `ObstacleGeometryTests`: 234/234 passed. Covers all 230 saved scene files,
  independent triangle/raycast comparisons on solid obstacles, 96 outward player
  sphere sweeps per saved perimeter, nonconvex mesh references, disconnected surfaces,
  scaled/rotated/mirrored children, and actual player movement resolution with
  different visual-root heights.
- `GauntletProgressionTests`: 32/32 passed.
- `NavigationTests`: 29/29 passed.
- An isolated real baking invocation for Campaign 8 produced two Mesh colliders
  from its two solid rock authorings. Its live ECS world also contained both meshes
  and the aggregate floor/perimeter compound. Horizontal environment raycasts hit
  the changing sloped rock edge rather than the former rectangular boundary.
- Campaign 8 ran with its first authored crowd, then a temporary runtime-only stress
  population of 270 enemies. After 60 warm-up frames, 180 timing samples over 239
  advancing frames reported CPU median 16.663 ms / p95 18.342 ms and frame delta
  median 16.671 ms / p95 16.790 ms. These are Editor snapshots, not a controlled
  before/after benchmark or target-hardware performance acceptance (OQ-001).
- `git diff --check` passed.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q` exited 1 with
  `Build FAILED`, zero warnings, zero errors and no diagnostic explanation.
  Unity compilation and tests are the verification source of truth for this pass.

Play mode was stopped and the original `New Scene 1` Editor scene restored. Runtime
stress entities were discarded. Campaign preview mode prevented ordinary campaign
progress writes. The existing unrelated Sidekick Tool Downloader window error was
observed; no attributable runtime baking or collision error was found.

Navigation retains the existing authored rectangles and clearance rules. Those are
route-planning approximations, distinct from the now surface-matched physical
colliders. Decorative scenery remains noncolliding. Moving/destructible objective
and character collision rules were not changed. This pass includes automated scene
coverage and a focused live check, not a manual playthrough of every encounter.
