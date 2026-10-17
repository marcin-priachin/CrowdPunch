# Full Cel production default

User decision on 2026-10-16: VISION-006; OQ-015 resolved.

Full Cel is saved in the game's production materials, campaign lighting and global
post-processing volumes. It does not require Art Lab, Editor globals, temporary material
substitution, or a runtime material scan. The generated URP shader snapshots preserve
EnemySkinning compute deformation and Sidekick character properties. Model/texture,
collision, animation and gameplay assets are retained.

Coverage:

- All 81 available campaign scenes and their SubScenes were audited.
- All 37 game-owned prefabs were audited, including inactive child renderers.
- 259 surface materials were converted or created during migration. Imported player
  and package-default materials have game-owned copies; vendor source assets are intact.
- Bootstrap enables HDR, post-processing and SMAA. All quality levels reference the
  production pipeline, with the example renderer's shadow/contact-shading configuration.
- Legacy examples and other game scenes share the default. Particle, trail, hazard,
  UI and sky shaders retain their dedicated rendering behavior.
- The unused `Scenes/New Scene.unity` has an existing missing BaseEnemyModel prefab
  reference. It was not modified and is excluded from subsequent migration runs.

The shared settings live in `Assets/CrowdPunch/Data/Presentation`. FullCelPost uses
Neutral tonemapping, contrast 5, saturation 4, exposure .25, bloom .06 / threshold 1.1 /
scatter .65, and vignette .12. Chapter white balance and colours are preserved. Ambient
lighting uses the selected 1.08 multiplier. No depth-of-field or motion blur is added.

`Crowd Punch > Art Direction > Apply Full Cel Default` can explicitly migrate newly
added game content. Existing converted materials and ambient palettes are not multiplied
again. Surface-material creation recipes and the common level builder now retain the
production style. Art Lab remains optional; Current restores the authored Full Cel baseline.

Verification:

- Unity C# compilation completed successfully; all three production shaders are supported
  and report no shader errors or warnings.
- CelPresentationTests: 83 cases passed (81 campaign scenes, prefab audit, camera/profile).
- EnemyAnimationTests: 26 passed.
- ArtLabTests: 2 passed.
- CampaignTests: 8 passed. The final rerun completed without new test-runner errors after
  allowing Edit Mode test cleanup to finish before entering Play Mode.
- Inspected normal Play Mode rendering in campaign 1, 40, 80 and 81, with Art Lab disabled.
  ECS render arrays use the production Enemy/Environment shaders; the player uses the
  production Player shader. Effective volume values were Neutral tonemapping and .06 bloom.
- Rechecked Gauntlet 28 with Art Lab disabled: the saved default matches the selected
  Full Cel example look, with 16 ECS enemies visible and Neutral tonemapping active.
- Presentation frames advanced while simulation was frozen for inspection. Crowd and
  boss deformation remained visible. These checks are visual smoke tests, not a complete
  combat playthrough or target-hardware performance benchmark.

The existing Synty Sidekick Tool Downloader invalid-window error remains unrelated to this
change. No standalone player build was produced during this verification; production shader
and material references are authored assets, and all 81 campaign scenes remain registered.

## 2026-10-17 hard shadows and fixed geometry detail

VISION-006 now specifies hard cast shadows and disabled automatic geometry LOD switching.
The production URP asset disables soft shadows, uses a 4096 directional shadow atlas,
one cascade and a 150 m shadow distance. Every quality tier uses this asset and hard-only
shadows. LOD crossfade is disabled and LOD0 remains included in builds.

`CelShadowDetailAuthoring` authors `forceMeshLod=0` on MeshRenderer and SkinnedMeshRenderer
components in game-owned prefabs and saved scenes, including inactive children and SubScenes.
Unity clamps `QualitySettings.meshLodThreshold` to .01, so lowering that threshold alone
does not disable LOD. The explicit per-renderer override disables automatic selection.
Any conventional LODGroup is reduced to its LOD0 renderers with infinite distance and
no crossfade. No authored LODGroups or generated model LODs were found in the initial audit.

