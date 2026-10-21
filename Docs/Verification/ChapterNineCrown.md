# Chapter Nine - Crown Above the Clouds

Date: 2026-10-21. Workspace: Agent A, Unity 6000.3.10f1.
Requirements: VISION-004..006, FINALE-001..007, COVER-001..005,
SHELL-001..006 and TRACK-001..006.

## Marble and silhouette refinement, 2026-10-21

The user's follow-up screenshot showed ivory columns merging into the similarly
coloured walls. Saved Level 81 now separates warm ivory marble piers from cooler
blue-grey marble wall/rear/return panels. The exterior terrace is pale grey marble;
the original combat floor stays mid-value blue-grey, with quiet 6 m slab joints.
Directional veins and soft pigment washes retain the established matte cartoon
finish without specular, normal distortion or extra geometry.

`PaintedMarble.hlsl` samples a continuous, metre-scaled spatial field. Derivative
filtering and a distance fade restrain thin veins; a default-off material strength
keeps earlier chapters unchanged. Only chapter-owned fixed architecture, terraces
and the floor opt in. Objective, character, hazard and rock skins retain their
previous material language. `Refresh Chapter Nine Marble Finish` assigns the
separate facade/terrace recipes without moving or rebuilding any object.

Against the current saved baseline: **all 128 bake/mesh/lighting files and all 25
receiver mask/chart/source signatures are unchanged**. Both scenes retain every
serialized component value except renderer material references. No fixed geometry,
UV charts, sun, lightmap indices, physics or gameplay settings were changed.
The four Chapter Nine presentation tests pass again. An initial shader compilation
error from a reserved variable name was corrected; the final shader imports and
renders without compiler diagnostics. Updated captures are in
`Images/ChapterNineCrown/Marble`.

Live Very Low and Ultra samples each captured 37 enemy bodies after 300 advancing
frames, with zero shader errors, zero realtime shadow casters and shadow distance
zero. Each quality was selected before a level reload so the existing baked-policy
clone belongs to the selected tier. An initial sample that changed quality after
loading bypassed that clone; it was replaced with the reload-based sample. The
script restores the original quality and the Editor returns to Bootstrap. These are
controlled health-guarded readability samples, not a human playtest or performance
benchmark. The focused .NET build passes again (0 errors, 34 existing warnings).

Campaign 81 retains its original 100 m court, boss, anchors, supply and collision
layout. Quiet blue-grey paving supports ivory galleries, royal-blue roofs and gold
details. Each facing side has a different skyline: crown palace, bell gallery,
sky gate and observatory. Connected front piers, return walls, recessed backing,
complete caps and two closed cloud-bank layers compose the elevated surroundings.
Scenery adds no colliders and stays outside the combat envelope.

The four existing installations retain their state/ownership rules. Two masonry
targets have fitted segmented construction, copper strike faces, gold reinforcement,
branching damage on four sides and existing state-driven breakup. The rotating
shield has blue panels and gold opening jambs. Reinforced blue shell armour exposes
a contrasting ivory/turquoise core, with existing shell-fragment feedback. A complete
royal transfer trolley has strike panels, low rollers, cargo, rail sleepers, matching
hardware and the original state-driven docking socket. No new gameplay clocks,
entity queries in MonoBehaviours, HUD elements or interaction rules were added.

Owned painted surfaces use continuous pigment, restrained physical texture scales,
world mapping on fixed objects and local mapping on moving/breaking skins. Texture
copies use full mips, repeat/trilinear, anisotropy 4, a 2048 cap and BC7 on Windows.
Only decorative materials opt into the existing camera cutaway. The owned post
profile uses exposure .18, contrast 3, saturation -5 and bloom .015, retaining
Neutral tonemapping, HDR, SMAA and vignette .12. Clear warm sun, cool fill and
blue-white distance haze follow PLANS.md; haze begins at 101 m and ends at 204 m.

## Verification

- Compared serialized original components by file ID: all 250 original transform
  poses, MonoBehaviour authoring and physics components retain their values.
  Transform child lists only gain presentation children. All 2,343 existing
  campaign-data files remain byte-identical. Original floor/collider geometry,
  settings, waves, IDs and progression are preserved.
