# Chapter Four - Overgrown Observatory

Date: 2026-10-19. Scope: saved Campaign 31-40 scenes, VISION-004..006 and the
Chapter Four compositions and inherited requirements in `PLANS.md`.

The chapter uses muted jade geometric court paving, ochre ruin masonry, old brass
instruments, patinated observatory domes, trailing exterior roots and wooded
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
Haze (.57,.68,.63) starts at `max(52, combinedCourt.extents.magnitude + 30)` and
extends another 103 m. The existing scene lighting lifetime owns restoration.

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