The explicit command is `Crowd Punch > Art Direction > Apply Hard Shadows and Full Detail`.
Full Cel migration also applies these settings, and level creation recipes retain hard
lights. The Chapter One model/landscape recipes author LOD0 overrides directly.
Vendor model imports, texture mipmaps, gameplay logic and collision geometry are preserved.

This trades automatic geometry simplification for stable silhouettes and shadow shapes.
The larger shadow map uses more GPU memory than the previous 2048 atlas. A target-hardware
performance benchmark is still required before claiming a frame-rate target.

Unity C# compilation succeeded. The command-line `dotnet build Assembly-CSharp.csproj
--no-restore --no-dependencies` again exited unsuccessfully with zero reported errors or
warnings; it is not counted as successful verification.

Reference: [Unity Mesh Renderer LOD override](https://docs.unity3d.com/6000.3/Documentation/Manual/class-MeshRenderer.html#mesh-lod).

Verification for this refinement:

- The explicit migration completed all 265 game prefab/scene assets successfully.
- A serialized audit found all 12,078 MeshRenderers and 24 SkinnedMeshRenderers fixed at
  LOD0, all 112 shadow-casting lights set to Hard, and 21 intentionally unshadowed lights
  preserved. No conventional LODGroups remain in these assets. All six quality tiers have
  hard-only shadows, no LOD crossfade and the lowest mesh-selection threshold.
- Compared modified scene/prefab components to HEAD: original geometry, transforms,
  collider data, gameplay parameters and renderer material references are unchanged.
  Unity also persisted 962 existing implicit default fields and updated one old renderer
  field spelling; these were inspected separately from the presentation edits.
- The existing Bootstrap/camera/Full Cel post-processing regression test passed.
- Live Chapter One rendering confirmed soft shadows=false, a 4096 atlas, one cascade,
  150 m range, Hard on the active sun, zero automatic GameObject mesh LOD selectors,
  zero LODGroups and zero ECS MeshLODComponents. Frames advanced from 4 to 1446 before
  pausing simulation for shifted-camera comparisons. Captures are in `Temp/HardShadows`.
- The pre-existing Synty Sidekick Tool Downloader error recurred on Play entry. No new
  environment/runtime rendering error was observed. A standalone build and target-hardware
  performance benchmark were not performed.

## Roof shadow edge and dash projection correction

The initial 4096 / 150 m single-map setting produced visible shadow texels on nearby
walls. It is superseded by an 8192 map across 110 m (still hard, still one cascade).
The live render texture was verified as `_MainLightShadowmapTexture_8192x8192_Shadowmap`.
This increases shadow memory and rendering cost; no target-hardware benchmark is claimed.

The dash's 5.5-degree FOV pulse changed the directional shadow fit, making static roof
shadows crawl. `CombatFeedbackSettings.DashFovDelta` now defaults to zero, and the saved
Resources settings explicitly use zero. Dash movement and other impact feedback remain
unchanged. A controlled Input System right-button event, followed by the controller update,
started the real dash path. Forty rendered feedback frames retained FOV=60 throughout.
Fixed-culling/projection experiments were not retained: live-frame tests showed that they
did not independently stabilize URP shadow fitting.

Detailed roof tiles also contributed a serrated caster outline. All ten Chapter One
scenes now contain 137 clean, two-plane roof shadow proxies using one shared mesh.
Only the proxies cast the tiled roof outline. Terracotta materials do not receive cast
shadows, preventing the proxy from self-shadowing the detailed visible tiles. Other
surfaces continue to receive shadows. Proxies are visual-only, shadows-only, static and
fixed at LOD0. The village authoring recipe preserves this setup.

Unity compilation succeeded and the village shader reported no compiler messages.
All nine existing CombatFeedbackTests passed after disabling the FOV pulse.
The final Chapter One scene/presentation regression also passed with the roof proxies
and updated shader. No prototype culling component or roof-caster experiment remains
in the project. Final close-up: `Temp/ShadowStability/FinalCleanRoof.png`.
Preview comparisons and the final close-up are in `Temp/ShadowStability`.
