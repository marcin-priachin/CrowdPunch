# Art Lab

VISION-006 selects Full Cel as the production default (OQ-015 resolved on 2026-10-16).
Art Lab remains available for optional comparisons; VISION-005 crowd readability remains
the criterion. Current now means the authored Full Cel baseline, and a newly opened
window defaults all four selectors to Full Cel.

Open **Crowd Punch > Art Lab > Open Art Lab**. Select legacy arena 28-31 and
**Play selected arena**. Use **Complete style** for Current polished, Soft toon hybrid,
Full cel shading, Graphic comic, or Miniature / diorama. The original scene setup is
restored after leaving Play mode.

Environment shading, player/enemy shading, lighting and post-processing can also be
selected independently. Current means original materials or original chapter grading.
Chapter white balance is inherited rather than overwritten by every style.
Store A and B to compare combinations. Freeze action stops simulation time while the
rendering loop continues. Save/recall camera and Arena overview keep framing consistent;
Resume camera restores the normal follower. Save screenshot exports the camera image
without overlay HUD. Restart encounter and arena switching resume action.

Preset assets are under `Assets/CrowdPunch/Data/ArtLab`. Inspect preset opens lighting
and Volume references in the Inspector. After editing a preset, use Refresh preview.
Closing the window, disabling preview, leaving an example, or exiting Play restores
the original materials and lighting. Imported materials and source scenes are not edited.

The custom forward shading preserves the generated EnemySkinning and Sidekick graph
deformation, material properties, shadow and depth passes. Shader generation snapshots
the installed graphs/URP; source graph or package changes require regenerating the
affected generated shader assets. Create Missing Assets does not overwrite existing
presets or shaders. The generated shaders are URP-only.

Comic uses surface silhouette ink and a subtle screen-aligned halftone in shadowed
surfaces. It is not a separate geometry outline pass. The imported outline feature's
override material was not applied to animated crowds because it does not automatically
preserve their deformation. Particle/hazard shaders are preserved. No depth of field,
motion blur or new gameplay HUD is added by the preview tool.

Editor statistics are observations, not target-hardware benchmarks. Preview substitution
has editor-only overhead and is excluded from player builds.

Original Art Lab verification on 2026-10-16 (before promotion to production):

- Unity script compilation succeeded; generated environment, enemy and player shaders
  report no ShaderUtil errors or warnings.
- ArtLabTests: 2/2 passed, covering independent shading channels, source preservation,
  ECS material/mesh ranges and colours, prefab spawning and restoration.
- GauntletProgressionTests: 32/32 passed, including examples 28-31.
- Visually compared all five complete presets on one frozen Gauntlet 28 encounter,
  including the animated player and ECS enemies. Comparison captures are in
  `Temp/ArtLabReview` (temporary local output, not committed assets).
- Switched through Gauntlets 29, 30 and 31 in Play mode and inspected their rendered
  environments and crowds. Returning to Gauntlet 27 disabled the bridge, restored
  all ECS array substitutions, and left no GameObject Art Lab material assignments.
- Exiting Play mode released temporary materials and restored the original scene/start
  scene configuration. The existing Synty Sidekick Tool Downloader invalid-window
  error still occurs during Play mode layout changes; it is unrelated to Art Lab.

These checks establish Editor preview behavior, not standalone-build performance or
the final production art direction. Target-hardware profiling remains future work.
