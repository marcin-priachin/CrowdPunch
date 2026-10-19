# Chapter Two - Emberworks Foundry

Visual implementation requested from PLANS.md on 2026-10-18. Requirements:
VISION-004..006, BARRICADE-001..009, COVER-001..005, SHELL-001..006,
TRACK-001..006 and the existing CHICKEN encounter rules.

## Saved presentation

Campaign 11-20 uses owned blue-grey paving, quiet earth, kiln brick, muted teal,
copper and brass. Each stage follows its plan composition: coal delivery, boiler
repair, furnace gate, pumpworks, pipe junction, freight dock, governor courtyard,
great foundry yard, switchback loading and smelter ring. Exterior art is noncolliding.
The original floor is the visible combat surface; no paving overlay was added.
Source packs, player/enemy/boss models, atlas detail and shared production post
processing are preserved. No packages, purchases or new gameplay systems were added.

The refresh menu is `Crowd Punch > Art Direction > Dress Chapter Two - Emberworks Foundry`.
Its generated main-scene root is replaceable; keep manual additions in separate roots.
The companion `Bake Chapter Two Shadows` menu must follow any fixed-geometry refresh.
Chapter-owned output is `Assets/CrowdPunch/Data/ChapterTwoFoundry`.

## Evidence collected

- The initially clean working tree was recorded before editing. A native before view
  is in `Temp/ChapterTwo/Before-11.png`.
- Compared 7,581 stable serialized gameplay properties across all ten main scenes and
  SubScenes: no differences after dressing. The raw 8,433-property snapshots include
  852 transient Unity object-reference child IDs, which were excluded from comparison;
  stable GlobalObjectIds and asset dependency hashes remain compared. Collider references,
  gameplay transforms, settings, navigation and wave data are included.
- Final bake batch completed all ten stages, with two lightmap/shadowmask atlases per
  stage. Receiver bindings and mip-limit compatibility passed the saved-lighting checks.
- Final native Play audit advanced 100 frames after enemy streaming in every stage,
  and 1,000 frames in stage 18. Every changing objective visual had LocalTransform and
  no ECS Static tag. Active pipeline shadow distance was zero in all ten stages.
  Stage 18 reached its full initial wave of 52 real enemy bodies. Captures and the
  frame/entity counts are in `Temp/ChapterTwo/Stage-*.png` and `PlayAudit.txt`.
- This audit uses the existing EditorPreview path, which isolates campaign saves.
  Temporary player health protection was discarded when Play ended. It does not
  establish manual combat or balance acceptance.
- Initial regression run: 162 passed, 12 failed. Ten failures were the previous
  universal ambient-multiplier assertion; Chapter 2 intentionally uses 1.0 cool fill.
  One new exclusion check did not include the Chicken's skinned renderers. One test
  was interrupted by an unrelated tool request timeout logged as an error. Corrections
  were applied. The rerun passed 11 cases; one remaining case was interrupted by a
  Pipeline transport error. Direct invocation of that assertion then passed. The 174
  original assertion cases have successful coverage across these runs; this is not
  a single clean 174-case TestRunner result.
- Final direct saved-scene checks: 21 passed, zero failed (ten saved lighting checks,
  ten finite/clear-of-arena placement checks and the interrupted cel assertion).
  Output: `Temp/ChapterTwo/FinalChecks.txt`. Existing Chapter One lighting assertions
  also passed; no Chapter One scenes or presentation assets were edited.

## Corrections following inspection

The first exterior used four strips around the rectangular floor bounds. This left a
visible inset around curved arenas. It was replaced by continuous noncolliding earth
12 cm below the real floor, extending beyond the haze distance. The original floor and
colliders remain unchanged. Closed pitched roofs, facade footings and drainpipes were
added to workshop silhouettes instead of retaining distant plain boxes. These fixed
geometry changes are included in the final bake batch.

Native review then exposed flat imported factory door meshes with zero depth. The
original fitting divided by that depth; Unity rejected the resulting transforms and
left a visible facade near the origin. The fitter now preserves a finite scale on
zero-thickness axes. An oversized four-vertex ground surface also interpolated distant
fog across the nearby yard. The replacement uses a subdivided flat mesh; this changes
only the noncolliding exterior, with the real arena floor still above it. Saved-scene
placement regressions cover finite transforms and local fog interpolation geometry.

