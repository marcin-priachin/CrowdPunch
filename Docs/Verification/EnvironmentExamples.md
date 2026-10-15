# Legacy environment examples

Created 2026-10-15. These are OQ-015 art experiments using existing gameplay.

| Legacy level | Environment | Existing objective/source |
|---|---|---|
| 28 | Sunlit Village Gate | Barricade and exit, Gauntlet_13 |
| 29 | Emberworks Foundry | Sliding rail object and socket, Gauntlet_16 |
| 30 | Overgrown Observatory | Rotating cover target, Gauntlet_14 |
| 31 | Frostbound Vault | Explosion-only shell and exposed core, Gauntlet_15 |

Open **Crowd Punch > Levels > Level Play Window**, choose **Legacy**, then select
28-31. Bootstrap registers all four after 27. No campaign catalogs, build scenes,
save identifiers, enemy profiles, original encounters or imported assets changed.

Each main scene contains editable imported-model scenery, low-contrast paving,
its directional lighting and bounded ambient particle systems.
Those particles are decorative and remain outside the playable floor. The original
ECS SubScene owns floor, perimeter, navigation, objectives and spawning. Copied waves
and project-owned URP materials are under `Assets/CrowdPunch/Data/EnvironmentExamples`.
Existing objective hit/completion effects remain in use. No new gameplay libraries
or per-enemy effects were added. `ExampleEnvironmentLighting` scopes presentation
settings to an example's loaded lifetime.

## Verification

- Unity compilation succeeded. Final `GauntletProgressionTests` run: **32 passed,
  0 failed**, including all four new scenes, valid floor/bounds/spawn references,
  31 legacy registrations and campaign-only build isolation.
- Loaded each example through Bootstrap in Play mode and inspected the gameplay
  camera. Observed live enemy populations of 16, 9, 10 and 13 respectively during
  the checks; these observations are not initial-wave count assertions.
- Confirmed an advancing player loop and a live default ECS world. An initial
  inspection during a script refresh was discarded and repeated after compilation.
- Controlled rail-state retargeting moved both render children with the objective
  by the same 2 metres. Restart returned its distance to zero and left exactly one
  scenery root and two ambient particle systems. This is a presentation/lifecycle
  check, not a claim of completing the encounter through normal player input.
- Original collider meshes, objective mechanics and settings remain shared/read-only;
  imported-model orientations and the nonuniformly scaled gate facing were checked
  and corrected in project-owned scene authoring.
- No new gameplay/baking errors appeared in the checked Play session. Existing
  player Animator-on-disable warnings occurred when exiting Play mode.
- The broad `dotnet build Assembly-CSharp.csproj --no-restore -m:1` was stopped
  while rebuilding the extensive Unity package dependency graph. It did not produce
  a final result; Unity compilation and the in-Editor tests above are the verification.

Human combat playtesting is still needed for aesthetic preference, camera composition
throughout movement and hardware-specific performance. These examples do not settle
OQ-015 or claim a completed campaign art pass.

## Environment finish pass

Each example now contains a 600 m surrounding landscape, multiple depth layers of
scenery, a winding service path, ground cover and a distinct skyline: village bell
tower, industrial chimneys/gantries, ruined observatory, or mountain fortress.
The flat presentation backdrop is inactive; the original combat floor remains intact.
Four perimeter practical lights and emissive lanterns support the themed lighting.
The foundry additionally has eight bounded chimney plumes (30 particles each).

The examples use a separate URP rendering asset with two soft shadow cascades and
half-resolution SSAO. Per-level Volume profiles provide ACES tonemapping, restrained
bloom, colour balance and vignette. HDR, post-processing and SMAA are enabled on the
gameplay camera while an example is loaded. Distance fog starts beyond the combat
space. No depth of field, motion blur or new gameplay effects are involved.

The imported free assets were sufficient for this pass. Optional sky additions:
[AllSky Free](https://marketplace.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014)
or [Poly Haven Kloppenheim 06 Pure Sky](https://polyhaven.com/a/kloppenheim_06_puresky).
These have not been downloaded or integrated. Poly Haven assets are CC0; AllSky's
store listing reports URP compatibility, which has not been tested in this repository.

Finish-pass verification: Unity compiled without errors; all 32 progression tests
passed again. Inspected all four environments in Play mode through the gameplay
camera and a temporary wider review framing. Confirmed active per-level ACES and
bloom overrides, HDR/post-processing and the separate example renderer. Restarting
31 left exactly one environment component, one Volume and two scene ambient effects.
Selecting 27 restored the original quality pipeline, Default-Skybox, fog disabled,
post-processing disabled, no antialiasing and zero example Volumes. No new runtime
errors appeared in that checked session. Existing Animator disable warnings remain.
The village review reported roughly 1.36 million triangles and 836 batches in the
Editor; this is a spot observation, not a target-hardware frame-rate benchmark.
The added scenery and SSAO still need performance testing on the intended hardware.

## Editing

Saved scenes are the playable deliverable. The create recipe refuses to overwrite
existing examples. **Refresh Example Scenery 28-31** replaces their generated scenery
roots and refreshes the layout/facing treatment; do not use it to preserve manual
edits inside those roots. Physics and source encounters remain independent of scenery.
**Polish Example Environments 28-31** replaces only the `World and atmosphere` child
and refreshes the example lighting profiles. **Refresh Example Scenery 28-31** includes
that finish pass. Both commands preserve original encounters and imported source assets.
