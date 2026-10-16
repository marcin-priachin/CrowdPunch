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
