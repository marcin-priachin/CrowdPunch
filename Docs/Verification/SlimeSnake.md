# Slime Snake verification

Requirements: SNAKE-001..007, INFO-004, VISION-006.

The shared EnemySnake prefab now uses Slime Variant at authored scale 2, its supplied texture,
Full Cel shading and sampled FBX animation. Ground spawning uses Slime's Spawn (the pack has
no clip named Spawn to Ground Idle). Existing physical tail, tip-only eligibility and gameplay
settings are preserved. Rebuild using Crowd Punch > Enemies > Rebuild Snake Prefab.

Slots: 0 Idle; 1-2 Move Forward In Place; 3-4 Head Attack (reserved); 5 Take Damage;
6 reverse Die; 7 Spawn; 8 Die (reserved); 9 Roll Forward In Place; 10 Die.

Validation (2026-10-25):

- Unity compilation passed; dotnet build Assembly-CSharp.csproj --no-restore passed with
  existing package/reference and deprecated-API warnings, no errors.
- 39 focused Snake/Slime assertion cases passed inside the assigned Unity Editor.
  The normal Test Runner was cancelled because it requested saving the user's already-dirty
  New Scene. Assertions ran directly with each fixture's setup/teardown, without scene switching.
- A broader direct run passed 90/91 cases before the final tail-color assertion consolidation.
  Its only failure was the existing Dasher scale expectation (expected 1, authored value 1.3).
- Isolated Unity baking produced the seven-bone Slime animation, a 0.8333334-second Spawn,
  animation-driven renderer ownership. Actual baked
  spawn/launch/recovery/defeat playback preserved the owner transform and physics velocity.
- Every sampled grounded pose clears the capsule floor; Spawn starts fully below it and ends
  at ground Idle. Animated vertices fit the renderer's culling bounds. The saved Idle skeleton
  matches the sample cache and its dependency hash is current.
- A rendered Idle preview confirmed the supplied texture and Full Cel shading.
- 64 actual sampled Slime heads: 20 warmups and 120 completed animation-system updates averaged
  0.03554 ms/update in an isolated Editor world. This excludes physics/rendering and is not an
  FPS or target-hardware result (OQ-001 remains open).

No live encounter playtest was run; the user's dirty scene and preview objects were preserved.
Local detailed evidence is under Temp/SnakeValidation/ (ignored by Git).

Follow-up (2026-10-25): protected tail defaults to matching Slime green; the head receives a
masked vulnerability color only when its tail is exhausted. Segment Radius (0.6m), Segment
Spacing (1.5m centre-to-centre) and Segment Height Offset (-0.4m relative to the head physics
origin) are editable on SnakeSettings. Size affects collider/navigation/contact radius as well
as the sphere visual. Attached height survives physics projection; detached pieces retain
their last height. Positive and negative offsets are supported.

Follow-up validation: Unity compilation and isolated baking passed; all 62 Snake-filtered
Editor tests passed through the standard Test Runner. The cel shader reports no errors, and
a two-state render confirmed green protected / gold exposed bodies with eyes preserved.
The current controller had reverted to Birb clips between requests; Rebuild Snake Samples
repaired Slime bindings/cache in place while retaining prefab transforms and settings.
The attached-height motor benchmark (64 heads + 384 segments + 200 unrelated bodies,
20 warmups + 120 completed updates) measured 0.310247 ms/update with no calling-thread
managed allocations, excluding physics and rendering. The current command-line dotnet build
returned failure with 0 errors/0 warnings and no diagnostics; Unity remains the compilation
source of truth. The earlier successful dotnet result above is a prior snapshot.

Startup tail collision follow-up (2026-10-25): same-owner attached contacts were already
filtered, but attached tails from different Snakes could repel one another. A real Unity
Physics probe reproduced 0.15m separation per sphere and 1.5m/s residual speed after five
0.02-second steps, with no attack. The filter now excludes every attached-to-attached pair.
Detached pieces preserve the existing owner-generation/external-bounce contact gates.
The tail motor also runs while a head is in ground-spawn recovery, steering displaced
segments back to the seeded path without teleporting them. Ordinary recovery and launched
head movement gates remain unchanged. Tests cover overlapping bodies in the real solver,
same/different owners, restored released contacts, and the spawn-only motor exception.
The Editor retained the old compiled Burst contact job during verification; the renamed
SegmentContactFilterJob refreshes that cache entry.

Startup-fix validation: Unity compilation passed; all 68 Snake-filtered Editor tests passed,
including actual-solver attached/released contact cases with Burst enabled. A first full
run was contaminated by a concurrent Pipeline timeout log while Burst compiled; the clean
rerun passed 68/68. Standalone dotnet returned failure with no diagnostics (0 errors/0
warnings). The saved radius 0.6m, spacing 1.2m and height offset -0.9m were preserved.

Segment model amendment (2026-10-25): SnakeOrb retains its GUID, spherical collider and
attachment/launch lifecycle, but replaces the sphere renderer with the same Slime model as
the head. It shares mesh, material, texture, authored source scale, controller and sample
cache. SlimeSegment playback reads the head emergence clock while attached, movement intent
from the tail motor, and its own launch state after release. Protected models keep their
source texture; exposed tips/released pieces use the masked vulnerable color.

Segment-model validation: all 70 Snake-filtered Editor tests passed. Isolated baking and
actual tail spawning produced seven animated Slimes (one head + six segments), seven bones
per model, shared sample-cache references, correctly remapped segment owners and exactly
one gold attached tip. A rendered head/tail preview confirmed matching models and textured
faces. 448 complete animated Slime entities averaged 0.25910 ms per completed animation
update (20 warmups + 120 samples), excluding physics/rendering. Unity compilation passed.
Standalone dotnet again failed without diagnostics (0 errors/0 warnings). User tuning was
preserved; current saved radius/spacing/Y offset are 0.3m / 0.6m / 0m.
