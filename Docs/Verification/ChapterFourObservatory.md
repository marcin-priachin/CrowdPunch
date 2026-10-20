# Chapter Four - Overgrown Observatory

Date: 2026-10-19. Scope: saved Campaign 31-40 scenes, VISION-004..006 and the
Chapter Four compositions and inherited requirements in `PLANS.md`.

The chapter uses muted limestone/sage damaged-brick court paving, ochre ruin masonry, old brass
instruments, masonry-coloured observatory domes, trailing exterior roots and wooded
surroundings. Joined cloisters have recessed backs, return walls, foundations and
capping courses. Closed garden escarpments and distant trees back all four sides;
subdivided exterior earth continues 250 m beyond the combined court bounds,
12 cm below the lowest original floor. Imported models are fitted uniformly and
grounded by their rendered bounds. Scenery is noncolliding and stays outside the
combined playable envelope. The original visible floors and physical meshes remain.

Each stage has its planned focal composition: broken armillary garden (31),
survey bays and tower (32), trailing-root gallery (33), three different ruined
sector vistas (34), large astrolabe (35), distant watchtower beyond the protected
approach (36), ruined relic observatory (37), multiple skyline groups around the
large terrace (38), alignment instrument and broken tower vistas (39), and a fallen
planetarium around the unchanged Dino arena and three pillars (40).

`ChapterFourObservatoryBuilder` dresses saved content without invoking campaign
creation recipes. It refuses to rebuild an existing owned scenery root. Separate
surface/appearance refresh commands preserve placement and baked receiver bindings.
Owned output is under `Assets/CrowdPunch/Data/ChapterFourObservatory`; source packs,
texture importers, earlier-chapter skins and character materials remain untouched.

The production Painted Foundry shader supplies continuous spatial pigment, cel
bands, depth/normal/Meta and DOTS support. Static surfaces use world projection;
moving objective skins use local projection. Decorative variants enable the
existing camera cutaway, with gameplay surfaces excluded. Texture copies use
2048px caps, full mips, repeat wrapping, trilinear filtering, anisotropy 4 and high
quality compression. Quiet floor detail and separate cliff/rock scales retain the
previously approved texture handling.

Chapter-owned grading retains exposure .18, contrast 3, saturation -5, bloom .015,
Neutral tonemapping and vignette .12. Sun Euler (50,-32,0), intensity 1.2,
tint (1,.94,.80); sky/equator/ground (.27,.38,.44), (.17,.25,.24), (.09,.13,.12).
Haze (.57,.68,.63) uses the approved scenery-only refinement below. The existing
scene lighting lifetime owns restoration.

## Scenery fog refinement (VISION-005/006)

The October 19 follow-up replaces Chapter Four's distance fog with a steady
arena-boundary fog bank. The user's screenshot correction supersedes the initial
50% whole-object tint, which left complete silhouettes and the horizon seam visible.
The union of the actual XZ playable floor outlines stays clear, with an additional
1 m margin. Outside it, a dense low sage bank submerges the lower portions of nearby
ruins: fully opaque up to 1.5 m above the lowest floor, feathering to 4.5 m, with
depth adding opacity. Nearby architecture retains roughly its upper half, rather
than exposing a completely outlined, uniformly tinted object. Depth fog becomes
fully opaque at 24 m beyond the arena. Exterior earth uses the same low-bank
opacity as scenery bases, becoming fully obscured by 6 m beyond the arena.
The ground and submerged objects therefore meet seamlessly, including their
baked shadows; there is no separate, weaker ground-fog curve.
The ground correction was reviewed in native centre, edge and corner views of
31/34/38, then in 16 warmed level-31 views including four outward-facing views
at the arena perimeter. The arena remains clear and the ground beneath obscured
scenery joins the same opaque bank. Unity reports no painted shader errors.

![Ground fog reaches the submerged scenery bases](Images/ChapterFourObservatory/FogGround.png)
This hides the existing horizon geometry before its boundary or corners can appear.
The painted sky matches the opaque bank and reserves its upper-sky wash for angles
above 33 degrees. Fog does not animate or change density with camera distance.

Conventional fog is disabled for these scenes, and gameplay material roles remain
zero. Characters, launched bodies, objectives, warnings and particles retain their
own presentation. The existing decorative camera cutaway remains active. The fog
only changes forward colour; depth, normals, Meta, DOTS and baked receivers retain
their existing contracts. `Refresh Chapter Four Scenery Fog Only` updates owned
materials and atmosphere without rebuilding scenery or rebaking shadows.

