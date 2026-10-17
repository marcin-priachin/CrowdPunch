# Chapter One: Sunlit Border Village

Implemented 2026-10-16 for VISION-004, VISION-005 and VISION-006.

All ten existing campaign stages retain their encounter names and progression IDs.
Their visual settings are:

| Stage | Environment |
| --- | --- |
| 01 | Orchard Commons |
| 02 | Stonecutters Lane |
| 03 | Market Court |
| 04 | West Village Gate |
| 05 | Caravan Square |
| 06 | Mill Courtyard |
| 07 | Bell Tower Garden |
| 08 | Harvest Fairgrounds |
| 09 | Watchtower Approach |
| 10 | Gatekeepers Keep |

Chapter-owned cel materials, textured courtyard paving, limestone boundaries, terracotta
houses, foliage, paths, fences, props, landmarks and rolling meadow backgrounds are
saved in the scenes. Decorative scenery has no colliders. Landscape placement adapts
to each floor, including the 100 m wave arena and circular Gatekeeper arena.
Trees that intersect building bounds are removed from the authored composition.

The production Full Cel Volume remains at priority 150: Neutral tonemapping, exposure
.25, contrast 5, saturation 4, bloom .06 and vignette .12. A priority-160 chapter Volume
overrides only white balance (temperature 4). Warm directional light, cool ambient fill,
sky and distance haze are scoped to each additive level by `CelLevelLighting`.
The optional sky/fog override defaults off outside Chapter One.

## Verification

- Unity compiled the final C# successfully. Final CelPresentationTests: 83 passed,
  including all 81 campaign scenes, game prefabs and Bootstrap/profile checks.
- CampaignTests: 8 passed, including chapter assets, progression and boss geometry.
- Compared every serialized SubScene component against the original Git revision.
  All existing transforms, colliders, mesh filters, encounter and objective references
  remain unchanged. Differences are renderer materials/visibility, whitespace, and
  Unity persisting existing default fields (`overrideGroundSurfaceHeight: 0`,
  `groundSurfaceHeight: -1`, `finaleBoss: null`). No SubScene objects were added/removed.
- Visually inspected Play Mode stages 01, 03, 06, 08 and 10. Wider inspection camera
  positions were temporary and were not saved to Bootstrap. Frame-count checks confirmed
  the player loop advanced; simulation was subsequently frozen for visual inspection.
- Corrected foliage atlas border artifacts, open house gables, overlapping tree canopies,
  single-sided banners and paving outline extraction for the round boss floor.
- Runtime volume checks confirmed Neutral tonemapping, .06 bloom and temperature 4.
  Transitioning from Chapter One to Campaign 11 restored fog=false, Default-Skybox and
  temperature 0. No chapter atmosphere leaked into the following chapter.
- No new game runtime errors were observed. The pre-existing Synty Sidekick downloader
  invalid-window error recurred during Play Mode entry. Imported `.blend` files also
  had pre-existing missing-Blender errors; this pass uses the supplied FBX models.
- `dotnet build Assembly-CSharp.csproj --no-restore` failed during generated project
  reference resolution without reporting compiler errors. A single-worker retry rebuilt
  many Unity package projects with MSB3277 dependency warnings and was cancelled after
  several minutes. Unity compilation and the Editor tests above are the verified checks.
- No standalone player build, full campaign combat playthrough or target-hardware
  performance benchmark was performed.

Preview captures are outside Assets in the ignored `Temp/VillagePreviews` folder.
Bootstrap is left ready to preview Campaign 01 on the next Editor Play session.

The explicit authoring command is `Crowd Punch > Art Direction > Dress Chapter One -
Sunlit Village`. It replaces the generated scenery root and reapplies the chapter's
visual settings; make manual edits inside that root after running the recipe.

## 2026-10-17 ground, shading and density refinement

- Replaced the meadow's three triangle-level colour materials with continuous world-space
  colour variation, imported grass texture and noisy, feathered grass/earth path edges.
  The existing landscape mesh silhouette is unchanged.
- Removed the paving overlay 8 mm above the combat floor and the thin rectangular road
  slabs. The original ECS floor now receives world-projected paving directly. Its mesh,
  height and collider remain unchanged.
