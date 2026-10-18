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

## 2026-10-18 baked environment shadows

- VISION-004/006: added native Progressive lightmapper shadowmasks for Chapter One's
  fixed scenery and arena geometry, with UV2 generated on owned mesh copies. Collision
  meshes, physical transforms, encounter rules and source model importers are preserved.
- Fixed render geometry is marked for static batching/GI/visibility. Barricade visuals,
  rotating cover, boss parts, characters and particles are not permanent baked casters.
- The meadow is partitioned into sixteen tiles using its original triangles/normals.
  Inner tiles have denser charts than the distant hills. Baked village visibility uses
  a hard threshold; directional cel bands and the approved post-processing remain.
- The Mixed sun supplies live directional cel lighting, while the scene-scoped pipeline
  clone sets shadow distance to zero. This disables real-time cast shadows, including
  character shadows. SSAO contact shading is retained. Unload restores the previous
  pipeline, shadowmask mode and shader flag.
- Baked maps ignore quality texture mip limits. This repairs the initial Entities Graphics
  CopyTexture source/destination mip-limit error on the Low quality tier. Static village
  shader receivers in SubScenes have explicit mask/UV-transform material bindings: the
  initial custom shader array path displayed an incorrectly shadowed floor despite valid
  bake data. The saved receiver bindings restored the rock and perimeter shadows.
- Stage 8 Play Mode: observed advancing frames, shadow distance 0 and UnityStats shadow
  casters 0. Inspected near and distant cameras after shader compilation completed. The
  main scenery and ECS floor both display baked shadows. Final verified capture:
  `Temp/BakedShadows/FinalStage8.png`; earlier diagnostic captures are not
  final results. Camera/health/debug changes are runtime-only and discarded on Play exit.
- The stage 8 saved-data test passed after adding receiver assertions. Full chapter
  bake/test results: all ten stages completed, producing 71 shadowmask atlas PNGs
  (about 20.2 MB on disk, excluding colour maps and native lighting data).
- Final checks passed: 14 BakedShadowTests, 83 CelPresentationTests and 8 CampaignTests.
  The 14 baked-data cases were rerun after adding saved-probe reference assertions and
  passed again. Unity compilation succeeded after the final code changes.
- Stage 7 live check: rotating cover angle changed from 3.25795865 to 4.77059269 radians,
  frames advanced from 1099 to 2436, all 36 cover panels lacked the ECS Static tag,
  and real-time shadow casters remained zero.
- Additive streaming initially cleared the global probe set in Play Mode despite valid
  saved probe data. BakedShadowLighting now scopes an owned probe copy as well as the
  pipeline. The final Stage 8 run retained 1083 probes through advancing frames, with
  shadow distance 0 and zero shadow casters. Transition to Campaign 11 restored shader
  flag 0, the original 110 m pipeline, no baked-only policy and no leaked probe set.
  Inspection changes were discarded; final Bootstrap is left in clean Edit Mode.
- Unity compilation succeeded. `dotnet build Assembly-CSharp.csproj --no-restore
  --no-dependencies` failed with missing package DLL metadata under Temp/Bin/Debug (132
  errors) and existing System.Net.Http version conflicts. Unity compilation is the source
  of truth; this command is not reported as passing.
- The existing Sidekick Tool Downloader invalid-window error recurred at Play entry/exit.
  Native OpenCL kernel compiler warnings and some transport timeouts occurred during bake
  setup; these do not constitute successful/failed bake results on their own.

Rebuild shadows after dressing or changing fixed geometry/sun direction using
`Crowd Punch > Art Direction > Bake Chapter One Shadows`. Dressing invalidates the
baked-only policy and restores realtime fallback until a bake successfully finishes.

## Dirt-path planting clearance (2026-10-18)

- VISION-005/006: moved 1,269 ground-plant groups from the dirt corridors onto green
  verges across Campaign 01-10. The final dressing pass now checks complete world-space
  plant bounds, the maximum noisy/feathered path envelope, arena bounds and fixed scenery.
  House vines and other attached decoration remain part of their building composition.
- Independent saved-scene audit checked 3,176 tree, bush, flower and grass groups:
  no overlaps with the protected dirt corridors or arena were found in any stage.
  Detailed results are in `Temp/Planting/Audit.json` (local verification output).
- All ten native shadow bakes completed. Intermittent texture-import setting writes
  were retried and verified; all chapter lightmap textures retain matching mip limits.
- The first baked-data test run exposed lost receiver properties during material
  refresh. Refresh now copies a fresh material snapshot into the existing asset,
  preserves its GUID, uses a matching asset name and saves the binding immediately.
  All ten receiver sets were refreshed after that fix; the rerun passed all 14
  BakedShadowTests. Production-presentation cases for stages 1 and 8 also passed.
- Unity compilation passed. Inspected saved Scene-view captures of stages 1 and 8,
  including cleared side paths and refreshed shadows. Captures:
  `Temp/Planting/Chapter1-finished-verge.png` and
  `Temp/Planting/Chapter8-finished-verge.png`.
