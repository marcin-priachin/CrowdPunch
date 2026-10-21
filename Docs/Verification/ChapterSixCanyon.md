# Chapter Six - Sunscarred Canyon Outpost

2026-10-21. Visual implementation of PLANS.md for Campaign 51-60
(VISION-004..006). Gameplay authority remains in the GDD and existing authoring.

| Stage | Saved presentation |
| --- | --- |
| 51 | Cistern court with blue plated cistern shell and contrasting exposed core |
| 52 | Wind-cut outpost, signal tower and cool combat paving |
| 53 | Stationary windwheel camp framing the existing reversible cover |
| 54 | Ore-haul yard, full transfer cart, rail fittings and physical docking socket |
| 55 | Asymmetric windward ruins and layered sandstone skyline |
| 56 | Five caravan sectors: stores, cistern, cliff, windwheel and signal tower |
| 57 | Oasis supply lane with distant palms and exact turquoise protected inlay |
| 58 | Large high-desert basin, mesa formations and open cross-court sightlines |
| 59 | Open mesa with low foreground stores and distant rock formations |
| 60 | Signal towers and distant windwheel around the unchanged Chicken arena |

The original collision-aligned floor remains visible, with quiet grey paving.
Continuous exterior sand sits 12 cm below the lowest combat floor and extends
beyond the haze range. A connected mesa rim closes every horizon; service stores
have complete side/rear walls and fitted roof caps. New scenery is noncolliding.
Painted daylight, cool shaded faces and restrained chapter-owned grading retain
the production character shaders and atlas assets. Camera-side decoration uses
the established scoped cutaway; gameplay surfaces and objectives remain excluded.

Owned textures use continuous pigment, material-specific metre scales, separate
cliff/mineral recipes, full mip chains, trilinear filtering, anisotropy 4 and a
2048px cap. Imported source models, textures and importers are preserved.
Moving objective surfaces use attached object-space mapping.

Barricade/core cracks are tapered branching meshes on the original damage stages.
The cistern shell uses existing fragment, recoil and cleanup state. The ore cart
is fully skinned, with multidirectional rollers, pale strike panels, cargo,
sleepers, rail fasteners and the existing blue docking semantics (TRACK-001..006).
The protected inlay's outside edge equals the authored zone, with no collider or
raised supply stack (PROTECT-001..004). Hazards in 54/58/60 keep exact footprints,
phase clocks and damage; quiet scorched sandstone/cooling wisps progress through
full amber warning/smoke to orange-red surfaces, two-tone flames and sparse embers.
The existing maximum 20 particle lanes, safe-state suppression, depth ordering
and unload/reset behavior are reused (GROUND-001..007).

Verification:

- Unity 6000.3.10f1 compiled the implementation. The Unity CLI update check
  confirmed installed 1.0.0-beta.13 was current.
- The game-assembly .NET check passed with 0 errors and 34 existing reference,
  obsolete-API and unused-field warnings using `dotnet build Assembly-CSharp.csproj
  --no-restore -m:1 -p:BuildProjectReferences=false`. The initial full dependency
  build was stopped after rebuilding much of the fresh checkout's package graph.
  The first direct attempt reported 59 missing package output DLLs; all had current,
  successfully compiled equivalents in `Library/ScriptAssemblies`. Those equivalents
  filled only the missing ignored `Temp/Bin` outputs before the passing check.
  Project files and installed packages were unchanged. Final log:
  `Temp/ChapterSixDotnetFinal.txt`.
- All ten saved main/SubScene pairs completed `CampaignShadowBake` successfully.
  All 12 `ChapterSixCanyonTests` passed: static decorative flags, LOD0/UV2,
  saved shadow masks, explicit ECS floor receivers, valid source references,
  gameplay exclusion from cutaways, noncolliding scenery, exact protected edge
  and full-mip texture imports. The six owned texture copies read back as
  2048 x 2048, 12 mip levels and BC7 on this Windows Editor.
