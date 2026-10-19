# Chapter Three - Highland Quarry and Aqueduct

Date: 2026-10-19. Scope: Campaign 21-30, VISION-004..006 and the Chapter 3
compositions in `PLANS.md`.

The saved main scenes contain chapter-owned, noncolliding quarry surroundings:
continuous faceted escarpments and a distant ridge, complete aqueduct arcades,
cut-block stock, extraction terraces, lifting stations, supply shelters and cold kilns.
The original collision-aligned court meshes remain the visible combat ground. An
extended, subdivided gravel surface joins their irregular boundaries 12 cm below
the lowest court. Imported nature meshes retain uniform scale and are grounded by
their rendered bounds. No imported asset, shared character material or gameplay
setting is edited.

Each level follows its planned composition: armorer's court (21), extraction/storage
terraces (22), broken aqueduct crossing (23), supply causeway (24), inert mineral
cut beside the real hot ground (25), winch court (26), cold kiln walk (27), broad
quarry basin (28), valve rotunda (29) and lifting machinery around the unchanged
Rolling Blob arena (30). The two gate sectors have separate extraction and storage
dressing. Landmarks stay outside the combined playable floor bounds.

`ChapterThreeQuarryBuilder`, `ChapterThreeQuarryAssets`, `ChapterThreeQuarryScenery`
and `ChapterThreeQuarryObjectives` own the saved appearance. The dress command
refuses to overwrite an existing quarry root. The separate appearance refresh
preserves scenery transforms, baked UV2 meshes, receiver mask/ST bindings and GUIDs.
There are no new runtime systems, packages, HUD elements or enemy queries.

The existing production painted shader supplies world-projected pigment, cool
cel shadows, warm limestone highlights and restrained grain. Moving objective
pieces use object-space paint and opt-in linear ECS tint. Decorative materials
enable the existing camera cutaway; ground, boundaries and gameplay objects do
not. The sky and priority-150 post profile are chapter-owned. Post values match
the approved starting recipe: exposure .18, contrast 3, saturation -5, bloom .015,
Neutral tonemapping, vignette .12 and cool-shadow/warm-highlight split toning.
Highland sun: Euler (48,-38,0), intensity 1.25, tint (1,.95,.83). Ambient sky,
equator and ground are (.29,.39,.50), (.20,.25,.28), (.10,.12,.13). Haze begins at
`max(52, combinedCourt.extents.magnitude + 30)` and extends another 103 m.

Gate masonry, iron straps, progressive fractures and tumbling pieces consume
existing barricade state (BARRICADE-001..009). Exit cues bind to each authored
destination and its own gate; intermediate gates keep their original progression
ownership. Limestone cargo and dark rails retain the track/socket bindings
(TRACK-001..006). The valve target uses a contrasting strike face and slate cover
with chalk opening edges (COVER-001..005). All real ground-hazard shapes, state
colours and timings, protected-zone boundaries and boss presentation remain intact.

`CampaignShadowBake` receives an explicit 21-30 scene list and writes chapter-owned
UV2 copies, shadowmask settings and receiver materials under
`Data/ChapterThreeQuarry/BakedShadows`. Fixed scenery is baked together with its
gameplay SubScene; moving targets, panels, cues and bosses are excluded. The
existing scene-scoped lighting lifetime restores pipeline, probes and atmosphere.

## Verification

- Original scene snapshots: `Temp/ChapterThreeQuarry/Baseline`. Serialized comparison
  passed 153 unchanged gameplay authoring, 74 physics and 339 original transform
  blocks, excluding the intentional sun and presentation profile changes.
- Baseline render captures: levels 21, 22, 28 and 30, four directions each, under
  `Temp/ChapterThreeQuarry/Views`. These are editor renders, not combat playtests.
- Triangle horizon checks passed all ten stages from centre, edges and corners at
  3 m and 8 m above the floor, every 10 degrees. An initial test teardown failed
  when Unity's runner supplied an empty scene setup; the fixture now handles that.
