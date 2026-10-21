# Chapter Eight - Ashen Crown Citadel

Date: 2026-10-21. Workspace: Agent A, Unity 6000.3.10f1.
Requirements: VISION-004..006, BARRICADE-007..009, COVER-005,
SHELL-001..006, PROTECT-001..004, GROUND-001..007 and PILLAR-006.

Saved Campaign 71-80 receives chapter-owned ash/charcoal painted surfaces, muted
red fabric, brass architecture, two continuous volcanic skyline layers and complete
four-sided fortress surrounds. Each stage follows PLANS.md's composition table.
Stage 74 retains seven sectors with varied roadside landmarks; 75 and 77 use their
true protected-zone dimensions; 76 has a full royal reliquary skin; 78 keeps the
100 m combat footprint; 79 has the distant throne; 80 retains all three Dino pillars.
No cargo objective exists in this chapter, so cargo checks do not apply.

Production uses the existing painted shader (continuous pigment, physical texture
scale, separate rock/cliff recipes, world mapping for fixed objects, local mapping
for fragments). Owned texture copies retain 2048 cap, full mips, repeat/trilinear,
anisotropy 4 and high-quality compression. The painted post profile starts at
exposure .18, contrast 3, saturation -5, bloom .015 and vignette .12. Warm directional
sun and cool fill retain cel form. Haze starts beyond the union of all floor bounds.
Only decorative surfaces opt into the established camera cutaway.

Barricades, target panels, exits and shell/core pieces use existing state-driven
presentation components. Protected inlays stay flush, noncolliding and inside the
exact authored breach boundary. Hazards use Citadel heated paving and the existing
20-lane/two-inactive-lane particle budget; no extra lights, clocks, damage or scans.

## Verification record

- Before dressing, recorded original SubScene transforms, MonoBehaviour authoring
  and physics components by serialized file ID, plus hashes of all campaign data.
  After dressing: **675 original components and 2,343 campaign data files match**.
  Original floor/collider geometry, settings, waves, IDs and progression are intact.
- Unity compiled the new recipes and tests, with compilationFailed=false.
- Gatehouse front/return/rear joints were corrected before the first lighting bake
  to avoid overlapping exposed faces.
- Initial Camera.Render captures were blank. Switched to URP SingleCameraRequest
  with sRGB readback; recreated baseline captures from temporary copies of the
  original Git scenes and removed those copies. Final matched before/after views
  cover 71/74/78/80; after views cover every stage. These are controlled Editor
  renders, not proof of runtime movement.
- All ten bakes completed without error, with 5/6/6/4/8/6/7/11/5/8 atlases in
  stages 71-80 respectively (66 total). Saved native masks, probes, UV2 meshes and
  ECS receiver bindings are present. Exterior/skyline chart density is bounded;
  fixed box charts retain metric proportions. Moving objective geometry is excluded.
- A surface refresh preserved all 287 receiver mask/chart/source signatures and
  all 538 current baked mesh/lighting assets byte-for-byte, across repeated refreshes.
- .NET: `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:q` passed,
  0 errors and 436 existing package/reference/deprecation warnings.
- Unity: 14/14 new Chapter Eight presentation cases passed, including all ten saved
  baked scenes, exact protected boundaries, four-face shell fractures and hazard
  state/cleanup. A test cleanup initially destroyed a mesh through deferred Destroy
  in Edit Mode; it now follows the established DestroyImmediate teardown pattern.
- Existing campaign Chapter Eight cases: 4/5 pass. The unchanged level-74 gate data
  uses requiredHits=3 for all seven gates; CampaignBarricadeRoomTests.AssertRooms
  expects 4. Confirmed all seven values against the original Git scene and unchanged
  settings hashes. This pre-existing gameplay/test mismatch is left intact.
- Additional regressions: 42/42 GroundHazard tests, 30/30 earlier-chapter baked-shadow
  tests and 3/3 shell-fragment presentation tests passed.

## Runtime review and profiling

Controlled Editor Play used the isolated Level Play preview. A temporary Editor
health guard kept the stationary player alive; the guard, camera captures and
queries are inspection tooling outside Assets and are not shipping gameplay.
Levels 75/77 temporarily moved the player near their actual protected endpoints.
The run restored Very Low quality and Bootstrap on exit.