- Related tests passed: hazard particles 6, rotating cover 10, track object 22,
  shell target 5 and shell fragment presentation 3. Total focused checks: 58.
- Three of four existing `CampaignChapterSixTests` passed. The combined scene
  fixture failed at `CampaignBarricadeRoomTests.cs:29`, expecting four required
  hits where level 56's existing `L56_Barricade.asset` specifies three.
  `git show HEAD` confirms three was already the baseline, and that asset is
  unchanged. This visual pass preserves its setting rather than changing gameplay
  to satisfy an older assertion.
- A current-checkout baseline compared 1,106 original transforms, mesh geometries,
  collider components and non-presentation gameplay authorings across the 20 scenes.
  No gameplay or triangle-position differences remained. Ten sun rotations changed
  intentionally; 150 static renderer meshes changed vertex/index layout only during
  UV2 unwrapping, with identical triangle positions. Original physics transforms,
  collider references, floors, objective settings and wave references are preserved.
- Reapplying `RefreshSurfaces` left all 1,297 compared chapter asset/scene files
  byte-identical, including receiver masks, chart transforms, source references
  and saved bake data. Earlier chapter assets, imported source assets, campaign
  data/settings and character models are outside the changed paths.
- Live Editor preview inspected 51, 53, 54, 57, 58 and 60 at Very Low, with the
  real gameplay camera. Frame counters advanced. Level 53 observed 263 distinct
  cover angles in 12 game seconds; the opening and contrasting target stayed visible.
  Level 51's intact shell and a controlled exposed/damaged core were inspected;
  branching fractures remained visible on adjacent exposed faces.
- Level 54 naturally moved the transfer cart during the review. A controlled final
  slide exercised encounter completion. This was state injection, not proof of
  completing the route through player-launched bodies. The cart/rail geometry and
  hit eligibility retain their existing ownership. Level 57's inlay was inspected
  with a controlled close camera and independently checked against its 8 x 4m
  authored boundary. No human defense completion is claimed.
- Natural inactive/warning/active phases were observed in 54, 58 and 60, with
  changing particle mesh geometry. Level 54's retry also observed the unintroduced
  state and restarted its natural cycle. Transitioning to 53 produced an empty
  particle mesh; controlled close captures show active two-tone flames above the
  exact sandstone footprint. The existing particle tests cover containment and
  safe-state suppression. No game-runtime exceptions were observed during these
  probes. A tooling request timed out while the bake occupied Unity's main thread;
  subsequent bake/status operations completed successfully.
- The active URP asset reported shadow distance 0 and baked-only shader flag 1.
  `QualitySettings.shadowDistance` still reported 15; it is not the active URP
  shadow distance. Stopping Play restored `CelRendering`, URP shadow distance 110
  and baked-only flag 0. The Editor is left stopped on Bootstrap. Preview mode
  isolated progress from the normal campaign save.
- Diagnostic 12-second probes recorded frame medians of approximately 16.6-16.7ms
  in 54/53/58/60 with the existing frame cap. Tooling/loading stalls were included
  (up to 4.4s in the first probe), player health was restored for inspection, and
  a concurrent .NET build was running. These are advancing-frame diagnostics,
  not performance acceptance or comparable benchmarks. The stage-58 view uses its
  initial crowd, not its final or worst-case wave.

Evidence: representative captures are in `Images/ChapterSixCanyon/`.
The repeat-refresh result, preservation audit and live probe text are under the
ignored `Temp/ChapterSix*` paths in this checkout. Generated scene-dependency cache
changes were restored; `git diff --check` passes.

Remaining acceptance limits: no standalone player build/run, full human combat
playtest, six-tier comparison, exhaustive all-angle camera sweep or target-device
performance acceptance is claimed. OQ-001 remains unresolved. The existing gate
fixture mismatch is recorded above; gameplay was not changed to conceal it.

