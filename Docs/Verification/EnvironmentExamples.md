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
its directional lighting and two ambient particle systems (32 particles each).
Those particles are decorative and remain outside the playable floor. The original
ECS SubScene owns floor, perimeter, navigation, objectives and spawning. Copied waves
and project-owned URP materials are under `Assets/CrowdPunch/Data/EnvironmentExamples`.
Existing objective hit/completion effects remain in use. No new runtime scripts,
gameplay libraries or per-enemy effects were added.

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

## Editing

Saved scenes are the playable deliverable. The create recipe refuses to overwrite
existing examples. **Refresh Example Scenery 28-31** replaces their generated scenery
roots and refreshes the layout/facing treatment; do not use it to preserve manual
edits inside those roots. Physics and source encounters remain independent of scenery.
