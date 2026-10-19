# Campaign Visual Update Plan

Created: 2026-10-18. Status: Chapters 2 and 3 are implemented; Chapters 4-9 remain planned.

Chapter 3's Campaign 21-30 scenes now use the Highland Quarry and Aqueduct
compositions below, chapter-owned painted materials, objective masonry/cargo skins,
scoped decorative cutaways and saved baked-only lighting. Gameplay authoring,
physical transforms, encounters and character models are preserved. Verification
and remaining acceptance limits are recorded in `Docs/Verification/ChapterThreeQuarry.md`.

Chapter 2 uses the Emberworks Foundry compositions below. Saved Campaign 11-20 scenes,
owned materials, objective skins and chapter-owned baked-shadow data are implemented.
Editor, objective-state, quality-tier and Windows player-build verification, along with
remaining performance/manual-acceptance limits, are recorded in `Docs/Verification/ChapterTwoFoundry.md`.
All Chapter 2 levels use the approved classic painted-cartoon treatment established
in level 11: pigment materials, drawn roof seams/window glazing and restrained grading.
The shared recipe preserves baked visibility and adjusts haze to each arena's size.
Camera-side decoration uses a scoped shader cutaway to protect the view of the player;
floors and gameplay obstacles remain visible and scene layouts are preserved.
The cross-chapter visual requirements below include the user's approved Chapter 2
shading, placement corrections and camera cutaways. Every later chapter and visual
refresh must carry them forward; a new setting does not reset these decisions.
The broader cross-chapter checklist remains open until its evidence is recorded.

On 2026-10-19 the user separately authorized bringing Chapter One's saved levels into
this painted language while preserving every object's placement. Campaign 01-10 now
use chapter-owned pigment/atlas materials, restrained grading and decorative camera
cutaways through `ChapterOnePaintedLook`; the original village composition and baked
lighting remain the reference. Evidence is in `Docs/Verification/ChapterOneVillage.md`.

## Scope and authority

Bring Chapters 2-9, Campaign 11-81, up to the finished Chapter One visual standard.
Keep Chapter One as the reference and regression suite. The campaign now contains
81 levels and nine chapters; historical documents describing level 80 as the finale
are superseded by FINALE-001..007 and the current catalog.

This is an environment and objective presentation pass. Preserve encounters, stable
IDs, campaign chapter/level names, progression, saves, enemy introductions, populations,
physics, floor geometry, navigation, objective settings and boss layouts. The proposed
setting names below are visual production labels, not replacements for catalog titles.
Preserve the latest player/enemy/boss models, atlas detail, deformation and readable
animations. Change their lighting integration only when needed to fit the environment.

Sources, in priority order:

- [AGENTS.md](AGENTS.md), [GDD](Docs/Design/GDD.md) and
  [OpenQuestions](Docs/Design/OpenQuestions.md).
- Current scenes and `Assets/CrowdPunch/Data/Campaign/Campaign.asset`;
  [CurrentArchitecture](Docs/Architecture/CurrentArchitecture.md).
- [Chapter One visual implementation and corrections](Docs/Verification/ChapterOneVillage.md).
- [Campaign encounter table and subsequent amendments](Docs/Design/Campaign80Proposal.md).
  Earlier chapter verification files are historical evidence, not permission to restore
  obsolete short tracks, single-room barricades or old enemy compositions.
- Legacy examples 28-31 are composition references for Village, Emberworks Foundry,
  Overgrown Observatory and Frostbound Vault. Their old experimental lighting,
  coplanar floor overlays and Art Lab preview setup are not production templates.

## Decisions already made

These are requirements inherited from the user's decisions and Chapter One/Two corrections.

1. **One Full Cel style throughout.** Environment, player, enemies, lighting and
   post-processing use the approved production look in builds, without Art Lab
   substitution (VISION-006). Backgrounds retain the approved classic hand-painted
   cartoon treatment established in Chapter 2. Distinguish chapters through setting, silhouette,
   ground treatment, palette and atmosphere rather than unrelated rendering styles.
2. **Readable action first.** Keep the player, enemy roles, launched bodies, aim
   previews, hazards and objectives legible at the actual game-camera distance
   (VISION-001..005). Scenery is quieter than the combat. Preserve exaggerated
   animation and transient hit feedback; do not add enemy health bars or HUD clutter.
3. **Directional form, not flat colour.** Maintain clear lit/shaded cel bands, cool
   fill and restrained contact shading. Ambient light must not wash out object form.
   Texture variation supplements lighting rather than replacing it with uniform tint.
4. **Hard shadows and fixed detail.** No soft shadow treatment or automatic LOD
   switching. Keep LOD0 and prevent distance-based detail/shadow popping. Extend
   Chapter One's baked-only environment policy to the remaining chapters as part of
   this plan; do not treat the old runtime shadow-map fallback as the finished result.
5. **Static means immutable.** Fixed scenery is static and baked. Characters,
   rotating cover, moving track blocks, destructible targets, toppling pillars and
   effects are excluded from permanent caster shadows. Preserve cel lighting,
   probes and contact shading on moving receivers. Do not introduce realtime
   character/object shadow passes as a workaround.
6. **Finished surroundings.** Every stage gets composed near, middle and distant
   scenery, a deliberate skyline and a setting-specific landmark. Avoid empty grey
   backgrounds, abrupt terrain edges and featureless borders. Keep foreground
   silhouettes low enough to see enemies and combat routes.