## Level 52 store-surface correction, 2026-10-21

The initial scenery inspection exposed two construction defects in the shared canyon store
recipe. Sloped canvas linework had only about 0.000165 m of clearance from the
awning's top face. Returning walls also stopped 0.015 m before the front masonry
and 0.010 m before the rear facade, leaving visible hairline corner gaps.

`ChapterSixCanyonAwningSeams` now positions each strip along the canvas normal
with 0.015 m of bottom-face clearance and keeps its ends inside the cloth.
`ChapterSixCanyonScenery.CloseStoreWallJoints` fits the returning walls exactly
between the existing front/rear wall faces. The shared recipe and saved stores
in 51-60 receive the same correction. Buildings, roofs, foundations, awnings,
objectives and gameplay retain their placement; only decorative wall joins and
linework change. Fixed shadows are rebaked for the corrected geometry.

The independent level-52 coplanar-face audit detected all four near-coplanar
awning strips before the correction and none afterward. Its remaining overlap
is between underside faces of the foundation and supply chest, hidden below the
ground. New per-stage regression checks inspect the actual saved mesh vertices
for seam clearance/containment and assert closed front/rear wall joints.

All ten final shadow bakes completed successfully. All 22 chapter presentation
checks passed, including the ten new joint/clearance cases. Repeating the
1,106-record preservation audit found no gameplay or triangle-position changes
to the original physical objects. The controlled level-52 live review advanced
279 frames over 6.003 game seconds while orbiting the affected left-side store
through 70 degrees. Three native Game-view captures show the corrected awning
linework and closed wall corners (`Images/ChapterSixCanyon/Canyon52-corrected-0.png`
through `Canyon52-corrected-2.png`). The active URP shadow distance remained zero.
Camera follow was restored and Play mode stopped on Bootstrap afterward.

## Level 52 dark arena-floor segment, 2026-10-21

The user's clarification identified the arena floor rather than the store scenery.
The saved floor's convex-fan triangulation was invalid for level 52's indented
outline: its eight top triangles included seven upward-facing triangles and one
downward-facing triangle, with overlapping coverage along the left arena edge.
Recalculated shared normals also allowed that invalid cap to affect floor shading.

`ChapterSixCanyonFloorSurface` builds a chapter-owned render mesh using the existing
ear-clipping routine in `GauntletProgressionBuilder`. The visible top now has eight
upward-facing triangles covering exactly 1,360 square metres, with separate cap/side
vertices and uniformly upward top normals. Outline coordinates, height, transforms,
navigation and the original `Campaign_52_Floor.asset` collider remain unchanged.
The original collider asset is neither edited nor replaced. Level 52's initial
dressing recipe also applies the correction, so regeneration cannot restore the fan.

Only level 52 was rebaked for this floor correction. All 23 chapter tests passed;
the new regression checks the original collider reference, top normals, surface
area and thousands of independently classified coverage samples for overlaps/holes.
The 1,106-record preservation audit reports one intentional visible-floor
retriangulation and no unintended gameplay or physical-geometry changes.

A live camera review advanced 309 frames over 6.009 game seconds along 24 m of
the affected left edge. Three native Game-view captures show continuous floor
shading without the dark wedge (`Images/ChapterSixCanyon/Canyon52-floor-fixed-0.png`
through `Canyon52-floor-fixed-2.png`). Active URP shadow distance stayed zero and
the baked-only flag stayed one. This is controlled visual verification, not a
combat playtest. Camera follow was restored and Play stopped on Bootstrap.


## Decorative supply clearance, 2026-10-21

