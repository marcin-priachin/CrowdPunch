# Chapter Seven - Frostbound Vault

2026-10-21. Visual implementation of PLANS.md for Campaign 61-70 (VISION-004..006).

| Stage | Presentation |
| --- | --- |
| 61 | Thawline court and quiet frost margins around the Snake introduction |
| 62 | Vault ascent with six sector landmarks and a final frozen signal tower |
| 63 | Meltwater galleries with channels outside the playable lanes |
| 64 | Frozen reliquary with blue armour, ivory core, brass sockets and branching damage |
| 65 | Three-buttress court with three separated skyline towers |
| 66 | Regulator rotunda, neutral shield panels, pale jambs and contrasting strike face |
| 67 | Low rime gardens outside the retreat routes |
| 68 | Great glacial basin with a broad slate court and layered ice skyline |
| 69 | Crystal delivery route, complete transfer carrier and matching rail/socket fittings |
| 70 | Frozen coliseum surrounding the original Blob rematch geometry |

The visible combat surface is the original authored floor with quiet slate-blue paving.
Continuous frost earth is 12 cm below the lowest floor and extends beyond the haze range;
two closed glacial rims cover every side. Joined galleries have fitted frost caps.
Entrances use individually jointed arch stones, closed sides/rears and complete roofs.
Imported castle towers retain uniform scaling. Frost and crystals remain outside the
combat envelope. New scenery is noncolliding and uses scoped decorative cutaways.

Chapter-owned textures use continuous pigment, distinct rock/ice/crystal recipes,
full mip chains, repeat wrapping, trilinear filtering, anisotropy 4 and a 2048px cap.
Moving objectives use attached object-space pigment. Cool daylight retains shaded
faces; the owned post profile uses exposure .18, contrast 3, saturation -5 and bloom
.015 with the production Neutral/HDR/SMAA/vignette baseline. Character source assets
and atlas textures are preserved.

Gate/core fractures branch at the existing damage stages, on both gate faces and all
four reliquary core faces. Shell fragments use the existing shell lifecycle and cleanup.
The cart's body, cargo, pale strike panels and multidirectional rollers follow its ECS
root. The original route, fixed orientation, rails and blue socket semantics remain
authoritative (BARRICADE-001..009, SHELL-001..006, COVER-001..005, TRACK-001..006).
Heated frost-stone hazards preserve their exact footprints, clocks and damage while
retaining cooling wisps, amber warning/smoke and active two-tone flames/embers
(GROUND-001..007). No new gameplay timers or state machines are introduced.

Verification:

- Unity 6000.3.10f1 compiled the final runtime, Editor and test code successfully.
  The CLI update check was unavailable because the configured network proxy refused
  the connection; the installed CLI successfully targeted only Agent A throughout.
- All ten main/SubScene pairs initially completed `CampaignShadowBake`, with two saved
  lightmap/shadowmask atlases per level. The shadow correction below supersedes that density.
  Level 67 was rebaked after adding the final
  garden shrubs. Saved masks, UV2, static exclusions, noncolliding decoration,
  external scenery clearances, haze bounds and objective cutaway exclusions passed.
- All 13 `ChapterSevenVaultTests` passed, including actual Vault particle-system
  updates in inactive/warning/active states and clearing when unintroduced.
  The focused Unity run passed 63 of 64 checks across Chapter Seven, cover, track,
  shell, shell fragments and hazard particles. The existing combined campaign
  fixture failed at `CampaignBarricadeRoomTests.cs:29`: it expects four required
  hits, while unchanged `Data/Campaign/Settings/L62_Barricade.asset` specifies three.
  The baseline hash confirms that gameplay asset is unchanged.
- The .NET game-assembly check (`dotnet build Assembly-CSharp.csproj --no-restore
  -m:1 -p:BuildProjectReferences=false`) failed with 132 missing generated package
  DLL references under `Temp/Bin`, independently of the successful Unity compilation.
  Packages and generated project files were not changed to work around that failure.
- A current-workspace baseline compared 1,182 original transforms, collider/gameplay
  authoring properties and triangle geometries across the 20 scenes: none changed
  or disappeared. Sun rotations are intentionally excluded. The bake can reorder
  render mesh vertices for UV2 while preserving triangle positions and colliders.
  An independent raw YAML comparison also found all 244 original physics/gameplay
  component blocks unchanged, including enum, boolean and object-reference fields.
  All 2,354 checked existing campaign/player files retained their original hashes.
- Reapplying `RefreshSurfaces` left all 1,830 compared chapter asset, scene and bake
  files byte-identical. This includes receiver masks, chart transforms, enable flags,
  material GUIDs and source references. Six owned texture copies read back at
  2048 x 2048 with 12 mips, BC7, trilinear filtering and anisotropy 4.
- Ten advancing Play-mode previews recorded 814-832 frames over 16 game seconds
  per stage at Very Low. Center orbits and four corner views used the real following
  camera, including normal/raised offsets. Original character models, tail highlights,
  tethers, telegraphs, hazards and boss silhouettes remained readable. The stage-68
  probe contained 68 enemy entities in its initial wave, rather than the final wave.