- Initial pilot review corrected inward cliff-face winding and complete arch
  spandrels before baking the chapter. Texture lookup was corrected to include
  the already imported `Assets/LowlyPoly` library.
- All ten final bakes succeeded. Main scenes use two atlases each, or three in
  22/24/28; their corresponding lightmap textures occupy approximately 64/96 MiB
  while loaded, plus the SubScene's explicit receiver atlas. Saved probe counts
  range from 168 to 1,083. Chapter-owned source/receiver/mesh assets total about
  16 MB on disk, before scene lighting data.
- Thirty `ChapterThreeQuarryTests` and twelve campaign/gate/exit presentation
  regressions passed. Final post-repair results are in
  `Temp/ChapterThreeQuarry/FinalTests.json`.
  Saved receiver, mip-limit, shader, dynamic-ancestor and 250 imported-model
  grounding checks passed in `SavedChecks.txt`. The appearance refresh was executed
  and the saved checks and immutable-authoring comparison passed again afterwards.
- Native Play captured 80 advancing camera views across 21-30, including four edge
  views per stage. All used the chapter profile, zero runtime shadow distance and
  reset cutaway globals. Captures/logs are in `Temp/ChapterThreeQuarry/Native`.
  These controlled camera positions include inspection positions rather than only
  player-reachable routes; this is not a human combat playtest.
  Representative captures are preserved in `Images/ChapterThreeQuarry/StoneCourt.png`,
  `QuarryTerraces.png` and `RollingPit.png` alongside this note.
- All six quality tiers passed level-28 entry, retry, transition into unbaked level
  31 and pipeline/atmosphere/global restoration checks (`Quality.txt`). Normal
  campaign saves remained untouched; the native checks used editor preview mode.
- A native two-sector level-22 gate review passed three accepted production-resolver
  hits, both damage stages, independent tumble, complete cleanup, continued exit
  requirement and retry (`Gate22/Play.txt` and captures). Hits were injected for
  presentation review rather than earned in combat.
- Controlled selection of level 28's authored final wave spawned 110 bodies through
  the real batching/physics path and observed a peak of 108 live roots. A 241-frame
  Editor sample had median 16.692 ms and p95 17.563 ms (`Crowd.txt`). This capped
  Editor diagnostic is not a target-hardware benchmark or a before/after performance
  comparison. Broader worst-case chains and effects still require profiling.
- Final C# build: zero errors, 385 package/reference/deprecation warnings, with none
  naming the changed source files. Unity compilation and painted-shader diagnostics
  also passed. The test runner once reported a late scene-restoration error when
  Play started immediately after result delivery; subsequent verification waits for
  runner cleanup before changing mode.
- A cleanup incorrectly classified SubScene-only lightmaps as unused by looking
  only at the main scene's lightmap array. The missing explicit floor mask was
  detected immediately. All ten complete bakes were rebuilt successfully. Saved
  checks now inspect every explicit SubScene material mask and UV transform as well
  as main-scene maps; all checks and the 30 tests passed after this repair.
- Repeating the appearance refresh exposed lost source-material tags, which made
  receiver materials refer to themselves during a rebake. The refresh now explicitly
  preserves its source tag after copying properties. All 175 receiver sources were
  restored from matching owned materials or the baseline renderer references. A
  further refresh, saved checks, all 30 tests and immutable-authoring comparison
  passed. Geometry, chart transforms and gameplay were unchanged by these repairs.

- A StandaloneWindows64 build containing Bootstrap and Campaign 20-31 succeeded
  in 129.3 seconds with zero errors. Output: `Temp/ChapterThreeQuarry/Player/Quarry.exe`
  (697,440,280 bytes including data). `BuildReport.json` records the 486 warnings,
  chiefly installed Sentis shader warnings and the expected disabled runtime
  Pipeline notice; none mention Crowd Punch shaders, lightmaps or baking failures.
  Packed data includes the production painted shader, chapter assets and entity
  scene artifacts. This verifies compilation/packaging; the standalone executable
  has not been visually playtested. Native visual evidence above is from the Editor.

