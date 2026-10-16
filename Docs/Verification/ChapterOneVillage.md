# Chapter One: Sunlit Border Village

Implemented 2026-10-16 for VISION-004, VISION-005 and VISION-006.

All ten existing campaign stages retain their encounter names and progression IDs.
Their visual settings are:

| Stage | Environment |
| --- | --- |
| 01 | Orchard Commons |
| 02 | Stonecutters Lane |
| 03 | Market Court |
| 04 | West Village Gate |
| 05 | Caravan Square |
| 06 | Mill Courtyard |
| 07 | Bell Tower Garden |
| 08 | Harvest Fairgrounds |
| 09 | Watchtower Approach |
| 10 | Gatekeepers Keep |

Chapter-owned cel materials, quiet courtyard paving, limestone boundaries, terracotta
houses, foliage, paths, fences, props, landmarks and rolling meadow backgrounds are
saved in the scenes. Decorative scenery has no colliders. Landscape placement adapts
to each floor, including the 100 m wave arena and circular Gatekeeper arena.
Trees that intersect building bounds are removed from the authored composition.

The production Full Cel Volume remains at priority 150: Neutral tonemapping, exposure
.25, contrast 5, saturation 4, bloom .06 and vignette .12. A priority-160 chapter Volume
overrides only white balance (temperature 4). Warm directional light, cool ambient fill,
sky and distance haze are scoped to each additive level by `CelLevelLighting`.
The optional sky/fog override defaults off outside Chapter One.

## Verification

- Unity compiled the final C# successfully. Final CelPresentationTests: 83 passed,
  including all 81 campaign scenes, game prefabs and Bootstrap/profile checks.
- CampaignTests: 8 passed, including chapter assets, progression and boss geometry.
- Compared every serialized SubScene component against the original Git revision.
  All existing transforms, colliders, mesh filters, encounter and objective references
  remain unchanged. Differences are renderer materials/visibility, whitespace, and
  Unity persisting existing default fields (`overrideGroundSurfaceHeight: 0`,
  `groundSurfaceHeight: -1`, `finaleBoss: null`). No SubScene objects were added/removed.
- Visually inspected Play Mode stages 01, 03, 06, 08 and 10. Wider inspection camera
  positions were temporary and were not saved to Bootstrap. Frame-count checks confirmed
  the player loop advanced; simulation was subsequently frozen for visual inspection.
- Corrected foliage atlas border artifacts, open house gables, overlapping tree canopies,
  single-sided banners and paving outline extraction for the round boss floor.
- Runtime volume checks confirmed Neutral tonemapping, .06 bloom and temperature 4.
  Transitioning from Chapter One to Campaign 11 restored fog=false, Default-Skybox and
  temperature 0. No chapter atmosphere leaked into the following chapter.
- No new game runtime errors were observed. The pre-existing Synty Sidekick downloader
  invalid-window error recurred during Play Mode entry. Imported `.blend` files also
  had pre-existing missing-Blender errors; this pass uses the supplied FBX models.
- `dotnet build Assembly-CSharp.csproj --no-restore` failed during generated project
  reference resolution without reporting compiler errors. A single-worker retry rebuilt
  many Unity package projects with MSB3277 dependency warnings and was cancelled after
  several minutes. Unity compilation and the Editor tests above are the verified checks.
- No standalone player build, full campaign combat playthrough or target-hardware
  performance benchmark was performed.

Preview captures are outside Assets in the ignored `Temp/VillagePreviews` folder.
Bootstrap is left ready to preview Campaign 01 on the next Editor Play session.

The explicit authoring command is `Crowd Punch > Art Direction > Dress Chapter One -
Sunlit Village`. It replaces the generated scenery root and reapplies the chapter's
visual settings; make manual edits inside that root after running the recipe.