7. **Minimum asset spend.** Start with imported assets, owned material variants and
   small generated meshes. No purchase or additional package is assumed by this plan.
8. **Preserve the camera view without changing layout.** Camera-side decorative
   objects must reveal the player and combat through the established scoped cutaway.
   Keep approved object placement, camera framing, collision and gameplay visibility.

## Cross-chapter visual requirements

These requirements apply to Chapters 3-9, subsequent Chapter 2 refreshes and updates
to any approved background. Chapter One remains a protected regression reference;
this plan does not authorize rebuilding or relocating its scenery. The separately
requested appearance-only refresh above preserves that reference. The chapter themes below change palette,
architecture and atmosphere within this visual language. Preserve approved layouts
during shader, material, post-processing, cutaway and objective-effect updates.

### Hand-painted backgrounds, shading and grading

- Keep the classic cartoon-background reference: broad painted colour shapes,
  restrained gouache-like pigment washes, subtle dry-brush grain, selective drawn
  construction lines and simplified detail. Simple geometry must read as an
  illustrated place through coherent materials, lighting and composition.
- Retain directional cel form, cool coloured shadows, warm or chapter-appropriate
  highlights and quiet contact shading. Preserve the hard baked-shadow policy,
  LOD0 and probe integration. Keep combat ground at a readable middle value with
  low texture contrast; scenery and surface marks must not compete with action.
- Use imported colour textures selectively, with broad tonal variation and sensible
  construction scale. Keep drawn seams, glazing/reflections and ink accents attached
  to the relevant surface. Avoid photoreal PBR shine, strong normal/specular detail,
  pervasive thick black outlines and full-screen painterly blur as substitutes for
  the approved material treatment. Preserve player/enemy atlas detail and animation.
- Pigment must stay stable during camera motion: world projection suits fixed ground
  and scenery; local/object mapping suits moving or breaking pieces and painted signs.
  Filter grain and fade subpixel detail toward the skyline. Distant layers use quieter
  detail, cooler/haze-related colours and reduced contrast while retaining silhouettes.
- Use chapter-owned materials and scoped post profiles. The approved Chapter 2
  priority-150 profile copies FullCelPost and retains Neutral tonemapping, HDR, SMAA
  and vignette .12; its overrides are exposure .18, contrast 3, saturation -5 and
  bloom .015, with restrained cool-shadow/warm-highlight split toning. These are the
  starting values for later painted chapters. Tune chapter atmosphere deliberately
  and record changes; preserve readability and restore the prior profile on unload.
  Do not reset an approved painted scene to the older shared grading values.
- Keep haze beyond the readable combat space, including wide arenas and long room
  chains. Chapter 2 starts it at `max(52 m, arena.bounds.extents.magnitude + 30 m)`
  and fades over another 103 m. Adapt to the full stage bounds when necessary;
  haze must not hide opponents, routes, warnings or objectives, or conceal an
  unfinished backdrop. Exterior ground continues beyond the visible/haze range.
- Preserve stable contact shading: the current CelRenderer uses full-resolution
  interleaved-gradient sampling, with compensated radius and restrained intensity.
  Check stationary repeated renders and moving views when changing this path.
  Flicker at geometry joins also requires a depth-surface audit.
- Future material/shader recipes must preserve DOTS, depth/normal, Meta and baked
  receiver contracts. Keep each explicit receiver's shadowmask and UV transform,
  stable material GUIDs and source references. Animated entity tint is opt-in;
  provide linear RGB to the painted shader and track material baking dependencies.
  Retain the level-12 shell and level-13 barrier skins when refreshing Chapter 2.

The implementation references are
[ChapterTwoPaintedLook](Assets/CrowdPunch/Scripts/Editor/ChapterTwoPaintedLook.cs),
[PaintedFoundry.shader](Assets/CrowdPunch/Shaders/Cel/PaintedFoundry.shader) and
[the approved Chapter Two verification/corrections](Docs/Verification/ChapterTwoFoundry.md).
Reuse their demonstrated rendering contracts with chapter-owned output; their
scene numbers, foundry palette and asset paths are not a batch recipe for other chapters.

### Background placement and complete surroundings

- Compose a connected place with functional relationships: joined building frontages,
  returning wings, service/loading areas, supported infrastructure and deliberate
  near/middle/distant layers. Each stage retains its own landmark and arrangement.
  Avoid isolated props distributed around an arena like a display collection.
- Cover every camera-facing side, including the side behind the player. Inspect full
  orbits and outward views from the centre, edges and corners, at gameplay and raised
  camera heights. Close sightlines through building gaps and behind the first row
  with return walls, recessed backing and further skyline/terrain layers. No side
  may expose a void, abrupt ground edge, missing building back or empty horizon.
- Preserve all approved scenery transforms and the immutable gameplay footprint in
  appearance-only refreshes. New chapter dressing belongs outside combat, spawn,
  navigation, objective and boss-motion clearances. Decorative scenery stays
  noncolliding. Low foreground forms and intentional sightlines complement cutaways.
- Fit imported models uniformly using their actual mesh axes and bounds; handle
  zero-thickness axes without division by zero. Keep machinery, doors, supports and
  architectural modules in believable proportion to each other. Build long pipes
  from constant-diameter runs, correct bends, flange joints and supports; do not
  stretch a pipe's cross-section to fill a placement envelope.
