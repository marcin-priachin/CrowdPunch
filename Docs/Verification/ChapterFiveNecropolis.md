# Chapter Five - Twilight Necropolis

Date: 2026-10-20. Scope: Campaign 41-50, following PLANS.md (VISION-004..006).

The chapter uses weathered slate paving, sage earth, dark decorative masonry,
pale objective faces and restrained ochre metal. The 2026-10-20 refinement adds
purple directional/ambient light and purple painted light/shadow pigments;
the priority-150 owned profile starts from the approved painted grading (exposure
contrast 3, saturation -5, bloom .015), with exposure -.08 and purple split toning. The owned
purple sky meets Chapter Four's existing floor-outline scenery fog. Low exterior
banks fade into dark purple, while distant ground and scenery become dark
silhouettes. Authored floors, objectives,
characters and hazards remain excluded from the scenery fog role. Outlines cover
the complete floor envelope, including the long room chain and 100 m arena.

Each stage has the planned composition: memorial court, four-sector cemetery rise,
twin mausoleums, bellwheel backdrop, procession crossing, ceremonial stone-haul
route, caretaker's approaches, great necropolis green, lantern garden and mausoleum
keep. Connected low cemetery walls, retaining plinths, returning frontages, grave
gardens, pines and a continuous distant ridge supply near/middle/distant layers.
Imported models are fitted uniformly against their vertices. New scenery is
noncolliding and uses decorative cutaway materials; gameplay surfaces are excluded.

Objectives retain existing authoring and ECS presentation ownership:

- Level 42's gates and 44's cover core use segmented stone, accumulated branching
  fractures, recoil/debris bindings and state-driven exit cues (BARRICADE-002..009,
  COVER-001..005).
- Level 46's complete ceremonial masonry cart, pale strike faces, rollers, rails,
  sleepers and docking socket follow the existing fixed-orientation translating
  objective and exact route (TRACK-001..006).
- Level 47's turquoise/slate memorial paving, ivory boundary and flush laurel mark
  the exact existing protected rectangle (PROTECT-001..004).
- Authored hazards use staggered scorched paving with the existing full-area amber
  warning, brighter active state, cooling smoke and bounded active flames/embers.
  No damage, timings, navigation, lights or HUD were added (GROUND-001..007).

Owned material copies use continuous pigment, world projection for fixed surfaces,
local projection on moving/breaking skins, separate cliff and small-rock recipes,
and chapter-local tiling textures capped at 2048 with full mip chains, repeat wrap,
trilinear filtering, anisotropy 4 and high-quality compression.

Verification record (updated as checks complete):

- Dark-silhouette refinement: reduced directional light to intensity .85 with a
  darker purple hue, reduced trilight ambient and painted light/shadow pigments,
  darkened fog/sky and lowered exposure to -.08. Distant material recipes remove
  texture/detail variation. Chapter Five's exterior earth and decoration opt into
  fog role 3: 12-32 m outside the floor outline, surface detail collapses into a
  dark silhouette and fog blending is limited to 16%. Other chapter roles retain
  their original fog appearance; combat materials remain role zero.
- The silhouette pass preserved all 20 scenes' transform/renderer/mesh blocks,
  all 518 materials' baked receiver bindings and 975 non-material files against
  a fresh pre-pass snapshot (the grading profile is the intended exception).
  The original gameplay/collider/physical-transform audit still passes. Unity
  compiled successfully; incremental `dotnet build` passed with 0 errors and
  5 existing dependency/reference warnings. All 13 Chapter Five tests passed,
  including a GPU check of flat dark ground/object silhouettes, clear gameplay
  and unchanged standard fog. All 10 Chapter Four GPU floor-edge checks passed.
- Live isolated Editor preview inspected the darker pass in 50 and 41 with
  advancing frames (native wait 1 -> 2, later captures at 560 and 2525). Controlled
  review cameras show the dark far ground/ridge and building/tree silhouettes
  against purple sky and low banks. Final captures are in
  `Images/ChapterFiveNecropolis/DarkSilhouetteUpdate/`. CameraFollow was restored
  before stopping Play. The existing animation-source exception recurred; no new
  presentation exception was observed. This pass has Editor/GPU verification;
  the earlier standalone build has not been repeated for the new shader branch.
- Purple-light refinement: directional hue, trilight ambient, painted pigments,
  sky and scoped split toning use purple. Decorative Limestone/Slate pigment is
  58% of its former base/wash value; objective source values stay unchanged.
  Fog reuses the existing Chapter Four outline shader, 4.5 m bank and material
  opt-in roles. The chapter selects a 72 m depth fade to retain distant building
  silhouettes; other chapters keep the component's original 24 m default.
- Against a fresh pre-refinement snapshot, all 20 scenes retain every serialized
  transform, MeshRenderer and MeshFilter block. All 518 materials retain their
  explicit receiver mask/ST/enabled/source bindings. All 975 remaining captured
  non-material files are byte-identical, including baked textures, lighting data,
  UV2 meshes and asset GUIDs; the grading profile is the intended exception.
  The original gameplay/collider/physical-transform audit also still passes.
- Live Editor preview inspected 41, 48 and 50 with advancing frames (first preview
  wait observed 1 -> 2; the second continued 2 -> 127 and later 436). The initial
  Chapter Four depth hid the distant buildings, so the final saved version uses
  72 m. Fresh 1400 x 900 captures are under `Images/ChapterFiveNecropolis/PurpleUpdate/`;
  courtyard and keep use temporary controlled review cameras. The large arena
  uses its gameplay camera. Temporary camera/player settings were restored.