## Final verification

Final geometry snapshot: all 7,581 stable gameplay properties still match the baseline.
Ground contact audit: 135 ground-level imported factory model groups, zero base gaps
over 3 cm and zero scenery colliders. Intentionally supported fittings and roof-mounted
models are excluded. Added visible pump-pipe supports and corrected the imported
governor cog orientation; both affected stages were rebaked successfully.
The freight gantries in 15 and 19 were aligned with their supports and rebaked;
the exit cue's former cube half-height offset was removed from its flat arrow mesh.

Controlled native state checks use the existing hit resolvers and track retargeting,
not simulated manual combat. Stage 12 showed two shell charges after its first hit,
then zero visible shell pieces and one exposed core without premature completion.
Stage 13's gate cleared while leaving its exit condition active. Stage 17 rotated
0.727 radians during the sample, produced 35 visible debris pieces on break and one
completion event. Tracks in 16 and 19 reached 10/10 m and 20/20 m, locked and emitted
one completion each. All five stages reset their objective state on retry. Evidence:
`Temp/ChapterTwo/StateAudit.txt` and `State-*-partial/resolved.png`.

During a bake, Unity Entities temporarily locked its SceneDependencyCache file and
threw before the bake-completion callback. The lightbaker had finished writing valid
new masks. Reimporting that SubScene and resuming finalization recovered the batch;
the final saved-lighting checks and native Play runs above passed afterward.

Chapter 10 -> 11 -> 21 transitions restored the shared pipeline and cleared the
baked-only shader flag outside the baked chapters. Repeated 21 -> 11 -> 21 across
all six quality tiers: Chapter 2 shadow distance 0 / baked flag 1 / 147 probes;
Chapter 3 shadow distance 110 / baked flag 0, with no stale Chapter 2 probe set.
Original Very Low quality was restored. See `Temp/ChapterTwo/Transitions.txt`.

Stage 18 diagnostic sample: 240 advancing Editor frames with 52 native enemies,
Very Low quality, i9-14900KF, RTX 4090, 65,317 MB system RAM. Median 16.68 ms,
95th percentile 16.76 ms, maximum 208.73 ms. Resident lightmap textures: 33,621,786
bytes; total Unity Editor allocation after repeated bakes: 28,518,453,619 bytes.
This includes Editor overhead and a frame spike; it is not a standalone benchmark
or an OQ-001 performance acceptance result. See `Temp/ChapterTwo/Profile.txt`.

The full serial .NET command was stopped after spending an extended period rebuilding
unrelated Unity package projects. The focused `dotnet build Assembly-CSharp.csproj
--no-restore --no-dependencies -m:1` finished with 45 missing package-DLL metadata errors
and four existing assembly-version warnings. It is not a successful .NET build.
Unity compilation completed with no compiler errors.

## Windows development player

StandaloneWindows64 development build (Bootstrap plus Campaign 11-20) succeeded:
zero errors, 513 warnings, 530,629,986 bytes, 625.299 seconds. Warnings are in existing
Sidekick code, obsolete URP render-feature APIs, legacy ECS aspects and package shader
variants; none name the changed foundry/bake code. The local QA executable is
`Temp/ChapterTwo/Player/CrowdPunch.exe` and is not a full-campaign release build.

Native packaged-player captures at Ultra quality verified stages 11, 17 and 18:
shadow distance 0, baked-only shader flag 1, and respectively 147, 168 and 1,083 probes.
The main scene reports one resident lightmap entry, with SubScene receiver masks
carried through the existing material bindings. Fixed scenery and arena shadows
remain visible in `Temp/ChapterTwo/Player-*.png`.

Native keyboard input traversed the large yard from (2.59, -16.71) to the north edge
(2.59, 49.48), west edge (-49.50, 46.25), south edge (-49.50, -49.48) and east edge
(49.48, -49.48), in XZ
coordinates. Camera follow advanced through these views. A right-mouse event queued
for the next native input update fired DashStarted at frame 329364. The CLI's immediate
input flush initially missed this one-frame action, so that attempt is not counted.
The shadow policy and 1,083 probes persisted through the traversal. This samples
moving viewpoints and a real dash; it is not a frame-by-frame popping analysis.

