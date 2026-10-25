# Armored Goblin Giant Dino (2026-10-25)

Implements the requested presentation revision for PILLAR-001/006 and VISION-005/006.
Campaign 40, Campaign 80 and legacy Gauntlet 22 now use
`Assets/CrowdPunch/Prefabs/Enemies/Goblin Giant Armored Variant.prefab`.
All authored transform scales, including the root's 1.5 scale, are preserved.
Translation grounds the exported poses; no collider fitting or runtime squash/stretch remains.

The Giant animation pack supplies Block for chase/burst, Cast Spell for the moving warning,
Take Damage for stagger, and Die for defeat. Warning/stagger playback follows the existing
combat timers. Eight blended directional jogs from the existing CrowdPunch animation
controller replace only leg bones during chase/warning/burst. Physics velocity selects
direction and stride cadence; the default leg playback cap is 2x. The pelvis, upper body,
shield, mace, helmet and shoulders retain the authored action pose. Death holds its final pose.

Dedicated Full Cel materials preserve the source textures and palette. LOD switching is
removed, mesh LOD0 is selected, and accessories are sampled as rigid skinned meshes.
Boss phase color flashes are removed; existing pillar colors remain. Gameplay tuning,
colliders, pillars, waves, arena geometry and boss health UI are unchanged.

## Verification

- Unity compilation passed with no compile errors.
- All 10 Dino presentation tests passed: every saved encounter, complete authored-scale
  comparison, six animated renderers, valid sample hashes, cel materials, all-pose floor
  clearance, phase selection, leg masking, constant palette and unchanged physics.
- The live run exposed a near-zero negative jog angle rounding to sector 8 in Burst.
  Sector wrapping was fixed in the shared sampler; both sides of zero now have regression tests.
- All 11 Chicken presentation regressions passed after that shared change.
- Broader boss appearance tests: 8 passed, 2 failed in unchanged Gatekeeper/Chicken floor
  assertions. Dino's source/floor and finite-transition checks passed.
- Dino pillar gameplay tests: 29 passed, 2 failed in the unchanged
  `Pillar006_SupplyHonorsCountAndReusesLaunchedOrPooledSlots` fixtures (counts 1 and 4).
  Those fixtures exercise supply without presentation systems; they are outside this revision.
- Live Campaign 40: frame advancement confirmed, all six animation entities baked with
  matching skin buffers, one body jog mask, and chase/warning/burst observed (phase mask 7).
  Rendered capture reviewed at `Temp/DinoGoblinValidation/campaign40-rerun.png`.
- Live Campaign 80: level index 79, six animation entities and one body jog mask confirmed.
  No new runtime errors followed the direction-wrap fix in either campaign. An unrelated
  Synty Sidekick editor-window error occurred while entering Play mode.
- Restored the original preview scene and play-mode start/background settings. Unity's
  test-runner save prompt was resolved by saving the user's preview edits.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` was attempted but spent several
  minutes rebuilding package projects and was stopped. The focused build with
  `-p:BuildProjectReferences=false` failed with 97 missing Unity-generated DLL metadata
  errors and four assembly-conflict warnings. Unity compilation and live baking passed.

No target-device performance run or full human boss-clear playtest was performed.