- Fit roofs, gables, facades, footings and internal/back faces as complete assemblies.
  Inspect undersides and side views. Use butt joints, deliberate depth clearance or
  recessed backing at adjoining bays, piers, windows, drains and loading steps.
  Shared exposed planes at the same depth are forbidden, including overlapping roofs
  and paving tops. Resolve competing geometry before changing lighting parameters.
- Keep the original collision-aligned arena floor as the visible combat surface.
  Exterior earth must join curved and irregular arena boundaries continuously; four
  rectangular strips are insufficient when they expose gaps. Keep exterior ground
  below the real floor with clear depth separation. Subdivide large exterior meshes
  sufficiently for local fog interpolation; a giant four-vertex plane can fog the
  nearby yard incorrectly. Chapter 2's exterior separation is 12 cm.
- Ground final models against rendered triangles or their intended support surface,
  after horizontal placement and assembly. Apply the 3 cm unsupported-gap audit
  threshold below, then visually inspect broad/sloped bases. Preserve intentional
  raised/support-mounted props. Check full swept volumes for moving decoration.
- Material, cutaway and objective-effect refreshes preserve existing baked lighting
  when geometry/sun placement is unchanged. Any fixed-geometry correction requires
  refreshed bake data and receiver validation before completion.

### Camera-side decorative cutaways

- Carry `CameraSceneryCutaway` and the `SceneryCutaway.hlsl` contract into every
  future environment material/shader used for camera-side decoration. Open a tapered
  camera-to-player corridor only for fragments between the camera and focus; scenery
  beyond the player remains complete. Current defaults are a 4.5 m camera radius,
  3.5 m player radius and 1.2 m feather with stable ordered coverage. Retain these
  defaults unless verified framing requires documented tuning.
- Enable the effect through explicit decorative material variants/flags. Exclude
  floors, exterior ground, collision boundaries, gameplay obstacles, cover, targets,
  shell/core pieces, track objects, exit cues, hazards, player and enemies. Shared
  source materials must not accidentally opt gameplay objects into the cutaway.
- Match clipping in colour, depth-only and depth-normal passes so removed scenery
  leaves no solid depth or AO silhouette. Keep full geometry in Meta and ShadowCaster
  passes so the baked environment and shadows remain intact. Do not move, delete or
  rescale objects, change collision, or shift the camera to produce the cutaway.
- Feed the focus from CameraFollow's final look target after camera feedback offsets.
  Scope shader globals to the active following gameplay camera's render. Scene views,
  previews, secondary cameras and inactive follow cameras see complete scenery.
  Clear globals after rendering and on disable; verify additive transitions and retry.
- Keep the existing bounded shader approach: no per-frame scenery scans, enemy ECS
  queries, physics raycasts, per-object material instances or extra transparency
  sorting path are needed. Ensure every newly introduced decorative shader supports
  the same cutaway before accepting its camera-facing placement.

## Chapter One and Two failures that must not recur

| Observed problem | Required treatment in every future chapter |
| --- | --- |
| Jitter where buildings/floor layers meet | Use the original collision-aligned floor as the visible ground. No nearly coplanar paving or road slabs. Give footings clean separation or deliberate overlap without shared vertical faces. |
| Blocky grass/earth boundaries | Blend world-projected ground materials with noisy, feathered masks. Share the same path definition with planting exclusion; do not assign broad colour patches per terrain triangle. |
| Empty scenes | Add useful clusters of setting props, framed vistas and layered silhouettes outside gameplay space. Density must be composed, not random clutter or repeated props over the entire arena. |
| Objects look unlit | Check normals, shader passes, band contrast and ambient fill with production lighting. Use quiet surface texture and broad material variation; do not compensate with emissive materials. |
| Serrated roof shadows and dash-related crawling | Use simple fitted caster proxies where detailed roofs produce noisy silhouettes. Disable the visible roof's caster when using a proxy. Keep dash FOV feedback at the established zero default; use baked cast visibility. |
| Misaligned house parts and windmill clipping | Fit modules against actual mesh surfaces, not only bounds. Close gables and inspect roof undersides from several angles. Check the entire swept volume of sails and moving decoration. |
| Wall texture does not fit the building | Match material type and texture scale to construction. Use restrained plaster wash on plaster; do not project unrelated decorative wall patterns over an assembled facade. |
| Shadow/detail popping on large stages | Force highest detail; bake fixed geometry with suitable charts for near and distant surfaces. Traverse the whole stage, including rapid movement and camera shifts. |
| Plants on dirt paths | Test the whole plant footprint against the full noisy path envelope, blend width, combat footprint and occupied props. Relocate to genuine green/plantable areas with a clearance margin. |
| Floating trees and rocks | Ground against the actual rendered terrain triangles, including bake tiles, after final horizontal placement. Use model-base contacts and a small overlap, not pivots, a smooth height formula or a neighbouring object's height. |
| Broken gates disappear without satisfying feedback | Preserve segmented construction, readable damage stages, local recoil, brief outward impact accents and noncolliding tumbling debris. Keep effects outside intact solids and clear them fully. |
| Exit looks like placeholder posts | Use a setting-specific destination landmark with a ground cue at the actual exit/radius. Reveal the open-route cues from objective state, not a separate timer or guessed condition. |
| Rotating objective lacks identity | Separate a conspicuous target from solid shield panels, emphasize the opening edges and show real motion. Fit visuals to `CoverGeometry`; keep target damage distinct from shield reflection. |