OQ-001 target hardware/FPS and human combat/readability acceptance remain open.

## Texture fit refinement (2026-10-19, VISION-004..006)

Implemented the agreed mapping, material separation and contrast changes in
Campaign 21-30 with `RefreshSurfaces`; no scenery rebuild or lighting bake ran.
Continuous 3D pigment removes normal-dependent coordinate breaks. Cliffs use
restrained horizontal strata; perimeter and imported small rocks use a separate
mineral recipe. Court/gravel detail, wash and grain are quieter. The new shader
path is opt-in: all 903 inspected Chapter One/Two materials retain the legacy path.

Six Chapter Three-owned texture copies import at 2048x2048, BC7 on the current
Windows target, with 12 mip levels, high-quality compression, repeat wrapping,
trilinear filtering and anisotropy 4. Original texture imports and global quality
settings are unchanged. Existing quality tiers disable anisotropy on Very Low/Low,
permit it on Medium/High and force it on Very High/Ultra; all retain mip limit 0.
Comparison views at matching cameras in levels 21 and 28 support retaining 2048
rather than increasing resolution to compensate for mapping and contrast.

Verification for this refinement:

- All 30 Chapter Three Editor tests and saved receiver/shader checks passed.
- 80 advancing native orbit/edge views across all ten levels passed the existing
  baked-shadow/profile checks. All six quality tiers passed level-28 entry, retry
  and restoration after leaving the chapter. Representative views were inspected
  for texture continuity and distance filtering; this is not a human combat test.
- Snapshot comparison of all 20 main/SubScene YAML files found only permitted
  MeshRenderer changes: every transform, collider and other component is unchanged.
  All 506 baked-lighting/mesh files and metadata match their original SHA256 hashes.
  All 175 explicit receiver masks, UV transforms and enable flags match HEAD.
- Unity reports no painted-shader diagnostics or compilation errors. The normal
  generated-project dotnet build stalled in package project-reference processing;
  building Assembly-CSharp with `BuildProjectReferences=false` passed with zero
  errors and 34 existing warnings. A new standalone build was not run for this pass.
- Evidence: `Temp/ChapterThreeTextureFit/Preservation.json`, `Views`, `Native`,
  `Quality.txt` and `DotnetBuildDirect.txt`. Matching before/after views are preserved
  alongside this note as `Images/ChapterThreeQuarry/TextureFitBefore.png` and
  `TextureFitAfter.png`.

## Barrier and core damage fractures (2026-10-19)

Level 22's two barriers and level 29's core now use tapered, branching fracture
meshes instead of thick horizontal damage bands. The second hit adds branches to
the first stage. One active fracture mesh per stage/face avoids overlapping legacy
bars. `ChapterThreeQuarryDamage.Refresh` updates these saved SubScenes without
rebuilding scenery or baking shadows; the objective construction recipe also calls
the same treatment. PLANS.md records the accepted presentation direction.

The five gate-presentation and ten rotating-cover Editor tests passed. Native
injected production-resolver hits verified both damage stages, level-22 destruction,
debris cleanup and retry, and level-29 automatic progression on destruction and intact
reset on re-entry. These checks inspect presentation and do not replace a combat
playtest. Unity compilation passed; the direct Assembly-CSharp build passed with
zero errors and five existing reference warnings. Snapshot checks allow changes
only to crack transforms, meshes and renderer visibility: gameplay components,
colliders, other placements and all 506 baked-lighting/mesh files remain unchanged.
Evidence: `Temp/ChapterThreeDamage`; saved captures:
`Images/ChapterThreeQuarry/BarrierFractures.png` and `CoreFractures.png`.