Validation: all 40 existing observatory Editor checks and 10 new saved-scene fog
checks pass. The new checks cover every floor envelope, scenery material role,
matching opaque sky and exclusion of gameplay materials across 31-40. All 713
checked baked-data and gameplay SubScene files retain their pre-fog SHA-256 hashes.
The direct C# build passes with zero errors and the existing 34 warnings.
The first fog pass captured 80 advancing game-camera views: four centre orbits
and four edge positions in each of 31-40. The shader's sky depth is pinned
to the far plane so it cannot draw over opaque arena geometry. All six quality
tiers passed load, retry and unload checks, restoring the previous fog state,
scenery-haze globals, sky, ambient palette and render pipeline. No new runtime or
shader errors were recorded after the final sky correction. These are Editor
checks, not a standalone-player or target-hardware performance acceptance.

The screenshot correction was then checked in 120 advancing native views: four
centre orbits, four edge views and four corner views per level. Forty additional
paired corner renders toggle the closed garden horizon mesh on/off. Their maximum
per-channel difference is 1/255, with zero pixels differing by more than 2/255:
the boundary contributes no visible silhouette or corner. Nearby arch bases fade
into the bank while their upper masonry remains visible; combat materials remain
excluded. All 713 recorded baked-data/SubScene hashes remain unchanged.

![Dense bank hides the environment boundary from the arena corner](Images/ChapterFourObservatory/FogCorner.png)

![Clear boss arena and fogged observatory surroundings](Images/ChapterFourObservatory/FogArena.png)

## October 20 boundary-offset correction (VISION-005/006)

Level 33 exposed the rectangular envelope's excess clear ground beside angled
arena edges. Saved scenes 31-40 now include the world-space convex outline of each
floor (4-8 edges per saved stage). The fragment shader measures the shortest
distance outside their union instead of outside their combined bounding box.
The existing 1 m clear margin and fully opaque ground at 6 m now follow angled
borders consistently. Material roles, bank height, geometry and baked assets are
unchanged. Global edge arrays/count are uploaded and restored by the existing
scene-lighting owner; the atmosphere refresh preserves them too.

All 10 new GPU readback cases passed: the production HLSL reports 0 m at borders
and 1/3/6 m at outward samples, with ground clear at 1 m and opaque at 6 m.
The other 40 existing geometry/material/horizon checks passed. Ten existing baked
probe checks failed because the referenced probe assets resolve to null in this
checkout; main-scene diffs contain only the new outline arrays and no bake data
or SubScene files changed. Unity C# compilation and the production painted shader
compile successfully. Direct dotnet compilation is blocked by missing generated
Temp/Bin dependency assemblies (CS0006).
Paired warmed Editor camera renders of level 33's northeast angled border confirm
the excess triangular ground strip disappears while the arena and rock border
remain clear (`Temp/HazeReview/Before.png` and `After.png`). These are static
scene renders, not a combat playtest. The final targeted GPU rerun passed 10/10.

## Damaged brick paving refinement, October 20 (VISION-004..006)

The floor now uses a chapter-owned copy of `Stylized_Bricks_005_basecolor`: painted
chipped brick faces, worn corners and irregular mortar, interpreted through the
existing shader's value detail and a quiet limestone/sage tint related to the ruins.
World projection uses .16 repeats/metre (approximately .78 x .39 m bricks), detail
strength .55, wash .06 and variation .015. No normal or specular maps are added.
The source `Court` material and its ten saved baked receivers use the same recipe.
`Refresh Chapter Four Court Surface Only` refreshes this surface without opening or
rewriting scenes, preserving each receiver's mask, UV transform and source tag.

Unity compiled successfully and the painted shader has no reported diagnostics.
The owned texture imports as 2048 x 2048 BC7 with 12 mip levels, sRGB, repeat,
trilinear filtering and anisotropy 4. Paired warmed static camera views of levels
33/34/38/40 show the worn brick joints at gameplay scale. All ten receivers retained
their exact mask/UV/enable bindings, and all 722 recorded scene/SubScene and
non-material bake-file hashes remain unchanged. These are appearance checks,
not a combat playthrough or target-hardware performance acceptance.
Repeating the refresh left all 478 checked chapter materials/brick-texture files
byte-identical. All ten saved-scene arena-haze/gameplay-material checks passed.

![Damaged brick paving beside the observatory ruins](Images/ChapterFourObservatory/BrickCourt.png)