All stages 71-80 advanced while captured: 302/274/737/154/264/269/272/234/273/271
frames during their respective review windows. Every Chapter 8 sample after load
reported one baked-lighting policy, shadow distance zero and zero realtime shadow
casters. Level 80 retained three live pillar entities. Stage 74 had no spawned
bodies in its short entry sample; this is load/presentation evidence, not completion
or a seven-sector combat playthrough.

Level 73 naturally reached Inactive, Warning and Active with both introduced
patches styled Citadel. Thirty-five distinct particle position samples establish
motion; safe samples retained smoke without an independent effect clock. Stage
transitions 80 -> 70 -> 81 -> 78 removed Citadel presentation and restored Vault or
unthemed styles. Level 81 restored its previous realtime-shadow policy (44 casters
in the captured frame), rather than retaining Chapter 8's pipeline. Re-entry to 71
and replay of 78 at Ultra advanced 302/301 frames. No current runtime Console errors
were observed; the earlier tool timeouts and fixed test-teardown error remain in
captured diagnostic history.

The live review caught original Dino pillar phase tints replacing the material
palette with white. Added `_MultiplyEntityColor` as a default-off painted shader
option and enabled it only on owned PillarStone/PillarBrass materials. The corrected
live capture retains ash stone and brass while preserving phase flashes and all
three pillar transforms/colliders. Re-ran all 14 new presentation cases: 14 pass.
No fixed geometry, sun or bake data changed for this correction.

Hardware: Intel Core i9-14900KF, NVIDIA GeForce RTX 4090. Two five-second stationary
Editor Play windows at stage 78 used its actual first wave (76 physical enemy
bodies), with a temporary health guard and no simulated victory or population edits.
Very Low/Ultra observed 166/164 enabled material/mesh entities, 239 GameObject mesh
renderers, mean 248/250 draw calls and 194/196 batches. The Editor main-thread marker
median was 16.71/16.69 ms, maximum 19.23/20.21 ms over 121 samples each. This includes
Editor scheduling, authoring caches and inspection overhead; it is not a standalone
FPS result. Total allocated Editor memory was 12.48/12.48 GB after loading/baking
many scenes, not per-level player memory. Both windows had zero realtime casters.
OQ-001 has no agreed hardware/FPS target; later waves, full combat, route traversal
and target-device profiling still require separate testing.

Owned texture readback: four 2048 x 2048 BC7 textures, 12 mip levels, trilinear,
anisotropy 4. Captured Very Low and Ultra without removing mip chains. Additional
regressions passed 5/5 gate-piece and 3/3 exit-cue presentation tests.

## Player build and remaining validation

A representative Windows Development build including Bootstrap and stages
71/73/76/78/80 succeeded: 892,549,755 bytes, 193.077 seconds, 0 errors and
513 compiler/package/shader warnings. No warnings name the changed painted or
hazard shaders. Runtime Pipeline control is not configured in this project, so the
standalone executable was not driven or visually reviewed; the captures above are
Editor Play. Build-generated URP settings, preloaded assets and dependency caches
were restored, leaving shared production settings unchanged.

Human playtesting, all objective completion states,
full-route/corner camera sweeps, paired cutaway views, later-wave performance and
full campaign player-build coverage are not claimed by this controlled pass.


## Final coverage correction

The final audit found level 74's 250 m exterior padding ended before its 295.118 m
haze end. Extended only its decorative ground to 315.118 m padding and reran the
explicit level-74 bake successfully (four atlases in the final bake). The repeatable scenery recipe now sizes padding from the
entire court/route bounds, with 20 m beyond the haze end and a 250 m minimum. Other
stages retain their existing extents. The final 14/14 saved-scene/presentation checks pass, including native mask/UV
binding equality and a check that the ground
continues beyond haze from every arena side. Gameplay floors, collider meshes,
gate/sector positions, settings and campaign data are preserved.

Evidence summaries and all 65 runtime samples: [ChapterEightCitadelResults.json](ChapterEightCitadelResults.json).
Final unique test scope: 101 pass, one unchanged campaign-data assertion fails (102 total).
Bootstrap is stopped and clean; temporary health guards were removed and original quality restored.