Ground-contact audits should start with Chapter One's maximum 3 cm unsupported-gap
threshold for ground-level scenery, then inspect wide/sloped bases visually. This is
a placement QA threshold, not permission for visible hovering. Preserve intentionally
supported props on tables, raised beds, shelves and buildings.

## Proposed chapter art directions

The themes and stage settings in this section are recommendations for implementation,
not previously approved chapter assignments. The locked Full Cel style above does not
depend on accepting a particular theme. Each chapter needs more than a palette swap:
change its architecture family, ground language, skyline and prop vocabulary.

| Chapter / levels | Proposed setting | Ground and palette | Landmark / atmosphere | Existing asset foundation |
| --- | --- | --- | --- | --- |
| 2 / 11-20 | Emberworks Foundry | Quiet blue-grey paving, worn metal inserts and earth margins; copper, brass and muted teal | Furnaces, pipes, tanks and chimneys; warm late-afternoon key with cool fill | `kenney_factory-kit_3.0`, Fantasy Props, village service buildings, imported metal/brick/wood textures |
| 3 / 21-30 | Highland Quarry and Aqueduct | Pale limestone courts, gravel and cut stone; cream, slate and dusty sage | Quarry faces, broken aqueduct spans and supply terraces; clear highland daylight | Stylized Nature, Kenney nature/castle, modular ruins, cliff/layered-rock/stone textures |
| 4 / 31-40 | Overgrown Observatory | Worn geometric paving and controlled moss/earth margins; muted jade, ochre stone and old brass | Broken observatory arches, instruments and root-framed ruins; soft sky colour with a crisp directional key | Ultimate Modular Ruins, KayKit dungeon, Fantasy Props, Stylized Nature and stone/grass/bark textures |
| 5 / 41-50 | Twilight Necropolis | Mid-value weathered stone with subdued green verges; slate, sage and warm ivory | Mausoleums, memorial silhouettes and quiet lantern housings; readable dusk rather than black night | `kenney_graveyard-kit_5.0`, KayKit dungeon, Kenney castle, nature and stone textures |
| 6 / 51-60 | Sunscarred Canyon Outpost | Cool grey combat paving within warm sand/layered-rock surroundings; blue-grey, sand and faded blue fabric | Mesa walls, cisterns, ruined outposts and caravans; bright dry daylight | Stylized Nature, modular ruins, village/Fantasy Props, sand/dry-mud/layered-rock textures |
| 7 / 61-70 | Frostbound Vault | Muted slate-blue combat stone; ice and pale frost mainly at the margins; cold stone, ivory and restrained brass | Frozen buttresses, vault entrances and crystal clusters; cool daylight with retained shaded faces | KayKit dungeon, Kenney castle, modular ruins, rock/ice/crystal textures; simple owned frost-cap meshes if needed |
| 8 / 71-80 | Ashen Crown Citadel | Mid-grey ash stone and monumental paving; charcoal structures, desaturated red fabric and brass | Fortress walls, broken ceremonial arches and distant volcanic silhouettes; warm rim against cool fill | Kenney castle, KayKit dungeon, factory accents, modular ruins and lava-rock/stone/metal textures |
| 9 / 81 | Crown Above the Clouds | Mid-value blue-grey ceremonial paving, ivory architecture and royal-blue/gold accents | A coherent elevated royal arena with a cloud-backed skyline; clean sky light and crisp forms | Castle/dungeon/ruins kitbash, existing finale presentation and owned sky/background meshes |

Do not imply new mechanics with these settings. Ice adds no sliding. Decorative water,
lava, ledges, machinery and graves add no damage, traversal or physics. Keep decorative
lava/water outside combat space and visually subordinate to actual hazards. Avoid
purple magical decoration competing with Wizard zones, flame decoration competing
with Dragon attacks, bright tethers competing with LinkedPairs, or frost/crystal
highlights hiding Snake vulnerability and Ninja warnings.

### A different background for every stage

The following labels define visual compositions. Preserve the existing encounter titles
and layouts. Each stage must have one identifiable focal feature plus at least two
additional differences in silhouette arrangement, prop clusters, planting, ground
motifs or distant scenery. Small lighting variations alone do not satisfy this.

| Chapter 2 stage | Proposed visual composition |
| --- | --- |
| 11 | Coal delivery yard; broad furnace silhouette and stacked fuel outside the court |
| 12 | Boiler repair court; shell target framed by service tanks and tool storage |
| 13 | Furnace gate; narrow reinforced gate between existing sealed sides, clear exit sign |
| 14 | Pumpworks runway; long pipe silhouettes and low side service bays |
| 15 | Pipe junction; asymmetric tanks and a crossing service gantry in the background |
| 16 | Freight dock; readable machine block, rails and physical docking socket |
| 17 | Governor courtyard; rotating industrial shutters around the distinct target |
| 18 | Great foundry yard; several distant workshop masses around the full large arena |
| 19 | Switchback loading bay; different storage clusters along the existing turning track |
| 20 | Smelter ring; Chicken arena framed by distant chimneys, with its existing interior intact |