The player uses EditorPreview through the existing property to prevent progress
writes. Temporary health protection is discarded on quit. Startup logged a rebuilt
D3D12 disk cache and blocked telemetry proxy connections; initial QA evaluator calls
also needed reflection because the Player evaluator omits URP/Entities references.
These are distinct from build errors and from gameplay assertions.

The local QA build temporarily enabled Pipeline runtime inspection. The project
setting was restored to disabled, its originally absent settings file was removed,
and Unity's build-generated preloaded-assets/shader-prefilter changes were restored.
The empty temporary Pipeline Resources folders were removed. The QA executable
contains that inspection server and should not be distributed.

OQ-001 still has no agreed hardware/FPS target. Manual combat/balance acceptance,
exhaustive camera-angle review and target-device performance remain outside this
presentation verification. Existing gameplay values and campaign progress are preserved.

## Composition revision after user review

The user found the initial stages empty and arranged like collections of props, with
disproportionately stretched pipes. The initial performance/build/state evidence above
describes that first pass; verification of the revised scenery is recorded here.

- Replaced the repeated loose perimeter props with joined factory frontages, returning
  workshop wings, connected service concourses, loading shelters, dock steps, drains,
  fuel bays and freight sidings. The combat footprint stays open and unchanged.
- Moved the built setting closer to the yard and joined the middle-distance workshop
  blocks into adjacent factory streets instead of isolated displays.
- Replaced the stretched imported pipes with constant-diameter round runs, rounded
  knees, flange joints and supports, connecting workshop headers and boiler bays.
- Imported factory machinery now fits uniformly inside its requested envelope.
  The original long pipe runs along X; stretching Z had distorted its cross section.
  Saved-scene regression checks now reject nonuniform imported-model scale.
- Chapter-owned recipes remain repeatable. The original objective bindings and
  gameplay authoring are preserved.

Revised scenery compilation passed and all ten scenes were regenerated and rebaked.
Final saved-scene assertions: 21 passed, zero failed, including uniform model scale,
finite placement, scenery collision exclusion and saved lightmap/receiver bindings.
The first run caught one late-reset shadowmask mip-limit flag in stage 16; it was
corrected, and the batch baker now reasserts saved chapter lightmap import settings
after all bakes finish. The subsequent full assertion run passed.

Ground/support contact audit: 115 imported models, zero failures against ground or
their loading platform. The final serialized gameplay comparison again matched all
7,581 stable properties with zero differences. Evidence is in
`Temp/ChapterTwo/Revision/FinalChecks.txt`, `Revision/PlacementAudit.txt` and
`Temp/ChapterTwo/RevisionFinal.txt`.

The revised native Editor Play audit completed all ten stages with advancing frames
and streamed enemies. Objective visuals retained LocalTransform, with no unexpected
Static tags; realtime shadow distance remained zero throughout. Stage 18 advanced
1,000 frames and observed 52 enemies. Captures are saved as
`Temp/ChapterTwo/Revision/Stage-11.png` through `Stage-20.png`; the audit is in
`Temp/ChapterTwo/Revision/PlayAudit.txt`. Final baked views of 11, 14 and 18 were
visually inspected for the joined frontage, pipe proportions and open combat space.
This is scenery and scene-wiring verification, not a new combat acceptance run.
The Windows build evidence above predates this composition revision; this revision
was verified in the native Unity Editor rather than a rebuilt standalone Player.
The console retains bake-time meta-write and tool timeout messages; the subsequent
saved-asset assertions passed. Entering Play also logged an invalid third-party
Synty Sidekick downloader window. No compilation failure was reported.

## Orbit coverage and depth-surface correction (2026-10-19)

User review identified flicker at building bases/overlaps, an empty side and visible
empty ground behind the buildings. The chapter recipe now surrounds all four sides
with factory streets and a second skyline row. The south side includes low delivery
buildings and service paving. Recessed continuous masonry closes aligned expansion
joints without placing two facade planes at the same depth.

Paving slabs meet edge to edge. Adjacent structural bays no longer overlap their
facades or roofs. Window mullions, facade piers, door footings, dock edges, loading
steps and coal-bunker corners have butt joints or depth clearance. Low-building
windows also terminate beneath their roof decks. These changes target competing
depth surfaces rather than adjusting runtime shadow-map distance.

