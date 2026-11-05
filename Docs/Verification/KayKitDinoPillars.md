# KayKit Dino Pillars

## Prefab reference repair (2026-11-05)

The final text-based scene ordering cleanup omitted 12 `stripped` prefab reference
records in each campaign SubScene. That happened after the original runtime review
and broke the boss hierarchy on subsequent imports. The records were restored
verbatim from the starting revision in both levels. The original nonvisual
comparison did not recognize suffixed document headers and missed this corruption.
Do not run the temporary text-based ordering script again; use Unity serialization.
`Pillar006_SavedScenesRetainLocalPrefabReferences` now validates every local file ID,
including stripped prefab records. All 12 boss presentation tests pass after repair.
Fresh runtime loads of both 40 and 80 now contain six boss-owned animated pieces:
the body with 45 skin matrices and five accessories with one matrix each.
The corrected full-record comparison preserves 106 original nonvisual records in
40 and 115 in 80, including all 12 restored prefab records per scene. Editor Play
was stopped after these checks. The unsafe temporary ordering script was removed.

Date: 2026-11-05. Requirements: VISION-005/006, PILLAR-002/003/005/006.

Campaign 40 and 80 now use the selected KayKit Dungeon Pack 1.1 `pillar.fbx`.
The source FBX, textures, import settings and licence are unchanged. The focused
menu command is **Crowd Punch > Bosses > Apply KayKit Pillars To Campaign 40 And 80**.
Legacy Gauntlet 22 is outside this visual update.

Generated meshes and six material variants live under
`Assets/CrowdPunch/Data/DinoBoss/Pillars`. The mesh retains all 136 source triangles.
Only its two plain shaft spans are lengthened; the cap, base bevels and chipped
middle collar retain their width-relative proportions. All geometry stays inside
the existing 3 x 18 x 3 m envelope, grounded at the unchanged fall pivot. Three
single-material renderers retain the previous three-renderer budget per pillar.
There are no added colliders, lights, LOD groups or runtime scripts.

The Observatory uses limestone with a sage collar and restrained ochre/brass lips.
The Citadel uses ash stone, a dark slate collar and brass lips. Copied production
Painted Foundry materials retain quiet stone detail and local mapping, without
scenery fog/cutaway. Multiplicative ECS event tint preserves these palettes at rest
and the existing impact/regeneration flashes. Chapter objective appearance passes
recognize the new visual hierarchy and retain all three material slots.

## Verification

- Unity 6000.3.10f1 compilation passed without errors. The approved Unity CLI
  update completed, verified as 1.0.0-beta.16.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:q` passed with zero
  errors and 318 existing package/reference/obsolete-API warnings.
- Saved-scene checks found three pillars per level, three renderers per pillar,
  combined bounds exactly 3 x 18 x 3 m, base alignment and no GameObject colliders.
- Serialized comparisons against the starting Git revision preserved 94 original
  nonvisual objects/components in 40 and 103 in 80, including pillar root poses,
  authoring, boss settings references and other arena content. Only pillar visual
  descendants changed; serialization block ordering is immaterial.
- Isolated Editor Play previews loaded both campaign encounters and baked three
  pillar entities plus nine tinted render entities each. Controlled camera captures
  below show the actual runtime materials; these use an inspection camera pose.
- A controlled player-owned topple invoked the existing eligibility/transition
  method successfully in 40. All three pieces received the same impact tint and
  inherited identical falling rotation vectors. In 80, advancing the existing fall
  system produced a 0.594-radian rotation shared by every piece. Advancing the
  existing miss lifecycle hid the pillar (scale 0, Waiting), then restored Upright,
  scale 1 and the same regeneration tint on all three pieces. These are injected
  lifecycle checks, not a human combat/balance playthrough.
- `DinoGoblinPresentationTests`: 10/10 passed.
- `DinoPillarTests`: 29/31 passed. The two failures are
  `Pillar006_SupplyHonorsCountAndReusesLaunchedOrPooledSlots(1)` and `(4)`, which
  expected 1/4 spawned members but got 0 at line 283. These use an isolated synthetic
  ECS fixture; their tests and crowd-supply/runtime code were not changed by this
  presentation update. Ownership, fall direction, contacts, regeneration and restart
  cases passed. The unrelated supply failures remain unresolved.
- Editor Play was stopped and Bootstrap restored. No gameplay settings, source
  models or baked environment lighting assets were changed.

![Campaign 40 limestone, sage and aged brass](Images/DinoPillars/Level40-KayKit.png)

![Campaign 80 ash stone, slate and brass](Images/DinoPillars/Level80-KayKit.png)