## Masonry roofs and closed gallery corners, October 20 (VISION-004..006)

The user's gallery screenshot exposed gaps caused by individual 5.68 m roof strips
on a 5.7 m bay spacing. Those strips also stopped inside the end return walls and
used the contrasting Sage material. Galleries now have one continuous 32 cm cap
fitted to the actual arch, back-wall and return-wall vertices in building-local
axes, extending 12 cm past the full footprint on all sides. Roofs and dome skins
use the same `Limestone Decorative` material as the masonry. Existing intentionally
ruined dome geometry is retained.

`ChapterFourObservatoryRoofs.Refresh` updates just these roof objects in scenes
31-40. The ordinary structure recipe uses the same fitting operation, so future
dressing retains closed corners. Building placement, masonry modules, roots,
instruments, camera cutaways, fog and gameplay layout remain authoritative.
Changed fixed geometry uses the existing chapter shadow-bake pipeline.

All ten rebakes completed successfully. All 70 observatory Editor tests passed,
including ten new saved-scene cases requiring a single cap per gallery, at least
10 cm coverage past every masonry vertex, contact with the highest wall, matching
roof/wall materials and static bake participation. Twelve warmed oblique corner
renders across 33/38/40 were inspected with haze disabled to expose construction.
The ten gameplay SubScenes remain byte-identical; a serialized audit also matched
4,281 gameplay components and non-roof presentation poses against the prior revision.
Unity C# compilation succeeded and the painted shader reports no diagnostics.
These are static Editor checks, not a combat playthrough or standalone-player test.

![Continuous masonry roof covers the gallery end corners; haze disabled for inspection](Images/ChapterFourObservatory/RoofCorner.png)

## Objectives and hazards

- TRACK-001..006: levels 32/39 use jade survey-instrument carts, brass cases,
  pale strike panels, transfer rollers, sleepers and rail hardware. They follow the
  existing fixed-orientation solid; route points, turns, hit steps and physical
  docking remain authoritative. The socket consumes the existing state binding.
- BARRICADE-001..009: all three level-34 gates use segmented ochre masonry,
  reinforcement, accumulated branching fractures and existing tumbling/cleanup
  components. Each destination binds to its own gate and original exit radius.
- COVER-001..005: level 35 retains the exact panels/opening, slate-blue shields,
  bright ivory gap jambs, a contrasting strike face and branching core damage.
- PROTECT-001..004: level 36 has flush jade paving, exact authored boundary and
  an inlaid constellation. It adds no raised obstacle or extra UI.
- SHELL-001..006: level 37 uses breakaway jade armour, brass fittings and a distinct
  ivory/amber core. Shell/core fractures consume existing damage stages; fragment
  motion and effects use the existing presentation components. Original skin
  renderers are disabled while their authoring and transforms are retained.
- GROUND-001..007: Chapter Four patches opt into `GroundHazardStyle.Observatory`.
  Heated ceramic joints replace quarry fissures. Safe/warning/active colors and the
  full-footprint pulse still use the existing phase. The bounded batched cartoon
  smoke/flame/ember mesh supports both themed styles with the existing 20-lane
  maximum and two inactive lanes. No clocks, damage, lights or per-enemy scans change.
  Wizard zones and Trail-created hazards retain their own existing presentation.
- PILLAR-001..006: boss skins, three pillars, routes and telegraphs are preserved.

## Verification

Evidence and remaining acceptance limits are recorded below as checks finish.
Baseline scenes and matching level-31/34 editor captures are under
`Temp/ChapterFourObservatory/Baseline` and `Views`.

- Serialized baseline comparison passed 196 original authoring components,
  80 physics components and 522 original transforms, excluding the deliberate sun
  and lighting-profile changes. Encounter assets, IDs and catalog are unchanged.
- Horizon triangle tests passed for all ten stages from centre, edges and corners
  at 3 m/8 m heights in every 10-degree direction. The ten Chapter Three horizon
  regression cases also passed.
- The first saved-content audit passed explicit ECS receiver masks/charts,
  source tags, lightmap mip limits, saved probes, dynamic ancestor exclusions and
  painted-shader diagnostics. All 300 directly placed imported nature assemblies
  met the 3 cm grounding threshold. Main scenes have one loaded 2048px atlas pair
  (33,621,786 bytes in the Editor measurement) plus separate SubScene receiver maps;
  bake logs report two atlas pairs across each main/SubScene combination.