- Unity compilation succeeds. Four Chapter Nine presentation tests pass: clear
  scenery envelope, masks/probes/receiver sources, four different anchor owners
  with dynamic skins, and complete cargo/shell/damage presentation.
  The existing finale/objective regression runner also passes 69/69 cases.
  The first Chapter Nine run exposed an empty-scene test-teardown assumption;
  fixed the teardown and reran all four cases successfully.
- .NET: the normal `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:q`
  was stopped while rebuilding the generated Unity package graph. The focused
  build with `-p:BuildProjectReferences=false` then passed in 27.22 seconds,
  with 0 errors and 34 existing reference/deprecation/unused-field warnings.
  Unity compilation remains the validation of the new Editor authoring scripts.
- Eight lightmap/shadowmask atlases and saved probes are present. Fixed box charts
  keep metre proportions and spans no longer than 12 m. Moving/state-dependent
  objective skins are excluded from static caster geometry. A pending script reload
  interrupted the first bake handler; cancelled and restarted after compilation.
  The final bake completed one scene with no bake error.
- Repeated surface refreshes retain all 25 receiver mask/chart/source bindings and
  all 128 bake/mesh/lighting files byte-for-byte. No rebake is needed for those
  material-only refreshes.
- Matched controlled Editor renders cover entry, the four anchors and overview in
  `Images/ChapterNineCrown/Before` and `After`. These renders show all authoring
  damage pieces before ECS presentation hides them; Edit Mode also retains its
  normal shadow policy. Runtime captures are separate evidence.

## Controlled runtime review

The existing `FinalePlayCheck` ran against the saved, baked Level 81 in isolated
Editor preview. It injects actions and restores player health; this is verification
tooling, not a human playthrough. Initial supply instantiated 37 physical bodies.
Flight strike/sweep warnings, falling, landing, initial stun, a real swept launched
body impact (400 -> 388 boss health), takeoff, spent anchors, fourth-use reset,
player-death retry, later supply stages and immediate victory cleanup all passed.
The fourth-use check injects completion handoffs for the remaining installations;
it is not evidence of manually solving each objective.

The current saved downed window is 10 seconds, with 7 seconds of initial stun.
The older probe expects both grounded warning types in one downed window. After
checking the original fall/stun/impact, extended only the temporary live ECS downed
window to 20 seconds to review both slam and shockwave. Saved settings were never
edited; retry reloads the authored tuning. Do not interpret this probe adjustment
as a gameplay fix or a change to encounter balance.

Captured advancing flight/fall/downed/takeoff/defeat states and active/paused/spent
anchor rings. Every sampled phase used one baked policy, zero runtime shadow
distance and zero realtime shadow casters. The new masonry has 52 dynamic visual
pieces across the two targets. Existing boss appearance, animation, cyan vulnerable
ring, attack telegraphs, crowd models and specialist cues remain visible.

Additional advancing samples cover 81 -> 80 -> 81, Ultra quality, a south-edge
cutaway enabled/disabled pair, and retry. Crown scenery unloads at 80, reappears at
81, and has one baked policy after reload/retry. Cutaway shader globals return to
zero after every sampled render. The sampled edge view remains unobstructed in both
captures; this pair does not prove clipping through a deliberately blocking building.
Restored Very Low quality, stopped Play, and returned the assigned Editor to Bootstrap.

On i9-14900KF / RTX 4090, a five-second stationary Ultra Editor sample with 66 enemy
bodies recorded 291 samples: median frame interval 16.69 ms, p95 22.01 ms and mean
384 draw calls. The existing stage-three probe independently recorded 709 frame
samples with mean 16.94 ms and p95 17.83 ms. These include Editor scheduling and
inspection overhead, and are not standalone FPS or target-device measurements.

## Acceptance limits

The transition through unchanged Chapter Eight level 80 reports an enemy-animation
sample source-hash mismatch from `EnemyAnimationBaker`. The same level-80 error is
present earlier in the Editor log before this chapter's implementation. It is outside
the Level 81 visual slice; no character samples or models were regenerated. Level 81
completed its controlled runtime checks without that error. The Console retains the
level-80 diagnostic after returning to Bootstrap.

Controlled Editor checks do not establish human combat readability, encounter
balance, target-device performance or a standalone build. OQ-001 hardware and
performance acceptance remains open. Cross-chapter/full-quality acceptance in
PLANS.md remains a broader project task.