- The refinement passed `dotnet build` with 0 errors and 34 existing warnings.
  All 12 Chapter Five presentation tests passed after the final depth change;
  all 10 existing Chapter Four GPU floor-edge fog checks also passed. Stopping
  preview restored scenery-fog settings and baked-only shader globals to zero.
  The existing animation-source import exception recurred in preview; no new
  runtime visual exception was observed. The standalone build recorded below
  predates this purple-light refinement, which has Editor verification only.
- Compared saved SubScenes against a snapshot from this checkout before dressing:
  101 gameplay authoring components, 129 collider components and 218 physical
  transforms are unchanged. The audit compares original component file IDs and
  serialized data plus physical transforms and their ancestors. Original collider
  mesh references remain intact; render meshes gain separate owned UV2 copies.
- Unity 6000.3.10f1 compiled the presentation recipes and style extension without
  errors. `dotnet build Assembly-CSharp.csproj --no-restore
  -p:BuildProjectReferences=false -m:1 -v:quiet` passed with 0 errors and 34 existing
  package/reference/deprecation warnings.
- Initial Scene-view inspection checked the memorial court, complete roof faces,
  continuous terrain and distance layers. The saved ten-scene shadow bake completed
  with no reported error. The 12 chapter presentation checks passed, including
  static/scenery UV2, noncolliding decoration, dynamic cue ancestry, floor receiver
  masks/source bindings and exact protected-boundary dimensions.
- All five imported colour textures read back as 2048 x 2048 BC7 with 12 mip levels.
  The selected Editor quality is Very Low; the active runtime pipeline has shadow
  distance zero. Hardware reported by Unity: i9-14900KF, RTX 4090, 65317 MB RAM.
- Existing hazard particle tests passed 6/6; the all-campaign progressive track-route
  check passed 1/1. Existing Chapter Five campaign tests passed 4/5. The failing
  `Loop002_ChapterFiveHasSafeNavigationExactWavesAndWiredObjectives` reaches the
  older room-chain fixture, which expects four required hits for level 42 while
  `L42_Barricade.asset` specifies three. `git show HEAD` confirms three in the
  original checkout; the setting and all gate authoring were preserved.
- A pre-existing `Enemy animation sources changed` import error recurred during
  legacy/campaign inspection. It was present in the Console before this pass;
  animation samples and character sources were not changed by the visual work.
- Applying `RefreshAppearance` twice after baking left all 1514 captured chapter
  asset/scene files byte-identical, including masks, lightmaps, UV2 meshes, receiver
  bindings and source references. The gameplay/collider/transform audit still passed.
- Live Play frames advanced (a native wait observed frame 778 -> 785). The isolated
  lifecycle probe traversed 40 -> 41-50, checked death/retry, all four gate rooms,
  natural periodic hazard phases, injected gate/core completion and the actual
  six-leg physics slide/arrival in 46. It verified defense breach/retry and all
  batches in 47, then all five large-arena waves through 128 physically spawned
  bodies in 48. Defeats were injected; a temporary Editor callback repeated them
  for remaining/late bodies. This was not a combat completion playtest.
- The lifecycle harness reached Chapter Complete, then its final aggregate
  assertion failed: 26 observed waves versus its older expected 25. Its trace
  includes the preceding Chapter Four boss and the repeated defense wave after
  breach. Manual follow-up independently confirmed next-unfinished index 50,
  the same value after reloading the isolated save, level 51 selectable, replay
  completion without progress loss/auto-advance, and replay retry at full health.
  The full result, including the failed assertion, is in `ChapterFiveLifecycle.txt`.
- The sampled level-48 final-wave Editor median was 17.5 ms on Very Low, with the
  existing frame cap, tool overhead and injected defeats. Earlier pooled entities
  remain in the probe's `owned roots` count; that count is not a live population
  measurement. This is a diagnostic observation, not whole-game/GPU acceptance.
- Close-up controlled cameras inspected the full cart, exposed branching core
  damage and the natural inactive/warning/active hazard cycle. Active two-tone
  flames, embers and smoke were visible above the footprint. The first capture at
  a phase boundary preceded delayed particle emission; the final active capture
  uses the focused live Game view after emission begins. Safe-state particle
  geometry and containment are covered by the existing particle tests.
- Temporary player/camera component changes were restored before stopping Play.
  Stopping restored the shared `CelRendering` pipeline, shadow distance 110 and
  `_CrowdPunchBakedShadowsOnly = 0`. Normal campaign progress was not used for
  inspection. Representative captures are in `Images/ChapterFiveNecropolis/`.
- A representative Windows x64 Development build of Bootstrap and 41-50 succeeded:
  0 errors, 515 warnings, 583123630 bytes, 704950 ms. Of these, 485 are shader
  warnings from the installed inference package, 28 are C# warnings in existing
  sources, and two concern missing optional runtime Pipeline configuration and
  symbol-upload authentication. None names the changed chapter sources, painted
  shader or ground-hazard shader. Summary: `ChapterFiveBuild.json`. Output is the
  ignored `Temp/ChapterFivePlayer/CrowdPunch.exe`. Unity's generated shader-prefilter,
  global runtime-settings and preloaded-asset changes were restored afterward.

Remaining acceptance limits: visuals were inspected in the Editor; the standalone
player has build verification only. The project has no enabled runtime Pipeline
server. No full player combat playtest, all-angle capture sweep, higher-quality
comparison or target-hardware performance acceptance is claimed. The older campaign
fixture, lifecycle aggregate and animation-source issues above remain visible.

Controlled Editor checks do not constitute a human combat playtest or target-hardware
performance acceptance. OQ-001 remains unresolved.