| Chapter 3 stage | Proposed visual composition |
| --- | --- |
| 21 | Armorer's stone court; cut blocks and a prominent quarry face |
| 22 | Quarry terraces; distinct extraction/storage dressing for the two existing sectors |
| 23 | Aqueduct crossing; a broken-span skyline framing the existing diagonal lane |
| 24 | Supply causeway; a clear protected endpoint with quiet storage beyond it |
| 25 | Geothermal cut; actual hot-ground patch visually distinct from inert rock seams |
| 26 | Winch court; track objective beside distant lifting machinery |
| 27 | Kiln walk; background kilns aligned away from the periodic warning strip |
| 28 | Grand quarry basin; layered cliff silhouettes around broad connected combat areas |
| 29 | Ruined valve rotunda; rotating opening readable against a quiet aqueduct wall |
| 30 | Rolling pit; quarry ring and low distant machinery around the unchanged Blob arena |

Chapter 3 damage feedback: level 22's barriers and level 29's central core use thin,
irregular branching fractures that accumulate across the existing damage stages.
Do not use thick straight horizontal bands or bars to communicate damage. Keep
fractures readable on both barrier faces and all four core faces, with a denser
branch network after the second hit. Preserve hit counts, rotation/cover rules,
recoil, destruction/cleanup, objective placement, collision and baked shadows
(BARRICADE-002..004/007..009, COVER-001..005, VISION-004..006).

| Chapter 4 stage | Proposed visual composition |
| --- | --- |
| 31 | Star-reader's garden; broken instrument silhouette and sparse planted borders |
| 32 | Survey track; numbered-looking architectural bays without new gameplay markers |
| 33 | Root gallery; trailing roots outside the paths and clear enemy-created trails |
| 34 | Survey gates; three distinct ruined terraces matching the existing room chain |
| 35 | Astrolabe court; observatory-themed rotating shutters with bright gap jambs |
| 36 | Watchkeeper's approach; a distant tower framing the protected point |
| 37 | Relic housing; an ornate shell that still communicates explosion-only protection |
| 38 | Great observatory terrace; multiple ruined skyline groups, quiet central ground |
| 39 | Alignment terraces; repeated track-language accents with unique end vistas |
| 40 | Fallen planetarium; frame all three existing Dino pillars without obscuring them |

| Chapter 5 stage | Proposed visual composition |
| --- | --- |
| 41 | Memorial court; paired monuments outside the tether introduction's open space |
| 42 | Cemetery rise; four distinct sectors progressing from memorial garden to mausoleum |
| 43 | Twin mausoleum court; separated background masses around existing cross-shots |
| 44 | Bellwheel circle; clockwork cover whose pauses remain visually obvious |
| 45 | Procession crossing; low markers outside the periodic hazard and cross lanes |
| 46 | Stone-haul route; a readable push block and socket among ceremonial masonry |
| 47 | Caretaker's approach; two scenic approaches converging on the protected endpoint |
| 48 | Great necropolis green; broad clearing with distant mausoleum clusters |
| 49 | Lantern garden; a quieter, more open composition before the boss |
| 50 | Mausoleum keep; new background silhouette around the exact Gatekeeper rematch arena |

| Chapter 6 stage | Proposed visual composition |
| --- | --- |
| 51 | Cistern court; a sheltered shell objective within service-building silhouettes |
| 52 | Wind-cut outpost; visible solid cover and a cool floor for reading Dragon flame |
| 53 | Windwheel camp; reversible cover framed by nonintersecting stationary machinery |
| 54 | Ore-haul switchbacks; clear track turns and hazard-free endpoint presentation |
| 55 | Windward ruins; an asymmetric skyline that does not hide casters or flame warnings |
| 56 | Caravan ascent; five scenic sectors with changing stores, cisterns and cliff faces |
| 57 | Oasis supply lane; protected point framed by storage and distant green vegetation |
| 58 | High-desert basin; large mesa-backed arena with readable cross-court sightlines |
| 59 | Open mesa; intentionally sparse foreground, richer distant rock formations |
| 60 | Clifftop signal court; distant towers frame the unchanged Chicken rematch arena |

| Chapter 7 stage | Proposed visual composition |
| --- | --- |
| 61 | Thawline court; subdued frost margins and ample visual space for the Snake tail |
| 62 | Vault ascent; six visually distinct existing sectors leading to the final side gate |
| 63 | Meltwater galleries; distant channels and low buttresses outside the playable lanes |
| 64 | Frozen reliquary; recognizable shell/core phases against a quiet vault backdrop |
| 65 | Three-buttress court; separated background silhouettes around the three approaches |
| 66 | Regulator rotunda; frost-edged machinery retaining the actual rotation timing |
| 67 | Rime garden; low frozen planting and clear retreat routes |
| 68 | Great glacial basin; broad slate court within layered ice/cliff silhouettes |
| 69 | Crystal delivery route; readable track/socket with crystals limited to the margins |
| 70 | Frozen coliseum; vault architecture outside the existing Blob rematch geometry |

| Chapter 8 stage | Proposed visual composition |
| --- | --- |
| 71 | Outer crown court; restrained fortress framing around the Ninja introduction |
| 72 | Royal shutter court; ceremonial shield panels and a distinct hittable target |
| 73 | Sentinel crossing; low side architecture that preserves ambush and warning reads |
| 74 | Seven-gate processional road; seven different existing sectors, ending at a royal waygate |
| 75 | Armory approach; a protected endpoint framed by noninteractive stores |
| 76 | Royal reliquary; familiar shell/core grammar with richer ceremonial trim |
| 77 | Final citadel approach; a different skyline and end-zone framing from stage 75 |
| 78 | Great muster ground; the largest architectural vista without crowd-obscuring props |
| 79 | Quiet crown terrace; simpler near scenery and an unmistakable distant throne silhouette |
| 80 | Ruined throne rotunda; preserve all three Dino pillars, warning areas and boss routes |