- Objective-skin checks passed all ten levels, including every gate/core face,
  four cart strike faces and rollers, local paint, exact protected-point bounds and
  two scoped hazard patches. All five owned color textures read back as 2048x2048,
  12 mip levels and BC7, with the intended sampling/import settings.
- The initial 30 visual tests passed. Six particle geometry/lifecycle tests and
  three shell fragment presentation tests passed. Three of four existing Chapter
  Four campaign tests passed; the remaining assertion expects four hits for later
  gates, whereas the saved level-34 gate settings specify three. The immutable
  baseline matches and campaign/settings assets have no diff. This visual pass
  preserves that existing gameplay value rather than changing balance to satisfy
  an unrelated assertion.
- Initial native review captured 80 advancing frames/views across 31-40 (centre
  and edge views in four directions), with the owned profile, zero runtime shadow
  distance and cleared cutaway globals. A paired cutaway inspection behind the
  southern gallery removed 55,451 substantially changed pixels and cleared globals
  after rendering. These include controlled inspection positions, not a combat
  playthrough.
- Native cart review passed forward/reverse motion, all turns, physical docking,
  four attached strike panels and retry on both 32/39. Production-resolver body
  hits were injected; the accepted forward-hit counts were 21/26 including the
  preliminary forward/reverse check. Routes and simulation were not shortened to
  match the historical five-hit hint. Native gate review passed both damage stages,
  three accepted resolver hits, tumble, complete clearing, continued room progression
  and retry on level 34. These are controlled presentation checks, not earned hits.
- Side-view inspection exposed world-AABB fitting under rotated cloister parents:
  width/depth were swapped, shrinking the side modules. Fitting now uses actual
  vertices in each imported assembly's local axes, uniform scale and a local
  grounding offset. All 258 gallery/window modules were corrected within their
  existing assemblies. Their fixed geometry requires fresh bakes; final checks
  below supersede the initial render/bake captures.
- Final fitting regression coverage passed: **40/40 Chapter Four visual tests**.
  All ten corrected bakes succeeded. Repeating the appearance refresh preserved
  all **200** captured lightmap/lighting-data/UV2 file hashes; receiver/probe/import
  checks and the immutable gameplay comparison passed again. The final 80-view
  native orbit review also passed against the corrected geometry.
- Final native relic review exercised 126 shell fragment entities through blocked
  punches, both armour fracture stages, the fresh exposed core, independent core
  damage, armour debris clearing, core-defeat campaign transition/unload and retry.
  The review harness was corrected to follow the actual automatic campaign advance
  after core defeat; it does not hold a finished level open with a parallel timer.
- Final native hazard review observed the natural inactive/warning/active cycle
  in 34, including changing flame-colored mesh geometry over at least ten active
  frames (220 vertices in the saved active capture), and the always-active patch
  in 37 (193 vertices). Safe/warning captures and sampled motion remain in
  `Temp/ChapterFourObservatory/Hazards`. Six geometry/lifecycle tests independently
  cover containment, safe phases and reset sampling.
- All six quality tiers passed native level-38 entry, retry and transition into
  unthemed level 41, restoring the prior pipeline, sky, ambient palette and global
  flags. Normal campaign progress was not written; checks used editor preview mode.
- Controlled selection of the authored level-38 final wave spawned all 120 bodies
  through the normal batching/physics path and reached 120 live roots. A 241-frame
  Editor sample measured median 16.6852 ms and p95 17.4867 ms. This capped Editor
  diagnostic is not a standalone benchmark, GPU acceptance or before/after comparison.
- Final direct C# build passed with zero errors and 34 existing warnings; none named
  changed source files. Native verification produced no new Console errors. Existing
  inactive-player-Animator warnings occurred around failure/transition during the
  controlled checks; player/animation code is unchanged.
- Representative final captures are retained in `Images/ChapterFourObservatory/`:
  `StarGarden.png`, `FallenPlanetarium.png`, `RelicDamage.png` and `HeatedCeramic.png`.
- Unity compiled the new authoring recipes and runtime style extension. A direct
  C# build with existing project-reference outputs passed with zero errors and
  34 existing package/reference/deprecation warnings. The ordinary parallel build
  failed without C# diagnostics; a serial dependency build was stopped after a long
  package-assembly rebuild. Unity remains the authoritative compiler. No standalone
  player build has been performed for this pass.

Automated scene inspection and controlled state injection do not constitute a human
combat playtest or target-hardware performance acceptance. OQ-001 remains open.