- Left Campaign 01 and its SubScene open in clean Edit Mode. No gameplay transforms,
  colliders or encounter authoring were intentionally changed by the planting pass.

## Terrain-contact repairs (2026-10-18)

- VISION-006: corrected 1,398 floating scenery placements across Campaign 01-10,
  including 126 in Campaign 09. Inspected 4,374 ground-level model groups. The original
  smooth height formula differs from the rendered six-metre terrain triangulation;
  some offset rocks also inherited the height of a nearby tree.
- `ChapterOneVillageGrounding` now follows the actual visible meadow triangles,
  including the split shadow-bake tiles. It compares model base contact points and
  uses a small overlap to hide seams around roots and rocks. It runs after planting
  clearance in the chapter recipe. Crates on market tables, raised-bed plants and
  building-attached decoration retain their intended support and placement.
- Saved-scene grounding audit found no remaining gaps over 3 cm in its ground-level
  targets. An independent check used temporary native mesh-collider raycasts against
  the rendered terrain: 1,571 trees/rocks and 135,181 contact samples across all ten
  stages, with no failures. Temporary query colliders were destroyed after each stage.
  Local reports: `Temp/Grounding/Audit.json` and `Temp/Grounding/NativeAudit.json`.
- Scenery positions were corrected through the Unity Editor. Grounding changes only
  the vertical positions of scenery; imported models, terrain geometry, collision
  authoring and encounter layout remain unchanged.
- All ten shadow rebakes completed. Retried transient texture-import setting writes;
  final audit found no mismatched lightmap mip limits. Unity compilation passed.
- The first lighting-test run included intentionally inactive market dressing in
  stages 3, 5 and 8. The check now follows the lightmapper's active-renderer rule;
  those existing disabled objects were not reactivated. All 14 BakedShadowTests then
  passed, along with Campaign 09's production-presentation check.
- Inspected Campaign 09 roots and rock bases after baking. Final local captures:
  `Temp/Grounding/Stage9-tree-contact.png` and
  `Temp/Grounding/Stage9-rock-grounded.png`. The earlier low rock-view capture clipped
  through the foreground hill and is not the final rock preview.
- Left Campaign 09 and its SubScene open in clean Edit Mode, with no temporary
  query colliders remaining. Shader asynchronous compilation was restored.

## Chapter One gate destruction (2026-10-18)

- BARRICADE-004 / VISION-006: Campaign 04 and 06 use segmented oak barricades with
  jagged joins, contrasting timber, reinforcing rails and iron bolts. Initial widths
  remain 22 m and 28 m, with the existing four-metre height and shared three-hit rules.
  The central rotating-cover target in Campaign 07 and other chapters retain their visuals.
- Accepted hits cause a short localized warm flash/recoil, progressively exposed
  fractures, outward splinters and brief dust. Final hits burst all boards and hardware
  into noncolliding, gravity-driven tumbling debris that settles flat and disappears
  after 1.5 s. The existing collider swap still opens the route immediately.
- Saved pieces: 288 in Campaign 04, 344 in Campaign 06. Meshes/materials are owned in
  `Data/ChapterOneVillage/Gates`. No purchased assets or new packages are required.
  The chapter recipe rebuilds these visuals; the Editor refresh command changes only
  the two existing gates' visual children. Dynamic pieces remain outside static bakes.
- Unity compilation passed. Five new presentation checks passed (late-entry hiding,
  persistent damage stages, front-face chip/dust visibility and complete debris cleanup),
  along with 12 existing BarricadeTests and the saved baked-lighting checks for 04/06.
  Total: 19 focused checks passed. The subsequent palette-only edit keeps each plank's
  three sections the same colour and was refreshed/saved and inspected in Play Mode.
- Live previews: frames advanced in Campaign 04 and 06. A controlled player-owned
  launched body travelled through the real physics pipeline and caused the first hit
  in each level. Further staged captures called the existing hit resolver; they are
  presentation checks, not claims of manual combat playtesting. Intermediate hits left
  the intact collider; final hits produced the broken collider. At 1.716 s after the
  final hit, Campaign 06 had zero visible pieces and still required reaching the exit.
  Restart restored three hits, sequence zero and all 344 presentation entities.
- Final palette captures: `Temp/GateVisuals/Final-intact.png`, `Final-damaged.png`
  and `Final-breaking.png`. Older captures show intermediate presentation tuning.
  The unattended player died before a later cosmetic capture; that preview was restarted
  and protected before the final captures. All camera, health and time changes were temporary.
- Warming 64 updates and measuring 1,000 updates in an isolated Editor world with the
  344 gate pieces and 5,000 dummy crowd entities measured 0.00985 ms/update idle and
  0.02708 ms/update during hit animation. This measures only presentation CPU work;
  it excludes rendering, real crowd simulation and frame-rate acceptance.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` still fails with
  the existing 132 missing package-DLL metadata errors under Temp/Bin/Debug and four
  assembly-version warnings. Unity compilation and Editor tests are the authoritative checks.
- Left Bootstrap in clean Edit Mode; restored asynchronous shader compilation and normal
  time. No runtime inspection callbacks or saved gameplay/progression changes are intended.