| Chapter 9 stage | Proposed visual composition |
| --- | --- |
| 81 | Crown Above the Clouds; four equally recognizable anchor stations in one coherent royal arena, a readable boss landing space and a distinct distant skyline for each camera-facing side |

For room chains, the listed sector counts are the current 1/2/3/4/5/6/7 gates at
13/22/34/42/56/62/74 (BARRICADE-007..009). Preserve ascending floors, spawn ownership,
hazards and the original final sector. Do not stamp one background across the whole route.

## Existing assets and production policy

Confirmed asset families under `Assets/CrowdPunch/Models`:

- `Medieval Village MegaKit[Standard]`, `Stylized Nature MegaKit[Standard]` and
  `Fantasy Props MegaKit[Standard]` for buildings, vegetation, rocks and dressing.
- `kenney_factory-kit_3.0`, `kenney_castle-kit`, `kenney_graveyard-kit_5.0`,
  `kenney_nature-kit`, `KayKit_Dungeon_Pack_1.1_FREE` and the imported
  `Ultimate Modular Ruins Pack - Aug 2021-20261016T162013Z-1-001`.
- Existing `kenney_particle-pack`, Cartoon FX copies and production combat effects
  for short accents. Reuse pools and caps; avoid lights, collision modules or
  one ParticleSystem/GameObject per ECS enemy.

`Assets/CrowdPunch/Textures/StylizedTextures` already contains grass, ground, sand,
dry mud, cliff/layered rock, paving, walls, wood, metal, ice, crystal, water and lava
texture families. These support the proposed surface range without new purchases.
Inventory individual usable FBXs, UVs, submeshes and texture maps before choosing a
specific prop; a pack's presence does not prove that every desired structure exists.

Use chapter-owned copies of materials and generated UV2 meshes. Preserve imported
models, source textures, source materials and their importers. Retain existing licenses.
Use the owned cel environment/village surface shaders where compatible, including
DOTS, shadow, depth, depth-normal and Meta passes. Keep the established player/enemy
deformation shaders. Do not replace the crowd shader with an incompatible toon shader.
Use quiet world-space detail on terrain/stone; use sensible local mapping for signs,
painted objectives and modular building details. Do not apply every imported PBR map
or a 4K texture to every small prop by default.

Build missing large silhouettes from simple owned meshes and kitbash existing modules.
Only identify a paid option if a prototype exposes a specific visible gap that cannot
be resolved economically with the current library. No shopping list is a prerequisite.

## Objective and hazard presentation

Keep a consistent interaction language while changing construction materials by chapter.
Use current authoring/state as the source of truth; do not implement parallel visual
timers or duplicate gameplay state.

| Feature | Presentation requirements | Rules to preserve |
| --- | --- | --- |
| Breakable gates | Setting-specific segmented construction, reinforcing details, physical damage stages, brief impact effects and fully clearing debris. Frame the real exit with the chapter's landmark language. | BARRICADE-001..009: later gates remain 2 m wide with sealed sides; all room-chain gates and final exit conditions remain authoritative. Never scale the wide Chapter One gate to fit by changing collision. |
| Exit destinations | Ground perimeter matches the authored exit position/radius; open-route arrows read clearly after the relevant gate state changes. Intermediate sector progression and final victory must remain distinguishable. | Do not turn every sector exit into a level-completion marker or let a cue bypass the remaining gates. |
| Rotating cover | Blue/neutral solid shield family, contrasting hittable target, conspicuous opening jambs, motion accents tied to the actual rotation and persistent physical damage. | COVER-001..005: use `CoverGeometry`; retain gap, thickness, radius, speed, pause/reversal modes, reflection and walking enclosure. Decor cannot bridge the opening. |
| Explosion shell/core | Reinforced outer shell, unmistakable shell breaks and a visibly different exposed core. Keep shell damage readable from several camera directions. | SHELL-001..006: explosion-only shell, fresh core attack, source/deduplication and paired supply rules. Decorative fireworks must not imply valid hits. |
| Track block and socket | Chunky movable object, continuous readable rail, clear forward/backward direction, distinct physical docking socket. Use themed machinery, stone cargo or reliquary skins. | TRACK-001..006: retain route lengths, turns, step motion, hit eligibility and physical arrival. Do not hide corners or endpoints with decoration. |
| Protected point | A recognizable destination, quiet approach framing and the exact existing zone boundary. Keep the threatened endpoint visible along the lane. | PROTECT-001..004: preserve zone, failure boundary, finite waves and enemy targeting. No extra UI or scenery blockers. |
| Ground hazards | Keep warning/active/inactive states and true shapes readable over every floor palette. Distinguish inert material decoration from harmful regions. | GROUND-001..007 and enemy-created hazard rules: no damage, timing, shape or safe-route changes. |
| Bosses and toppling pillars | Retain the existing finished boss appearances; theme the scenery around them. Preserve exposed parts, telegraphs, support ammunition and pillar wind-up/fall silhouettes. | Boss rules and PILLAR-001..006: exact arena/collider layout, three pillar positions and fall paths, attack ownership and timings. |
| Finale anchors | Carry familiar gate/cover/shell/rail visual grammar into four royal stations; make usable, paused and spent states clear. Keep effects subordinate to boss attacks. | FINALE-001..007: exactly four flight anchors, persistent partial progress, physical docking, ownership restrictions, phase pauses and reset after the fourth use. |