The shared `CelRenderer` contact-shading feature now uses full-resolution
interleaved-gradient sampling. Intensity and sample count are unchanged; the authored
radius compensates for the former blue-noise method's internal 1.5 multiplier.
This affects every scene using that renderer. It increases the pixel work of the AO
pass; standalone GPU performance acceptance remains outside this verification.
Inspection of the installed URP source confirmed per-render texture/offset changes
in its blue-noise path. Five fixed-camera repeated renders were identical both before
and after the change, so that measurement alone does not prove a flicker reduction.

Before the final bake, all 12,960 backdrop rays passed from centre, edge and corner
samples at two heights, with zero exposed sightlines. All ten paving-overlap checks
passed. The first coverage pass found aligned joints in six stages; recessed backing
closed those openings before the final bake. The native composition review captured
48 advancing camera views across stages 11, 14 and 18, including full orbits and
outward-facing edge views. These preliminary views used realtime lighting while the
scenes awaited their final bake; they verify composition rather than baked lighting.

The serialized comparison against the preceding repository revision matched 346
gameplay authoring, collider and physical-transform blocks with zero differences.
Visual child lists are excluded because the dressing recipe regenerates them.
Evidence is under `Temp/FoundryFix/`: `CoverageBeforeBake.txt`, `OrbitReview.txt`,
`ContactShading.txt` and `GameplayComparison.txt`.

All ten final shadow bakes completed. The saved-scene run passed 37 checks with no
failures: ten placement/collision checks, ten lightmap/receiver checks, ten orbit and
paving checks, six changing-object bake exclusions and one contact-shading setting
check. A later detailed surface audit identified one exposed drain-end/pier contact
in stage 11. The recipe now separates coincident drain ends; stage 11 was regenerated,
rebaked and passed four targeted checks. Other remaining cube-face matches in the
audit are internal pier/footing backs against the workshop walls or column tops
covered by roofs, rather than exposed competing faces.

Evidence: `Temp/FoundryFix/FinalChecks.txt`, `FinalChecks-11.txt` and
`Surfaces-Final.txt` (the surface report precedes the final drain-end correction).
The subsequent gameplay comparison still matched all 346 blocks. Unity compilation
passed. The console retains bake-time meta-write messages, but the final saved texture
and receiver assertions passed; Play entry also logs the existing invalid Synty
Sidekick downloader window. This slice does not include a new standalone Player build.

The final baked Play review completed another 48 advancing camera views across 11,
14 and 18. Every recorded view reported realtime shadow distance zero. Representative
north/south yard views, the pumpworks side view and a large-yard corner were inspected.
Captures and frame/position evidence are in `Temp/FoundryFix/Baked/`, including
`OrbitReview.txt`. Play mode was stopped after verification.

## Level 11 painted-background pilot (2026-10-19)

VISION-005/006: `LevelElevenPaintedLook` assigns scene-owned materials under
`Data/ChapterTwoFoundry/PaintedLevel11`. `PaintedFoundry.shader` combines imported
brick, wood, sand, stone and rock color textures with stable world-space pigment
washes, filtered grain, drawn roof seams and UV-based painted glazing. Near brick,
roof and window variants break repetition; skyline materials have quieter detail and
cooler values. No normal/specular effects or full-screen painterly blur are introduced.
The priority-150 scene Volume owns a copied profile with restrained split toning,
saturation -5, exposure .18, contrast 3 and bloom .015. The chapter builder reapplies
the pilot only for stage 11. Existing sun placement, hard shadow charts, probes,
colliders, transforms and gameplay authoring remain intact.

Unity compiled the shader with zero reported shader messages. The runtime inspection
confirmed HDR, camera post-processing and baked-only shadow distance zero. Four native
gameplay-camera orbit views advanced 70 frames each (`Temp/Painted11/Orbit.txt`). Level
12 restored its original materials and FullCelPost profile on transition. Four existing
stage-11 geometry, surroundings, baked-shadow and contact-shading checks passed. The
serialized gameplay comparison matched 346 blocks across Chapter 2 with no differences.
`dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors and 498
existing package/reference/deprecation warnings. No standalone build or target-hardware
GPU performance claim is made for this visual pilot. Existing Synty downloader-window
errors still occur on Play entry.