- Each level's actual registered ECS court material carried its baked shadow mask.
  Dynamic objective visuals had LocalTransform and no ECS Static tag. The initial
  name-based receiver counter in `report.txt` returned 0/0 because runtime names were
  unavailable; `ecs-receivers.txt` records the independent material-based verification
  for all ten stages. Runtime URP shadow distance was zero and the baked-only flag one.
- Natural phase progression was observed after controlled runtime exposure of
  not-yet-introduced patches: levels 62/65/67/68/70 showed inactive, warning and active;
  level 64's always-active pockets showed active. Cycle durations/offsets and gameplay
  clocks were preserved. Existing particle checks cover shape containment and bounded
  budgets. Transitioning to unthemed Chapter Eight left zero hazard particle vertices.
- Controlled reliquary damage showed shell fractures, cleared shell debris, a fresh
  ivory core and branching core damage. Subsequent fresh entry restored 4/4 explosions
  and 5/5 core health. The core completed during the controlled live review; this is
  not proof of a player completing the shell/core fight through valid launched hits.
- The regulator recorded 713 distinct sampled angles, stopped with zero angular change
  during a controlled one-second hit pause and resumed afterwards. The crystal carrier
  moved from 0 to 2 m and reversed to 0 m using the existing motion systems. Full route
  completion, every turn and physical docking through player combat remain manual checks.
- Paired cutaway-on/off captures used fixed live camera/player positions and a temporary
  low camera offset to stress an otherwise obscuring gallery. Colour/depth obstruction
  cleared with the effect, gameplay rocks remained present and post-render globals were
  zero. Fresh Ultra entry retained the chapter's baked pipeline and zero shadow distance.
  Changing quality mid-scene can bypass the existing scene-scoped pipeline override until
  reload; this shared lifecycle behavior was not changed by the chapter dressing.
- Hardware: i9-14900KF, RTX 4090 (24 GB), about 64 GB RAM, 3840 x 2160 Game view.
  Initial-wave frame medians were approximately 16.68 ms with the existing frame cap;
  p95 values were 17.4-18.4 ms. These Editor probes include controlled camera/player
  positioning and health resets; they are diagnostics, not target-device performance
  acceptance. OQ-001 remains unresolved. No standalone build or human balance playtest
  is claimed.
- Stopping Play restored `CelRendering`, shadow distance 110, baked-only flag zero and
  zero cutaway globals. The assigned Editor is stopped on Bootstrap at its original
  Very Low tier. Editor preview isolated campaign progress from normal saves.
- Temporary diagnostic errors were separated from game errors: the initial audit
  used unsupported serialized enum/bool reads, and a live probe queried arena data
  four frames before SubScene streaming completed. The raw component comparison above
  independently verifies those authoring fields. The probe now waits for arena data;
  its four transient Console entries were archived and cleared after stopping Play.

Evidence is under `Docs/Verification/Evidence/ChapterSevenVault/`; representative
screenshots are under `Docs/Verification/Images/ChapterSevenVault/PlayerCamera/`.
The broader PLANS.md acceptance checklist remains open for full human traversal,
combat/docking completion and representative final-wave/device profiling.

![Frostbound Vault gameplay](Images/ChapterSevenVault/PlayerCamera/Level61.png)

## Column shadows and building joints - 2026-10-21

The user reported staircase-shaped column shadows and flickering building edges.
Matched close views reproduced the shadow defect on level 61's west gallery.
Its long box walls reused unit-cube lightmap islands: the measured density varied
from roughly 1-1.3 texels/m along a wall to 17-23 texels/m vertically. Increasing
texture size alone would retain that distortion. Separately, the vault return
walls overlapped the outer faces of the front piers and rear slabs.

`ChapterSevenVaultShadows` now authors metre-proportioned UV2 islands for static box
scenery, subdividing each outside face into patches no longer than 12 m, with 10 cm
chart gutters. It preserves each box's outside surface, normals and transform and
adds no internal caps. The chapter bake uses local density scale 4 for these boxes;
the shared resolution, shader, hard-shadow threshold and other chapters are unchanged.
Meshes are shared by physical dimensions. Main-scene scenery is the only target;
gameplay floors, collision, actors and objectives retain their original geometry.

`ChapterSevenVaultJoints` fits side walls between the real front/rear slab faces.
The same correction runs in future dressing and the explicit saved-scene repair.
Landmark roots, roofs and architectural outer footprints remain in place.

The level-61 pilot measured uniform wall density of about 34.5-37.3 texels/m and
produced straight hard shadow edges in matched camera views. Its independent
same-facing coplanar triangle audit fell from 13 overlap groups to one buried
underside at ground height; no exposed corner overlap remains. Two stationary
and six moving-camera samples advanced 194 frames over 3.458 seconds after the fix.
The pilot uses five atlases instead of two: increased baked texture storage is
the cost of retaining crisp fixed detail, with no added runtime shadow pass.

Final verification:

- Unity compilation passed. All ten levels were rebaked successfully: five combined
  main/SubScene atlases in 61/63/64/65/66/67, eleven in the six-sector level 62, ten
  in the large basin 68 and seven in 69/70. `bake-atlas-counts.json` records the combined
  counts; `chapter-chart-audit.json` separately records the main-scene subset.
  Wall density is now approximately 24.6-38.2 texels/m across the whole chapter,
  with nearly equal physical scale in both chart directions.
- All 21 Chapter Seven presentation checks passed, including eight added UV-scale
  and wall-joint cases spanning ordinary, six-sector, large and boss stages. The
  focused run passed 71 of 72 checks; its sole failure is the already-recorded
  level-62 gate-hit expectation (four versus unchanged three), not a visual regression.
- All 244 raw physics/gameplay component blocks and 2,354 checked campaign/player
  files remain unchanged. Of 3,359 existing transforms, only the 106 decorative
  return-wall spans changed. No landmark root, roof, gameplay or character transform
  moved. Subdivision changes only box surface tessellation and UV2.
- The matched stationary pair is pixel-identical; six moving-camera views retain
  the straight hard shadow edge and no longer expose the overlapping corner faces.
  This is a controlled Editor review, rather than exhaustive human traversal.
- The long six-sector level ran for 292 advancing frames over 5.006 seconds at
  Very Low, with its actual ECS floor receiver bound, URP shadow distance zero and
  baked-only flag one. No game-runtime or compilation errors appeared in the
  current Console. The earlier five-second tooling timeout while binding a bake
  is retained in the diagnostic history; it did not cancel the bake.
- A post-bake surface refresh left all 2,217 compared chapter assets/scenes/bake
  files byte-identical, preserving the new receiver masks and chart transforms.
- The .NET check was rerun and again reported the same 132 missing generated
  package DLL references under `Temp/Bin`, with four warnings. Unity's compilation
  and the Unity tests above succeeded independently. The new log is `dotnet.txt`.

The increased baked texture storage is an explicit tradeoff for the corrected fixed
detail. Other chapters, global quality, shaders and realtime shadow policy were not
changed. Target-device memory/performance acceptance remains part of OQ-001.
The assigned Editor is stopped on Bootstrap after verification.
Evidence: `Evidence/ChapterSevenVault/ShadowEdges/` and
`Images/ChapterSevenVault/ShadowEdges/`.

![Corrected column shadow](Images/ChapterSevenVault/ShadowEdges/After-00.png)

## Cartoon snowfall (2026-10-21, VISION-005/006)

Campaign 61-70 now include sparse, gently falling faceted snow with white faces and
ice-blue bevels. A shared unlit shader antialiases the silhouette, respects scene
depth/fog and fades flakes near the camera. Native particles drift, sway and tumble,
fade over their lifetime, and prewarm for an immediate settled snowfall. One system
per main scene covers the complete court/route and nearby scenery. Emission scales
with area up to 100 flakes/second; the live budget never exceeds 1,024 per scene.
No runtime script, ECS query, particle collision, accumulation, lights or shadows
were added. The existing floor and hazard/objective presentations remain intact.
`ChapterSevenVaultSnow.Refresh` updates all ten scenes; future dressing includes it.

Verification:

- Unity compilation succeeded; all 31 Chapter Seven presentation tests passed,
  including ten saved-scene snow budget, scaled-time and no-collision/light cases.
- Actual game-camera samples in levels 61/62/68/70 advanced 333/298/161/297 frames
  over about five seconds each. Both samples per level showed changing snow.
  Observed live counts were 576/940/961/661, within budgets 629/1024/1024/718.
  Baked-only lighting remained enabled and URP shadow distance remained zero.
- Using the real `FeedbackTimeController.SetPaused` route, 576 flakes retained
  identical positions and count over 0.853 seconds at time scale zero. Resume
  restored time scale one and advancing flake positions. The earlier direct
  time-scale probe in `live-review.json` was overridden by that controller and is
  superseded by `pause.json`/`resume.json`.
- Transition to level 71 left zero Chapter Seven snow systems. The preview run
  then stopped and restored Bootstrap. The current Console has no compilation or
  game-runtime errors; the earlier bake-tool timeout remains in diagnostic history.
- All 2,207 compared existing chapter assets, bake files and SubScene files remained
  byte-identical. All 9,146 existing main-scene serialized object blocks remained
  identical; only the snow objects/components and scene-root entries were added.
  All 2,354 protected campaign/player files also remained unchanged. No rebake.
- The separate .NET build still reports the existing 132 missing generated package
  DLL references and four warnings. Unity compilation/tests succeeded independently.

These are controlled Editor checks and a bounded weather budget, not target-device
performance acceptance or human combat playtesting. Evidence is in
`Evidence/ChapterSevenVault/Snow/`; representative game-camera captures are below.

![Cartoon snow in the vault court](Images/ChapterSevenVault/Snow/Court.png)
![Cartoon snow across the large glacial basin](Images/ChapterSevenVault/Snow/Basin.png)