The Chapter One `ChapterOneGateVisuals`, `ChapterOneExitVisuals` and
`ChapterOneRotatingTargetVisuals` recipes are references, not drop-in batch commands
for later scenes. Their scene numbers, wide gate dimensions, root names and village
materials are specialized. Reuse existing presentation components where their behavior
fits; add a narrow presentation component/system only where a new visual requirement
cannot be represented correctly. Preserve the Mono/ECS boundary in AGENTS.md.

## Authoring and implementation sequence

- [ ] **Capture the current baseline.** Read current main scenes/SubScenes and record
  immutable gameplay authoring, transforms, floor/collider meshes, settings references,
  navigation, waves, boss geometry and IDs. Note existing user edits. Capture matching
  gameplay-camera views before changing a chapter.
- [ ] **Pilot Chapter 2.** Establish the ground, cel materials, architecture and skyline
  in stage 11. Exercise shell 12, narrow gate/exit 13, track 16 and rotating target 17;
  prove large-arena coverage in 18 and boss readability in 20. Finish the remaining
  stages before calling the chapter complete. Use the same quality checklist thereafter.
- [ ] **Prepare bounded reusable authoring pieces.** Extract only demonstrated shared
  responsibilities: surface recipes, visual placement/grounding, static-bake preparation
  and receiver binding. Keep chapter composition in chapter-owned recipes/data. Avoid
  a monolithic scenery script or speculative universal environment framework.
- [ ] **Dress saved content through Unity APIs.** Create replaceable chapter scenery
  roots with deterministic variation. Add presentation children to existing objective
  roots without recreating the encounter. Do not rerun campaign creation builders to
  obtain visuals; they create/tune gameplay and may refuse or overwrite authored content.
  Make future visual refreshes preserve the stage layout and intentional hand edits
  through explicit ownership or separate manual-detail roots.
- [ ] **Build surfaces before prop density.** Apply world-projected ground to the existing
  visible floor, fit surrounding terrain to its edge, establish background silhouettes,
  assemble landmarks, then add clusters and planting. Do not introduce collision slopes
  or change the current flat-ground simulation to achieve a scenic hill (VISION-004).
- [ ] **Run placement cleanup in the right order.** Assemble buildings; check swept moving
  parts; exclude plants from paths and combat areas; then ground final placements against
  rendered triangles. Recheck occupied bounds after relocation. Inspect roots, rock bases,
  building seams, roof joins and intentional supports from low and gameplay cameras.
- [ ] **Integrate objectives.** Add chapter-specific skins and state-driven cues, then
  verify visible bounds against physical geometry and actual completion conditions.
- [ ] **Bake only after geometry settles.** Process the main scene together with its
  gameplay SubScene, bind receivers, validate probes/import settings, and save assets.
  Any later fixed-geometry or sun-direction change invalidates that bake.
- [ ] **Complete Chapters 3, 4, 5, 6, 7 and 8 in order.** Use the same workflow but distinct
  composition sheets. Preserve completed chapters as regressions rather than modifying
  shared materials to solve a local palette issue.
- [ ] **Finish Chapter 9 as its own slice.** Stage 81 combines a large arena, four different
  objective types and the finale boss. It needs a full phase/anchor readability review,
  not only the ordinary boss-stage checklist.
- [ ] **Run cross-chapter integration.** Check transitions, six quality tiers, representative
  player-build scenes and campaign integrity. Record any remaining visual or performance
  limits with evidence rather than calling a partial pass finished.

### Lighting, baking and state restoration

- Retain the shared production `FullCelPost.asset` baseline: Neutral tonemapping,
  exposure .25, contrast 5, saturation 4, bloom .06 and vignette .12. Preserve HDR,
  post-processing and SMAA. This shared asset is the source/fallback; painted scenes
  keep their approved chapter-owned overrides listed above. Do not introduce motion
  blur or depth of field over combat.
- Keep the priority-150 production Volume, using the chapter-owned painted profile
  where established. Chapter atmosphere may use a scoped
  priority-160 profile for restrained white balance. Change light/sky/ground palette
  deliberately; do not bake chapter colours into player/enemy atlas textures.
- Use `CelLevelLighting` and the existing baked-lighting lifetime pattern. On additive
  unload, restore sky, fog, ambient palette, pipeline, shadowmask mode, shader flags and
  the previous probe set. Keep camera-cutaway globals scoped/cleared. Test both
  ordinary transitions and retry/replay paths.
- Generalize bake input to explicit chapter scene lists. `ChapterOneShadowBake.Begin`
  currently accepts only 1-10 and uses Chapter One paths/assets; do not call it with
  later stage numbers or route future outputs into Chapter One's asset folder.
- Give immutable meshes owned UV2 copies and appropriate chart density. Tile distant
  landscape where needed; preserve original triangles/normals and collision references.
  Budget atlas count/memory for 100 m arenas and long room chains before baking all stages.
- Exclude changing geometry by responsibility/authoring, not a fragile object-name list.
  **Do not put animated cues under static ancestors.** Entities baking can flatten the
  hierarchy even when child flags are cleared. Verify runtime LocalTransform and absence
  of the ECS Static tag on every moving/state-dependent presentation entity.