- House footings are wider than their walls, eliminating the overlapping vertical faces
  at their bases. Chapter sun bias is explicitly configured independently of pipeline bias.
- Added a chapter-owned opaque cel surface shader with three directional bands, cool fill,
  cast shadows, SSAO contact shading, fog, world-projected texture detail and DOTS support.
  It includes shadow, depth and depth-normal passes. Player/enemy shaders, shared production
  post-processing, source model packs, source texture imports and other chapters are unchanged.
- Lowered Chapter One ambient fill to preserve shaded faces. Increased village houses and
  background trees, with planted verges, flowers, benches, produce, stored timber, trellises
  and wider doorsteps outside the authored combat area. Grass and vine colours match the
  chapter palette. The existing building/canopy exclusion pass remains in use.
- All ten stages rebuilt successfully. No campaign SubScene has a Git difference.
- Unity C# compilation succeeded. The command-line `dotnet build Assembly-CSharp.csproj
  --no-restore --no-dependencies` exited unsuccessfully with zero reported warnings/errors;
  it is not treated as a successful compilation check.
- All 83 CelPresentationTests passed after the final material/scenery rebuild, including
  the 81 campaign scenes and prefab/Bootstrap checks. The new shader reported no compiler
  messages after exercising both GameObject scenery and baked ECS floor rendering.
- Inspected final Play Mode stages 01, 06, 08 and 10 from gameplay and/or temporary overview
  cameras; frame counters advanced (for example 5 to 407 in the final session). Close-up
  captures from shifted camera positions show clean wall/footing separation and ground
  contact shading. These are visual checks, not a full combat or performance benchmark.
- Transition from Campaign 10 to 11 restored fog=false, Default-Skybox and white balance
  temperature 0. Chapter One retained temperature 4 with the shared Full Cel profile.
- The existing Sidekick Tool Downloader invalid-window error recurred on Play entry.
  An inactive-player Animator warning also appeared after an unattended combat death.
  Neither originates in this environment change. Some inspection requests timed out during
  Editor loading; subsequent requests and rendering succeeded.

Preview captures for this refinement are in the ignored `Temp/VillagePolish` folder.

## 2026-10-17 building assembly repair

- VISION-005/006: replaced the mismatched imported gables and box walls with two shared,
  closed house-shell meshes, retaining the tiled roofs, windows, doors and timber trim.
  All 134 houses across Campaign 01-10 now use these shells. At 142 samples along both
  gables, the shell overlaps the tile underside by at least 0.087 m and stays at least
  0.106 m below the outer roof surface. The geometry check used the actual imported mesh,
  including the curved eaves, rather than its bounding box.
- Reassembled mills in Campaign 06/08: the annex stands beside the tower, connected by a
  recessed low passage. The sail plane clears both buildings; an axle joins it to the tower.
  The former coplanar tower/house side walls and intersecting house roof are removed.
- Removed the decorative repeating wall map from chapter-owned Cream plaster. A quiet
  procedural wash preserves warm plaster variation with existing directional cel lighting,
  contact shading and hard shadows. Other surface materials skip the extra wash noise.
- Rebuilt all ten stages. Saved-scene audit: 134 house shells, zero obsolete imported
  gables, 137 roofs with 137 clean shadow proxies, no scenery colliders and all scenery
  MeshRenderers fixed to LOD0. Encounter authoring remains in the original SubScenes.
- Unity C# compilation succeeded. Presentation tests for Campaign 01, 06 and 08 all passed
  (three cases). Village Surface reported no shader compiler messages after Play rendering.
- Inspected Campaign 06 from front and rear, including close roof joins, and Campaign 08
  from the opposite front angle. Captures are in `Temp/VillageBuildings`. Runtime inspection
  camera/health changes were discarded; Unity was returned to clean Bootstrap Edit Mode.
- The pre-existing Sidekick Tool Downloader window error recurred at Play entry/exit.
  One authoring request timed out at the transport layer; the builder continued and completed
  all ten stages without an authoring error. No new runtime error was identified.