The shared supply recipe could embed crates in store masonry and cactus branches
in the low sandstone yard returns. `ChapterSixCanyonPropClearance` now checks the
complete rendered bounds of crates (including straps) and cactus assemblies.
It moves only these noncolliding decorative props to the nearest clear point
on a deterministic 0.25 m grid, retaining ground height and at least 0.2 m of
clearance from architecture and other supply props. The complete bounds must
remain outside the authored combat bounds expanded horizontally by 2 m.
Buildings, fences, foundations, gameplay transforms and colliders are retained.
The pass is part of initial dressing and has a separate saved-scene refresh.

The saved-scene correction moved 725 decorative supply props across 51-60.
All 33 chapter presentation tests passed before rebaking, including ten new
saved-geometry checks for building/wall/prop clearance and attached crate straps.
Unity compiled the Editor code successfully. The requested runtime-project
`dotnet build Assembly-CSharp.csproj --no-restore` exited unsuccessfully with
zero reported errors or warnings; its log is
`Temp/ChapterSixPropClearanceDotnet.txt`. This is separate from the successful
Unity compilation and Editor test run; no runtime C# changed in this correction.


All ten corrected scenes completed their shadow bakes without errors.
Reapplying placement to all 892 saved supply assemblies moved zero objects.
The preservation audit compared 1,106 original transforms, mesh geometries,
colliders and gameplay authorings with zero unintended differences; its existing
approved sun rotations, UV2 recharts and level-52 visible-floor correction remain.
Close static renders of the corrected store yards in 52 and 56 were inspected:
`Images/ChapterSixCanyon/Canyon52-supply-clearance.png` and
`Images/ChapterSixCanyon/Canyon56-supply-clearance.png`. These are controlled
Editor renders of saved geometry and lighting, not a combat playtest.

After rebaking, all 33 chapter presentation tests passed again.


## Cartoon desert dust, 2026-10-21

Added sparse windblown sand puffs to Campaign 51-60 through
`ChapterSixCanyonDust`, including both initial dressing and a saved-scene refresh.
Each scene owns four emission bands around the full combat bounds, including
long multi-sector routes and the large basin. Three rounded lobes and a broad
base produce a flattened cartoon silhouette; a procedural unlit shader supplies
a pale sand highlight, ochre underside and alpha fades without bitmap textures.
The bands emit 1.4-5 puffs/second each with 4-6 second lifetimes and an absolute
cap of 32 particles per band (128 per scene). Drift runs along and away from the
arena edges. Particle extents remain outside the combat floor. Offscreen systems
pause; effects use scaled time and have no collision, triggers, lights, shadows,
GI, probes or custom runtime update script. Existing shadow bakes are retained.

Unity compiled successfully, and all ten new dust cases passed. They simulate
each saved scene's effects and check nonempty output, particle extents outside
the expanded combat bounds, bounded count, shadow/GI/collision exclusion and
shader compilation. The full chapter suite returned 42 passed / 1 failed.
The remaining failure is the existing level-60 supply chest/rock intersection
at `ChapterSixCanyonTests.cs` in the supply-clearance case. A focused rerun
reproduced it. Comparing every original serialized record with HEAD found zero
changes across all ten main scenes; this pass only adds the dust root and its
particles. No scenery, gameplay, collider or SubScene geometry was edited.

The level-52 preview advanced 308 frames over 6.005 game seconds with 58-63 live
dust particles. Native gameplay and close yard captures were inspected:
`Images/ChapterSixCanyon/Canyon52-desert-dust-0.png` through
`Canyon52-desert-dust-2.png`. The puffs remain in the sandy exterior and leave the
combat paving clear. This was controlled visual review, not combat acceptance:
the existing `EnemyAnimationBaker` source-signature mismatch was already present
in the Console before this work and recurred during preview. No animation assets
were rebuilt. Camera follow was restored and Play stopped on Bootstrap.

The runtime-project `dotnet build Assembly-CSharp.csproj --no-restore` exited
unsuccessfully with zero reported compiler warnings or errors; log:
`Temp/ChapterSixDustDotnet.txt`. This is separate from successful Unity
compilation and dust verification. No target-device performance claim is made.