- Use hard baked visibility and saved probes; keep runtime shadow distance zero in
  completed baked chapters. Confirm the active pipeline and shadow-caster statistics
  during Play rather than trusting serialized settings alone.
- Keep lightmap mip-limit settings compatible with Entities Graphics texture arrays.
  Validate after imports and at Low quality. Retry transient metadata-write failures,
  then read back the final values; a started bake is not a successful bake.
- Preserve static SubScene receiver mask/UV-transform bindings when refreshing materials.
  Use fresh source snapshots, stable GUIDs and matching asset names as in the corrected
  Chapter One binder. Inspect the actual ECS floor as well as GameObject scenery.
- Only enable the finished baked-only policy after usable data and receiver bindings
  pass. If dressing invalidates a bake, make that state explicit and complete the rebake
  before handing over the chapter. Do not leave temporary fallback lighting as the result.

## Validation and definition of done

Every chapter must satisfy all of the following; screenshots alone are insufficient.

- [ ] Every stage has its own setting composition, complete background and readable ground.
  Inspect entry, objective approach, corners, distant edges and the player's real camera
  travel. Include wide arenas, all room-chain sectors, defense endpoints and boss views.
- [ ] Compare with the approved painted Chapter 2 reference: stable pigment, selective
  linework, directional cel bands, restrained grading and quieter distant detail.
  Check several moving camera angles, stationary repeated renders and objective debris;
  refreshes must preserve approved profiles, receiver bindings and dedicated skins.
- [ ] Verify continuous background coverage on every side from centre/edge/corner views
  at two heights. Audit uniform imported-model scale, pipe diameter/supports, finite
  transforms and exposed facade/roof/paving depth conflicts. Cutaway views must also
  reveal finished surroundings rather than gaps hidden by foreground buildings.
- [ ] Capture paired cutaway-on/off edge views with advancing frames. Confirm the player
  and combat remain visible, gameplay materials stay excluded, and depth/AO silhouettes
  disappear with decorative colour. Verify secondary/inactive cameras, post-render global
  cleanup, additive transitions and retry without moving scenery or changing the layout.
- [ ] Compare player and every enemy type present against the floor, scenery and shadows.
  Inspect aim previews, returned shots, flames, tethers, tail-tip highlights and warnings
  during motion and effects, not only an empty frozen arena.
- [ ] Audit path planting, terrain contacts, module joints, windmill/machine swept volumes,
  coplanar layers, missing faces, visible culling and foreground occlusion. Add temporary
  editor-only mesh queries for independent grounding checks if needed; remove them afterward.
- [ ] Audit fixed static flags, LOD0, caster exclusions, UV2, shadowmasks, probes, mip limits
  and static receiver bindings. Traverse the largest stage with dash and camera motion:
  no creeping/jittering cast edges, LOD popping or distance shadow disappearance.
- [ ] In Play, confirm frame/rotation counters advance. Check intact, hit, damaged, open,
  moving, stopped, spent and destroyed states where applicable. Verify debris/effects clear,
  blockers match visuals, objective completion still requires the right condition and
  retry restores the initial appearance. Separate controlled state injection from real
  physics/combat evidence in the verification notes.
- [ ] Compare the immutable gameplay baseline with the saved result. Rendering changes
  must not alter collider meshes, objective dimensions/settings, floor heights, wave data,
  spawn safety, navigation, boss routes, chapter boundaries or existing save behavior.
- [ ] Run Unity compilation and relevant existing presentation/campaign/objective tests.
  Extend meaningful checks for new state or baking risks; do not add tests that merely
  mirror decoration placement. Run the AGENTS.md .NET check when practical, and report
  its real result separately; generated-project failures do not override Unity's verdict.
- [ ] Profile rendering, memory, bake data, presentation and representative actual crowds.
  Include stages 18/28/38/48/58/68/78, the long gate chains and stage 81. Count real physical
  LinkedPair endpoints and Snake bodies. Keep full detail; optimize mesh sharing, materials,
  density and atlas layout before considering any change to the user's no-LOD decision.
- [ ] Record hardware, quality, crowd/renderer counts and measurement scope. OQ-001 has no
  agreed hardware/FPS target; do not invent one or claim an isolated presentation timing
  proves whole-game performance. Camera/input open questions do not authorize redesigns.
- [ ] Test additive atmosphere/probe/pipeline cleanup at chapter boundaries, replay and retry,
  including transitions from Chapter One and into/out of the finale. The production look
  must work without the Art Lab window or editor-only material substitutions.
- [ ] Inspect at least a representative player build for shaders, lightmaps, ECS rendering,
  quality settings and objective effects; expand build coverage when a chapter adds a new
  shader/material path. Do not describe Editor-only checks as player-build verification.
- [ ] Save a chapter verification note with exact stages/states checked, failed-then-fixed
  issues, test results, representative captures, profiling scope and remaining limitations.
  Leave clean stopped Editor scenes, restore temporary cameras/time/health and keep normal
  campaign saves untouched by inspection.

Deliverables per chapter: saved main scenes and presentation-only SubScene changes;
owned materials/meshes/atmosphere assets; complete baked lighting and receiver data;
repeatable visual refresh integration; verification notes and representative captures.
Document architecture changes only if ownership or data flow actually changes. Do not
rewrite accepted gameplay design to match a cosmetic implementation choice.
