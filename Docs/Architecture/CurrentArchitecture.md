# Crowd Punch â€” Current Architecture

Status: Repository snapshot
Last inspected: 2026-10-15
Unity: 6000.3.10f1

This document describes what exists now. It is not a desired future architecture and does not make prototype behavior into a design requirement.

## Goblin Giant Elite (2026-10-24, ENEMY-009, INFO-004/006, VISION-006)

`EnemyElitePrefabBuilder` retains `EliteEnemy.prefab` and its physics footprint while
instantiating the supplied Goblin Giant Elite Variant at its authored scale. Translation
aligns the visual floor with the capsule floor. The Humanoid Giant clips are sampled
into the existing 11-motion format; slots 3/4 use Sweep Attack, 5 Take Damage, 6 reverse
Die, 7 Spawn and 10 Die. `EnemyAnimationProfile.Goblin` uses the existing animation-driven
visual flag, suppressing material pulses and procedural shake/squash. Dedicated
`GoblinElite` materials preserve the source texture under `CrowdPunch/Cel/Enemy` and
are excluded from monster-atlas replacement. Spawn From Ground is enabled in the
Elite settings and reuses the warning, ground locking and respawn path.

`GoblinJogSampling` also generates Elite pelvis-relative leg samples. Elite full-body
and directional leg blending interpolate rotations separately from authored scale,
preventing shrinking between rotated poses. Upper-body and equipment poses remain
authored; skinning owns no physics transform or gameplay movement.

Elite locomotion reads resolved planar `PhysicsVelocity` after simulation, expressed
relative to current facing. It uses full directional leg poses even during slow setup
adjustments and keeps stepping through residual braking motion; an old contact-attempt
flag cannot suppress this layer. Direction changes do not vector-blend through idle.
`GoblinJogSampling` measures both feet's projected travel for each of the eight retargeted
jogs at authored scale and stores `EnemyAnimationAuthoring.JogCycleDistances` on the
prefab. The baker adds those distances to `EnemyJogSamples`; Elite gait phase advances
by actual travel divided by the blended directional stride, independent of the authored
maximum movement speed. Idle/blocked bodies stop locomotion. Baseline and Ranged retain
their existing playback path; spawn, recovery, defeat and authored upper-body poses
keep their existing ownership.

`EnemyAnimationAuthoring.MaxLegAnimationSpeed` caps jog playback relative to the original
blended clip duration (1 = original speed, 2 = double, 0 = unlimited). It is baked into
`EnemyAnimation` and clamps only gait phase advancement. The Elite prefab defaults to
2, and rebuilding it preserves the saved cap. Physical movement and upper-body clocks
are independent; reaching a low cap can permit foot sliding at higher travel speeds.

`ElitePunchSystem` enters `CommittedSweep` only after valid staging/alignment and
effect eligibility. It owns elapsed sweep time, normalized pose progress, one-shot
launch state, target reservation and committed direction. Setup revalidation and
navigation recovery no longer run during the committed animation. Crowd support holds
the selected projectile's staging position, and facing preserves the committed yaw.
The selected live projectile bypasses punch-volume and active/recovering/launched
eligibility changes at contact; other area victims retain ordinary geometry and flags.
Armor, defeat and pooling gates remain authoritative. Presentation reads sweep progress
directly, so damage feedback cannot restart the committed animation or move contact.
Wind Up Duration retimes anticipation to the original Enemy Launch Frame; the remaining
Sweep Attack frames play at source rate before cooldown. The builder records the
source clip's last frame and frame rate for the Inspector slider and baked timing.

## Chicken Proximity Cast And Teleport (2026-10-24, CHICKEN-002/006/007)

The default proximity response during Pause now plays the supplied full-body Cast Spell (1s),
then teleports to the farthest corner of the collider-inset rectangular arena relative to the
player's current position. The existing finish-pause alternative and committed shot/rush cycle
remain available. `ChickenCastTuning` owns the cast duration; the model builder samples motion 5
and serializes its clip duration in all three settings assets. AI advances Cast to a transient
Teleport phase. `ChickenTeleportSystem`, between AI and movement before physics world build,
owns the discontinuous position write, clears velocity and contact sweep history, preserves scale
and height, and resumes Pause. Ordinary movement remains velocity-owned. Cast contact uses the
existing health/protection/push bridge; no teleport path sweep is damaging. Hits and defeat cancel
the action through the existing phase transition. Cast animation bypasses directional jog masking.

## Goblin Chicken Boss Presentation (2026-10-24, CHICKEN-001/007, INFO-004, VISION-006)

`ChickenModelBuilder` replaces Chicken presentation in Campaign 20/60 and Gauntlet 20 with the
supplied Giant Wizard prefab at authored scale and Giant-pack Humanoid clips. CPA3 body, shoulder,
hat and wand samples remain ECS-owned; CPJ1 directional jogs override only legs below the
original pelvis. `ChickenSkinPose` preserves authored scales through rotation interpolation.
There are no phase tints or procedural deformation. Textured dedicated Full Cel materials are
protected from monster-atlas refresh. Simulation and Sweep playback share the configured
zero-based projectile release frame; `ShotSequence` preserves upper-body follow-through during
the immediate rush. Rush facing follows the current player independently of the committed
destination; the existing physics angular velocity applies yaw and the jog layer supplies backward
or sideways steps. Pause and Cast track current player facing too. During Pause, the movement
system writes signed idle yaw rate to `ChickenTurnState`; presentation selects leg-only Turn
Left/Right (samples 6/7), finishes the step before returning to idle, and repeats for continuing
turns. The existing jog leg mask selects the bones; turn legs are composed under the Idle pelvis.
Upper-body and equipment skin matrices share a continuous Idle clock, including on direction
changes and turn restarts. Cast/Sweep override idle turning. Export removes the clips' authored 90-degree body yaw
relative to frame zero, preventing a second rotation on top of physics facing. Reset clears turn
state and sampled playback. Paired releases retain minimum spacing, and hit/defeat cancel the Sweep.
Model alignment uses translation only. See `ChickenBoss.md` and `../Verification/GoblinChicken.md`.

## Goblin Minion Dasher Presentation (2026-10-23, ENEMY-005..008, INFO-004, VISION-006)

`EnemyDasherPrefabBuilder` instantiates the supplied Goblin Minion prefab inside the
existing Dasher physics root, preserving authored scale and the shared prefab GUID.
Its 11-motion samples use Idle, Walk Forward In Place, Run Forward In Place, Roll Forward In Place
twice (preparation/dash), Take Damage, reverse Die (rise), Spawn, Look Around (dash
recovery), Roll Forward In Place (launched) and Die. Preparation follows the telegraph clock;
the dash plays once across travelled/max distance, including obstruction redirects.
Launch loops Roll Forward In Place and repunch restarts it. Shared recovery uses Die then rise.
Animation writes only skin matrices. Dasher pose interpolation uses quaternion slerp
and retains the sampled bone scales, avoiding the shrinking caused by linearly
blending rotated matrix axes. Root and descendant scales match the supplied prefab.
`EnemyVisualOwner.AnimationDriven` suppresses
body tints, flashes, bobbing, shaking and squash. The obsolete grey-box
`DasherPresentationSystem` was removed; its bridge/impact ordering now follows
`EnemyReadabilitySystem`. Dedicated materials retain the pack texture with the
production `CrowdPunch/Cel/Enemy` shader and are protected from monster-atlas refreshes.
LOD0 and disabled moving-mesh shadow casting preserve the established rendering policy.
The model is aligned to the unchanged capsule floor through translation only.

## Goblin Baseline Presentation (2026-10-22, ENEMY-012, INFO-004, VISION-006)

`EnemyBaselinePrefabBuilder` retains the shared `EnemyBaseline.prefab` identity and physics
root while instantiating the user-authored Goblin Trooper variant as its visual source.
The pack's Humanoid clips are evaluated through its avatar in the Editor into the existing
11-motion, 32-frame sampled skinning format. `EnemyAnimationProfile.Goblin` selects idle,
walk/run, charging Run Forward, damage, airborne jump, Die and reverse-Die rise.
Both Goblin profiles hold motion 9 at the original Jump In Place frame 6 (6/30 seconds),
with identical matrices across all 32 samples. Playback uses the first sample without
advancing and tips the skin 90 degrees top-forward around LaunchPivot. Both prefab
builders configure the owner-centred pivot and include the tipped pose in render bounds.
Body and one-bone equipment use the same pose/pivot; owner yaw and physics keep their
existing responsibilities. Existing prefabs were updated in place, preserving authored scale.
Recovery divides the existing recovery clock between fall and rise; no gameplay clock or
physics transform is owned by animation. The mace is converted from a rigid hand attachment
to a one-bone skinned mesh so the same sampled ECS path animates its attachment.

Skinned render entities carry `EnemyVisualOwner.AnimationDriven`. Readability writes a
neutral material multiplier and impact presentation skips procedural deformation/flashes
for those entities. Other enemy profiles retain their existing feedback. Goblin-owned
materials retain the supplied textures under `CrowdPunch/Cel/Enemy` and are excluded from
the Ultimate Monsters atlas refresh. Source prefabs and imported pack materials are preserved.
The common spawn settings continue referencing the shared baseline prefab, including boss
ammunition and campaign waves. No runtime Animator or additional Mono-to-ECS bridge is added.

Both Goblin bodies optionally reference `EnemyAnimationAuthoring.JogSamples`. The Editor's
`GoblinJogSampling` retargets the existing `EnemyMovement.controller` and its eight directional
jog clips from `Animation/` onto the supplied Humanoid rig. CPJ1 blobs store only the eight
thigh/calf/foot/toe skin matrices relative to the pelvis, plus the pelvis bind inverse.
`EnemyAnimationBaker` validates the controller/mesh/avatar hash and bakes `EnemyJogSamples`.
The normal Goblin pose is evaluated first. While Active, the same animation job blends the
jog directions relative to owner facing, smooths movement weight, and composes the masked
legs below the original pelvis. Upper-body matrices and hand/head equipment stay on their
original pose, including contact attacks and ranged Cut Attack. A separate jog phase keeps
the gait continuous when upper actions change. Spawn, launch, recovery, defeat and Baseline contact charges bypass
the leg layer. Charges loop the supplied full-body Run Forward motion (slot 2); preparation
uses idle rather than Cut Attack. Ranged anticipation and projectile-release poses retain
their existing mapping. `EnemyFacingSystem` faces charging Baselines along planar physics
velocity, falling back to movement intent and committed direction at rest. It does not
modify launch/charge velocity, direction commitment or contact timing. No animation layer changes physics or owner transforms (INFO-004, ENEMY-002/012).
**Crowd Punch > Animation > Apply Goblin Jog Legs** updates existing prefabs in place without
rebuilding their original samples or model transforms; both prefab builders also configure
the leg samples for future explicit rebuilds. Render bounds include the leg-motion envelope.

## Chapter Eight Citadel Presentation (2026-10-21, VISION-004..006)

`ChapterEightCitadelBuilder` dresses saved Campaign 71-80 scenes. Chapter-owned
assets and scenery supply quiet ash paving, charcoal fortress galleries, muted red
banners, brass seals, complete gatehouses and a continuous volcanic horizon. The
seven existing level-74 sectors receive distinct roadside landmarks; stages 75/77,
78, 79 and 80 retain their protected lanes, muster ground, terrace and Dino layout.
Exterior padding follows the full route haze range, including level 74.
All decoration remains outside combat and has no colliders. Decorative materials
use the existing scoped camera cutaway; gameplay floors and objectives do not.

`ChapterEightCitadelObjectives` uses existing barricade, exit-cue, rotating-cover
and shell-fragment authoring. `ChapterEightReliquaryVisuals` owns level 76's royal
shell/core skin, fragments and branching damage. `ChapterEightProtectedPointVisuals`
marks the exact authored zones in levels 75/77 with flush turquoise stone and supply
inlays. No new gameplay state, ECS bridge, timer or objective rule is introduced.
Chapter 8 contains no cargo-track objective. The painted shader's opt-in
`_MultiplyEntityColor` mode lets the owned PillarStone/PillarBrass materials retain
their pigment under the existing Dino pillar phase flashes. Its default is zero,
so earlier skins keep their established replacement-tint behavior. No ECS data or
Dino phase logic changes.

`GroundHazardStyle.Citadel` appends a serialized presentation style for heated ash
masonry. The existing phase owner and bounded particle geometry retain full-area
warning, cooling wisps, active flame/ember and unload/reset behavior. Other chapter
styles retain their material flags. `CampaignShadowBake` selects chapter-owned
metric box charts and lower-density exterior/skyline charts only for this output.
Native shadowmasks, probes, receiver bindings and `BakedShadowLighting` use the
existing lifetime/restore path. Surface refresh preserves material GUIDs, source
references, shadow masks and chart transforms. Verification is recorded in
`Docs/Verification/ChapterEightCitadel.md`.

## Chapter Seven Vault Presentation (2026-10-21, VISION-004..006)

`ChapterSevenVaultBuilder` dresses the saved Campaign 61-70 scenes without rebuilding
encounters. `ChapterSevenVaultAssets` owns the painted slate, frost, ivory, brass and
crystal recipes and copied texture import settings. `ChapterSevenVaultScenery` composes
noncolliding vault galleries, complete arched entrances, frozen buttresses, imported
castle towers and two continuous glacial skyline layers outside the combat envelope.
The original collision-aligned floor remains the visible gameplay surface. Decorative
materials opt into the existing camera cutaway; floors and objectives remain excluded.

`ChapterSevenVaultSnow` authors one native ParticleSystem in each main scene, covering
the complete court/route and adjacent scenery. Its unlit faceted-flake shader uses
ice-blue bevels, antialiased edges, fog and near-camera fading. The emitter prewarms,
uses scaled time and world simulation, and varies falling speed, drift and tumble.
Area-scaled emission is capped at 100 flakes/second and 1,024 live flakes per scene.
It adds no runtime script, ECS state, particle collision, light or caster shadow.
Snow unloads with the level; adding it does not modify baked receivers or geometry.

`ChapterSevenVaultObjectives` binds segmented masonry, branching damage and exit cues
to existing authoring. `ChapterSevenReliquaryVisuals`/`ChapterSevenReliquaryMeshes` skin
the existing shell/core phases and fragment motion. `CampaignCargoTrackVisuals.ApplyVault`
supplies a complete crystal transfer carrier, attached cargo, rollers, rail hardware
and the existing docking-state colours on level 69. Cover panels retain their original
geometry, gap and pause response. No new gameplay state or bridge is introduced.

`GroundHazardStyle.Vault` is appended to the serialized presentation enum. Its heated
frost-stone paving uses the original footprint and phase; the existing bounded cartoon
particle renderer supplies cooling wisps, warning smoke and active flames/embers.
Damage, clocks, navigation and safe spawning are unchanged (GROUND-001..007).

The chapter owns its sky, post profile and explicit `CampaignShadowBake` output under
`Data/ChapterSevenVault`. The bake gives exterior earth/skyline appropriately low chart
density, excludes changing objectives by component responsibility and binds ECS floor
shadow masks. `BakedShadowLighting` and `CelLevelLighting` retain scene-lifetime pipeline,
probe and atmosphere restoration. Surface refresh preserves receiver masks, chart
transforms, source references and material GUIDs. Verification:
`Docs/Verification/ChapterSevenVault.md`.

`ChapterSevenVaultShadows` gives static box scenery physically proportioned UV2 islands
and higher local chart density through the explicit chapter bake. Each outside box face
is divided into at-most-12m patches without internal caps; transforms, outer surfaces
and gameplay colliders are preserved. The UV recipe shares meshes by physical dimensions.
Other chapters retain their existing unwrap path and density. `ChapterSevenVaultJoints`
fits return-wall spans between the real front/rear slabs, removing exposed coplanar
overlaps in existing scenes and future dressing without relocating landmarks.

## Chapter Six Canyon Presentation (VISION-004..006)

`ChapterSixCanyonBuilder` owns Campaign 51-60's Sunscarred Canyon Outpost
presentation through chapter-owned surfaces, scenery, objectives and atmosphere.
Cool grey combat stone sits within warm sandstone service yards, cisterns,
faded blue awnings, stationary windwheels and a continuous layered mesa horizon.
Each stage has its own landmark; stage 56 retains its five authored sectors.
Decorative geometry is noncolliding and uses the production painted shader's
scoped camera cutaway, continuous pigment and baked receiver contracts.
The dry daylight profile uses distance haze sized from the entire arena bounds.
`ChapterSixCanyonDust` authors four native ParticleSystems in a separate
scene root around the combat bounds. A chapter-owned procedural unlit shader
draws flattened, two-tone sand puffs. World-space tangential/outward drift and
short lifetimes keep the complete puffs off the combat floor. The effects use
scaled time, have a combined 128-particle cap, and cast no shadows or collision.
No custom runtime component or ECS ownership changes are introduced.
`ChapterSixCanyonPropClearance` separates decorative supply chests and cactus
assemblies from architecture, yard walls and each other, retaining 0.2 m of
clearance and excluding the expanded combat bounds. Straps translate with their
chest. The deterministic Editor-only pass runs during dressing and can refresh
existing scenery; it adds no runtime components or colliders.
Level 52 has a chapter-owned ear-clipped visible floor mesh with separated cap/side
normals. Its indented outline previously overlapped under convex-fan triangulation.
The renderer uses the corrected mesh while its MeshCollider retains the original
campaign mesh; footprint, height, navigation and physics authoring are unchanged.
`CampaignShadowBake` writes chapter-owned UV2 meshes, masks and probes, with
reduced chart density for the exterior ground and mesa rim. Surface refreshes
preserve receiver source, mask and chart bindings without rebuilding scenery.

`ChapterSixCisternVisuals` uses the existing shell fragment/state presentation
for level 51. Barricades and reversing cover reuse branching fracture geometry;
`CampaignCargoTrackVisuals.ApplyCanyon` owns level 54's complete ore carrier,
rails and socket. Level 57's flush turquoise supply inlay derives its edge
from the actual protected zone. `GroundHazardStyle.Canyon` opts into scorched
sandstone and the existing bounded phase-driven cartoon particle system.
Footprints, clocks, damage, ECS ownership, bridge data and update order are unchanged.
See `Docs/Verification/ChapterSixCanyon.md` for verification scope.

## Chapter Five Necropolis Presentation (VISION-004..006)

`ChapterFiveNecropolisBuilder` owns the saved Campaign 41-50 presentation through
chapter-specific surface, scenery, objective and atmosphere recipes. Memorial
gardens, connected cemetery walls, mausoleums, bell towers and a closed wooded
ridge sit outside the complete authored floor envelope. The existing production
painted shader supplies continuous pigment, decorative cutaways, DOTS and baking
passes. The chapter uses purple painted light, darker decorative masonry and a
scoped purple dusk profile. It reuses Chapter Four's floor-outline scenery fog:
exterior earth and decoration fade into purple banks while combat floors and
dynamic objective cues remain outside the fog material roles.
`CelLevelLighting.sceneryHazeDepth` defaults to the existing 24 m fade; Chapter
Five selects 72 m and opts its exterior ground and decoration into silhouette
material role 3. Beyond 12-32 m outside the actual floor outline, detail collapses
into a flat dark purple silhouette with limited fog blending, including the ground
and distant ridge. Existing roles 1/2 keep their original complete fog dissolve.
The 4.5 m low fog bank and loaded-scene restoration of all fog globals are unchanged.
`CampaignShadowBake` writes owned UV2 meshes and explicit ECS receiver bindings;
large exterior earth and the ridge use reduced chart density. Surface refreshes
retain material GUIDs and receiver mask/chart/source bindings without rebuilding
the environment.

Gate/core pieces use existing barricade state and branching fracture meshes.
`CampaignCargoTrackVisuals.ApplyNecropolis` skins level 46's complete cart and
rails using owned materials; earlier chapter recipes retain their behavior.
Level 47's protected endpoint uses flush stone paving and a memorial laurel inlay
whose marked outside edge matches the authored breach rectangle.
`GroundHazardStyle.Necropolis` adds staggered scorched paving to the existing
footprint shader and opts into the existing bounded phase-driven particle system.
Gameplay, phase clocks, footprint ownership, bridge data and update order are
unchanged. See `Docs/Verification/ChapterFiveNecropolis.md` for verification scope.

## Chapter Four Observatory Presentation (VISION-004..006)

`ChapterFourObservatoryBuilder` edits the saved 31-40 scenes through chapter-owned
surface, scenery, structure and objective recipes. It preserves gameplay authoring
and physical transforms and rejects accidental re-dressing; surface/atmosphere
refreshes preserve approved placement and explicit baked receiver mask/chart/source
bindings. `CampaignShadowBake` accepts the explicit chapter scene list and writes
owned UV2 meshes and receivers. Large exterior ground and the wooded horizon use
reduced chart density; moving objectives and cues remain excluded from fixed casts.
`ChapterFourObservatoryRoofs` fits a single continuous gallery cap to the actual
masonry vertices in building-local axes, including return walls and a small overhang.
Its scoped refresh updates existing roofs and dome materials in 31-40; changes to
fixed roof geometry require the existing chapter shadow bake.

`CampaignCargoTrackVisuals.ApplyObservatory` extends the existing cargo recipe to
32/39 with owned palettes, retaining its original 19/26 behavior. Gate/core fractures
reuse the existing generator with an explicit output folder. Level 37's relic skin
uses existing shell fragment and barricade presentation data; level 36's protected
point is a noncolliding flush constellation inlay.

`GroundHazardStyle.Observatory` is appended to the presentation-only style enum.
The ground presentation system passes that style to the existing footprint shader
for heated ceramic tiles. The existing bounded particle system now accepts both
Quarry and Observatory styles, with the same phase, sampling and lifecycle owners.
No gameplay system, input bridge, hazard clock or Mono/ECS ownership changes.
`CelLevelLighting` also owns Chapter Four's optional scenery-only haze for the
scene's lifetime. Serialized counterclockwise XZ outlines of each convex playable
floor define the arena union. `SceneryHazeFloorOutline` extracts them from world-space
mesh vertices during atmosphere authoring; no runtime geometry queries are needed.
The shader measures distance to these edges, so angled borders retain the same
fog offset as axis-aligned borders. Combined floor bounds remain a fallback for
older scenes without outlines. An
opt-in Painted Foundry material role submerges nearby decoration in dense low sage
fog, fading both distant scenery and exterior ground completely into the chapter's
matching painted sky. Serialized floor height anchors the bank across elevated stages.
Conventional distance fog is disabled in this mode to keep gameplay clear.
The bounds/edges/count/colour/settings shader globals and prior atmosphere are restored on
unload. Earlier chapters retain their existing distance fog and default material
role zero. There are no runtime renderer scans, ECS reads or fog volumes.
See `Docs/Verification/ChapterFourObservatory.md` for evidence and acceptance limits.

## Boss Appearance Pass (COMBAT-008, BOSS-002/008, CHICKEN-007, ROLL-007, PILLAR-006, FINALE-003/007)

`BossAppearanceBuilder` applies appearance updates to the eight campaign boss introductions/rematches, four legacy boss SubScenes and the finale main scene. It replaces only model children and assigns dedicated atlas materials; encounter roots, collider geometry, settings, waves and progression remain authored. The normal boss recipes reproduce the same model treatment. Source FBXs and their importers remain unchanged.

The Gatekeeper uses the supplied Polygonal Magma Orange prefab in campaign 10/50 and legacy
Gauntlet 11 (BOSS-002/008, VISION-006). `GatekeeperMagmaBuilder` preserves encounter settings,
physics part identities and transforms, generates a default 4.4m head silhouette, and fits
both rig fists to the existing 5.2m physical diameter. Owned materials use the supplied orange
texture under `CrowdPunch/Cel/Enemy`; original pack assets are preserved. `BossAppearanceBuilder`
and the legacy encounter builder use the same path for future rebuilds.

The existing sampled ECS skinning format stores Idle, Move Forward WO Root, Explode,
Take Damage, Die, Cast Spell, Punch Attack, Swing Attack, Charge, Spawn and Idle in slots 0-10.
`GatekeeperAnimationSystem` uses accepted-hit sequences to play complete damage reactions,
with movement, transition and defeat driven by the original encounter state. It adds no squash.
`GatekeeperRigBaker` records the RigLArm1/RigRArm1 descendant masks, palm bind points,
model entity and animated hand bounds measured from the supplied mesh and skin samples.
`GatekeeperArmAnimationSystem` runs after body sampling,
selects each arm's authored attack/stagger/guard pose independently from its physical hand's
phase clock, then overrides arm-root translation through the sampled skin matrices. Each hand's
horizontal mesh bounds center follows its physical damage area. A fixed radius measured across
the clips and the current model world scale determine its size from `BossPart.Radius`, so later
model scaling does not enlarge the fists. Interpolated mesh bounds conservatively enclose the
animated vertices; a presentation-only vertical lift keeps their bottom above the encounter
`ArenaGroundPlane.SurfaceHeight` plus `GatekeeperRigAuthoring.groundClearance` (default 0.02m), including defeat. Finger
articulation stays authored. Campaign 10/50 retain the user's model scale of 6. No MonoBehaviour queries entities, no runtime
Animator is introduced, and presentation never writes physics transforms or attack clocks.
The primitive hand renderers are removed; invisible ECS hand owners keep collisions and
committed attacks. Whole-arena skin bounds cover their travel. `BossPresentationSystem`
retains world telegraphs and combat feedback but no longer changes Gatekeeper material colors.

`GatekeeperRigAuthoring.leftFistScale/rightFistScale` are independent constant visual multipliers
(default 1). The rig baker records them; skinning applies them without changing collider sizes,
damage or attack clocks. `GatekeeperHandFootprintBakingSystem` records matching conservative
hand envelopes. Coordination clips committed attack positions to the configured circular
`BossTuning.HandCourtRadius` (19m in all three round courts); the motor limits each predicted
physics step using the same envelope. Authored model scale, ground clearance and the original
attack telegraphs remain unchanged. No wind-up growth or marker-coverage fitting is applied.

`RollingSpikeBuilder` replaces the Rolling boss visual in campaign 30/70 and legacy Gauntlet 21 with the supplied Spike prefab. A constant uniform authoring scale fits the sampled idle/spin silhouette to the existing collision footprint; proportions are preserved and the visual is translated to the collider floor. Runtime never changes model scale. Dedicated cel materials preserve Spike textures. Slots 0-5 contain Idle, Hide and Spin Attack In Place, reversed Hide to Idle for wind-up, Take Damage, Die and forward Hide to Idle. `RollingAnimationSystem` owns sampled playback through `RollingAnimationState`, uses accepted-hit sequences for reactions, and preserves committed combat clocks. Quaternion interpolation preserves authored bone scale. Procedural rolling, squash, floor-angle correction and material pulses are removed. ECS physics and encounter settings remain unchanged (ROLL-001/006/007).

Other bosses retain their existing presentation: `BossAnimationAnchor` grounds Chicken/Dino
poses; `BossSkinPose` blends samples and their bounded silhouette accents. Rolling Spike uses its authored spin clip without collider fitting. Legacy `BossHandPoseSystem` only affects
remaining primitive visuals, and no longer has targets in the authored Gatekeeper scenes.

The finale uses a dedicated royal-blue body UV palette while preserving its gold crown, face and wing membranes, an atlas-backed URP material, cyan vulnerability emission and the existing ring. Its Mono presentation bridge triggers strikes when ECS warnings resolve, adds landing compression, and distinguishes flight/fall/takeoff without reading entities. Per-clip sampled support heights settle its floating source model onto the ground during vulnerability and blend away through fall/takeoff.

`BossAppearanceTests` checks source hashes and grounded sampled geometry, full-roll clearance, finite transitions, unchanged physical transforms/velocities and non-accumulating hand deformation. See `Docs/Validation/BossAppearance.md` for verification evidence and remaining visual tuning limits.

## Enemy Appearance Pass (INFO-004, ENEMY-005/007, COMBAT-011/014)

The October 15 appearance pass uses the supplied Ultimate Monsters atlas on all normal/elite
monster materials. `EnemyAppearanceMaterials` applies point-filtered, uncompressed atlas sampling
and is called by each monster prefab builder, so rebuilding a prefab preserves its appearance.
The supplied atlas and guide images are unchanged. Dasher presentation now uses the
Goblin Minion path documented above. Sampled culling bounds use the same bone matrices as
the GPU vertices, avoiding double application of the imported FBX scale. The Exploder uses the smooth GreenBlob; generated mesh copies
remap only its green body UV swatch to the atlas's amber swatch, retaining its face details. Its material enables a 1.2-second amber emission pulse in the
  ECS skinning graph, accompanied by a bounded 6.5% render-only swelling pulse. This steady
  rhythm identifies its explosive role and does not represent a timed fuse. The body and separate eye
  use bounds calculated from the exported skin matrices, preventing the imported FBX scale
  from moving the eye bounds offscreen and making it disappear during movement.
Their existing physics capsules, movement tuning, attack rules and ECS ownership are unchanged.

`EnemyVisualOwner.UsesAtlas` distinguishes textured skinned renderers from primitive fallback
renderers. `EnemyReadabilitySystem` uses a neutral multiplier for atlas bodies, retains transient
telegraphs and Snake vulnerability feedback, and preserves the palette while launched. Atlas hit
flashes brighten the texture instead of flattening every surface into one role color. The existing
DOTS/compute-deformation Shader Graph retains its full-precision indexing and now has a small
palette-colored shadow fill and reduced smoothness. Unity Toon Shader is installed but is not
assigned to ECS crowds: its imported shader does not implement this project's custom deformation path.

The existing Burst animation job advances creature flight samples, restarts them on re-punch,
adds bounded velocity pitch, and blends recovering creatures upright over at most 0.24 seconds.
Idle recovery does not play the terminal death clip. Dragon attack/launch flame playback keeps its
specialized ownership. Render-only breathing, moving bounce, contact/Dasher anticipation and a
short landing spring compose in `EnemyImpactVisualSystem` with impact deformation, using the
cached authored matrix without accumulating scale. No presentation system writes physics transforms.

`EnemyEffectsBuilder` creates project-owned Cartoon FX copies for punch, body collision,
ground impact and explosion. Source scripts and lights are removed from these copies; the two used
CFXR shader importers are explicitly configured for URP. Every particle system is non-looping,
uses no destructive stop action, and has a capped particle count. Existing impact pools still own
capacity and bridge consumption. `ExplosionFeedback` prewarms up to its existing pool capacity of
particle accents and retains a faint expanding radius pulse. Pause/restart clears both. No enemy
Animator GameObjects or per-enemy particle objects are added.

Validation: 19 animation and 8 combat-feedback checks passed, including re-punch reset, recovery,
physics isolation, 300 repeated render-accent updates without scale accumulation, and pool-safe
imported prefab settings. In a live Play session, punch and explosion particles expired and
reused the same pooled objects. A controlled editor-preview late wave advanced with 133 enemy bodies,
248 animated meshes and 250 renderers. With physics paused, 20 warmups followed by 120 completed
updates measured animation 0.0392 ms, readability 0.0239 ms and impact accents 0.0145 ms per update,
with zero calling-thread managed allocations in each measured loop. These are Editor CPU system
measurements, not standalone frame-rate/GPU acceptance results; OQ-001 and final art approval remain open.

Trail and Wizard authoring enable `RotateLaunchAroundOwner`. The animation baker converts the
owner origin plus the configured launch clearance into model space as `EnemyAnimation.LaunchPivot`; flight and recovery rotate skins
around that shared body center rather than their feet. Their bounds cover the centered turn.
This changes presentation only and leaves the physics transform and launch velocity untouched.

## Final Encounter (FINALE-001..007)

The campaign now has 81 stable IDs and nine chapters. `CampaignCatalog.chapterStarts` describes chapter boundaries; the last chapter contains only `cp-081`, Bring Down the Sky. `CampaignProgress` still stores completion by stable ID, so existing 80-completion saves naturally resume at the new finale. The catalog and build settings append level 81 without modifying prior scenes. This supersedes the historical index-80 end-sentinel and eight-chapter descriptions below. Level Play reads the catalog and needs no special finale path.

`FinaleBoss`/`FinaleTuning` own the flying, falling, downed, takeoff and defeated states. The boss is an ECS solid using the established Barricade geometry/aim/impact pipeline, without Enemy, dynamic mass or launch state. `FinaleCycleSystem` runs before ordinary pre-physics work; only its downed phase exposes the solid collider. The boss remains stationary during vulnerability. `FinaleAreaAttack` resolves committed warning geometry against the player snapshot and crowd, respecting armor and paired launches, with boss-owned launch attribution. It never targets installations.

Four `FinaleAnchor` entities reuse covered targets, shell/core resolution, physical rail docking and hit-count targets. `FinaleHitResolution` gates all objective entry points by flight phase and player launch ownership. Completion changes the boss phase immediately, so later events in the same chain cannot queue another downing. Shell resolution retains its distinct impact and blast events and direct-punch core exception. Cover clocks and rail interpolation stop while paused. The fourth used anchor resets the complete set only when takeoff finishes. `FinaleEncounterReset` restores both encounter and anchor state on retry.

Boss body impacts use swept Barricade contact detection; blasts use the existing explosion resolver. `FinaleHit` records separate impact/blast identities, pruned when their source or launch no longer exists. Grounded-body reflection keeps launch velocity magnitude, clears homing, and redirects player-facing rebounds sideways out of the contact face. `PunchAimAssist` excludes paused anchors and invulnerable boss phases.

Sixteen finale-owned wave sequences reserve up to 37, 50 and 66 physical bodies across the three health stages. Each of four anchor banks starts with four Baselines and one Exploder, adds two Baselines at stage two, then two Baselines and one Exploder at stage three. Three specialist additions and one LinkedPair sequence retain their existing recipes. The former maximum remains 60 root enemies plus six Snake segments. Additive sequences use the existing `FinaleSupply.MinimumStage` gate, so active replenishment slots stay capped at each stage and retry returns to the smaller first-stage supply. Individual slots respawn through existing warning, spacing and placement checks. `FinaleSupplySystem` recycles a pair only after both endpoints finish, and recalls remote, active ammunition slots when the player returns to their bank. It does not allocate additional ammunition. A Snake head waits for old detached projectiles before respawning, bounding tail population. Exact compositions, replacement delays and safe rectangles are saved in `Data/Campaign/Finale`; bank/stage/recall settings are on the scene's supply authoring. Shared ordinary enemy profiles remain unchanged. `Apply Finale Phase Supply` updates the saved finale banks and adds any missing stage sequences without duplicating them.

Boss death immediately suppresses hostile player damage through `PlayerEcsBridge.EncounterVictory`. `FinaleVictorySystem` then clears all enemy roots, pending spawn reservations, pairs and persistent hostile effects through an ECB. Spawn systems reject the defeated owner. `GauntletCompletionSystem` gives finale victory precedence over anchor and wave states, reporting completion after the short victory animation. The presentation bridge clears damage suppression on retry or scene exit.

`FinalePresentationSystem` copies only positions, phase, warning dimensions, health fraction and anchor statuses to `FinalePresentation`; the MonoBehaviour never reads/stores entities. The supplied Goleling FBX plays through a finale-owned Animator controller and copied looping flight clips. A temporary look-height offset on the existing TPP camera frames the flying giant and is restored on scene exit. Cyan/amber/dark floor rings show active/paused/spent anchors; existing target cracks, shell pieces and rail/socket visuals show progress. Main-scene presentation and ECS SubScene are both saved under `Scenes/Campaign/Campaign_81`.

Chapter Nine's `ChapterNineCrownBuilder` dresses those saved scenes with Crown Above
the Clouds scenery and chapter-owned painted surfaces. Four connected gallery sides
and cloud layers remain outside the original 100 m court. `ChapterNineCrownObjectives`
uses existing barricade, shell-fragment, cover-panel and track/socket presentation;
it adds no encounter state or bridge queries. `ChapterNineCrownShadows` supplies metric
box charts to the explicit-scene `CampaignShadowBake` path. Fixed scenery uses saved
shadowmasks/probes and the existing scoped `BakedShadowLighting` lifetime; moving and
state-dependent anchors retain local pigment, probes and no permanent caster shadows.

The chapter-owned floor and fixed building materials can opt into
`PaintedMarble.hlsl` through the production painted shader. It adds filtered spatial
veining and optional metre-scaled paving joints to albedo only. Its default strength
is zero, so earlier chapters retain their existing surfaces. The marble refresh
separates ivory piers, cool facades and pale terraces without changing geometry,
lighting data, receiver bindings or gameplay/presentation ownership.

Initial boss settings: 6000 health, three stages at 67%/34%, 2.5s fall, 24s downed with 7s initial stun, 2.5s harmless takeoff and 3s victory; attack cadence and warning sizes remain editable. The 6-8 minute/6-8 downing targets require human combat playtesting. See `Docs/Verification/FinalEncounter.md` for actual checks and limitations.

The finale scene also owns `FinaleBossHealthBar`, a noninteractive screen-space uGUI canvas. `FinalePresentation` forwards health fraction through the same ECS presentation endpoint, without UI entity access. It displays a long red bar centered at the top with the player's fill thickness and top margin, without text or background. The cyan boss tint and pulsing body ring indicate vulnerability through grounded attacks and end at takeoff. The opening hint sits below the bar. Pause/failure/completion screens hide the bar, and unloading the finale removes it.

## Four-Enemy Campaign Integration (LOOP-009)

The existing 80-level campaign now introduces LinkedPair at 41, Dragon at 52, Snake at 61 and Ninja at 71. The exact 2026-10-14 recurrence and counts are in Campaign80Proposal.md. Fifteen finite wave-clear encounters use 48 revised wave assets; earlier introductions recur in every later chapter. Main-scene opening hints explain the introductions and mixed encounters. Existing safe spawn banks, navigation/floor/obstacle assets, objectives, boss support, stable level identifiers, catalog, build registration and saved progress are retained.

The shared editor `Wave` recipe now represents Dragon, Snake, Ninja and a separate LinkedPairs count. `WaveAsset` writes F/S/N as exact ordinary profile guarantees and pairs through `linkedPairProfile`/`linkedPairCount`. The chapter-five-through-eight recipes and hints match the saved content. **Crowd Punch > Campaign > Apply Four Enemy Revision** updates those existing assets in place after checking installed levels, wave wiring, prefab availability and unsaved scenes; it preserves wave GUIDs and authored spawn regions instead of rebuilding whole chapters. The four revised large arenas use simultaneous finite waves, replacing their prior eight-body batches.

No runtime enemy system or Mono/ECS ownership changed. The existing baker adds two endpoints per pair to clearance, while Snake spawns its six tail bodies independently and counts only the head. Baseline substitutions preserve each non-introductory wave's initial physical-body budget, with peak populations 128/138/138/148 at 48/58/68/78. Wizard/armor ammunition safeguards and Wizard-zone completion gates are retained; pair records, detached Snake orbs and Dragon flame add no completion waits. Shared settings retain six-segment tails and two concurrent Ninja ambushes. Legacy Gauntlet_24..27 remain development-only and unchanged.

See [campaign enemy revision verification](../Verification/CampaignEnemyRevision.md) for automated asset checks, compilation, baked-world validation and remaining human playtesting.

## Linked Pair And Legacy Gauntlet_27 (LINK-001..007)

`LinkedPairAuthoring`/`LinkedPairBaker` bake dedicated `LinkedPairSettings` into endpoint tuning and `LinkedPairEndpoint`. `EnemyLinkedPairPrefabBuilder` uses Mushnub Idle, Walk, HitRecieve and Death clips with the established sampled ECS skinning pipeline. Mint bodies and a visible tether identify the role; no new HUD is added.

Wave assets distinguish `linkedPairCount` from ordinary `totalEnemyCount`. Baking adds two endpoints per pair to the runtime clearance count. `LinkedPairWaveSpawning` reserves two nearby navigation/floor/hazard-valid positions atomically before creating separate enemy roots through `EnemySpawnInitialization`. Each endpoint receives normal wave ownership and independent spawn-profile health. A separate `LinkedPair` record owns endpoint references/lifetimes, sequence generation, leader, flight/recovery state, previous sweep positions and shared `LinkedPairClock` victims. It is not an extra enemy/objective. Pending spawn warnings reserve and activate both endpoints atomically, waiting if either endpoint is obstructed.

`LinkedPairPositioningSystem` runs after crowd intent and before navigation, writing opposite-side player-relative sweep goals with local separation and bringing blocked partners around cover through normal pathfinding. `EnemyMovementSystem` keeps velocity steering ownership. `LinkedPairConstraintSystem` runs after movement and homing; capped velocity-only tension acts at maximum length, blocked flight loses momentum, and proposed endpoint motion is checked against solids. It never projects positions through walls.

`PunchResolution` uses `LinkedPairLaunchResolution` for both flights, with one pair launch per player punch timestamp and independent damage to endpoints in the punch volume. Solver propagation starts the partner inside `EnemyLaunchCollisionSystem` before collision damage, sharing ownership and asymmetric leader/follower momentum. Explosion resolution picks the nearer leader once per blast identity/lifetime and preserves independent endpoint splash. Endpoint bodies keep ordinary chain, aim and target-specific rules.

`LinkedPairDamageSystem` runs after propagation and before explosions/recovery. A spatial hash limits enemy scans; swept capsules with conservative gap inflation detect fast crossings. Cover rays reject blocked spans. One clock buffer gates each victim/lifetime. Only flying links affect enemies. Armor stages/protection windows, protected Snake parts, boss and puzzle targets are gated. A launch-mode contact can detach an exposed Snake tip through the existing sever pass; push mode cannot bypass attached protection. Exploders queue ordinary detonation in both modes. Already-flying victims preserve velocity and ownership, including lethal deferred damage. `LinkedPairPlayerHitSystem` sends accepted damage through the narrow `PlayerEcsBridge.LinkedPairDamageReceived` event to the existing direct health-damage path, bypassing ordinary invulnerability after pair-clock gating.

`LinkedPairLifecycleSystem` owns shared settling, timeout and recovery before ordinary recovery, which skips intact pairs. Zero health stays attached during flight. Actual defeat, missing/pooling partners, changed lifetimes or stale/unloaded sequence generations detach survivors to Baseline and destroy records via ECB. Restart clears records and hit buffers before recreating waves. ECS presentation draws tethers in one mesh; MonoBehaviours never query/store enemies.

`GauntletProgressionBuilder.BuildLinkedPair` creates Swing Together with two solid cover blocks, flanking space and exact 2+6 then 4+12 finite populations. Bootstrap registers legacy entry 27 without changing campaign catalogs/saves or build scenes. Defaults: 7 m span, 0.45 m width, 8 player/10 enemy damage every 0.5 s, 15 m/s launch or 5 m/s added push, 5 s flight cap and 1.25 s recovery. Endpoint health matches the Baseline profile.

Verification on 2026-10-14: 23 linked-pair EditMode cases and 28 progression cases passed. The controlled real-world check passed exact waves, baked endpoint bodies, simultaneous launch, solver body propagation, re-punch leader switch with changing tether angle, shared recovery, retry health/clocks, final completion and unload cleanup. Additional controlled solver checks verified an incoming boss-owned body launches both partners with inherited ownership, and an out-of-bounds endpoint clears the tether and increments defeat accounting. `dotnet build Assembly-CSharp.csproj --no-restore -m:1` completed with zero errors and 344 existing package/deprecation warnings. The broad 658-case run passed 649 before updating the stale 23-level legacy assertion; the other eight failures concerned unchanged campaign barricade required-hit assertions (six) and Dino supply fixtures (two). Their implementation/assets were not changed by this feature.

The completed-update link-damage microbenchmark measured 0.6665 ms with 2 pairs + 200 ordinary enemies and 3.0877 ms with 2 pairs + 1,000 enemies, after 20 warmups over 120 samples. This measures indexing, swept membership and damage on the Editor main thread, excluding full physics/rendering; it is not an FPS claim. Evidence is in `Temp/LinkedPairValidation`. Human balance/controller playtesting remains useful for the delegated tuning.

## Flame Dragon And Legacy Gauntlet_26 (DRAGON-001..007)

`DragonAuthoring`/`DragonBaker` bake dedicated `DragonSettings` into `DragonTuning`,
`DragonFlameState`, source-owned `DragonFlameClock` records and sampled `DragonMouthFrame`
positions. Dragon is appended as archetype 9; existing enum identities remain stable.
The prefab owns these specialized components, while shared spawn-profile creation
supplies ordinary Baseline health, enemy tier, launchability, physics and wave ownership.
The Dragon profile disables ordinary contact damage/push. Health presentation omits
Dragon bars. No UnityEngine references enter runtime ECS components.

Before physics, `DragonPositioningSystem` overrides Baseline intent with a reachable
distance-band destination while retaining local separation. Existing navigation and
`EnemyMovementSystem` own final steering/velocity. `DragonFlameCycleSystem` runs after
impulse writes, advances committed warning/burst/cooldown and synchronizes the mouth
samples/animation phase; launches interrupt the normal cycle without resetting hit
clocks. Generation changes reset records; surviving recovery starts a full cooldown.
`DragonFlameAvoidanceSystem` runs before navigation after other tactical intent writers,
using active eligible cones and lateral separation; launched/committed movement is skipped.

After recovery and shared facing, `DragonFlameDamageSystem` resolves current-position
XZ victim disks against the mouth sector and Unity Physics solid-cover rays. It builds
one 4m spatial index of enemy candidates per step; source clocks are source/victim
lifetime keyed and retained through reentry/state transitions. Ray collectors ignore
enemy bodies, triggers and floor faces. Ground projection is explicitly authored
(`groundY = -1` in this encounter), independent of arena-volume center or flight altitude.
Intact armor/protection, bosses and protected Snake parts are filtered through existing
rules. Damage uses `EnemyDamageResolution` with emitter attribution. Normal bursts
ignore historical launch credit retained by the body's recovery state. Small horizontal
push directly adds velocity only for victims outside Launched; it never creates an
ExternalImpulse, launch propagation or Exploder detonation request. `DragonPlayerHitSystem`
publishes dedicated damage/push through `PlayerEcsBridge.DragonFlameReceived`, handled by
Mono-owned player health without ordinary invulnerability gates.

`DragonFlamePresentationSystem` draws a pooled aggregate vertex-color mesh for moving
warnings, mouth wind-up and active flames, clipping radial boundaries with the same
solid-cover query. `EnemyAnimationSystem` samples supplied Flying_Idle/Fast_Flying/
Headbutt/Death poses using the same fixed-step phase as the head attachment. Headbutt
is a delegated warning pose; no authored breath animation is supplied. Prefab builder
calibrates and samples the supplied Head bone. Presentation does not modify physics.
Pooling and `GameRestartSystem` clear flame state/clocks; effects have no entities or
persistent hazards to keep an encounter alive.

`BuildDragon` authors Gauntlet_26 (Flame Front), exact finite waves 6+1 / 12+2,
a 34x36m enclosed arena, two 2x4m solid covers and broad side routes. It registers
index 25 in Bootstrap's legacy sequence for Level Play. Campaign catalog/scenes,
player build scene list and prior encounters remain unchanged. Tuning is in
`Data/Settings/DragonSettings.asset`; model/prefab/controller/sampled animation assets
are generated through the established ECS skinning workflow.

Verification (2026-10-13): 38 Dragon regression cases pass for lifecycle/range commitment,
approach/retreat/hold intent, angular rates, height/radius membership, cover/body shielding, all settings modes,
independent source clocks and state transitions, armor protection, Exploder/elite/boss
rules, deferred defeat and kill attribution; eight existing facing cases also pass. The controlled baked-world check passes
ordinary punch/Dragon-body solver propagation, warning-to-burst progression, surviving
recovery/full cooldown, zero-health flight, exact finite waves, retry clocks and final
completion/unload. A direct live bridge check confirms player damage during ordinary
invulnerability. A 250-enemy/10-active-Dragon run advanced solver and animation for
three seconds without gameplay errors. Completed CPU update means over 120 iterations:
positioning 0.0068ms, cycle/head samples 0.0037ms, avoidance 0.0748ms, damage with due
ticks/spatial indexing 0.3396ms; zero calling-thread managed bytes in each loop.
Measurements freeze physics and advance synthetic tick clocks, excluding GPU/FPS,
so they do not settle OQ-001. Human balance/readability playtesting remains necessary.
Unity compilation and serial `dotnet build Assembly-CSharp.csproj --no-restore -m:1`
pass; existing package/deprecated-aspect warnings remain. The parallel MSBuild attempt
failed silently at project-reference evaluation; serial execution resolves it.

## Ninja Enemy And Legacy Gauntlet_25 (NINJA-001..006)

`NinjaAuthoring`/`NinjaBaker` add independent `NinjaTuning`, `NinjaState` and
`NinjaPresentation` to `Prefabs/EnemyNinja.prefab`. The normal-tier Ninja archetype
uses the existing spawn profiles, warnings, health, lifetime and wave ownership.
Its spawn profile copies Baseline health; attack/movement tuning lives in
`Data/Settings/NinjaSettings.asset`. Retreat speed is baked from the referenced
player movement settings (currently 12m/s); approach speed defaults to 4m/s.
Archetype separation masks now use ushort to accommodate Ninja's ninth enum value;
existing override bits and random streams remain unchanged without a Ninja override.

`NinjaDecisionSystem` follows chase and precedes navigation/movement. It writes
distance-band intent, tracks initial/periodic/minimum cooldowns and proximity entry,
and serially reserves at most two ambush slots. Recovery releases the reservation.
Slot retry defaults to .15-.35s; failed destination retry is .35-.65s. Readiness is
retained across these retries. A new enemy lifetime resets all Ninja scheduling;
shared launch phases suppress attacks and reactivation receives a fresh cooldown.

`NinjaTeleportSystem` runs after the normal movement motor. Only a pending teleport
captures crowd/spawn reservations and warning/active patches. It tries the direct
rear point, then alternating 10-degree offsets up to +/-60 degrees. Navigation
physical-radius anchors/region IDs and room ownership reject disconnected sections; continuous
geometry, capsule-height static sweeps and occupancy reject unsafe destinations or
blocked attack paths. No navigation topology fails closed. The single validated
teleport writes position, clears cached navigation, locks the attack direction,
and enters stationary arrival warning. The .45m-radius, 2m-tall capsule fits the
2m rear offset beside the player's 1.5m radius; configurable larger victims/bodies
increase the offset only enough to prevent overlap.

`NinjaMovementSystem` writes intentional dash velocity, caps the final step at the
remaining distance, and sweeps solids to stop before obstruction. In the physics
simulation, `NinjaDashContactSystem` disables ordinary unlaunched enemy body pairs
only during a committed step. Launched-body pairs remain enabled, and a launched
Ninja immediately regains all ordinary solver contacts. No collider cloning or
Dasher combat components are involved. `NinjaInterruptSystem` follows punch/damage
resolution. Post-physics `NinjaCollisionSystem` follows shared launch propagation
and cancels incoming-body interruptions before `NinjaPlayerImpactSystem` evaluates
the swept player hit; shared recovery follows these systems. Final truncated dash
steps retain their sweep until combat consumes it. Shared player health accepts
damage and knockback through the existing bridge/invulnerability path.

The existing skin-matrix animation job selects sampled Ninja Run/Idle, Duck
preparation, a held Punch arrival/dash pose and shared flight/Death playback.
Body colors strengthen preparation/arrival readability. `NinjaSmokeBridgeSystem`
publishes only departure/arrival positions through `PlayerEcsBridge` to a bounded
eight-effect `NinjaSmokePresentation` pool on the player. It contains no entities
or enemy queries. No directional indicators, HUD or audio were added.

`GauntletProgressionBuilder.BuildNinja` authors only the 42m-square Gauntlet_25,
two interior cover obstacles, navigation, Ninja assets and legacy registration in
Bootstrap. Two sequential finite waves contain 1 Ninja + 6 Baselines, then 3 Ninjas
+ 12 Baselines. Campaign assets and the Bootstrap-plus-80 player build list are
unchanged. Select Legacy > 25 Behind You in the Level Play Editor window.
Navigation covers the full court. Continuous positions beside its walls connect
to an inward raster cell at the actual Ninja radius, avoiding false cancellations
from the grid's coarser clearance classes without relaxing obstacle sweeps.

`NinjaEnemyTests` covers authored defaults, all scheduling modes, concurrency,
initial/cooldown/lifetime behavior, teleport topology/occupancy/hazard rejection,
full-warning retries, locked arrival, static swept obstruction, player hit and
invulnerability, ordinary punch launches, and legacy/build isolation.
`NinjaEncounterPlayCheck` runs controlled real-solver/lifecycle checks with injected
launches and wave defeat; it is not a balance playtest.

Verification on 2026-10-13: all 21 Ninja checks passed, along with the related enemy
regression checks after correcting an older landing-lifetime fixture to include
the movement component required by respawn. The actual baked encounter passed both
finite waves, natural preparation/arrival/dash/recovery, two-slot arbitration,
Ninja-to-Baseline solver propagation, an incoming launched Baseline interrupting a
committed dash with Boss ownership intact, completion, and additive restart at
full Baseline health (currently 5). Output is in `Temp/NinjaValidation`.
The final capsule queries also reject thin obstacles between endpoint spheres,
and a real wall-adjacent target at z=20.5 teleported safely to z=18.5.
Warmed synthetic decision updates with three Ninjas measured .0106ms with 200
ordinary enemies and .0223ms with 1,000. Actual baked-capsule physics including the
selective body-pair filter measured .4807/.6806ms per completed step at 200/1,000
Baselines, with .0230/.0746ms Ninja collision interpretation. These local checks
exclude rendering/full gameplay and do not resolve OQ-001 hardware/FPS targets.
Unity compilation is authoritative. An initial command-line build passed with
existing warnings; a later regenerated-project build stopped in
`_GetProjectReferenceTargetFrameworkProperties` without reporting a C# error.

## Snake Enemy And Legacy Gauntlet_24 (SNAKE-001..007)

`SnakeAuthoring`/`SnakeBaker` put `SnakeTuning`, `SnakeHead`, `SnakeTailLink` and
`SnakePathPoint` on a normal-tier Birb prefab. Its dedicated Baseline spawn profile
uses the existing wave pipeline. Only the head has `EnemyWaveOwnership`; each orb
stores head/generation and encounter identity in `SnakeSegment` for cleanup, without
joining defeat counts. No enemy entity queries or state enter player MonoBehaviours.

`SnakeTailSpawnSystem` creates tails through an ECB after head warning activation.
It restores tail tuning on a new lifetime and seeds the longest clear rear path.
Head health has one source: `SnakeEnemySpawnSettings.maxHealth`, applied by ordinary
spawn/reset systems. Tail construction and exposure preserve that health, including
values below one. `SnakeSettings` contains no competing head-health override.
The chase job excludes orbs and gives heads continuous navigation-owned pursuit
outside pressure allocation. `SnakeTailMovementSystem` samples bounded recent path
history by arc distance, adds a sinusoid, reduces sideways wave against swept static
clearance, and drives attached physics velocity with an intentional path motor.
Attached enemies remain physical bodies; movement never teleports their XZ positions.
Protected heads use zero inverse mass while retaining velocity-owned pursuit, so
solver contact cannot push them; exposure restores their stored dynamic mass.
After the ordinary acceleration motor, `SnakeHeadMovement.LimitStep` sweeps the
active head's actual next horizontal step against navigation clearance. Residual
turning momentum is stopped before entering blocked space; navigation can then steer
from a valid position. Physical-radius corner/bounds escape remains permitted. The
guard writes velocity only and skips launched/recovering heads.

SNAKE-002 now exposes only the last attached orb. `SnakeHitRules` uses its stable
index and head attached count/generation as the shared gate for punches, explosions,
collision propagation, aim locks and previews. Head protection is unconditional while
any tail remains; the previous AlwaysVulnerable option was removed. A complete hit batch
can launch only the tip exposed at its start. Punch and impact sever systems detach
that orb without deleting any predecessor, then decrement attached count to expose
the next tip. Missing/defeated terminal pieces are skipped to avoid a stranded weak point.
Pre-physics severing follows punches and precedes
damage/impulses; post-physics severing follows collision/explosion/Wizard/pillar
resolution and precedes recovery. Shared hit rules block protected-head damage,
launch, recoil and previews, while player punch detection still confirms cooldown.
Attached-orb damage is ignored until detachment. Both explosion mode alternatives
are baked from `Data/Settings/SnakeSettings.asset` and obey tip-only eligibility.
`EnemyReadabilitySystem` applies that asset's gold vulnerable color only to the current
tip, then to the head after tail exhaustion; other parts and detached orbs use Birb blue.
Renderer-owned material components handle both orb MeshRenderers and Birb skinning
without allocating materials or adding enemy GameObjects/HUD indicators.

The October 15 Birb replacement uses `Models/UltimateMonsters/Blob/Birb.fbx` with
resampled Idle/Walk/Death animations and its supplied atlas. A generated 32 x 32
`BirbBodyMask` keeps face/eye/beak pixels and recolors only the blue body swatch.
The Snake builder imports its saved controller before recording sample dependency
hashes and restores Idle after sampling, so baking retains playback/color hooks
and the saved head faces forward rather than inheriting the terminal Death pose.
The shared skinning graph's existing `_SkinColor` property is DOTS-instanced;
`SnakeBodyColor` lives only on Snake head render entities and is baked alongside
`EnemyVisualOwner`. `EnemyReadabilitySystem` writes the protected blue or exposed
gold skin color while retaining a neutral atlas multiplier. Primitive tail renderers
use the same linear-space settings colors directly. Rebuild Snake Prefab and Apply
Monster Atlas both restore the mask and matching tail material. No new health,
movement, collision, tail-count or damage rules are introduced.

`SnakeContactSystem` disables original-snake contacts for attached or unbounced
detached orbs, including detached siblings of the same head/generation. A bounced orb
can contact attached pieces without their reciprocal filter re-disabling it; an
unbounced detached counterpart still keeps its own exemption. The existing
`EnemyLaunchCollisionSystem` collision-event job writes `BouncedOutsideOwner` after a
meaningful external impulse with horizontal relative closing speed. Floor support,
departure distance and own-snake contacts cannot enable it. No spatial-clearance scan
remains. Aim selection, propagation correction and homing respect the same original
owner exemption until the first external bounce. Other snakes/crowds remain valid targets.
Swept `BarricadeReboundSystem` and `CoverReflectionSystem` also record external rebounds
through `SnakeProjectileCollision`, covering fast contacts without solver events.
Barricade/cover sweep selection skips owner parts while the exemption remains active.
`SnakePunchOcclusion` tests the current owner's collider at mid-height in the horizontal
punch footprint (contact-radius fallback for colliderless fixtures), transforming by
current head position/rotation/scale. Player punch resolution, live aim-source eligibility
and presentation previews share this check, including detached repunches. Moving around
the head restores eligibility without stale collision-world positions.
Ordinary solver propagation, ownership, feedback and launched-player danger remain
shared. Recovery defeats detached orbs at launch end; `SnakeOrbCleanupSystem`
destroys them and stale/ownerless attached pieces. Restart destroys every orb before
resetting heads and wave generations, ensuring full-tail reconstruction.

The Editor builder creates animated Blob/Yeti assets, spherical orb physics/rendering,
a 36x38m clipped court with two navigation obstacles and a single finite 12 Baseline +
2 Snake wave. Counts remain editable in `CP24_01_Snakes_And_Ammunition.asset`.
Bootstrap's legacy array includes `24 Cut The Tail`; no campaign catalog or player
build scene list changes. Rebuilding this encounter is explicit and restores open scenes.

## Bomb Exploder Presentation (2026-10-23, INFO-004, ENEMY-010/013)

`EnemyExplosivePrefabBuilder` wraps `Assets/Bomb Cute Series/Prefabs/Bomb.prefab`
in the existing Explosive physics root and retains its prefab GUID and spawn references.
It preserves the source prefab's authored scale and proportions; floor alignment
changes only the visual position. Explosive spawn initialization adds no role-specific
`PostTransformMatrix` scale, preserving the prefab's baked transforms.
Death samples (Die and Explode) translate the complete pose upward only when a
skinned vertex would cross the capsule floor. Sampling preserves bone rotation,
scale and proportions and computes culling bounds after this correction.
The Bomb texture uses `CrowdPunch/Cel/Enemy`; monster-atlas reapplication skips this
material folder. Native pack spark particles are omitted because they do not follow
ECS skin matrices. No enemy GameObject Animator runs in gameplay.

The existing 11-slot, 32-frame sample format stores Idle, Forward In Place,
Going To Explode, Bite Attack (two reserved slots), Take Damage, Spawn (recovery),
Spawn (optional emergence), Die, Take Damage (launch), and Explode. The Explosive
profile plays authored contact pursuit, recoil, recovery and defeat poses without
procedural pitch, tint flashes, pulsing emission or squash. Render children are
marked `EnemyVisualOwner.AnimationDriven`, as for the Goblins. Launch sequence
changes restart recoil; pooling resets playback. Optional ground spawning remains
at its existing setting.

Immediate detonation and damage remain owned by the combat systems. After defeat,
`EnemyLandingAnimation.OrdinaryDefeatDuration` allows the Bomb's Die clip to finish;
an exploded body instead uses the short Explode duration, including when contact
detonation had no prior launch. `DefeatedEnemyLifecycleSystem` records the visual
deadline once before pooling. See `Docs/Verification/BombExploder.md`.

## Animated Ground Spawning (2026-10-22, INFO-006, COMBAT-018)

`EnemySpawnSettings.SpawnFromGround` is optional and defaults off. Profile baking copies
it into `EnemySpawnProfile`; initialization stores it in the prefab's disabled enableable
`EnemySpawnAnimation`. `EnemyAnimationAuthoring.SpawnMotion` identifies a sampled motion
(-1 means unavailable). Post-baking copies that motion's duration to the owner. The Goblin
builder samples the pack's Spawn clip in slot 7, preserving below-floor motion, configures
both body and mace and enables the setting on the shared Baseline profile.

After normal warning clearance, `EnemySpawnWarningSystem` grounds opted-in supported
bodies using the authored arena floor and physics collider bottom, preserving the reserved
XZ point. It locks `EnemyGroundConstraint`, clears velocity and starts the existing
Recovering clock for the clip duration, enabling `EnemySpawnAnimation`. Existing movement
and attack rules suspend activity while normal incoming combat remains available.
`EnemyAnimationSystem` overrides recovery poses with the shared spawn progress for every
skinned part. `EnemyRecoverySystem` disables the spawn state on completion or interruption;
pooling also clears it. Restart and pooled reuse enter the same warning/activation path.
No new MonoBehaviour, timer system, UI or alternate physics integration is introduced.
Missing clip/floor/collider or an unchecked option falls back to existing drop spawning.

## Enemy Spawn Warnings (INFO-006)

`EnemySpawnInitialization` reserves the fully initialized prefab at its chosen location,
adds `EnemySpawnWarning` and disables the root plus its linked children. Wave counters
include the reservation immediately, while Disabled excludes it from AI, combat, physics
and enemy rendering. Spawn-space, Wizard-cap, ammunition-supply and pillar-slot queries
explicitly include disabled reservations. Pooled returns and legacy restart use the same
warning path; wave restart destroys pending roots as well as active enemies.

`EnemySpawnWarningSystem` runs after the player bridge and ground-hazard cycle, before
wave spawning. After one second of simulation time it enables the reserved enemy at the
advertised position. Player overlap, blocked ground or warning/active ground hazards hold
the warning until safe; invalid sequence ownership destroys the reservation. The existing
physics-world index is restored for pooled returns before activation.

`EnemySpawnWarningPresentationSystem` batches all amber pulsing ground rings into one
dynamic mesh and one draw using the existing Resources/WizardZone material. No enemy
GameObjects, HUD elements or per-enemy materials are added. Duration (1s), minimum marker
radius (.75m) and ring width (.12m) are prototype tuning.

## Campaign spawn spacing (2026-10-12, LOOP-002/008, INFO-006)

All campaign wave assets use Batched spawning. Ordinary waves reserve up to four
positions every 1.5 seconds; level 1 uses three per second; the eight large arenas use
up to eight every two seconds. Existing defense batches (four every three seconds,
or four every four seconds in level 24) remain unchanged. Composition, defeat-based
activation, pre-wave delays and objective/boss tuning remain authored as before.

Campaign banks expand along their existing sides within arena/room bounds, away from
hazards and at least 3.5m from track routes. `CampaignSpawnLayoutBuilder` provides the
Editor menu `Crowd Punch/Campaign/Apply Spawn Spacing`; rerun it after rebuilding campaign
content with the original chapter, track or barricade builders. It edits campaign wave
assets only and restores the open scene setup. Navigation and physical-ground checks
remain authoritative for sampled positions in irregular floors or banks containing rocks.

`EnemySpawnSpacing` captures planar positions and `EnemyMovementSettings.SeparationDistanceMin`.
Wave batches, ammunition supply, pillar supply and pooled returns reject a candidate
closer than the larger minimum of the two profiles, in addition to collider clearance.
Per-archetype randomized preferences still control movement steering; spawn spacing uses
the common profile minimum. Successful reservations enter shared occupancy immediately,
including across sequences before ECB playback. Failed candidates remain queued for retry.

Both chase and ranged positioning include disabled warning positions as separation
neighbors without admitting them as AI/attack targets. The enemy movement motor limits
approach velocity using the reserved minimum distance and braking acceleration. This
changes motor intent rather than teleporting enemies or overwriting launched momentum.
Committed Dasher motion and launched bodies retain their physical behavior; if they enter
a warning, activation waits for the space to clear. Warning activation checks current
occupants and same-update activations, keeping the advertised location fixed; pending
legacy authored warnings do not mutually block one another.

Verification on 2026-10-12: Unity compilation and all five spawn-spacing checks passed,
including navigation/room-bank inspection across all 80 scenes. The broader campaign run
passed 37/43 checks; six stopped at the pre-existing four-versus-three barricade-hit
assertion in `CampaignBarricadeRoomTests.AssertRooms` (unchanged scenes/settings). Track
route, composition/safeguard, boss-geometry and legacy-build checks passed. In a live
Campaign 78 Editor preview, the first wave reached 76/76 spawned and zero pending warnings
by 25.18s of simulation, with no runtime errors. The unattended player died during the
smoke check; restoring health/temporary invincibility resumed the same wave without an
asset change or campaign completion. This is a spawning smoke check, not a pacing playtest.

A warmed Editor spot check with 148 active enemies and eight reservations measured
0.0356ms per motor update (schedule plus completion), 0.6972ms per occupancy capture,
and 0.0271ms per full placement scan. These are local synthetic timings without collision
simulation, not OQ-001 hardware/FPS acceptance. `dotnet build Assembly-CSharp.csproj
--no-restore` failed during project-reference resolution without reporting C# errors;
Unity compilation was used as the authoritative code check.

## Campaign Chapters 1-8 (2026-10-10)

This section supersedes the historical fixed-run/build-registration descriptions below.
Bootstrap now references `Data/Campaign/Campaign.asset`: 80 stable IDs, eight chapter names,
and eighty available scene paths. `Scenes/Campaign/Campaign_01` through `_80` are editable additive
encounters with separate ECS SubScenes. Levels 4/6 use barricades, 7 uses rotating cover and
10 uses the copied Gatekeeper arena with unchanged geometry and campaign-owned settings.
Level 8 has a 100 x 100m floor and four finite waves of 40/60/76/94 enemies (270 total).
Shared ordinary profiles, original 23 gauntlets and legacy tuning are preserved.
The 2026-10-11 TRACK-006 update supersedes the straight-rail lengths/hit counts in the
chapter descriptions below; see Progressive campaign track routes for the current routes.
BARRICADE-007 likewise supersedes their single-gate layouts; see Progressive barricade rooms.

Chapter 2 adds Exploder and Dasher combinations, a shell/core target in level 12, a barricade
in 13, bidirectional tracks in 16/19, rotating cover in 17 and the Chicken boss in 20.
Level 18 has a 100 x 100m floor and finite waves of 52/70/88/108 (318 total). The shell uses
the existing zero-surviving-Exploders pair replenishment; tracks use existing physical arrival
completion. No runtime objective or enemy ownership/system ordering changed.

Chapter 3 adds armor with ammunition safeguards (21/28), Elite support (23/28),
finite protected-point defense (24), permanent ground patches (25/26/28), periodic
patches (27/28), a narrow gate (22), track (26), cover target (29), and Rolling Blob (30).
Level 28 is 100 x 100m with 59/77/96/110 initial enemies including three total Elites
(342 total before existing supply/replenishment). Hazard cycles are 4s inactive,
1.5s warning and 2.5s active; level 28's periodic pair has a four-second offset.
The defense batches four Baselines every four seconds across 12/16/20-enemy waves,
with five-second inter-wave delays and first-breach failure. `CampaignChapterThreeBuilder`
authors these assets using existing ECS systems and copies the Rolling Blob arena
without geometry changes, with separate boss settings and six replenishing Baselines.
Campaign gates from level 13 onward are 2m wide; solid side terrain seals the exit
and sits behind the front of the gate so edge shots hit the gate before the rocks.

Chapter 4 introduces Wizards (31) and Trails (33), mixes defense attackers (36), and
copies the complete Dino/pillar arena (40). Levels 32/39 use tracks, 34 a four-hit narrow
gate plus periodic shortcut, 35 a four-hit rotating target with a Trail, and 37 a shell
with a permanent hazard in the outer corner. Level 32 rotates rail, block, socket and
markings together by -25 degrees. Level 38 has a 100 x 100m footprint and 70/86/102/120
initial enemies (378 total including two Elites), with Wizard and armor supply safeguards.
Wizard wave-clear encounters wait for persistent Wizard zones; Trails alone do not gate
completion. Level 36 explicitly disables replenishment and hazard completion gates;
its 18/21/23-enemy waves arrive in batches of up to four every three seconds. Dino retains
three successful pillar hits and two local Baselines per unconsumed pillar in addition
to eight general support enemies. Settings belong to the campaign, and shared mechanics,
ordinary profiles, system ordering and boss arena geometry remain unchanged.

Chapter 5 adds levels 41-50: a four-hit narrow gate (42), stop-and-go cover with an
85-degree opening and 3s rotation/1s pause (44), diagonal five-hit rail (46), finite
defense without Elites (47), and the Gatekeeper rematch (50). Level 48 spans 100 x 100m with
70/86/94/110/128 initial enemies (488 including two Elites); ordinary Elite, armor
and Wizard safeguards still apply, while Trail remnants do not gate completion.
Permanent 3 x 6m rectangular pairs sit in level 42's inner pockets and level 48's
outer pockets. Level 45 has a periodic side crossing. Level 50 copies the original
boss transforms and collision meshes, adds navigation authoring and two 3 x 8m
periodic side sectors staggered by four seconds, and maintains 12 Baselines plus one
Ranged enemy with four-second replenishment. Its campaign settings use 360 health,
.65/.65/.85s warnings and unchanged 2.8/2.8/3s recoveries. The configurable baked
warning floor defaults to .8s so legacy tuning retains its previous behavior.
Protected-zone levels exclude Elites (PROTECT-001); level 47 has 24 Baselines and two
Exploders in its final wave. Ordinary Elite AI is unchanged.
`EnemyWaveSpawnSystem` omits Elite replenishment tags in protected-point sequences;
`EliteWaveReplenishmentSystem` also rejects those owners. This preserves finite
22/26/26-enemy defense waves, arriving in batches up to four every three seconds.

Chapter 6 adds levels 51-60 using existing runtime mechanics. Level 51 uses the shell
and specialized Exploder pair, 53 reverses its 90-degree cover opening every five seconds
at 30 degrees/s, 54 has a six-hit diagonal rail, and 56 a four-hit narrow gate. The
protected-point encounter (57) has 22/26/26 finite normal enemies, no Elites, batches of
four every three seconds and five-second cleared-wave pauses. Wizard encounters in 55
and 58 use their existing ammunition and persistent-zone safeguards; armor supply applies
where armor appears. Level 58 spans 100 x 100m with five waves of 77/96/102/118/138 initial
enemies (531 including three Elites). Levels 54/58/60 each have two periodic 3 x 8m strips
with four-second stagger, 4/1.5/2.5s inactive/warning/active cycles and safe spawn banks.
The Chicken rematch (60) preserves the original arena's transforms and collision meshes,
with campaign-owned 2100 health, movement-leading shots, .8s wind-up, 1.5s shot spacing,
and unchanged single/paired/paired stages. Its support is eight Baselines and two
Exploders with four-second replacements. No runtime enemy ownership or system ordering
changes are needed. `CampaignChapterSixBuilder` authors the new scenes and settings;
`CampaignLifecycleCheck.StartChapterSix` verifies the Chapter 5/6 boundary and progression
using an isolated save and controlled completion injections.

Chapter 7 adds levels 61-70 without changing runtime ownership or system order. Level 62
places its four-hit, 2m gate and exit to the northeast; unequal solid terrain spans seal
both sides behind the gate's front face. Level 64 takes four shell explosions, 66 pauses
its 90-degree cover for one second on each successful hit (otherwise 25 degrees/s), and
69 uses a straight five-hit rail. Levels 62/67 have paired periodic strips, 64 has two
permanent rectangular pockets, and 65 has one optional periodic side strip. Level 68
spans 100 x 100m with six finite waves of 68/86/94/102/118/138 initial enemies (606 total,
including two Elites), four separated islands, and three outer periodic strips offset
by thirds of the eight-second cycle. Armor and Wizard safeguards use the existing
wave settings; Trail remnants alone never gate completion. The Rolling Blob rematch
(70) preserves the original arena geometry, uses 600 health, physical reflection and
2.5/1.8/1.2s pauses with unchanged roll speeds, and maintains eight Baselines plus two
Exploders. Two periodic outer pockets preserve its central space. All settings and
waves are campaign-owned. `CampaignChapterSevenBuilder` creates the assets;
`CampaignLifecycleCheck.StartChapterSeven` probes the Chapter 6/7 boundary and progression.
The authoring floor mesh now triangulates concave outlines with ear clipping instead of
a vertex fan. Chapter 7's stepped/notched floor visuals (61/64/69) were rebuilt; existing
chapter assets were not regenerated. The continuous convex floor collider remains in
place to avoid triangle-edge deflections, with enclosing walls limiting playable space.

Chapter 8 adds levels 71-80 using the same runtime ownership and system order. Level 72
has a four-hit cover with a 100-degree opening, 30 degrees/s rotation and six-second
reversals; its bounded support includes a Wizard and a Trail. Level 74 uses the four-hit
2m gate with recessed sealed terrain and two permanent hazard pockets. Level 76 uses
three shell explosions and the existing specialized Exploder pair. Protected-point
levels 75/77 have no Elites, no Wizard supply and no persistent-zone completion gate;
their finite waves arrive in batches of four every three seconds with five-second
cleared-wave pauses. The approved armor ammunition safeguard remains enabled only in
75 wave 2 and 77 wave 1. Its replacement bodies retain wave ownership and count toward
clearance and protected-zone breaches. Level 78 spans 100 x 100m with six waves of
76/96/102/108/128/148 initial enemies (658 including four Elites), broad crescent islands,
armor/Wizard safeguards and two periodic outer strips. Levels 73/80 also have paired
periodic strips with four-second stagger. The Dino rematch (80) preserves the original
arena and all three pillar positions, requires three successful pillar hits, uses
6/4.5/3.5s chase stages and a one-second warning, and keeps two local Baselines per
pillar plus 10 Baselines, one Ranged enemy and one Dasher in general support.
`CampaignChapterEightBuilder` creates these scenes/settings; all 155 campaign waves and
81 build scenes (Bootstrap plus levels 1-80) are registered. `StartChapterEight` on
`CampaignLifecycleCheck` exercises the preceding milestone through the final campaign
screen using controlled injections and an isolated save.

The Chapter 4 multi-wave profile found hidden pooled bodies still generating contacts
at their common storage position. `EnemyRespawnSystem` now queues a shared-component
change through its local ECB when a body actually enters the pool: `PhysicsWorldIndex`
becomes `uint.MaxValue`, reserved here as an unsimulated pool index. Visible landing
responses stay in world 0. Safe respawn restores world 0 before the next physics build;
colliders, masses, ownership, corpse timing and replenishment delays are preserved.
The repository has one simulated physics world (0); adding another world must preserve
this reserved-index contract. This prevents accumulated earlier waves from generating
quadratic contacts below the arena, including costly Wizard collision-event scans.

`GauntletSequence` remains the sole scene lifecycle owner. It selects campaign paths or the
preserved legacy scene arrays, handles completion/failure/retry, places and heals the player,
and exposes menu state. `CampaignCatalog` owns authored identity/content availability;
`CampaignProgress` owns versioned local JSON completion, sequential unlocks, backup recovery
and atomic replacement. The normal save is `Application.persistentDataPath/campaign-v1.json`.
`CampaignSavePath` selects that directory for builds and for Editors without a local override
(LOOP-007). Agent Editors may use ignored `UserSettings/CampaignSaveDirectory.txt`, containing
`LocalSaves` on one line, to place normal and Editor-preview campaign files in their own
project-root `LocalSaves/` folder. Absolute directories are also supported. Both the local
configuration and `LocalSaves/` are ignored by Git. Empty or unreadable configurations fail
rather than falling back to a personal save. Player builds never read the override. Worktrees
keep the shared product name and ProjectSettings; window labels remain local Editor tooling.
Replays cannot regress saves. Unavailable future chapters can be unlocked but never loaded;
Chapters 1-7 completion offer Continue into the next implemented chapter.
Chapter 8 ends on CampaignComplete; index 80 is the completed-save sentinel and cannot load a scene.
Existing stable-ID saves need no migration.

For LOOP-007, Bootstrap's `GauntletSequence` references the shared `GameRuntimeSettings` asset
and passes `StartWithAllLevelsUnlocked` into `CampaignProgress` at startup. This optional,
default-off session policy bypasses sequential unlock checks for chapter and level selection;
completion records, Continue, and save format remain unchanged. New Campaign retains the
session policy after resetting completion. `CanSelect` still rejects unavailable catalog entries.

`PauseMenu` routes campaign presentation to the narrow uGUI `CampaignMenu`, retaining its
existing input asset and EventSystem. Main/selection/confirmation/pause/failure/milestone pages
use controller-selectable buttons. Player movement/punch/look inputs are suspended while menu
or transition gates are active. `FeedbackTimeController` still owns those independent time gates.
No runtime MonoBehaviour queries enemy entities. Health binding occurs in Start because
Bootstrap may create PlayerHealth after the sequence's early Awake.

The existing Level Play window has Campaign and Legacy tabs. Campaign Editor previews permit
any implemented level and do not write the normal campaign save; legacy launches use Editor-only
scene loading without needing build registration or reading/writing campaign progress.
`CampaignBuildRegistration` keeps Bootstrap and available campaign scenes in build registration,
including after legacy recipes try to register their scenes. Validation/legacy scenes stay out.

`CampaignChapterOneBuilder` reuses the existing geometry/wave/environment authoring functions
with explicit campaign output folders. Its creation command refuses to overwrite an existing
catalog; subsequent tuning is performed directly on saved assets. It reloads asset references
after single-scene loads, which can unload cached Unity objects. Waves, floor meshes, objective
and boss settings belong to the campaign; ordinary profiles and nature materials remain shared.

`CampaignChapterTwoBuilder` also refuses to overwrite an existing batch. It copies the complete
shell/track objective visuals from legacy scenes and remaps cross-root socket references into
the new scenes, with separate settings. The Chicken main/subscene pair is copied with arena
transforms and collision meshes preserved. Its campaign crowd is six Baselines at a four-second
replacement delay. Build registration now contains Bootstrap plus forty encounters.

`CampaignProgress` skips disk writes for already saved completions and retries transient file
replacement locks with a bounded total delay of 70ms. Persistent I/O failures still surface in
the existing error path. Tests exercise a deliberately locked backup and replay backup stability.

`CampaignTests` covers progress persistence, corruption recovery, sequential unlocks, asset
references and the large-wave specification. `CampaignLifecycleCheck` is an explicit Editor-only
controlled probe using an isolated temporary save and injected defeats. It checks loaded ECS
ownership, all ten transitions, retry, chapter completion and replay. Its evidence is not a
human combat, duration or difficulty playtest. See `Docs/Verification/Chapter1.md` for results.
`CampaignChapterTwoTests` checks existing-save continuation, Chapter 3 availability, objective
references and the unchanged Chicken arena. `CampaignLifecycleCheck.StartChapterTwo()` starts
at Gatekeeper using a temporary save and checks Chapter 2 plus the chapter boundary. See
`Docs/Verification/Chapter2.md` for that batch's evidence and playtest limits.
`CampaignChapterThreeTests` covers continuation through level 30, mixed crowd counts,
defense and hazard wiring, navigation clearance, and preservation of the Rolling Blob arena.
`CampaignLifecycleCheck.StartChapterThree()` checks the Chicken-to-Chapter-3 boundary,
defense failure/retry and batching, hazard phases, track arrival, and chapter completion
using an isolated save. See `Docs/Verification/Chapter3.md` for results and the large-crowd
profiling that led to the bounded Elite staging search described below.
`CampaignChapterFourBuilder` authors levels 31-40 without overwriting existing encounters.
`CampaignChapterFourTests` covers save continuation, large-wave safeguards, navigation,
objective references, and identical Dino/pillar geometry. `StartChapterFour()` extends the
controlled lifecycle check through mixed defense, including final partial batches, and
checks the six pillar ammunition bodies. Evidence is in `Docs/Verification/Chapter4.md`.

## Architectural Shape

Crowd Punch uses a hybrid Unity architecture:

- The large player, input, camera, player health, and UI are GameObjects with MonoBehaviours.
- Enemies, crowd movement, enemy physics, combat requests, spawning, lifetime, and ECS presentation data are Entities.
- `PlayerEcsBridge` is the narrow runtime boundary between the GameObject player and ECS.
- Authoring components in the arena subscene are baked into runtime ECS data.

## Scenes

- `Assets/CrowdPunch/Scenes/Bootstrap.unity` â€” persistent GameObject scene and application bootstrap. Its `GameBootstrap` object owns the fixed `GauntletSequence`; it contains no arena SubScene.
- `Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_01.unity` â€” First Line, the first additive gauntlet, containing its player entry point, brief opening hint, light, and arena SubScene reference.
- `Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_01` through `Gauntlet_23` each contain their matching ECS SubScene. Twenty-three gauntlets are included after Bootstrap in Build Settings and in the Bootstrap level selector. The first ten remain ordinary encounters; gauntlet 11 is The Gatekeeper boss, gauntlet 17 introduces Wizards, gauntlet 18 introduces protected-point defense, gauntlet 19 introduces Trail enemies, gauntlet 20 introduces the Chicken boss, gauntlet 21 introduces the Rolling Blob, gauntlet 22 introduces Dino Pillars, and gauntlet 23 introduces fixed ground hazards. The separate navigation validation scenes remain outside progression.
- Authored gauntlet scenes load additively around Bootstrap. Each owns a `GauntletLevel` entry point and its own ECS SubScene containing layout collision, arena bounds, spawns, and waves.

## Source Layout

The Rolling Blob encounter is described in [Rolling boss ownership and lifecycle](RollingBoss.md).
Its separate dynamic, upright body uses Unity Physics and capsule sweeps against actual static
geometry for immediate obstacle redirects. Cycle stage is committed separately from health stage;
hits never rewrite surviving action timers. Three boss types now share bounded wave ownership,
health-bar presentation, sideways body rebounds, completion and restart integration.

The Chicken encounter is described in [Chicken boss ownership and lifecycle](ChickenBoss.md).
Its separate kinematic body, manual swept bouncing projectiles and combined hit resolver do not
enter ordinary Enemy lifecycle queries. Both bosses share bounded wave ownership/replenishment,
the health-bar bridge and level completion while retaining separate attack/damage state.

The first boss is described in [Boss encounter ownership and lifecycle](BossEncounter.md).
Its separate head/hand components never enter ordinary Enemy queries. The dedicated baked boss
settings asset and one supporting EnemyWaveSettings wave own tuning; focused coordination,
physics motion, collision, replenishment, reset and presentation systems implement the encounter.

Gauntlet 11 uses `BossRoundArenaBuilder` (**Crowd Punch > Levels > Apply Round Boss Arena**), also called by the boss rebuild recipe. A grass disk and 64 overlapping rock perimeter colliders enclose a 19m court; the head follows a 20m circular route and protrudes inward. The existing player collision bridge blocks traversal behind the head. The supporting wave spawn rectangle is inscribed inside the court. Shared nature materials and the `rock_largeA` / `ground_grass` meshes reuse the earlier visual recipe. No runtime system or ownership change is needed.

The ten gauntlet SubScenes use nature-kit environment visuals authored by
`Scripts/Editor/GauntletNatureEnvironment.cs` (**Crowd Punch > Levels > Apply Nature Kit Environment**).
It fits `rock_largeA`, `path_stone`, `ground_pathTile`, and `ground_grass` from
`Models/kenney_nature-kit` to the existing perimeter, lanes, entry aprons, and backdrop.
Repeated pieces are combined into one visual mesh per original renderer under
`Data/GauntletLayouts/Nature`; shared, instanced URP materials live in `Materials/Nature`.
Custom floor polygons retain their vertices with separate grass-top and stone-side materials.
Original transforms, floor collision meshes, perimeter colliders, bounds, and encounter
authoring are unchanged (LOOP-002, VISION-004). This adds no runtime system or collider.
Reapplying preserves existing material edits. After using **Rebuild Ten Gauntlets**, reapply
the nature environment; that older recipe still creates the original blockout visuals.
The kit selection implements the requested environment replacement without closing the
broader art-direction question OQ-015.

All game code is under `Assets/CrowdPunch/Scripts`:

- `Mono` â€” GameObject player, camera, UI, and bridge registry.
- `Authoring` â€” inspector-facing baking inputs.
- `Configuration` â€” reusable ScriptableObject tuning consumed by scene-facing MonoBehaviours and bakers.
- `Bakers` â€” conversion from authoring objects to ECS components.
- `Components` â€” data-only ECS state and requests.
- `Systems` â€” initialization, bridge, AI, movement, combat, physics, lifetime, and presentation.
- `Groups` â€” explicit update phases.
- `Aspects` â€” one legacy learning aspect; new code should prefer direct component/query APIs.
- `Editor` â€” arena authoring inspector support.

There are currently no game-specific assembly definitions; scripts compile into Unity's generated assemblies.

`LevelPlayWindow` (**Window > Crowd Punch > Level Play**, also **Crowd Punch > Levels > Level Play Window**)
reads Bootstrap's authored `GauntletSequence` in an isolated preview scene and starts Editor Play Mode
from a selected gauntlet. It temporarily sets `EditorSceneManager.playModeStartScene` to Bootstrap;
an editor-only, one-shot `SessionState` selection is consumed by `GauntletSequence.Start` before
the usual first-level load. Normal additive loading, restart and onward progression remain owned
by the sequence (LOOP-002/006). The previous Play Mode start scene is restored on returning to Edit Mode.
The chosen level is remembered in local Editor preferences; no scene asset or build gameplay changes.

## Ownership Boundary

`EnemyFacingSystem` runs in `GamePostPhysicsGroup` after respawn processing and the Dasher
rotation lock. Active and Recovering enemies normally face the available player's horizontal
position (INFO-004), with yaw angular velocity cleared and position/linear velocity preserved.
A charging Baseline faces resolved planar velocity, then movement intent/committed direction
when stationary, including while the player is unavailable; this never steers its charge.
Active Dashers normally face the player, but a Dasher in its committed `Dashing` phase faces
its locked dash direction. Launched enemies
instead face current horizontal physics velocity, including after collisions and homing,
even when the player is unavailable. Defeated rotations are untouched. Enabled respawn requests and coincident horizontal
positions skip facing. The resulting root rotation feeds enemy-local animation blending.

`Enemy.prefab` nests the humanoid `Models/BaseEnemy/BaseEnemy.prefab`, fitted to its existing
physics capsule. `EnemyMovement.controller` remains an editor-side animation source: runtime
enemies do not create GameObject Animators. `EnemyAnimationSampling` evaluates its idle and
eight directional poses (including mirrored diagonals), plus the `Flying.fbx` state, at 32 normalized phases per motion.
The generated `Animation/EnemyMovement.bytes` contains durations and 88 root-relative skin
matrices per pose. Rebuild it with **Crowd Punch > Animation > Rebuild Enemy Samples** after
editing the controller, clips, avatar, or mesh. The baker rejects stale source hashes and
bone ordering; it converts the samples into a shared blob with no runtime UnityEngine references.

`EnemyAnimationAuthoring` belongs to the skinned renderer. Its baker stores the owning enemy
entity, shared pose blob, blend response, and per-instance playback state there.
`EnemyAnimationSystem` runs a Burst parallel job in `GamePresentationGroup`, which precedes
Entities Graphics' `DeformationsInPresentation` group. It reads `DesiredMovement` in enemy-local
space, normalizes by `EnemyMovementSettings.MoveSpeed`, damps the blend, and interpolates idle,
the two surrounding movement directions, and neighboring sample frames into `SkinMatrix`.
Cycles start at entity-specific phases to avoid synchronized crowds. Matrix blending is a
sampled locomotion approximation, not a general runtime Animator-controller interpreter.

`EnemyBaseline.prefab` uses the Goblin Trooper Variant through the sampled GPU-skinning
path and `EnemyAnimationProfile.Goblin`; see Goblin Baseline Presentation above for the
contact, hit and lifecycle mapping. Its textured materials live under
`Materials/Enemies/GoblinBaseline`. The controller, body/mace CPA3 samples, equipment mesh
and shared prefab can be rebuilt through **Crowd Punch > Enemies > Rebuild Baseline Prefab**
(ENEMY-012, INFO-004). The older `Baseline` profile remains available to other creature
prefabs that use its simpler idle/walk/launch/death mapping.
All enemy prefab builders enable `Collider.providesContacts`; the baked physics collider must
raise collision events for `EnemyLaunchCollisionSystem` to propagate launched state (COMBAT-002/003).

`EnemyDasher.prefab` uses the same sampled GPU-skinning path with the generic-rigged
`Models/UltimateMonsters/Flying/Dragon.fbx`. Its `EnemyAnimationProfile.Dasher` selects a
narrow state mapping: `Flying_Idle` loops while stationary, `Fast_Flying` loops while moving,
and `Death` plays once for `Defeated`. Intentional dashes and the shared `Launched` phase both
hold the first sampled frame of `Fast_Flying`, leaving dash direction and launch physics under
their existing ECS owners (ENEMY-005/007/008). The generated `EnemyDasherMovement.bytes`,
controller, prefab, and skinning material can be rebuilt with **Crowd Punch > Enemies > Rebuild
Dasher Prefab**. `EnemyDasherSpawnSettings` references that prefab directly.

`DasherTelegraphBridgeSystem` publishes each preparing Dasher's presentation ID, position,
locked aim, and normalized preparation progress through `PlayerEcsBridge`. `CombatFeedback`
owns a bounded `DasherTelegraphParticlePool` of the generated `Prefabs/Feedback/DasherTelegraph`
effect. Each active effect follows its preparing Dasher and scales toward dash commitment;
leaving `Preparing`, interruption, pooling, restart, or suspended feedback clears it without
changing gameplay state or storing an ECS entity in a MonoBehaviour (ENEMY-005).

Physics transforms and velocities are read-only to animation. Launched bodies play Flying
from its first frame on launch entry or a changed launch sequence (including re-punch),
clamp at the final frame, and apply visual pitch from vertical versus horizontal velocity.
Their collider remains upright; yaw comes from `EnemyFacingSystem`. At zero velocity,
the last facing is retained. Sampled bounds include all possible flight pitches.
On launch end, Recovering and Defeated bodies play `Falling Flat Impact.fbx` once and hold
its endpoint (COMBAT-010/011). Living enemies fit the impact into their remaining recovery
window before resuming locomotion; a re-punch immediately restarts Flying. The landing
clip imports vertical root motion into the pose, so the body lowers during the fall.
Sampling raises any below-floor retargeted pose to the model ground plane using the same
four-weight skin matrices used by ECS, and includes that offset in the sampled bounds.
This moves only the visual body; physics position and collider remain unchanged. The height
regression verifies all 32 poses stay above the model floor, finish grounded and lower the
body by more than 0.8 model units.
The landing pose does not inherit flight pitch. The visual post-baking system puts `EnemyLandingAnimation.Duration`
on the owner. Defeat processing uses that duration as `RespawnRequest.PoolNotBefore` for
previously launched bodies, so a stopped body is not pooled before its impact can play.
Pooled enemies reset playback and skip pose updates. Sample format CPA3 contains eleven motions;
Flying and impact endpoints are sampled explicitly, while locomotion wraps between samples.
Impact validation: ten animation cases and a lifetime check pass, covering recovery/defeat
impact playback, endpoint hold, re-punch interruption, and pooling before/after the visibility
deadline. A live defeated enemy showed advancing impact playback at phase 0.506, eleven
baked motions, finite skin positions and `IsPooled = 0`. The C# build has zero errors.
Flying validation: eight animation regression cases and seven facing cases pass, including
re-punch restarts, endpoint hold, flight pitch, redirected velocity and unavailable player.
A live launched enemy reported ten baked motions, advancing Flying playback, finite skin
matrices and a facing/velocity dot product of 1. The C# build passes with no errors.
`EnemyAnimationVisualBakingSystem` connects the additional skinned rendering entities to
`EnemyVisualOwner` and the existing body-color feedback (INFO-004). `EnemySkinning.shadergraph`
retains the Sidekick surface shading, adds compute deformation, and multiplies the result by
the ECS `_BaseColor` override. Sampled motion bounds include a small padding for culling.
The graph uses full precision: the compute deformation buffer index must remain a float
for the package's DOTS-instancing lookup; inheriting the Sidekick graph's half precision
produces an invalid DOTS shader variant.
`EnemyStaticShapeMesh` resolves the BaseEnemy model's fixed blend-shape proportions into
`Animation/EnemySkinningMesh.asset` during the same rebuild command. The enemy prefab uses
this generated mesh with its original bone weights, bind poses, topology and surface data.
The source model remains the input on every rebuild, so shape deltas cannot accumulate.
The current source uses two blend-shape frames at weights 0 and 100; generation rejects
unsupported active frame layouts. The renderer explicitly uses four bone influences so
editor bounds sampling does not depend on the selected quality tier.
Runtime enemies need only bone deformation, avoiding the GPU blend-shape path that produced
intermittent out-of-range vertices on the current Direct3D12 setup. Proportions are fixed;
future animated facial/body morphs would require a separately validated runtime path.
This uses the installed Entities Graphics experimental deformation path; GPU work and mesh
cost still need profiling against the unresolved crowd/hardware targets in OQ-001.

Validation (2026-09-10): six isolated ECS regression checks cover looping/interpolation,
enemy-relative direction, partial-speed strafing, launch/recovery/defeat without physics
writes, pooling/reuse, and missing owners. Unity Play Mode confirms the baked renderer,
body-color ownership, animated poses and shadows. An isolated 500-enemy/88-bone CPU playback
measurement averaged 0.30 ms per update over 300 updates after warmup on an i9-14900KF
(RTX 4090); this includes scheduling/completion but excludes GPU rendering and gameplay.
A separate live rendering check reached 518 animated instances, with 518 valid distinct
owners and body-color renderers, independent phases, and no shader errors. This is a
rendering/ownership smoke check, not an end-to-end crowd frame-rate benchmark.

Glitch validation (2026-09-11, Direct3D12): before the static-shape mesh change, a clean
120-frame GPU bounds probe found 48 frames exceeding four local units (maximum 15.63).
After rebuilding, both the single-enemy and 64-enemy checks completed 300 frames with
zero non-finite or oversized vertices (maximum absolute coordinate 1.77 and 1.83).
The mesh regression compares CPU-skinned positions and normals with the original model
within 0.0001, and checks unchanged bone weights, bind poses and topology. It and the six
ECS playback checks pass. The C# build completes with zero errors and package warnings.

`PlayerModel.prefab` owns the supplied `Models/Player/rig.fbx` humanoid skin, its textured
`PlayerFighter` production cel material, `Animation/PlayerMovement.controller` and
`PlayerMovementAnimation`. `PlayerAppearanceBuilder` rebuilds this visual prefab while retaining
its root/component identities. The visual root scales the small source model by 2.35;
authored upper-chest, head and hand scales emphasize the broad brawler silhouette. The existing
scene-owned player transform and collision geometry remain unchanged. The single skin uses a
conservative animation bound and an Always Animate Animator. The supplied normal map is imported
as linear normal data and used at low strength; matte shading preserves the base texture detail.

The `MoveX`/`MoveZ` directional blend tree uses project-owned copies of Heavy Fighter's Idle,
WalkForward and WalkBackward clips. Forward/backward playback is 1.35x. The pack has no strafes,
diagonals, hit reaction or dash, so the existing directional jogs and Hit To Body reaction remain.
Copies under `Animation/HeavyFighter` retain humanoid retargeting, loop locomotion and strip events;
horizontal travel is extracted and discarded by disabling Animator root motion. Supplied model,
texture and animation binaries and animation importers remain unchanged.
`PlayerController.LocomotionVelocity` publishes only
commanded movement (including the committed dash displacement), excluding external knockback.
The presentation component reads it relative to player facing, normalizes by authored move
speed, clamps to the jog blend radius, and damps the Animator parameters. Disable and player
reset clear the published velocity. The model discovers its parent controller at runtime,
so the Bootstrap prefab instance inherits animation support without additional scene wiring.
This preserves PLAYER-002 camera-forward facing and PLAYER-005 independent dash/punch timing.
`PlayerController.DashDirection` exposes the committed dash direction for presentation.
`PlayerMovementAnimation` switches the base layer to a four-direction dash blend tree and
blends diagonal directions. Each non-looping child has a normalized one-second duration;
the `DashSpeed` playback multiplier is the inverse of the gameplay dash duration, so each
clip plays once over the full dash rather than looping or scrubbing its motion time.
The supplied Standing Dodge FBXs in `Animation/Dash` are copied into project-owned non-looping
humanoid clips with extracted horizontal/root rotation travel; root motion stays disabled.
Dash end, interruption and presentation disable restore locomotion. The existing upper-body
punch layer and torso correction remain independent of dash direction and timing (PLAYER-002/005).
`PlayerDashAnimationBuilder` applies this setup and is also called by the appearance rebuild.
Animation follows movement intent, so pressing against a blocking wall still plays locomotion.
`PlayerIdleFootPlanting` stabilizes the project-owned Idle clip's body reference and both
humanoid foot IK targets at the first idle pose, while preserving other animation curves.
The locomotion state's foot IK solves the legs onto that stance. Height is based on the
feet with vertical root travel extracted, so the sole plane is model-space zero. Walking
and strafe clips retain animated goals and blend out of the idle stance through the existing
directional blend tree. Player roots, colliders and gameplay movement are unaffected. The
appearance builder reapplies this treatment on rebuild; `Plant Player Idle Feet` applies it
to the existing saved Idle without rebuilding character geometry or resetting punch tuning.
No ECS ownership, physics ordering, or open art-direction decision (OQ-015) changes.

`PlayerPunchAnimation` drives the always-weighted `Upper Body Punch` override layer using
`PlayerUpperBody.mask` (torso, head, arms, and fingers; root and legs remain locomotion-owned).
The non-looping Heavy Fighter `Punch` clip is sampled through the state's `PunchTime` motion-time
parameter. Clip-specific ready, contact and rotation-end frames are serialized on
`PlayerPunchAnimation`; the prefab's 30 fps clip uses frames 4, 8 and 8 (scene overrides are
preserved). The ready frame is held when available. `PlayerPunch.PunchStarted` fires
only for accepted requests and immediately skips from Ready Frame to Contact Frame before
the ECS hit/miss acknowledgement, independently of `snapToContactOnHit`. While awaiting a
result or when cooldown is zero, post-contact playback uses the Inspector's
`punchSpeedMultiplier` (prefab 1.35). `PlayerPunch.CooldownStarted` immediately
switches presentation to the contact pose for either acknowledged outcome. Cooldown posing
takes precedence over strike playback: `CooldownProgress` plays forward from contact to the
clip's end, wraps to frame zero, then plays forward to the configured ready frame. Both
segments share the selected hit/miss duration at one continuous clip playback rate. A zero
duration permits normal strike/follow-through playback and then returns to ready. Another
accepted punch restarts the strike even if the previous visual has not finished. `PunchStateReset` cancels visual
playback on gameplay reset/disable. Animation never delays hits, spends cooldown, or changes
dash movement (PLAYER-005, PLAYER-009). The shared cooldown property also drives hand feedback.
`PlayerPunchCooldownFeedback` falls back from the configured legacy `hand_r` name to the
humanoid RightHand (`mixamorig:RightHand` on this model), scales it from the authored
cooldown multiplier to the ready multiplier, and creates two child particle systems at runtime.
The looping aura increases its emission and size during recovery before changing to the ready
color; a separate burst flashes once when recovery completes. It restores the animated bone
scale before each Animator evaluation so the presentation cannot accumulate scale drift.
`PlayerPunchAnimation` also exposes `readyYaw` and `punchYaw` offsets in degrees (current prefab:
-8 and 5). Cooldown frames interpolate from punch yaw to ready yaw; the ready frame holds ready
yaw, and strike frames interpolate back to punch yaw until the rotation-end frame.
Punch yaw then holds through the rest of the strike clip.
`PlayerAppearanceBuilder` also samples 41 model-relative spine rotations across the punch
timeline with the locomotion layer held at its idle reference. After Animator evaluation,
`PlayerPunchAnimation` interpolates that reference before applying the authored yaw. This
removes inherited walking/strafe hip rotation from the upper body, keeping the torso/head
oriented as in idle at the same punch/cooldown pose while hips and legs retain locomotion.
The correction blends away with the existing hit-reaction weight and uses the same
restore-before-evaluation path as the yaw offset; it does not accumulate between frames.
After Animator evaluation, `LateUpdate` offsets the humanoid spine around the model's up
axis, carrying the torso, head, and arms without rotating the root or legs. The previous
offset is restored before the next animation evaluation and on disable to prevent drift.
This is presentation only: camera-forward facing and gameplay punch direction stay unchanged
(PLAYER-002, PLAYER-004, PLAYER-005).

The 2026-10-21 player appearance verification is recorded in `Docs/Verification/PlayerAppearance.md`.

| Concern | Current owner | Boundary/data |
|---|---|---|
| Input, player transform, and punch | GameObject | `PlayerController` owns movement and committed dash timing; `PlayerPunch` owns immediate attack input and acknowledged hit/miss cooldowns; `PlayerPunchCooldownFeedback` presents recovery on `hand_r`. Punching does not interrupt a dash. |
| Player health and invincibility | GameObject | `PlayerHealth`; `PlayerHitAnimation` plays Hit To Body on accepted damage; invulnerability remains health-owned |
| Camera | GameObject | `CameraFollow` |
| Scene bootstrap and UI | GameObject | `GameBootstrap`, UI MonoBehaviours |
| Fixed gauntlet sequence and additive scene lifecycle | GameObject | `GauntletSequence`, `GauntletLevel` |
| Player state visible to ECS | Bridge â†’ ECS singleton | `PlayerSnapshot` (including collision-resolved velocity), `PlayerHealthSnapshot` |
| Punch command visible to ECS | Bridge â†’ enableable ECS request | `PunchRequest` |
| Enemy contact reported to player | ECS â†’ bridge event | `EnemyContactHitReceived` |
| Enemy initial spawn, waves, and pooling | Authoring â†’ baked profile â†’ ECS | random `SpawnSettings`, authored spawn points, ordered wave buffers, shared initialization, and respawn systems |
| Enemy tactical intent, navigation, and movement | ECS | `NavigationIntent`, cached `NavigationPathState` / waypoint buffer, resolved `DesiredMovement`, Unity Physics velocity |
| Static solid geometry and navigation grid | Authoring / baking | `SolidObstacleAuthoring`, `NavigationArenaAuthoring`, immutable clearance/region blob; no dynamic crowd occupancy |
| Enemy archetypes and attacks/effects | ECS | `EnemyArchetype`, ranged state, and explosive settings/state |
| Ranged projectile trajectory and lifetime | ECS | `RangedProjectile`, velocity-led fixed fire-time start/target, ECS transform evaluation |
| Enemy combat state | ECS | health, damage, impulse, explicit launch lifecycle, death/respawn requests |
| Punch trajectory preview | ECS â†’ bridge â†’ GameObject | `PresentationBridgeSystem`, `PlayerEcsBridge`, `PunchTrajectoryPreview` |
| Punch cooldown feedback | GameObject | `PlayerPunchCooldownFeedback` scales `hand_r` and owns the generated cooldown-aura and ready-flash particles; tuning lives in `PlayerPunchSettings` |
| Enemy body feedback | ECS rendering | Baked `EnemyVisualOwner` connects root/child renderers to gameplay state; `EnemyReadabilitySystem` writes body color. Animation-driven profiles including Dasher retain a neutral multiplier and bypass procedural deformation. |
| Temporary normal health and elite health UI | ECS â†’ presentation registry â†’ Canvas | `EnemyHealthBarVisibility`, `EnemyHealthBarBridgeSystem`, `EnemyHealthBarCanvasRegistry`, `EnemyHealthBarCanvas`; no repeated normal launch/recovery text, and zero-health normal bars are suppressed. |

The same presentation registry and pooled Canvas now draw one shield icon per remaining Armored armor stage.
The canvas never queries enemy entities, and Armored does not publish health bars.

MonoBehaviours do not retain or query enemy entities. `PlayerBridgeRegistry` exposes the one active `PlayerEcsBridge` to the few managed systems that cross the boundary.

## Update Flow

### Initialization

`GameInitializationGroup` runs in Unity's initialization phase:

1. `BootstrapSystem` establishes singleton/runtime state.
2. `EnemySpawnSystem` instantiates the initial enemy pool.
3. `GameRestartSystem` handles managed restart coordination.

### Gauntlet Level Flow

`GauntletSequence` belongs to the persistent Bootstrap scene and loads one configured gauntlet scene additively at a time. A gauntlet scene owns its presentation layout, one `GauntletLevel` marker with an authored player entry point, and an ECS SubScene for level-specific collision and encounter data. The transition pauses scaled simulation, unloads the previous scene and its baked entities, loads the next scene, places the GameObject player at the authored entry point, and requests the established ECS restart reset. It never queries or retains enemy entities.

`GauntletCompletionSystem` runs in `GamePresentationGroup` and reports through the narrow `GauntletCompletionRegistry`. Ordinary gauntlets require every loaded wave sequence to complete; an empty loading interval cannot advance. When a boss is loaded, its defeat state is authoritative and supporting-wave completion cannot win early. The Bootstrap flow consumes one completion signal and loads the next scene (LOOP-002/006). Gauntlet 10 advances to The Gatekeeper; its defeat advances to Gauntlet_12 (Crack the Shell). Chicken defeat advances to Gauntlet_21 (Rolling Blob), and its defeat advances to Gauntlet_22 (Dino Pillars). Only the final authored gauntlet sets `GauntletSequence.RunComplete`. The existing pause menu presents Run Complete, Play Again, and twenty-two selectable levels (BOSS-007, CHICKEN-006, ROLL-006, PILLAR-006).

`GauntletLevel` owns optional opening-hint text alongside its entry transform. `GauntletSequence` publishes that text and an entry counter to the existing `PauseMenu`, which shows a ten-second hint in levels 1-2 and a two-column selection grid in the menu. This is GameObject presentation metadata, not an enemy bridge. The authored display names are separate from scene-loading names. The existing restart button now delegates through `GameBootstrap` to `RestartCurrentLevel` when a gauntlet is active, resetting final-completion state and reusing the additive reload path. Legacy scenes still use soft restart.

### Initial Spawn Workflows

The arena supports two independent initial-layout inputs, and a SubScene may contain either or both:

- `SpawnerAuthoring` bakes the established `SpawnSettings` random-radius batch. Its center, radius, count, deterministic random sequence, and reusable `EnemySpawnSettings` asset retain their existing meaning.
- `AuthoredEnemyGroupAuthoring` is an organizational parent. Each child `AuthoredEnemySpawnPointAuthoring` references one existing `EnemySpawnSettings` asset and bakes one `AuthoredEnemySpawnPoint` with the child's exact world-space position, including Y. Each point bakes independently, so a null settings asset or prefab suppresses only that point.

Both inputs bake the same `EnemySpawnProfile`. `EnemySpawnSystem` resolves either a random position or an authored position and delegates prefab instantiation, common movement/health/contact tuning, separation selection, respawn policy, archetype identity, material override, and Baseline/Ranged/Explosive/Dasher component setup to `EnemySpawnInitialization`. `EnemyAuthoring` is only the prefab marker that bakes the common component layout; `EnemySpawnSettings` is the single tuning source for each spawned variant. Shared approach and retreat speeds serve both ranged positioning and Dasher positioning instead of maintaining duplicate Dasher values. Runtime behavior is always selected through `EnemyArchetypeKind`, never names. The initial spawn system has no `SpawnSettings` requirement, so authored-only scenes work. It consumes processed source components through an ECB, allowing newly loaded scenes to initialize once without respawning previous inputs.

Wave spawning is a third, independently selectable workflow and does not gate either initial-spawn workflow. Each `EnemyWaveSettings` ScriptableObject contains one wave's total count, weighted references to existing `EnemySpawnSettings` profiles with optional guaranteed minimum counts, positive-area world-space XZ rectangles, pre-wave delay, per-wave activation mode and timed-activation duration, and `AllAtOnce` or `Batched` cadence. Guaranteed normal allocations are interleaved in authored profile order and distributed proportionally over the complete wave; weighted selection fills the intervening slots. Minimums whose sum exceeds the wave's normal total make the wave invalid. `EnemyWaveSequenceAuthoring` holds the ordered wave references plus the deterministic seed, minimum player distance, and bounded placement retry count. Its baker declares dependencies on the sequence, wave assets, profiles, and prefabs, then flattens immutable definitions into ECS buffers; runtime components contain no Unity object references.

`ArenaAuthoring` bakes two independent world-space volumes. `ArenaBounds` is the enemy spacing/distribution area consumed by AI, movement containment, pooling, and edge respawn. `EnemyDefeatBounds` is consumed only by `OutOfBoundsSystem`; leaving it requests pooling for respawning enemies or terminal defeat for fixed wave enemies. Each volume has its own authored center offset and size, while an unset defeat size falls back to the spacing size for existing scenes.

`EnemyWaveSpawnSystem` owns timing, deterministic weighted profile allocation, and candidate sampling. Each wave selects whether its next wave activates after all current-wave enemies are defeated, all current-and-previous wave enemies from the sequence's current run are defeated, or its authored duration has elapsed once spawning finishes; current-wave defeat gating remains the default. The sequence maintains a running undefeated-owned-enemy count so cumulative gating does not require a crowd-wide query. Rectangles are selected proportionally to area and sampled uniformly, so equal valid world area has approximately equal probability. A conservative spherical clearance is derived from each prefab's authored collider bounds. Every candidate must remain inside its rectangle, clear the GameObject player's published position/radius and configured extra separation, and produce no Unity Physics point-distance hit against enemies or blocking geometry. Candidates spawned in the same update are also checked against one another. Attempts are bounded; blocked enemies stay pending, retry on later updates without restarting the pre-wave delay, and emit only throttled diagnostics.

Every wave instance still goes through `EnemySpawnInitialization`, preserving prefab/archetype identity, health, state, presentation overrides, projectile data, separation randomization, and archetype-specific setup. It additionally receives `EnemyWaveOwnership` with sequence entity, run generation, and wave index. `EnemyWaveDefeatCountSystem` observes terminal `EnemyLaunchState.Defeated` or an enabled pooling request after bounds handling and before pooling, marks each current-run ownership record once, decrements the sequence's cumulative undefeated count, and increments the current-wave defeat count when applicable. Legacy and authored enemies therefore cannot advance a wave. Wave instances explicitly bake their per-instance `EnemyRespawnSettings.Enabled` override to false, so they pool after defeat but never perform continuous arena-edge respawning; source assets and non-wave instances keep their existing policy.

Only one wave is spawned at a time per sequence. Progress first waits for the configured successful-spawn count, then applies the current wave asset's activation mode: exact current-wave defeat count, cumulative undefeated count, or its duration. The next asset's pre-wave delay begins after that condition. Timed waves may overlap previously spawned survivors. Zero-count waves progress safely. An invalid nonempty wave enters an inspectable stopped state without affecting other spawners. Empty sequences and completed final waves enable `EnemyWaveEncounterComplete` and never loop.

Each random enemy owns `RandomEnemySpawnRegion`; each authored enemy instead owns `AuthoredEnemyInitialPosition`. A full `GameRestartSystem` reset randomizes legacy enemies inside their own regions and restores authored enemies to their exact initial positions without archetype matching. It also clears velocity, health/damage/launch state, collision histories, ranged attack and positioning state, Dasher action state and hit history, explosive state, movement intent, and enableable transient data. Normal defeat pooling is unchanged: `EnemyRespawnSettings` still controls whether an enemy returns, and enabled enemies respawn at an arena edge rather than at an authored point. Disabled enemies stay pooled until full restart.

### Pre-Physics Simulation

`EnemyGroundConstraint` owns each enemy's cached floor height, landed lock, and player-punch snap request (COMBAT-018). `ArenaAuthoring.GroundSurfaceHeight` bakes the flat, world-space floor surface into `ArenaGroundPlane`, defaulting to the existing arenas' -1 surface height. An explicit override supports other flat floor heights. `EnemyGroundConstraintSystem` runs last before physics and accounts for the baked collider's bottom offset relative to that plane. Legacy worlds without the authored singleton retain the static-floor ray lookup, excluding dynamic bodies. Initial falling remains physical until the collider bottom reaches the floor; a valid player punch requests immediate grounding before simulation. `EnemyGroundReconciliationSystem` runs first after physics and projects grounded Y position and velocity back onto that plane, preserving solver-produced XZ momentum. This is an explicit vertical constraint, not normal movement integration. Pooling requests are excluded, and restart/edge respawn clear the cached state. Wave placement and ordinary replenishment additionally use `EnemyGroundSpawnClearance` to reject static geometry at floor level, independently of spawn height, so elevated borders cannot become landing platforms. The floor remains cached after initialization; traversable slopes and changing floor heights are not implemented.

Campaign_65 verification on 2026-10-11 captured two Active enemies with cached height 2 at (-16.88,16.38849) and
(16.12379,16.88). The actual floor surface is -1, giving ordinary bodies transform height 0 and Elites -0.5 after
collider offsets. Ground/landing/navigation/hazard regressions passed 41 cases in the Editor. A ten-second physics
observation of the 21-enemy wave, including two forced replenishments, recorded no grounded bodies on raised planes;
both captured border columns failed floor-level placement clearance. Initial airborne falling remained unlocked.
Player health was temporarily protected and restored. Evidence is in `Temp/border-height-diagnosis.txt`,
`Temp/border-height-regressions.txt` and `Temp/border-height-replay.txt`.

`GamePrePhysicsGroup` runs as a direct child of `FixedStepSimulationSystemGroup` before `PhysicsSystemGroup`:

1. `PlayerBridgeSystem` copies the latest GameObject player snapshot, health, and punch request into ECS.
2. `EnemyChaseSystem` produces explicit tactical destinations and a separate separation vector for navigation, alongside the legacy blended movement intent used when assistance is disabled. `EnemyCrowdPressureSettings` supplies a global cap, and the closest active baseline or explosive enemies up to that cap receive pressure assignments. Every other ordinary melee enemy owns a stable low-discrepancy coverage slot across the inset `ArenaBounds` rectangle and moves back toward that slot when released from pressure, preserving arena-wide interior launch opportunities rather than roaming between edge-heavy waypoints (COMBAT-016). Slot restoration uses normal movement speed while an enemy is materially displaced and reduces, but does not disable, separation influence until it nears the assigned area; this prevents crowd repulsion from overpowering interior coverage. An active explosive inside its authored contact-attempt range bypasses the pressure cap, continuously targets the player, and retains its spawn-selected separation distance/weight including archetype overrides throughout pursuit and contact attempts (ENEMY-010, COMBAT-013). The existing active-enemy snapshot pass also selects the closest eligible unexploded explosive by horizontal player distance, breaking ties by entity index. Only that enemy may suppress separation when the blended direct-pursuit vector has no positive projection toward the player. Protected-point attacker selection restricts eligibility. This adds no extra crowd scan or persistent state. If that direct player target lies beyond the navigation spacing grid but the complete route is clear of authored static obstacles, `EnemyNavigationSystem` preserves the direct physics pursuit instead of resolving the goal back to the grid edge; this keeps COMBAT-017's distribution volume separate from physically reachable arena space. Other assigned enemies approach deterministic surround-ring slots, using normal speed outside their authored charge distance and the charge multiplier inside it. When a slot is compressed by the inset arena bounds, the system samples deterministic golden-angle alternatives and selects the candidate that preserves the most intended radius. Assigned enemies use per-enemy randomized intervals to make brief, speed-scaled contact attempts toward the player. Each enemy uses its spawn-selected default separation distance and weight unless its profile's override array contains an entry for the nearby enemy's explicit archetype, and ordinary contact attempts retain separation with an independently authored weight. Ranged, Dasher, and elite systems retain their later archetype-specific overrides.
3. `RangedEnemyPositioningSystem`, `DasherDecisionSystem`, `ElitePunchSystem`, and `EliteCrowdSupportSystem` make their existing tactical overrides. Each intent writer publishes an explicit `NavigationIntent` destination, arrival behavior, mode, and separate local-separation vector alongside legacy `DesiredMovement`. Destinations are never reconstructed from a blended direction.
4. `WizardCastSystem` advances cast state after the player bridge and wave spawning. `WizardPositioningSystem` writes its dedicated distance-band intent, and `WizardHazardAvoidanceSystem` adds local zone avoidance after the other tactical writers. `EnemyNavigationSystem` runs after these intent writers and before both movement systems. It resolves clearance-valid direct travel or a budgeted cached route, then applies terrain-safe local steering to `DesiredMovement`. `EnemyMovementSystem` remains the normal Unity Physics velocity owner and rejects non-`Active` enemies. `DasherMovementSystem` retains committed straight-dash velocity ownership. See [Static obstacles and navigation](Navigation.md) for scheduler, settings, lifecycle, and inspection details.
5. `PunchAimAssistSystem` maintains an ECS-owned target lock for each punchable enemy in the live punch volume. A physics ray from the source enemy along player facing replaces the lock when it hits another eligible enemy inside the configured range and horizontal-angle limit; misses retain the lock only while it remains within those limits, and the smallest-angle rule supplies an initial in-cone fallback. `PunchDetectionSystem` uses that locked direction for each hit, clears existing linear and angular velocity before a normal player launch in any eligible state, starts a fresh `Launched` sequence with the current punch data, and enables impulse and damage requests (PLAYER-003, PLAYER-004, COMBAT-014).
6. `DamageApplicationSystem` applies enabled damage requests, clamps health, and resolves immediate defeat or records launch-deferred defeat.
7. `RangedEnemyAttackSystem` evaluates ranged state after punch and damage resolution, cancels invalid wind-ups, predicts a fire-time intercept from the collision-resolved player velocity and configured lead multiplier, and instantiates a projectile when a valid wind-up completes. The projectile locks that target and does not home (ENEMY-002, ENEMY-003).
8. `ApplyImpulseSystem` adds gameplay impulse to `PhysicsVelocity`.
9. Unity Physics simulates motion and collisions.

Ordering between systems that share only a group should be made explicit when correctness depends on it. The attribute graph, not filename order, is authoritative.

### Post-Physics Simulation

`GamePostPhysicsGroup` runs as a direct child of `FixedStepSimulationSystemGroup` after `PhysicsSystemGroup`:

- `EnemyLaunchCollisionSystem` interprets solver-resolved enemy impacts, resolves launch propagation first, applies configured smallest-angle direction correction to newly propagated horizontal velocity while preserving its solver-produced speed and vertical velocity, retains the selected candidate as that launch's homing target, and independently queues eligible impulse-scaled collision damage.
- `WizardImpactZoneSystem` observes qualifying physics contacts before collision damage and also sweeps against the Mono player's snapshot. It also checks launched Dasher sweeps because their enemy solver contacts are disabled. A launched Wizard's first qualifying contact or an active/recovering Wizard hit by a launched enemy creates an immediate fixed zone; incoming source/continuous-flight history deduplicates repeated contacts. `WizardZoneSystem` resolves zone membership and player ticks for both kinds; only impact zones query and affect enemies. It runs after launched-player impacts and explosions, before recovery. `WizardPlayerHitSystem` delivers its ECS hit buffer through the player bridge before recovery; per-zone player protection remains ECS-owned.
- `EnemyLaunchHomingSystem` runs before physics after gameplay impulses. A launched body with a player aim-assist or propagation target turns its horizontal velocity toward the still-living active/recovering target by the configured maximum degrees per second while preserving horizontal speed and vertical velocity (COMBAT-012, PLAYER-004).
- `ExplosiveCollisionTriggerSystem` requests an explosive detonation when either participant in an enemy collision is `Launched`; `ExplosionResolutionSystem` then resolves explosion overlap chains to a same-frame fixed point before recovery.
- `RangedProjectileSystem` evaluates each fixed trajectory, performs a swept player-radius hit check, forwards one accepted hit through `PlayerEcsBridge`, and destroys the projectile on hit, after falling below its authored world-space minimum altitude, or on expiry. It does not apply arena-bound cleanup because the unconstrained GameObject player can currently provide a valid target outside `ArenaBounds`.
- `EnemyRecoverySystem` advances living `Launched` enemies through low-momentum dwell and `Recovering` back to `Active`; a zero-health launched enemy enters `Defeated` directly when launch ends.
- `PlayerContactDamageSystem` uses full three-dimensional enemy/player proximity and reports the closest accepted hit through the bridge, so entities above or below the player cannot produce planar-only contact damage.
- `OutOfBoundsSystem` compares enemy positions with the separate baked three-dimensional `EnemyDefeatBounds`. Escaped enemies whose profile allows respawning enter the existing pool immediately; fixed wave enemies instead enter terminal `Defeated` state with zero health so their ownership is counted and they cannot block cumulative encounter completion from outside the authored arena.
- `DefeatedEnemyLifecycleSystem` converts the one-shot defeat marker into the existing respawn request.
- `EnemyRespawnSystem` brakes, pools, resets, and respawns defeated or otherwise invalid enemies.

### Presentation

`GamePresentationGroup` runs in Unity's presentation phase:

- `HealthBarPresentationSystem` updates ECS health-bar presentation data and expires one-second post-damage visibility.
- `EnemyHealthBarBridgeSystem` publishes living normal enemy health while post-damage visibility is enabled, and elite health while alive.
- `EnemyReadabilitySystem` maps archetype and non-launch gameplay feedback to body color through baked renderer ownership. Entering `Launched` preserves the archetype body color; movement and animation communicate launch state. Dasher presentation remains specialized and keeps its launched shape override without a launched color override.
- `PresentationBridgeSystem` is the explicit ECS presentation bridge point.
- `PresentationBridgeSystem` selects enemies currently inside the live punch volume, reads the same ECS-owned aim-assist target lock used by punch detection, and publishes their initial launch segments through `PlayerEcsBridge`; `PunchTrajectoryPreview` renders those segments as pooled semitransparent world-space lines.

Normal-enemy health bars are transient damage feedback only. The bridge no longer publishes normal launch/recovery labels or exhausted-health normal bars. `EnemyHealthBarCanvas` retains pooled views and never queries or stores enemy entities. The legacy state-label rendering option remains available in the canvas, but receives no normal labels from this iteration's bridge.

## Transient State Pattern

Frequently toggled state is represented by enableable components to avoid archetype churn:

- `PunchRequest`
- `ExternalImpulse`
- `KnockbackRecovery`
- `DamageRequest`
- `DeathRequest`
- `RespawnRequest`
- `EnemyHealthBarVisibility`

Enemy lifecycle is represented by the non-enableable `EnemyLaunchState` component because every enemy is always in exactly one of `Active`, `Launched`, `Recovering`, or `Defeated`. Its phase, last launch cause, explicit `Player`/`Enemy` launch owner, and propagated-launch count remain visible in the Entities inspector. Any launched enemy, including one at zero health with deferred defeat, can be punched again; a recovering enemy must still be alive. Before applying a normal player-punch impulse in any eligible state, `PunchResolution` clears the body's linear and angular `PhysicsVelocity`, so old momentum cannot skew the previewed direction. Elite knockback remains additive. This experiment extends the original launched-body replacement rule to Active and Recovering bodies. The transition then increments its launch sequence and resets its cause, owner, damage, recovery timing, propagated-launch count, and last propagation impulse to the new launch. A player punch therefore replaces enemy ownership rather than leaving stale player-threat state on the body. `Health` exposes current and maximum health, while `EnemyDamageState` records the last applied damage and whether zero-health defeat is currently deferred for development inspection.

`DamageApplicationSystem` is the explicit pre-physics health stage after punch detection. Punch detection establishes `Launched` before damage is evaluated, so a same-frame lethal launching punch deterministically defers defeat and still receives its impulse. After physics and collision propagation, `EnemyRecoverySystem` chooses either normal recovery for a living projectile or direct defeat for a zero-health projectile. `DeathRequest` is enabled only on the transition to `Defeated`, making the lifetime handoff idempotent; `DefeatedEnemyLifecycleSystem` consumes it once and enables `RespawnRequest`.

Collision damage is queued post-physics into the target's existing `DamageRequest` and applied during the next pre-physics damage stage. `EnemyLaunchState.LaunchDamage` carries the originating punch damage through every propagated launch; `EnemyCollisionDamage` is the shared rule that converts estimated impulse into the configured multiplier of that value up to its cap. `EnemyLaunchState.LaunchSequence` identifies each continuous launch. Each target's `CollisionDamageHistory` buffer suppresses repeat damage from the same source sequence; `CollisionDamageHistoryCleanupSystem` removes entries when the source leaves that launch. Propagation and collision damage have independent impulse thresholds. Only launched-to-active/recovering impacts are damage-eligible, and propagation copies launch ownership before damage is queued so a lethal propagated target defers defeat without losing provenance.

`LaunchedEnemyPlayerImpactSystem` handles the hybrid boundary after physics and before recovery/contact damage. Because the GameObject player is outside the ECS collision world, it performs a swept enemy-to-player radius test and estimates impact impulse from the body's post-physics speed and dynamic mass. Every launched body qualifies regardless of launch ownership. Damage uses the same `EnemyCollisionDamage` threshold and curve as enemy targets, then applies `EnemyLaunchSettings.PlayerOwnedLaunchDamageReduction` only for the current Player owner (COMBAT-015). The final damage is clamped to `EnemyLaunchSettings.MaximumPlayerImpactDamage` for every owner. `GameRuntimeSettings` exposes both settings, baked by `GameSettingsBaker`; the shared asset currently uses a 0.8 player-owned reduction and a 25-health-point player impact cap. A zero cap prevents launched-body damage to the player. Propagated Player chains receive the ownership reduction; elite, boss and environmental launches receive only the final cap. Reduction and capping precede strongest-impact selection and do not change enemy damage or independent explosion damage. `PlayerImpactLaunchSequence` permits at most one player impact per continuous source launch. Simultaneous qualifying bodies consume their launch impact and the strongest damage is sent through `PlayerEcsBridge`; normal player invulnerability remains MonoBehaviour-owned. Explosion and other independent player-damage rules remain separate.

`GameSettingsAuthoring` reads reusable `GameRuntimeSettings` ScriptableObject data and bakes `EnemyLaunchSettings` as scene-level singleton configuration. Its provisional sandbox tuning includes independent propagation/damage impulse thresholds, propagated-launch aim-correction radius, launched-body homing degrees per second, base/per-impulse/maximum collision-damage multipliers, useful-momentum threshold and dwell, and recovery duration. Player movement and punch settings assets own their respective tuning and Input System asset/action selection, while `EnemySpawnSettings` owns the enemy prefab, common movement, health, contact behavior, archetype tuning, initial crowd tuning, and whether enemies from that spawn profile return after pooling. Every profile retains a default separation-distance range and weight, plus an optional array of target-archetype entries containing their own range and weight. Baking randomizes the default and configured target-specific distances per enemy; movement selects the matching entry from the nearby enemy's `EnemyArchetype`, falling back to the defaults. Duplicate target entries are warned about in the custom Inspector and resolve last-entry-wins during baking. The baked per-enemy `EnemyRespawnSettings` preserves that policy when multiple spawners use different profiles; disabled respawning leaves defeated enemies pooled until a game restart. Scene MonoBehaviours retain only scene-instance wiring such as bridges, cameras, and origin transforms.

Full restart destroys all old wave-owned instances, increments each sequence run generation, clears counters and completion, restores its initial random seed, and lets the first wave start its full pre-wave delay again. Destroying the old generation prevents stale pooled entities or defeat observations from affecting the new run and avoids double-spawning. Legacy random and authored enemies continue through their existing in-place reset semantics.

## Ranged Enemy Archetype

`EnemySpawnSettings.Archetype` explicitly selects `Baseline` or `Ranged`; no prefab, scene-object, or presentation name participates in runtime identification. Every spawned enemy receives `EnemyArchetype`. A ranged selection additionally receives the baked `RangedEnemySettings`, `RangedPositioningState`, and `RangedAttackState`. The existing `EnemySpawnSettings.asset` remains baseline. `RangedEnemySpawnSettings.asset` is used by a second ordinary `SpawnerAuthoring` in the arena subscene to add five ranged enemies without changing the 500-enemy baseline batch.

`EnemyRanged.prefab` uses `Prefabs/Enemies/Goblin Trooper Wizard Variant.prefab` through
the sampled GPU-skinning path and `EnemyAnimationProfile.Goblin` (INFO-004, VISION-006).
Its supplied Idle, Walk/Run Forward In Place, Take Damage, Jump In Place and Die motions
share the Baseline Goblin mapping. Slots 3 and 4 both contain the full Cut Attack clip.
`EnemySpawnSettings.ProjectileLaunchFrame` selects its original zero-based release frame
(currently 0-25, default 10); the builder stores the clip's final frame in settings.
Profile baking normalizes that frame into `RangedEnemySettings.ProjectileLaunchProgress`.
Slot 3 retimes anticipation to that pose using the existing wind-up clock. An increment
of `RangedAttackState.ProjectilesSpawned` starts slot 4 at that same pose and plays the
rest once at authored speed. Changing the release frame requires no sample rebuild.
Cancellation and launch discard the firing pose without changing
combat state, physics or projectile timing (ENEMY-002/004). The builder preserves the source
visual scale, and ranged spawn initialization adds no procedural proportion scaling.
Hat and wand are converted to
single-bone skinned meshes so all three renderers follow the same sampled rig. Render
entities carry `AnimationDriven`, bypassing body tint pulses, flashes and deformation.
Generated instanced Full Cel materials preserve source textures under `Materials/Enemies/GoblinRanged`;
the shared atlas tool excludes these materials. The shared ranged settings enable Spawn
From Ground and every skinned part uses the supplied Spawn in slot 7 (INFO-006). Mesh-specific
CPA3 samples, accessory meshes, controller, prefab, materials and settings reference can be
rebuilt through **Crowd Punch > Enemies > Rebuild Ranged Prefab**.

All ranged numerical settings are provisional and live on the ranged spawn settings asset: preferred minimum/maximum distance, engagement range, approach/retreat speed, initial delay and per-instance variation, wind-up, base cooldown and per-shot cooldown variation, damage, player invulnerability duration, projectile speed, horizontal aim-spread radius, fire-time target Y offset, arc height, minimum world-space altitude, lifetime, and radius. Independent per-enemy cadence plus initial and per-shot timing variation is the first-pass multi-attacker control; there is no global simultaneous-attack cap.

`RangedEnemyPositioningSystem` owns the ranged approach/hold/retreat decision. It reuses the baseline active-enemy separation input and writes `NavigationIntent` plus legacy `DesiredMovement`; reachable distance-band goals preserve approach/retreat/hold semantics, and `EnemyNavigationSystem` resolves final steering before movement; `EnemyMovementSystem` remains the velocity owner and its `Active` gate prevents ranged steering from overwriting launch or recovery velocity. `RangedAttackState` exposes eligibility, lifecycle phase, remaining time, emitted count, and cancelled-wind-up count for Entities inspection. Attack evaluation runs after punch and damage application, so same-frame launch or defeat cancels before emission. Pooling resets both attack and positioning state; already-fired projectiles have no shooter reference and remain independent.

`RangedProjectileSystem` uses a deterministic parametric path. At fire time, a deterministic per-enemy/per-shot point inside the authored horizontal spread radius and the authored target Y offset are added to the sampled player position; the time to that point is derived from horizontal distance divided by authored speed. Horizontal/world-space position is `lerp(start, fireTimeTarget, t)` and vertical readability adds `4 * arcHeight * t * (1 - t)`. The system does not clamp `t` at `1`, so after crossing the sampled aim point the projectile continues at the same horizontal speed and along the descending parabola until hit, minimum-altitude cleanup, or expiry. The target is never updated after firing. The prefab is a yellow grey-box sphere with a baked kinematic collider on the `RangedProjectile` layer. Its collider explicitly excludes the Default layer used by enemies and the arena, so it produces no enemy collision events or reactions; player contact is checked against the ECS player snapshot because the player remains a GameObject outside the ECS physics world.

Projectile damage calls `PlayerEcsBridge.ReceiveEnemyHit`, which converts the configured amount for the existing `PlayerHealth` event pipeline. `PlayerHealth` remains authoritative for invulnerability, health clamping, damage acceptance, and death. Projectile simulation is data-only and its baked mesh/material is presentation. There was no compatible projectile pool, so this first pass uses command-buffer instantiation and destruction; this should be revisited only if profiling at representative projectile counts shows structural-change cost is material.

Systems get the entity for mixed singleton state through a non-enableable component such as `PlayerSnapshot` or `MatchState`, then inspect or toggle enableable state explicitly.

## Explosive Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Explosive`. `EnemyExplosive.prefab` uses the generic-rigged
`Models/UltimateMonsters/Blob/GreenBlob.fbx`; both skinned renderers receive generated instanced materials and
mesh-specific CPA3 sample blobs. Its `EnemyAnimationProfile.Explosive` loops `Idle` while stationary and `Walk`
while moving, holds the first `Walk` frame while launched, returns to locomotion during recovery, and plays `Death`
only after entering `Defeated`. Rebuild the controller, samples, materials, and prefab through **Crowd Punch > Enemies
> Rebuild Explosive Prefab**. `ExplosiveEnemySpawnSettings.asset` references this prefab directly.

The spawn system uses the prefab's common movement components, adds `ExplosiveEnemySettings`, `ExplosiveEnemyState`,
and an enableable `ExplosiveDetonationRequest`, then overrides the material base color to orange for gameplay
readability. `ExplosiveEnemySpawnSettings.asset` and the arena's ordinary `SpawnerAuthoring` follow the same profile
pattern as the ranged archetype.

`ExplosiveCollisionTriggerSystem` reads Unity Physics collision events after simulation. It requests the explosive participant when either enemy was already in the authoritative `Launched` phase, without an impact threshold. `ExplosionResolutionSystem` also requests an active explosive whose baseline contact radius overlaps the player. `PlayerContactDamageSystem` excludes explosive archetypes so this contact produces the explosion instead of ordinary melee contact damage.

Explosion resolution marks `ExplosiveEnemyState.HasExploded` before applying any effects, making multiple contacts and overlapping blasts idempotent. Each blast uses a constant radius check with no falloff, accumulates the existing `DamageRequest` and `ExternalImpulse`, and calls the same `EnemyLaunchTransition` used by punches and collision propagation. Explosion-launched enemies therefore carry explosion damage as their launch-chain damage and follow normal collision propagation, deferred defeat, recovery, and pooling rules. Newly overlapped explosives enable their own detonation request; the resolver repeats until no request remains, allowing legitimate overlap chains to complete in the same post-physics frame. The source explosive transitions directly to `Defeated` and enters the established death/respawn handoff.

The existing player bridge receives explosion damage through the normal `PlayerHealth`/invulnerability event and publishes a separate presentation-only event. Player/elite knockback is tuned independently from normal-enemy knockback; boss knockback is baked as future-compatible configuration but is not consumed because bosses do not exist. `ExplosionFeedback` renders a short-lived expanding grey-box sphere without exposing ECS entities to MonoBehaviours. Its dedicated transparent URP shader is loaded from `Resources`, keeping the shader as an explicit Windows player-build dependency instead of relying on a runtime `Shader.Find` lookup that may be stripped.

## Current Prototype Behavior Versus Design

## Pause And Level Selection

`PauseMenu` is a scene-level MonoBehaviour UI boundary created on the persistent game canvas by `GameBootstrap`. It reads
the shared `Assets/InputSystem_Actions.inputactions` `Game/Pause` action, toggles `Time.timeScale`, and owns no ECS data.
It also owns cursor capture: active gameplay locks and hides the cursor, while pausing releases and shows it for menu
interaction. The component reapplies the current cursor state when enabled and when application focus returns (PLAYER-011).
Its runtime-built menu restores selection to Resume when opened so the Input System UI module can navigate and submit with
a gamepad. Level buttons are populated from the fixed `GauntletSequence`; selecting any entry calls the same additive
level-loading path, and selecting the active entry therefore unloads and reloads that gauntlet. The loader continues to
reactivate the GameObject player, restore full health, reset player movement and punch state, place it at the authored
entry point, and request the established ECS restart handoff. The
old always-visible restart button is disabled; restart is now represented by selecting the active level in the pause menu.

## Elite Projectile Punch

Elite spawn profiles now bake `ElitePunchSettings` and initialize an inspectable `ElitePunchState`; no ordinary enemy
receives attack state. The explicit phases are `InitialDelay`, `SelectingTarget`, `Repositioning`, optional `WindUp`, and
`Cooldown`. Each new attempt advances the elite's stored ECS random stream for its inspectable tactic state, while actual
projectile selection scans the eligible set and always chooses the closest active normal enemy with a deterministic
entity-index tie break. It re-evaluates that choice on the existing retarget interval during setup, so a newly closer enemy
replaces the reservation without requiring per-frame selection.
Retargeting continues while the projectile is staging; only setup timeout pauses during that wait (ENEMY-009).

Only active, normal-tier, health-bearing, non-defeated, non-pooled targets qualify for a coordinated shot. The legacy elite
search range and target-to-player distance fields do not filter this nearest-enemy rule. Spawn order affects only the
entity-index tie break when squared distances are exactly equal.

During setup, the desired position is behind the projectile relative to the live player, on the elite's movement plane.
`ElitePunchSystem` runs after `EnemyChaseSystem` and before `EnemyNavigationSystem` and `EnemyMovementSystem`, making its tactical intent override
explicit while leaving velocity integration in the existing movement system. It turns the elite toward the live
target-to-player launch direction at the profile's existing `TurnSpeed`; ordinary chase facing is not used as a hidden
attack prerequisite. Search eligibility uses the dedicated active, recovering, and already-launched switches, while final
punch-effect eligibility is independently rechecked immediately before execution. Setup speed uses the common movement
acceleration and braking values to calculate an arrival speed from remaining distance, allowing fast traversal while
decelerating into the authored position tolerance. Outside that tolerance, the requested speed is never allowed below the
selected target's horizontal speed plus the relative speed needed to cross the tolerance under the same braking model;
this prevents a moving target from creating a permanent follow gap. Position, facing, the exact shared punch
volume, target/player displacement, phase eligibility, reservation ownership, setup timeout, and entity existence are
revalidated through repositioning and wind-up. Zero wind-up executes on the first valid update; invalid wind-up returns to
setup instead of firing. `TelegraphActive` is presentation-only state and is never required for execution.

`PunchResolution` is the focused common resolver used by both player and elite punches. It owns capsule-like forward/radius
evaluation, position-weighted impulse direction, Dasher interruption, `ExternalImpulse`, `DamageRequest` accumulation, and
the authoritative `EnemyLaunchTransition`. Elite-launched normal enemies carry the explicit `ElitePunch` cause and retain
the existing launch sequence, collision history, collision damage, propagation, deferred-defeat, and recovery pipeline.
The elite can affect only its selected projectile or every eligible normal-tier enemy in the exact volume. Optional direct
player contact uses the same ECS-to-Mono hit bridge and remains distinct from later launched-body collision damage.

Every spawned enemy carries a small reservation record. An elite writes ownership only while setting up, and releases it
on execution or cancellation; stale owners and defeated/pooled targets are cleared independently each update. Shared
reservations are an authored policy. Restart and pooling reset attack state, random state, target references, telegraph
state, and reservations, while wave-owned destruction continues to remove the entire old generation. Elite tier gating is
unchanged, so elites remain damageable and knockback-responsive but never enter `Launched`; normal replenishment remains
owned by the existing wave counters.

`EliteCrowdSupportSystem` runs after elite target selection and before movement integration. For each active elite, its
selected projectile (or the closest active normal before selection) first tests its current location as a staging point.
The test excludes static-world ray obstruction, occupied target space, and non-defeated enemies inside the finite route
from the elite to its eventual behind-projectile position. If blocked, the projectile first checks two deterministic rings
of eight nearby candidates, preferring lateral displacement. If those local samples cannot provide full elite clearance,
it searches navigation-cell rings outward until it finds a reachable staging cell with a direct, body-clear elite approach.
The projectile may route around static terrain to reach that distant cell; the elite remains stationary until it arrives.
This lets a selected enemy beside a long obstacle move far enough away for an unobstructed aim and approach. It requests
zero speed when its current approach lane is clear or no sampled alternative is available; only a verified clear current
lane publishes `IsStaged`. With navigation enabled, staging also checks clearance-class-inflated static geometry and arena
bounds for the projectile path and elite approach, plus navigation anchors at both destinations. A centre ray alone does
not prove that an elite can reach the behind-projectile point. The target reservation publishes `IsStaged`; while it is false,
`ElitePunchSystem` requests zero elite movement and does not spend setup timeout, preventing two moving goals from chasing
one another. The expanding search checks at most 32 in-bounds cells per elite per update,
with a 256-step limit for skipping out-of-bounds cells. `EliteStagingSearch` stores the
outward cursor and a candidate in system-owned managed scratch, keyed by the versioned
elite entity. A candidate is revalidated against current bodies and terrain before reuse.
The search resets when its projectile or arena owner changes, or a participant moves
more than four metres from its search origin; an exhausted search retries after half a
second. Inactive elites and an unavailable player discard cached searches. This bounds
the long-obstacle fallback in the 100 x 100m campaign arenas without changing staging
clearance or publishing a pending search as a ready shot. Sampling and clearance reuse
the elite's existing crowd-corridor radius and position tolerance. Other active normal enemies in the finite
projectile-to-player corridor override chase intent with lateral movement toward the nearest side.
Launched, recovering, defeated, disabled, and pooled enemies are excluded. When several elites are active, each normal
supports its nearest active elite with entity index as the deterministic equal-distance tie break. The support layer only
writes explicit `NavigationIntent` and legacy `DesiredMovement`; navigation resolves terrain steering and physics velocity remains owned by `EnemyMovementSystem`.
If a projectile recovers outside navigation spacing bounds, support first requests a clear interior reentry point and keeps
it unstaged. Navigation's existing slow escape movement permits that ingress while checking the complete segment against
inflated static obstacles; it does not teleport enemies or change physical arena/defeat bounds (COMBAT-017).
Projectile staging also uses the existing body-side approach calculation around the waiting elite, using their combined
navigation-agent radii. Terrain clearance checks the waypoint and tries the opposite side when needed, preventing a
staging destination from driving the projectile directly into its stationary owner.

If an unanchored projectile's ingress segment is blocked by its waiting elite, `EliteCrowdSupportSystem` requests a
short elite yield away from that projectile (ENEMY-009, COMBAT-017). The yield requires a valid destination anchor and
full class-inflated terrain clearance; it writes movement intent only. The projectile remains unstaged and setup time
stays paused. Once ingress is clear, ordinary projectile staging and elite setup resume. This handles boundary pins
where projectile-only side detours cannot fit between the owner's collider and the arena edge.
The 2026-10-11 Campaign_43 recurrence captured elite (-15.01012,-13.75446) and projectile (-15.61143,-14.85344).
The projectile's nearest ingress point (-14.5,-13.5) crossed the elite, while the elite already had a valid anchor.
The navigation/hazard/elite cases passed 58/58 in the Editor. A 15-second physics replay using captured crowd positions
showed the original projectile returning inside and three subsequent direct elite launches after recovery. Player health
was restored during observation; evidence is in `Temp/elite-recurrence-diagnosis.txt`,
`Temp/elite-recurrence-regressions.txt` and `Temp/elite-recurrence-replay.txt`.

Elite setup also requests navigation reentry before its staging hold when physics has pushed the elite outside its
clearance-class anchor (ENEMY-009, COMBAT-017). The existing swept-body escape validation and slow navigation movement
return it to an interior cell without teleportation. Setup timeout remains paused and an interrupted wind-up returns to
repositioning. This breaks the circular wait where no projectile staging point can pass clearance for an unanchored elite.
The 2026-10-11 capture placed the elite at (14.537292, -0.3160982) in a (-15, -15)..(15, 15) grid with no static
obstacles; its projectile had a valid anchor but the elite did not. The elite/navigation regression cases passed 40/40
when invoked in the Editor. A Campaign_23 physics replay using the captured crowd positions recorded five direct elite
launches in 15 seconds after inward recovery, then paused. Player health was restored during observation; this validates
the deadlock fix rather than encounter balance. Evidence is in `Temp/elite-stuck-oct11.txt`,
`Temp/elite-oct11-regressions.txt` and `Temp/elite-oct11-replay.txt`.

After executing a punch, `ElitePunchSystem` clears the launched target and retains the authored cooldown. During that
cooldown it continuously finds the next closest eligible active normal and uses the same behind-projectile destination and
arrival-speed calculation as setup. It does not reserve or punch that enemy until cooldown ends. This prevents ordinary
player-chase intent from visually interrupting the coordinated sequence between consecutive shots.
Failed speculative cooldown routes cannot restart cooldown before target selection. Selection and retargeting publish a
hold intent to retire the previous route through `EnemyNavigationSystem`; only a failed reserved, staged approach cancels
the current attempt into cooldown. This prevents the Gauntlet_07 failure loop from suppressing all subsequent reservations.

Both cooldown approach and reserved-target setup pass their desired direction through the same collision-avoidance helper.
When the direct segment to the behind-projectile destination crosses the target's punch-radius clearance, the helper chooses
a deterministic side waypoint around the target; once the target no longer blocks that segment, movement returns directly
to the desired position. Unity Physics remains responsible for actual collision response.

Gauntlet_07 stall verification (2026-09-20): before the fix, the live elite remained at attack sequence two while failed
speculative routes repeatedly restarted cooldown. After the fixes, a 75-second Editor observation recorded eight distinct
elite launches with 23 peak active enemies, and the longer state trace reached attempt 19. The idle player's health was
restored for observation and earlier normal-only waves were cleared with injected damage; this was a behavior check, not
a balance test. Crowd-blocked staging can still pause while waiting for a clear lane or a closer eligible projectile.
ProfilerRecorder samples in that run averaged 0.143 ms for elite punch and 0.521 ms for crowd support (maxima 3.160/5.890 ms);
these are local Editor system costs, not standalone performance guarantees. Regression coverage includes failed-cooldown
retry, retargeting during staging, actual setup failure, body-clear staging, blocked fallback readiness, projectile bounds
reentry, and obstacle rejection during reentry. Build and regression artifacts are under `Temp/elite-*` and
`Temp/EliteRegressionResults.xml`.

The subsequent paused recurrence exposed an additional corner case: projectile 337 at (-6.421355, 9.660804) was
physically clear of the obstacle spanning (-6, 10) to (-3, 13), but inside its square-inflated navigation corner.
`TryEscape` now checks a swept circle of the actual body radius against rectangle faces and corners. This lets a round
body leave the conservative corner margin while still rejecting paths that cross or touch physical obstacle clearance.
Ordinary paths keep square inflation and the escape destination must still be a class-valid interior cell. Regression
coverage uses the captured coordinates, checks nonzero ECS movement intent, and rejects intersecting/tangent sweeps.
The runtime/geometry suites passed 48 tests after this correction. A fresh Gauntlet_07 replay restored the captured player,
elite, projectile and crowd positions with fresh wave health/velocity state: the projectile left the corner and four direct
elite launches were recorded within 15 seconds. The replay was paused after observation. Captures and replay evidence are
in `Temp/elite-paused-recurrence.txt`, `Temp/elite-paused-diagnosis.txt`, `Temp/elite-corner-replay.txt`, and
`Temp/EliteCornerRegressionResults.xml`; the replay is a targeted reproduction, not a restored save of the original run.

## Elite Enemy And Elite-Gated Waves

`Elite` is appended after the four original serialized `EnemyArchetype` values. Baking maps it to the explicit
`EnemyArchetypeKind.Elite` runtime identity and adds `EnemyTier { Elite }`; neither combat nor presentation infers elite
status from prefab, asset, or object names. Elite profiles reuse `EnemySpawnSettings` for their prefab, health, ordinary
melee movement/contact behavior, and all common tuning. Their `KnockbackResponse` uses the existing `PlayerElite` tier.

`EliteEnemy.prefab` uses `Models/UltimateMonsters/Big/Yeti.fbx` through the sampled GPU-skinning path.
Its `EnemyAnimationProfile.Elite` loops `Idle` while stationary and `Walk` while moving, plays `Punch`
during the authoritative elite wind-up, restarts `HitReact` when health decreases, and gives `Death`
priority on defeat. Generated instanced materials live under `Materials/Enemies/Elite`; mesh-specific
CPA3 sample blobs, the controller, prefab, materials, and `EliteEnemySpawnSettings` reference can be rebuilt
through **Crowd Punch > Enemies > Rebuild Elite Prefab** (ENEMY-009, INFO-004).

All enemy damage continues through `DamageRequest` and the shared health resolver; `DamageApplicationSystem`
handles pre-physics requests and Wizard zones resolve their ticks before post-physics recovery. Punches, launched-body collision damage,
explosions, and launched Dashers can therefore defeat an elite normally. `EnemyLaunchTransition` is gated by `EnemyTier`:
normal targets enter the shared `Launched` lifecycle, while elite targets receive the applicable existing elite-tier
impulse without changing launch phase. This preserves normal deferred-defeat and future boss-tier behavior.
Player punches additionally carry `PlayerPunchSettings.EliteKnockbackMultiplier` through the existing Mono-to-ECS punch
bridge. It scales normal- and dash-punch strength only for `EnemyTier.Elite`; punch damage and all non-punch impulse paths
remain unchanged. The default multiplier is `1`, preserving existing tuning until explicitly authored.

`EnemyHealthBarPolicy` is the per-entity presentation rule. Normal enemies retain temporary damage bars and existing
state labels. Elites use `AlwaysWhileAlive`; `EnemyHealthBarBridgeSystem` explicitly bypasses the canvas's global normal
health-bar option for that policy, publishes no normal launch-state label, and withdraws the pooled view on defeat,
respawn ineligibility, or loss of presentation eligibility.

`EnemyWaveSettings` retains `totalEnemyCount` and its weighted normal list and adds an ordered fixed-count elite list.
The baker rejects Elite profiles from the weighted pool, rejects non-Elite fixed entries, declares dependencies on the
wave, profile, and prefab assets, and flattens valid elite entries into `EnemyWaveEliteProfile`. Each wave definition stores
separate normal and elite profile ranges plus total configured elite count; runtime buffers contain no Unity object
references.

`EnemyWaveSpawnSystem` consumes the ordered elite counts before making any weighted normal selection. Failed elite safe
placement remains at the head of the queue. Elites and normals share rectangles, deterministic RNG, player clearance,
physics overlap checks, bounded retries, batch capacity, batch interval, and warning throttling; a batch may finish the
elite queue and use remaining capacity for normals.

Elites and normals both count toward the wave's initial spawn budget. Once every configured elite and normal has spawned,
the wave begins its authored activation policy: `AllEnemiesDefeated` waits for every owned enemy from the wave, while
`DurationElapsed` starts its duration immediately. Normals in an elite wave use the existing pool to replenish while at
least one same-wave elite remains non-defeated. A returning normal clears its observed defeat and restores the sequence's
undefeated/current-wave counters; once the last elite is defeated, pooled normals remain pooled and the surviving crowd
can be cleared normally (ENEMY-011). Elites never replenish.

`EnemyWaveOwnership` records sequence, wave, generation, and idempotent defeat observation. Restart
increments the generation, destroys each old wave-owned root individually so its complete prefab `LinkedEntityGroup` is
removed safely, clears spawn/defeat counts and elite cursors, restores the
initial seed, and re-enters initialization so the first-wave delay is applied again. Legacy random and authored enemies
retain their existing in-place restart and pooling behavior.

## Dasher Enemy Archetype

`EnemySpawnSettings.Archetype` can select `Dasher`. The existing enemy prefab is instantiated as a variant with
`DasherSettings`, `DasherState`, and a per-action `DasherHitHistory` buffer. Its explicit phases are `Positioning`,
`Preparing`, `Dashing`, and `Recovering`; the shared `EnemyLaunchState` remains authoritative for punches, launched
collision chains, recovery, defeat, and pooling. A punch or defeat therefore interrupts every Dasher phase, and pooling
resets both state and hit history.

The decision system maintains an authored distance band and evaluates the configured corridor policy only when entering
preparation. Preparation faces the live player and either stops immediately or brakes using normal movement. Direction is
resampled and locked when the telegraph expires. Dash movement writes the locked horizontal velocity until maximum travel.
After a static-obstacle collision, `DasherObstacleRedirectSystem` adopts the solver's horizontal result as the new locked
direction and restores authored dash speed; a stopped head-on result reflects the incoming direction so the body leaves
the contact instead of remaining embedded. Maximum distance accumulates the redirected path length rather than measuring
straight-line displacement from the start. Player hits are limited by a per-dash flag; enemy hits use a source,
target, and action-sequence history. Both use the existing player bridge, `DamageRequest`, `ExternalImpulse`, and
`EnemyLaunchTransition` pipelines.

Enemy prefab colliders bake on the dedicated `Enemy` Unity Physics category. Each Dasher receives a unique runtime collider
with package-owned cleanup data. While intentionally dashing or in the shared `Launched` phase,
`DasherColliderModeSystem` removes the enemy category from that collider's mask, so enemy contacts never reach the solver
and therefore cannot deflect, displace, spin, or slow the Dasher. The solid filter is restored on leaving those phases.
Static geometry stays in the collision mask and remains solver-owned.

Dash commitment also stores a yaw-only `LockedRotation`. Player punch launch stores the punch direction immediately;
other launch sources capture facing from launch velocity on their first pre-physics update. The rotation is reapplied before
and after simulation while dashing or launched, and yaw angular velocity is cleared after simulation, preventing visible
solver-induced turning without disabling static collision response.

On each new shared `LaunchSequence`, `DasherVelocityCaptureSystem` preserves the incoming launch direction but normalizes
the horizontal launch speed to the Dasher's authored `DashSpeed`. This occurs once per launch, so subsequent static-geometry
response can still slow or stop the Dasher normally.

Because solver-free enemy pairs do not emit collision events, `DasherEnemyImpactSystem` performs a swept ECS overlap from
the Dasher's pre-step position to its post-step position using the existing enemy contact radii. It retains per-action,
per-target deduplication for player-launched Dasher impacts and writes `DamageRequest`, `ExternalImpulse`, and
`EnemyLaunchTransition` state. Intentional dashes are ignored by this enemy-impact path, so they pass through enemies
without damage, knockback, launch state, or other gameplay effects.
The sweep also prevents fast dashes from tunnelling through gameplay impacts. When a player-launched Dasher overlaps an
unexploded explosive enemy, this same contact path queues `ExplosiveDetonationRequest` before `ExplosionResolutionSystem`;
an intentional enemy dash does not trigger that launched-body interaction.

Launched-Dasher knockback direction blends its horizontal travel direction with the horizontal direction from the Dasher
to the struck target. The authored `LaunchedImpactPositionWeight` controls the blend: `0` produces a straight-ahead push
and `1` pushes directly toward the side on which the target was struck.

Launched-Dasher impacts have independent normal-enemy, elite, and boss tuning. `KnockbackResponse` provides those existing
three conceptual tiers without introducing elite or boss behavior: ordinary enemies are always momentum-transparent,
while elite and boss momentum preservation is authored independently. Static geometry remains solver-owned. Grey-box
feedback uses the existing per-entity material colour and post-transform overrides: warning pulse, bright elongated
dash/launched motion streak, distinct resting silhouette, and dark recovery. This stays entirely inside ECS rendering;
the current presentation architecture has no entity-linked GameObject trail renderer.

The current implementation proves architecture and basic interactions, but several behaviors are placeholders:

- Punch detection uses a line/capsule-like distance test and independently assigns impulse, damage, and launched state. Enemy collision damage is also independently thresholded rather than inferred from propagation.
- Player movement and dash remain transform-driven MonoBehaviour movement. `PlayerController` submits each intended horizontal displacement through `PlayerEcsBridge`; `PlayerObstacleCollisionSystem` first resolves any shallow overlap, then sphere-casts the configured player radius against baked non-enemy geometry, resolves one wall-slide pass, and returns the corrected position to the controller. Zero-distance contacts permit motion away from their surface so corners cannot trap the player. This keeps SubScene obstacle collision inside the ECS physics world without allowing the MonoBehaviour to query or retain entities.
- Dash punches start immediately through the ordinary punch pipeline, use the same tuning as standing punches, and do not alter committed dash movement (PLAYER-005). Matching results select the separate hit/miss cooldown (PLAYER-009).
- A launched enemy, including a zero-health enemy with deferred defeat, can propagate launched state to and independently damage an active or recovering enemy when Unity Physics reports solver-estimated contact impulse above the respective authored thresholds. Unity Physics supplies the transferred velocity; gameplay may rotate newly propagated horizontal velocity toward the smallest-angle eligible target inside the configured correction radius without changing its magnitude or vertical component. One source launch damages each target at most once but may damage multiple targets. Defeated enemies and launched-versus-launched pairs are ineligible. The final effect grammar remains unresolved.
- Enemy chasing and contact damage exist as prototype behavior.
- A player health bar exists. Living damaged normals may display temporary health per INFO-001; body presentation communicates normal launch/recovery state. Elites retain persistent health bars.
- Ten compact gauntlets are authored through the additive lifecycle, replacing the original four encounters. Exact normal compositions, finite timed overlap, and cumulative clear gates bound active populations to 30. Gauntlets 7 and 10 contain one replenishing elite each. The complete boss-ended MVP run remains unfinished.

Do not preserve these details merely because they exist. Preserve the ownership boundary and system timing while evolving behavior toward accepted GDD rules.

## Packages Relevant To Runtime Architecture

- Unity Entities feature set (`com.unity.feature.ecs` 1.0.0)
- Input System 1.18.0
- Universal Render Pipeline 17.3.0
- Unity 6 built-in physics modules plus Unity Physics supplied through the ECS feature set

Package versions in `Packages/manifest.json` and `packages-lock.json` remain the source of truth.

## Player Punch Cooldown Confirmation

Player punch containment uses a horizontal rectangle defined by the authored range and radius: forward distance from
zero to range, and lateral distance up to radius. Height is checked independently against radius, so shorter enemies
do not lose horizontal reach. Detection, aim-assist membership, and trajectory preview all use `PunchResolution.Contains`
with the player-punch cause (PLAYER-003). Elite punches retain their circular cross-section.

`PunchDetectionSystem` records resolution and whether any `PunchResolution.TryApply` call succeeded on the existing
`PunchRequest`, then disables the request. `PresentationBridgeSystem` reads that result through the non-enableable
`PlayerSnapshot` singleton and sends the request sequence and hit flag through `PlayerEcsBridge.PunchResolved`.
This result is delivered even when there are no enemies or trajectory preview is disabled. `PlayerPunch` waits for its
matching result before accepting another request and selects the configured hit or miss duration (PLAYER-009).
The shared settings retain the existing 1.25-second hit cooldown and add `MissCooldown` at 0.25 seconds; zero disables
the chosen outcome's cooldown. `activeCooldownDuration` normalizes progress for that particular recovery, so hand
feedback and contact-to-ready animation follow the same clock. One `CooldownStarted` event is raised per nonzero
acknowledged cooldown; duplicate results cannot restart it. A multi-target hit starts one hit cooldown. Resetting
clears the cooldown clock; resetting or disabling the punch clears its pending state, so stale
results cannot apply cooldown to a later request. Enemy detection remains ECS-owned and Burst-compatible.

## Deliberate Shot Experiment (2026-09-06)

See [design diagnosis, alternatives, tuning and playtest protocol](../Design/DeliberateShotsIteration.md).
The GDD remains the accepted baseline; this document records the implemented experiment.

- `EnemyContactDamageSettings.AttemptWindUpDuration` is authored in the existing spawn profile. Baseline
  `EnemyContactAttemptState` now also owns `IsWindingUp` and `CommittedDirection`. `EnemyContactCommitment`
  handles prepare/commit/cancel/cadence without physics or presentation writes. `EnemyChaseSystem` emits
  zero intent during preparation and direction-committed intent during the finite attempt. Committed
  attempts retain a pressure slot until they finish; otherwise the closest eligible enemies receive it.
  Launch/recovery interrupts the attempt; pooled reset uses the existing whole-state reset.
  Elite staging and corridor-clearance overrides also cancel contact commitment so body warnings match actual intent.
  Setting wind-up to zero selects the previous immediate pursuit behavior. Explosive, ranged, Dasher
  and elite overrides retain their ownership and timing.
- `EnemyVisualBaker : Baker<MeshRenderer>` discovers an `EnemyAuthoring` ancestor through dependency-aware
  baker access. It adds `EnemyVisualOwner` and a URP color override to its **own** renderer entity. A parent
  baker must not add components to a child's primary entity. Owner references remap during prefab
  instantiation, so both ordinary root meshes and the elite child mesh follow their correct gameplay entity.
- `EnemyReadabilitySystem` is a Burst-compatible parallel presentation job over those renderers, with
  read-only gameplay lookups. It writes body color only. Existing Dasher presentation still owns its
  specialized color and shape. Ranged/explosive silhouettes are set once in spawn initialization using
  `PostTransformMatrix`; physics collider geometry is unchanged. No per-enemy GameObject is added.
- The health bridge now publishes normal bars only for living recently damaged enemies. It does not
  publish normal launch/recovery labels. Elite health policy remains always-visible while alive.
- `PunchResolution` now clears linear/angular velocity before all eligible **normal player launches**,
  including Active and Recovering bodies, so incoming momentum cannot skew the previewed direction
  (PLAYER-004, COMBAT-014). This intentionally revises the prior Active first-punch momentum rule;
  elite additive knockback and enemy-originated punch semantics remain unchanged.
  The punch pipeline, cooldown acknowledgement, bridge data and physics phase ordering are unchanged.
  Arrowheads are local preview presentation only. Authored assist is 18 m / 12 degrees;
  propagation correction and homing are disabled by their existing zero settings. The collision system
  avoids allocating a full correction-candidate snapshot when correction is disabled.
- The shared authored impulse damage curve now caps at three times originating damage, including
  player impacts. This improves durable-target payoff without a separate scoring or reward system.
  Gauntlet 4 supplies 48 mixed normals plus one replenishing elite. Gauntlet 3's 200-enemy wave is unchanged.

## Verification Constraints

The ten-gauntlet content uses 39 inspector-editable wave assets under
`Data/Settings/Waves/Progression`, unchanged enemy profiles, and one existing wave sequence per
SubScene. No spawning schema was added. Every recovery break and final wave uses cumulative
clear; timed activation only assembles finite cohorts inside a stage. Elite cohorts drain
before the next stage. See [the progression and validation record](../Design/TenGauntletProgression.md).

`GauntletProgressionBuilder` is an explicit Editor authoring recipe, not a runtime loader.
Rebuilding replaces generated content, so ordinary tuning should edit the saved wave assets
and scenes. Floor MeshColliders use convex baking, producing one solid hull rather than
triangle-mesh collision edges that can deflect grounded enemies inside the court
(VISION-004, COMBAT-018). Floor outlines and heights are unchanged. Low perimeter rails bake into Unity Physics; rail collision
faces extend above the visible mesh to intersect the elevated hybrid player sphere cast.
Movement bounds are inset from the perimeter and defeat bounds permit limited launch travel
outside the court before terminal defeat. There are no internal navigation obstacles. A
non-colliding presentation backdrop below each floor occludes pooled bodies that would
otherwise be visible beneath compact courts from the orbit camera.

Four lifecycle corrections support the sequence without changing enemy profiles or attacks:
`GameRestartSystem` destroys independent fired `RangedProjectile` roots and linked children on
restart; `EliteWaveReplenishmentSystem` also checks the ownership record's terminal
`DefeatCounted` flag, because pooling restores an elite's launch phase to Active for generic
pool bookkeeping. A pooled defeated elite must not re-enable its normal cohort (ENEMY-011).
`EnemyWaveDefeatCountSystem` explicitly runs after `OutOfBoundsSystem` and also counts
enabled pooling requests, including normals that leave defeat bounds while replenishment is
enabled and therefore bypass terminal launch state. The normal respawn path already reverses
that ownership count on return. An out-of-bounds pooled normal now permits completion if its
elite dies before it returns, instead of leaving a permanently positive undefeated count.

`GamePrePhysicsGroup` observes `GameRestartRegistry.Sequence` and a rebuild request from
`GameRestartSystem` after actual cleanup. Either skips one gameplay fixed-step update so the
following `PhysicsSystemGroup` rebuilds its collision world before pre-physics queries resume.
Those queries otherwise read the previous step's world, which may reference collider blobs
released by additive teardown or pooled-Dasher cleanup. This is a restart-only warmup;
ordinary system order and combat/physics tuning are unchanged. The cleanup notification is
needed because the new SubScene's `MatchState` can become available after the Mono request.

`GauntletProgressionTests` checks authored references, geometry, bounded compositions, and
isolated ECS lifecycle regressions. `GauntletSequenceSmokeCheck` is an explicit Editor-only
probe that keeps an idle player alive and injects `DamageRequest` to clear stages. Its logs
and gameplay-camera captures go to `Temp/GauntletValidation`; it does not measure player
skill, intended clear times, or balance.

- Unity compilation, baking, scene wiring, and play-mode behavior are the final verification sources.
- A generated `Assembly-CSharp.csproj` may be stale until Unity refreshes assets.
- Physics changes require checking representative crowd density and profiling, not only single-enemy correctness.
- Changes to bridge fields, component ownership, system order, scenes, or package boundaries must update this document.

## Player Hit Reaction

GameBootstrap adds PlayerHitAnimation to the player root instead of renderer blinking.
It listens to PlayerHealth.DamageAccepted and triggers the masked Hit Reaction Animator
layer above the punch layer. Hit To Body plays once in place, with a short entry/exit blend.
The component releases the layer weight to zero after playback and on disable, so locomotion
and the current punch pose resume. Punch spine yaw fades out with reaction weight and returns
afterward. Rejected invulnerable hits do not restart the clip. Damage, invincibility duration,
movement, and punch eligibility remain unchanged (PLAYER-002, PLAYER-005, PLAYER-009).
An accepted PlayerPunch.PunchStarted immediately cancels the hit reaction, clears its pending trigger, and sets its layer weight to zero so the punch pose and yaw regain priority. Cooldown-rejected punch input does not interrupt the reaction (PLAYER-005, PLAYER-009).


## Combat Feedback Pass (2026-09-12)

This presentation pass reinforces VISION-004/005, COMBAT-001/003/012 and INFO-004.
It changes no attack eligibility, damage, launch impulse, targeting, dash movement, enemy
archetype, or wave configuration. OQ-001 (target hardware/performance), OQ-015 and OQ-016
remain open; the restrained defaults are tunable presentation choices, not new gameplay rules.
No audio, HUD, decals, bloom or third-party dependencies were added.

### Event ownership and ordering

- `EnemyBaker` adds `EnemyImpactFeedback`, a fixed-size strongest-pending mailbox per enemy.
  `PunchResolution` records only successful applications. `EnemyLaunchCollisionSystem`
  records eligible launched-body contacts and launched-body/world contacts using the existing
  collision stream. `DasherEnemyImpactSystem` records its existing deduplicated swept hits.
  These producers write data only; they never reference UnityEngine presentation objects.
- `ImpactVelocityCaptureSystem` runs last in GamePrePhysicsGroup and records pre-solver
  velocity. Ordinary collision strength uses relative speed along the contact normal and
  solver-estimated impulse. When momentum is received and transferred within the same physics
  step, impulse times source inverse mass supplies a transferred delta-velocity estimate.
  World impacts use incoming normal speed. Dasher hits use preserved launch velocity.
- `EnemyLaunchState.FeedbackChainDepth` starts at one for player launches, increments from
  the source on ordinary and Dasher propagation, and is capped at 64. A re-punch begins a
  new depth-one chain. This is presentation metadata, independent of damage and existing
  PropagatedLaunchCount. Only player-owned depth escalates presentation.
- `CombatFeedbackBridgeSystem` runs in GamePresentationGroup after the existing punch result
  bridge, before the final visual composition. It consumes pending mailboxes, filters weak
  contacts, starts short visual envelopes, and publishes bounded `CombatFeedbackMessage`
  values through `PlayerEcsBridge`. It also sends bounded launched-trail samples. Mono receives
  opaque visual keys and launch sequences, never enemy Entities or an EntityManager.
- `EnemyImpactVisualSystem` runs after role colors, Dasher shape and enemy animation. It
  composes flash with their current color and applies direction-based deformation to renderer
  LocalToWorld only. Each renderer caches its current undeformed matrix and last output;
  every deformation is rebuilt from that baseline. LocalTransform, authored scale,
  PostTransformMatrix, physics collider geometry and sampled skin matrices remain untouched.
  Both MeshRenderer baking and additional skinned render entities receive this state.
  The old hardcoded health-bar-timer flash was removed so there is one enemy impact flash.

### Scene presentation and configuration

Bootstrap's GameBootstrap references
`Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset` and automatically installs
`FeedbackTimeController` and `CombatFeedback` on the bootstrap object. The latter uses
existing PunchResolved, DamageAccepted, DashStarted/Ended and PunchStateReset events.
It prewarms `ImpactParticlePool` (eight instances per effect by default) and
`LaunchedTrailPool` (48 short TrailRenderers by default), attaches `CombatCameraFeedback`
to the main camera, and prepares `PlayerDamageFlash`. No manual scene plumbing is required.
Missing particle prefab references deliberately select built-in placeholders using the
included Resources shader. Optional custom prefabs should be finite particle bursts.

All effect tuning except punch-pose timing lives on CombatFeedbackSettings:

| Inspector group | Controls |
| --- | --- |
| Time | Punch freeze (45 ms), exceptional depth (4), enabled toggle, scale (0.22), duration (95 ms), 1.5 s retrigger interval |
| Impact significance | Speed range (2-16 m/s), impulse minimum (1.5), environment minimum normal speed (4 m/s), contact interval (120 ms), single-hit ceiling and capped chain gain |
| Camera | Punch/damage/collision/exceptional directional kick, shake, decay, maximum displacement, significance threshold, collision interval and distance falloff |
| Particles | Seven optional effect prefabs, pool capacity, per-frame impact budget (8), scale range, placeholder count, dash emission interval |
| Launched trails | Capacity, minimum speed, 140 ms length in time, width and color |
| Visual-only enemy deformation / flash | Squash amount/duration/threshold, flash duration and distinct enemy/player strength/color |
| Dash camera | FOV delta (5.5 degrees) and transition time (75 ms) |

Base speed intensity is normalized over the configured speed range. A single-impact ceiling
(default 0.7) leaves room for chains: multiply by `min(maximumChainMultiplier,
1 + max(0, depth - 1) * chainGainPerDepth)` and clamp the result to [0,1]. Strong standalone,
small-chain and exceptional events therefore remain distinguishable even at high speeds.
The impulse minimum independently rejects insignificant solver contacts. Feedback cooldowns
apply only to accepted presentation impacts; weak floor contacts do not suppress later hits.

Ordinary collisions use solver average contact positions. Punches use the struck enemy's
position because the existing volume attack has no fist contact manifold; Dasher swept
contacts use the midpoint of the overlapping bodies. Player damage estimates contact from
player radius and accepted push direction, falling back to facing when no direction exists.
These approximations do not change hit geometry or manufacture gameplay collision data.

### Restoration and readability

Punch freeze is driven once by the existing hit-confirmed PunchResolved event (PLAYER-009),
not by input or per-target particles. `PlayerPunchAnimation.ConfirmContactPose` evaluates
its existing time-parameter Animator layer at the configurable contact frame before freezing.
The existing animation component exposes strike speed, follow-through speed, contact frame
and a confirmation-pose toggle. Fist/arm bone scaling was deliberately omitted because the
humanoid rig is retargeted; the existing animation/upper-body yaw system remains authoritative.

FeedbackTimeController centrally composes hit-stop, exceptional slow motion, menu pause and
additive transition gates. Hit-stop wins while active; an unexpired slow-motion window then
continues, followed by the captured baseline. Deadlines use unscaled time. Pause and transition
cancel temporary effects and never capture a transient zero scale as their resume value.
The fixed timestep and Unity Physics integration are unchanged; gameplay timers and movement
remain on scaled time, while feedback restoration continues on real time. Reset, disable,
death and restart cancel temporary state. PauseMenu and GauntletSequence retain separate gates.

CameraFollow removes the previous translation before its normal follow solve and composes
new feedback afterward. Rotation is never shaken, preserving camera-forward punch aiming.
FOV is baseline plus one smoothed offset, with dash end/interruption and reset restoring it.
Collision camera responses are distance attenuated and rate limited. Exceptional events require
player ownership, configured depth, sufficient strength and the global retrigger interval.

Enemy flashes reuse ECS material properties. Sidekick's player shader lacks a general body
tint: PlayerDamageFlash briefly appends one shared transparent overlay to the existing renderers
and restores their preallocated original material arrays. It does not instantiate per-body
materials. Trails and fallback particles use one shared vertex-color material. Authored particle
prefabs share a separate URP unlit alpha-blended material and soft-disc texture. Trail samples are limited
to launched bodies above minimum speed; unobserved, pooled, reused or new-launch slots clear
before release/reassignment. Particle exhaustion drops cosmetic work. The bridge allocates no
per-frame enemy arrays. Restart and pooling explicitly reset mailboxes as well as launch state.

The existing ExplosionFeedback sphere presentation now prewarms a bounded reusable pool
(default 16, configurable on that component) and clears on reset/disable/transition. Its sphere
appearance and authored explosion duration/radius are unchanged. Its shader is also reused
for the player overlay. This removes the previous instantiate/destroy cycle per explosion.


### Verification of this pass

- Runtime and Editor assemblies compile with zero errors. Existing package assembly-conflict,
  obsolete ColliderAspect and unused legacy-aspect warnings remain.
- Six EditMode regressions passed in Unity: time overlap/expiry; menu and transition ownership;
  re-punch ownership/depth reset; bounded mailbox priority/contact throttling; repeated renderer
  deformation preserving physics transforms and exact authored-scale restoration; trail release
  and capacity reuse. Tests are in `Tests/Editor/CombatFeedbackTests.cs`.
- A real six-body arranged line produced one confirmed punch, 14 impact messages, depth three,
  37 peak particles and six active trails. The successful punch reached timeScale zero and
  restored to one. An empty-volume smoke request produced no freeze or effects.
- A temporary 200-body crowd in Gauntlet 3 (created only in Play mode, without changing wave
  assets) produced a real chain reaching depth 11: 154 impact messages over four seconds,
  77 peak particles and 46 active trails. Exceptional slow motion was observed and scale
  restored to one. Particle/trail object count remained 104, the prewarmed budget. The later
  pooled explosion implementation adds a separate fixed budget of 16 existing-style spheres.
- An Editor ProfilerRecorder sample at that crowd size measured CombatFeedbackBridgeSystem
  at 0.270 ms mean and 0.333 ms maximum across 180 samples. This measures the bridge in this
  local Editor run, not total rendering/physics cost, standalone-build performance or a
  supported hardware target. OQ-001 remains unresolved. A gameplay-camera capture was inspected
  at `Temp/CombatFeedback-chain.png` for crowd visibility; it is not a final-art approval.
- Real Input System dash presses, including an early disable/interruption followed by a second
  dash, produced two starts/two ends. FOV peaked at 62.837 degrees from a 60-degree baseline and
  restored to 60.00006 degrees. Player hit overlays appeared on the model and all 31 inspected
  renderer material arrays restored. Background crowd attacks continued during that test.
- Editor script-reload testing exposed managed-pool loss and camera destruction-order hazards.
  Pools now reconstruct when Unity retains scene references across reload, and teardown checks
  Unity object validity before touching the camera. These are presentation lifecycle fixes.

No manual Unity Editor setup is required for Bootstrap or the shipped gauntlets. The seven
particle slots now reference authored prefabs in `Assets/CrowdPunch/Prefabs/Feedback`.
Punch phase controls live on the existing PlayerPunchAnimation component on the player model;
they preserve PLAYER-005/009 gameplay timing and do not require rig changes.

- Final lifecycle verification after the fixes: a script reload retained exactly 56 particle
  systems and 48 trails with settings rebound and timeScale one. Repeated explosion requests
  used the same 16 spheres and all expired. Reset left zero trail vertices/emitting slots,
  timeScale one and FOV exactly 60 degrees. Play mode was stopped and the temporary stress
  crowd was discarded. The pre-existing Synty downloader/disabled Animator Editor warnings
  are unrelated to feedback gameplay.


### Authored combat particles

`CombatFeedbackSettings.asset` assigns PunchImpact, EnemyImpact, PlayerDamage,
EnvironmentImpact, DashStart, DashMovement and DashEnd. Each prefab has a directional
fleck burst and a soft-wisp child, sharing `Materials/Feedback/CombatParticles.mat` and
`ParticleSoftDisc.asset`. Warm punch flecks, pale enemy impacts, coral damage and tan dust
distinguish event types; pale cyan dash effects stay short. This supports VISION-004/005
and INFO-004 without changing damage, launch, dash or chain rules.

The existing ImpactParticlePool instantiates eight copies per effect (56 roots, 112 particle
systems). Each activation emits 5-11 particles, with a maximum authored lifetime of 0.32
seconds. No looping emission, collision modules, lights, shadows or per-instance materials
are used. World-space simulation preserves emitted particles during movement, and hierarchy
scaling retains the existing impact-intensity control. All systems use AlwaysSimulate so
offscreen effects expire and release their pool slots.

Tune burst count, lifetime, color, shape and size on each prefab's root and SoftWisps child.
Global scale, pool size and impact limits remain on CombatFeedbackSettings. No scene edits
or additional runtime components are required.

Verification: all seven authored burst counts, particle expiry, replay and recursive clear
passed in Unity. A rendered preview was inspected at `Temp/CombatParticlePrefabs.png`.
Live bridge requests activated seven pool slots, all expired within one second, then all
seven replayed with the same 112 systems. Reset left zero live systems. The existing Synty
Sidekick downloader Editor-window error remains unrelated to these assets.


### Cartoon feedback tuning (2026-09-14)

The user's requested exaggeration strengthens silhouette changes and directional bursts
while preserving VISION-004/005, INFO-004 and PLAYER-005/009. This is presentation tuning,
not a resolution of the broader art/camera questions OQ-015/016.

- Punch freeze is 45 ms; exceptional chains use 95 ms at scale 0.22, still gated at depth
  four and by the existing 1.5-second retrigger interval.
- Punch/damage/collision/exceptional kick strengths are 0.17/0.28/0.085/0.22 metres.
  Displacement is capped at 0.34 metres, with 100 ms decay and no camera rotation.
  Collision impulses remain strength/distance gated, now spaced at least 220 ms apart.
- Squash rises from 0.13 to 0.27 with 160 ms recovery; hit flashes last 65 ms and have
  maximum strength 0.9. Existing velocity/intensity multipliers still temper ordinary hits.
- Launched trails are 0.24 metres wide before velocity scaling, with alpha 0.58 and only
  140 ms history. The minimum speed rises to 4 m/s to exclude sluggish bodies.
- All seven particle prefabs have larger, more saturated flecks, slightly longer finite
  bursts and restrained low-alpha wisps. Global scale is 0.7-1.45. Particle count remains
  5-11 per event; the bridge emits at most eight effects per frame instead of twelve.
  Dash movement emits at 55 ms intervals; pool capacity and shared materials are unchanged.
- Chain gain rises to 0.18 per depth, capped at 1.8, retaining the single-impact ceiling
  of 0.7. Strong singles, short chains and exceptional chains remain distinct.
- This feedback pass set strike speed 1.85 and follow-through speed 0.85. The subsequent
  2026-10-21 player appearance pass replaces its old +50/-42 torso yaw and clip poses as
  described in the player presentation section above. Dash FOV remains baseline plus
  5.5 degrees with 75 ms smoothing.

Configuration lives in CombatFeedbackSettings.asset, the seven Feedback prefabs, and
PlayerModel's PlayerPunchAnimation. Script defaults match the new central tuning.
Build passed with zero errors and existing package/legacy warnings. All six existing
feedback regressions passed in Unity. Particle preview and a gameplay-camera saturation
capture were inspected. Sending 140 presentation requests filled the fixed 56 root slots
without increasing the 112 particle systems; all expired and reset cleared every system.
Overlapping configured hit-stop/slow motion restored timeScale to one. This saturation
check verifies presentation capacity, not a new whole-crowd performance benchmark.
Real Input System dash verification produced two starts/two ends, including an early disable/interruption. FOV peaked at 65.37495 degrees and restored exactly to the 60-degree baseline. Play mode was stopped without saving temporary scene state.

## Static Obstacles And Navigation

When an active body has no terrain anchor after physics contact, `GroundHazardNavigationSystem` now uses the same
actual-radius swept escape check as terrain navigation and checks the complete ingress segment against active hazards
(COMBAT-017, GROUND-005). It cancels stale hazard routes and requests slow movement toward the clear interior cell.
It does not send that escape through class-inflated `Safe`, which would reject the outside-margin starting point and
cancel valid terrain escape whenever any active hazard exists in the arena. Hazard-intersecting escapes still wait.
Verification on 2026-10-11 used three captured Campaign_28 positions along the obstacle spanning (-15,-11)..(-9,19).
Terrain navigation requested 1.5 m/s at each position before the old hazard overlay reduced it to zero. The updated
navigation/hazard/elite suites passed 56 cases in the Editor. In a two-second Campaign_28 physics replay, all three
remained Active, moved roughly 10-11m and regained valid anchors. Temporary enemy health protection isolated movement
from hazard defeat and was restored afterward; this was not a balance test. Evidence is in
`Temp/obstacle-stuck-diagnosis.txt`, `Temp/obstacle-fix-regressions.txt` and `Temp/obstacle-fix-replay.txt`.

The dedicated validation arena adds baked rectangular solids, class-based clearance and reachable regions, explicit tactical intent, a shared budgeted A* scheduler, and optional editor diagnostics. The original ten-level progression is unchanged. See [Navigation ownership, settings, and system order](Navigation.md) and [performed validation and profiling](../Validation/NavigationValidation.md). Projectile cover, explosion occlusion, and terrain-aware homing are not introduced: existing projectiles pass static solids, explosions remain radial, and launched-body homing may select an obstructed target while physical wall collision remains active.

Navigation participation anchors now default to the clearance-valid spacing-bounds centre (or nearest valid cell), with an optional explicit override for disconnected layouts. See [anchor selection](Navigation.md#spawning-and-lifecycle). This is baked configuration, not a per-frame search.


## Armored And Gauntlet_12 (ENEMY-014/015)

Armored appends serialized archetype value 5 and stays Normal tier. EnemySpawnSettings owns
three armor timing/recoil values; shared profile baking and spawn
initialization add EnemyArmorSettings, EnemyArmor and ArmorHitHistory only to this archetype.
Recovery leaves armor alone. Pooling and restart reset it; wave reload destroys old owned roots.
Armored never publishes an individual health bar. The existing health-bar presentation bridge
publishes its remaining armor count to the pooled screen-space Canvas instead. The Canvas displays
one shield icon per stage, hides the indicator on break, defeat or pooling, and reuses views across spawns.
GameBootstrap keeps Canvas ownership; MonoBehaviour UI does not query enemy entities.

ArmorHitResolution is shared by solver body impacts, swept launched-Dasher impacts and explosions.
Per-target history stores source entity and launch sequence, sharing identity between an explosive
body and its blast. A protected contact is consumed so sustained contact cannot become a late hit.
History cleanup follows current source launches, retaining exploded sources until pooling/reset.
It does not accumulate across source lifetimes. Two accepted shield hits write a modest planar recoil and a
stagger deadline; chase and movement both honor the deadline. The second hit removes the last shield
without launching or damaging health. Subsequent eligible hits use the existing source-specific launch
and damage pipeline. DamageApplicationSystem blocks health damage through the post-hit protection window.
Player punch detection reports connection separately from PunchResolution's gameplay result.
Preview filters protected sources; assist/homing candidate eligibility deliberately retains them.
Elite selection, stale/area punch resolution and direct boss scattering reject protected targets.

EnemyChaseSystem uses its existing local-separation neighborhood and writes direct player pursuit
for Armored without allocating pressure slots. Navigation remains the path owner. Armored uses
MoveSpeed * ChargeSpeedMultiplier; stagger never lets normal movement overwrite recoil velocity.
EnemyReadabilitySystem uses one stable Armored tint at every stage; EnemyImpactVisualSystem still
composes transient flashes over it. The shield indicator alone communicates the stage count.
EnemyAnimationProfile.Armored reuses CPA3 sampling: Idle/Run, HitReact on armor-hit sequence changes,
a sideways Idle flight pose, Jump_Land recovery and Death defeat. Physics never depends on the poses.
EnemyArmoredPrefabBuilder regenerates the Orc_Skull prefab, controller, materials and samples.

Wave settings optionally reference a Baseline armoredAmmunitionProfile. Its baked profile is used
only after the wave's original pending spawn queue drains. ArmoredAmmunitionSupply checks owned
current-generation bodies at most four times per second and uses the wave's existing ranges,
physics clearance, player distance and navigation checks. One supplemental root replaces the previous
counted supplemental root, keeping live allocation bounded. Each addition increments cumulative
undefeated count and a per-wave supplemental budget; ordinary defeat counting decrements/increments
the established counters. Wave completion includes that budget. Restart resets it. No MonoBehaviour
queries enemies and no alternate runtime spawning framework is introduced.

GauntletProgressionBuilder.BuildArmored authors only Gauntlet_12 and its two waves (1+6, then 3+12
Armored+Baseline), navigation, opening hint and Bootstrap/build-list registration. Nature-kit rendering
uses the existing recipe. Rebuilding the original ten or boss preserves later sequence entries.
Default armor tuning: 0.30 s stagger, 3 m/s planar recoil, 0.25 s hit protection.
The stable body tint and shield icons are presentation constants.

Scene, physics, regression and crowd-cost evidence is recorded in [Armored validation](../Validation/Armored.md).

## Barricade And Gauntlet_13 (BARRICADE-001..005)

Gauntlet_13, "Break Through", follows the unchanged Gauntlet_12. The main scene supplies the
entry and nonblocking opening hint. Its SubScene contains a 28 x 36 m court, one solid
28 x 4 x 1.2 m barricade at z=11, and a green exit at z=15. It reuses the nature environment,
existing Baseline/Explosive prefabs, wave placement and pooling. The Bootstrap selector and
build list now continue to Gauntlet_14. The authored sequence is not a decision about the
final game's total length.

`BarricadeAuthoring` and `BarricadeBaker` bake shared hit-count state, collider references,
exit geometry and tuning from `Data/Settings/BarricadeSettings.asset`. The collider is a
dedicated static Unity Physics box; changing durability never changes an enemy's archetype.
The baker owns immutable intact and zero-filter collider blobs, swapping the component's
reference on destruction instead of mutating a shared collider. No enemy health component
or health-bar presentation is attached to the barricade.

Chapter One exits in Campaign 04 and 06 use static oak waygates, pennants and a
ground perimeter matching the authored completion radius. `VillageExitCueBaker`
links the ring/arrows to their gate through pure `VillageExitCue` data.
`VillageExitPresentationSystem` reads gate durability to reveal forward arrows and
pulse colour after destruction. It owns only cue transforms and colour; the existing
completion system still requires the player to reach the authored exit. The fixed
landmark participates in the chapter shadow bake; animated cues do not cast shadows.
`ChapterOneExitVisuals` refreshes the two saved exits and is used by the chapter recipe.

Chapter One gates in Campaign 04 and 06 opt into `VillageGatePiece` presentation.
`ChapterOneGateVisuals` authors segmented, jagged oak planks, reinforcing rails, bolts,
damage fractures and bounded splinter/dust meshes under the existing barricade root.
`VillageGatePieceBaker` adds only presentation data; `BarricadeVisualBaker` retains
the existing gate link and per-entity colour. `VillageGatePresentationSystem` owns
their hit recoil, staged fractures, outward chips and ballistic tumbling/fading debris.
The generic barricade visual loop excludes these pieces, while its shared impact
feedback still runs. Collision, hit counting, replenishment and exit rules are unchanged.
The pieces have no colliders, use Full Cel surfaces and remain excluded from static
lighting bakes. Restart/scene unload recreates or removes them with the SubScene.
The chapter builder reapplies the same presentation when rebuilding levels 4 and 6.

`BarricadeImpactSystem` runs last in pre-physics, after launch homing and ground constraint.
Swept AABBs cheaply reject distant bodies; remaining launches cast their actual collider
through the upcoming fixed-step displacement in the existing collision world. The closest
blocking lateral hit is authoritative, so a nearer enemy or solid is not shot through.
`BarricadeHitResolution` counts source entity + launch sequence once. Owner masks accept
Player/Enemy/Boss/unowned launches by default. Independent radial explosions also use this
resolution and the closest point on the box; an explosive impact requests the existing
explosion pipeline, sharing its identity with the blast. History expires on pooling,
destruction or a changed source launch sequence, and restart clears it.

Destroying hits swap the collider before the physics world rebuild and preserve incoming
velocity. Intact hits queue a `BarricadeRebound` buffer entry; `BarricadeReboundSystem` runs
after physics and before ordinary launch propagation and explosions. It reflects horizontal
incoming velocity at the configured multiplier, clears the homing lock, and corrects only
normal penetration if a fast discrete step tunneled through the contact plane. It does not
perform normal enemy movement. A wall broken by another body in that step cancels pending
rebounds, letting the bodies continue through. Ordinary collisions, damage, ownership and
recovery remain under their existing systems.

The existing punch detection tests overlap against the box surface and returns its existing
connection result for cooldown, with no barricade damage. The barricade is an additional
candidate for the existing persistent punch lock, propagated correction and homing. Selection
still uses target centers and the same ranges, angles, ray replacement and tie-breaking rules.
The existing initial-direction preview follows that lock; it does not predict rebound.

An optional `BarricadeCrowdSequence` link on `EnemyWaveSequenceAuthoring` requires one wave
and excludes a boss owner. Initial allocation remains `EnemyWaveSpawnSystem`; its finite
allocation never advances by kills for this objective. Spawned roots carry
`BarricadeCrowdMember`. Replenishment enables the existing `EnemyRespawnSystem` while the wall
is intact, reusing the wave's safe placement routine (currently named `BossCrowdPlacement`),
authored ranges and ownership counters. The saved wave contains 14 Baselines and 2 Explosives;
composition and the 16-root bound are editable in `CP13_01_Barricade_Crowd.asset`. Pool delay is
2 seconds after the ordinary defeat/pooling animation. Destruction stops the pending initial
queue and pending respawns; it never changes a surviving enemy's attack or health state.

`GauntletCompletionSystem` reports this objective only when durability is zero and the
available player's snapshot enters the 2 m exit radius. It ignores surviving crowd count and
uses the existing additive transition/completion registry. `GameRestartSystem` restores
durability, collider and buffers; level reload removes the wave-owned crowd as before.

`BarricadeVisualAuthoring`/baking and `BarricadePresentationSystem` own presentation-only
entities: solid plate/ribs, one then two crack rows, configurable flash and shrinking/scattering
debris. They never change the collision geometry. Impacts use the existing player presentation
bridge and particle pool, without a new HUD. Defaults are 0.85 rebound, 0.18 s flash and 0.65 s
debris. `GauntletProgressionBuilder.BuildBarricade` authors only level 13 and its assets;
rebuilding resets that level's recipe while preserving existing barricade tuning.

See [Barricade validation](../Validation/Barricade.md) for actual checks and remaining playtests.

Campaign level 13's painted foundry gate is authored by `FoundryBarrierVisuals`, also
called by the Chapter Two art recipe (BARRICADE-004/006). Its thin copper skins, blue-steel
returns, teal braces, brass rivets, irregular fracture stages and brief flecks/powder reuse
`VillageGatePieceAuthoring`/baking and `VillageGatePresentationSystem`. The 99 bounded
visual entities add no colliders or simulation responsibility. Authored landing heights
account for the existing gate collider extending below the court. The painted materials
opt into attached pigment and entity tint, with linear material color supplied by
`BarricadeVisualBaker`; other materials retain their existing color path. The baker tracks
material dependencies so palette edits rebake correctly. Gate durability, footprint,
sealed sides, exit cues and completion remain unchanged. See
[Chapter Two verification](../Verification/ChapterTwoFoundry.md).

## Rotating Cover And Gauntlet_14 (COVER-001..005)

Campaign 07 has a chapter-owned village training-machine appearance, rebuilt by
`ChapterOneRotatingTargetVisuals` and the Chapter One recipe. Three-material shutter
meshes retain the 36 existing `CoverPanelAuthoring` bindings and use `CoverGeometry`
for both their saved and runtime pose. Blue planks, brass rails/rivets, direction
accents and ivory end panels make the rotating opening visible without altering its
collider or timing. The four-faced bullseye's segmented wood, caps, paint disks and
damage splits reuse `BarricadeVisual` plus `VillageGatePiece` recoil, fracture and
debris presentation; no new simulation system is introduced. A separate static
turntable base marks the existing walking enclosure and participates in the shadow
bake. Moving shutters and damage pieces remain outside static lightmaps and do not
cast realtime shadows. Other rotating-cover encounters retain their own visuals.

Gauntlet_14, "Return to Sender", is saved after Gauntlet_13 in Bootstrap and Build Settings.
Its 34 x 34 m court surrounds a central barricade target and a 5 m radius rotating cover.
`RotatingTargetSettings.asset` supplies four required hits (covered targets clamp to at least two),
the existing ownership mask, target rebound and damage feedback. `RotatingCoverSettings.asset`
supplies a 75 degree opening, 35 degrees/second default continuous rotation, rotate/pause and
reversal modes, optional pause/acceleration on accepted hits, and a 1.0 reflection multiplier.
Settings are baked: edit assets, let Unity rebake, then reload the level. Wave composition and
placement remain in `CP14_01_Rotating_Cover_Crowd.asset` (14 Baselines, 2 Explosives).

`RotatingCoverBaker` builds one immutable compound arc collider from 36 overlapping boxes.
The ECS root rotates before the existing `BarricadeImpactSystem`. `CoverGeometry` supplies
the identical panel geometry to baking and presentation; panels explicitly request nonuniform
scale transform data. A separate stationary cylinder is query-visible to the existing player
collision bridge. Its manually owned transforms survive baking. `CoverEnclosureContactSystem`
runs between Unity Physics contact creation and Jacobian creation and disables cylinder
contacts only for `Launched` bodies. Walking/recovering bodies and the player remain blocked;
ordinary enemy-enemy contacts and the shield/target colliders are unaffected. Navigation bakes
a conservative square footprint around the enclosure through the existing grid baker, so walking
routes and safe wave placement do not enter it. The low plinth marks the walking exclusion.

`BarricadeImpactSystem` includes cover bounds in its existing broadphase rejection and queries
the current cover pose directly, since the collision world available before physics contains
the previous pose. Launched sweeps ignore the stationary enclosure; nearer enemies and ordinary
solids still block shots. Shield hits queue `CoverReflection` instead of target damage or explosion.
`CoverReflectionSystem` runs after physics, before launch propagation/explosions. It corrects
normal penetration for tunneled impacts, redirects incoming planar speed toward the player
snapshot sampled for that impact step, applies the multiplier and clears homing. It preserves
launch sequence, ownership, damage history and recovery timers. No launch-distance component
was added: the user explicitly selected preserving the existing damping/momentum model.

The original target hit resolver, history, crack/flash/debris presentation and explosive request
pipeline are reused. For a covered target only, blast damage requires an eligible launched
source inside the enclosure; outside blasts cannot damage it, even with a large radius. Target
impact and explosion share source plus launch sequence and count once. Aim-assist rays ignore
cover/enclosure but otherwise retain existing target selection; fallback selection, homing and
the short direction preview still accept the intact target. No preview predicts shield motion.

`Barricade.CompleteOnDestruction` selects immediate completion for this target; Gauntlet_13
still requires its exit. The existing barricade link to wave allocation/pooling supplies bounded
replenishment using the shared boss/elite-era safe placement infrastructure. Target destruction
stops pending allocation/respawns, and completion uses the existing registry/level flow.
Restart clears pending cover reflections and restores angle, rotation timers and observed-hit
state alongside target durability/history; scene reload removes the encounter and owned crowd.

`GauntletProgressionBuilder.BuildRotatingCover` authors only Gauntlet_14 and registers it without
rewriting earlier level content. Rebuilding preserves existing target/cover tuning assets but
reapplies the wave/layout recipe. The plinth is generated from geometry settings by that recipe.
No new player control, enemy archetype, health bar, progression system or launch-range model exists.
Balance, final art and run-duration tuning remain future playtesting work. See
[Rotating cover validation](../Validation/RotatingCover.md) for measured checks and limitations.

## Shell And Gauntlet_15 (SHELL-001..006)

Gauntlet_15, "Crack the Shell", follows Gauntlet_14 in Bootstrap, selection and Build Settings.
The saved 30 x 30 m court contains one central stationary target, a navigation footprint and
four surrounding spawn regions. Initial allocation is 12 Baselines and 2 Explosives, bounded
to 14 roots by the existing wave/pool infrastructure. Earlier level content is preserved.

`ShellTargetAuthoring`/`ShellTargetBaker` add independent shell/core state to a `BarricadeAuthoring`
solid. `ShellTargetSettings.asset` owns three required explosions, five core health (the current
Baseline profile's maximum at creation), and a two-second exploder replacement delay.
Core health is independently editable; later Baseline tuning does not silently overwrite it.
`ShellSolidSettings.asset` reuses rebound, Baseline pool delay, flash and debris tuning. Its
hit count is only the solid's alive/completed flag; shell hits and core damage never pass through
the ordinary barricade hit-count resolver. Wave population and regions live in
`CP15_01_Shell_Crowd.asset`. Settings changes require rebaking/reloading as elsewhere.

The existing pre-physics swept `BarricadeImpactSystem` branches to `ShellHitResolution` for
shell targets. The same nearest-blocker cast, collision geometry, rebound queue and post-physics
penetration correction are reused. Core body damage uses `EnemyCollisionDamage.Calculate`,
with incoming normal speed divided by inverse mass estimating impact impulse against the static
target. `BarricadeHitHistory` deduplicates core impacts by entity and launch sequence. Shell-only
contacts do not spend core impact eligibility. Exploder contact still requests the ordinary
post-physics explosion pipeline. That pipeline resolves every detonation once; shell targets
receive blast damage independently of body-impact history. A single blast resolves either a shell
hit or core damage, so the breaking blast cannot leak through. A subsequent blast in the same
fixed step is a fresh attack and can damage the exposed core.

`PunchDetectionSystem` reuses the solid overlap/cooldown confirmation, dispatching blocked shell
punches or normal core damage. Neither phase has enemy movement, health bars, or the enemy
launch lifecycle. The static collider remains unchanged across exposure. Core death alone marks
the shared solid destroyed and lets `GauntletCompletionSystem` report immediate completion.
Existing persistent aim selection, propagated correction, homing and short initial-direction
preview accept the same solid in both phases. No bounce prediction was added. Navigation bakes
the target footprint through `NavigationArenaBaker`; Mono player collision reads the normal ECS
collision world through its established bridge.

`BarricadeCrowdReplenishmentSystem` retains Baseline replenishment until core death.
`ShellExploderReplenishmentSystem` runs after explosions and before respawn. Once initial wave
allocation finishes, it starts a delay only when no living exploder remains, then releases both
existing pooled slots. Per-member pending flags prevent an already returned slot being issued
again while its partner waits for safe placement. Defeat animation, pooling and safe placement
remain owned by `EnemyRespawnSystem`; those can extend the visible replacement delay. Exposure
cancels pending replacements without changing survivors. No additional crowd entities are allocated.

`ShellPresentationSystem` animates shell plates and core scars through the existing visual baking
data and a `ShellVisual` marker. Shell cracks progress with explosion damage; core tint/scars
progress with health loss. Shell break scatters/shrinks the armor, revealing turquoise core geometry.
Blocked punches flash blue at low impact intensity; successful hits flash gold with the existing
environment impact particles. There are no new UI bars. The opening hint uses `GauntletLevel`.

Campaign level 12 now opts into `ShellFragmentAuthoring`/`ShellFragmentBaker` and
`ShellFragmentMotion` for its painted foundry casing (SHELL-006). The bounded 134 visual
entities include breakaway enamel plates, bolts, progressive fractures, short sparks/powder,
and an eight-section blue-steel core with ceramic sockets and brass flanges.
`ShellFragmentPresentationSystem` owns their cached poses, recoil, ballistic tumble and
short cleanup animation in `GamePresentationGroup`; the legacy `ShellPresentationSystem`
excludes this optional component and still handles other shells. These pieces have no physics
or collision responsibility and read existing shell/core damage only. Restart restores poses.
`FoundryShellVisuals` rebuilds only level 12's visual children and is reused by the chapter art
recipe. Its materials opt into object-space pigment and ECS tint in `PaintedFoundry.shader`;
other painted materials retain their existing projection/color behavior. Camera cutaway stays
disabled on every shell material. The physical root, collider, health and explosion rules are
unchanged. See the level-12 shell section in
[Chapter Two verification](../Verification/ChapterTwoFoundry.md).

`GameRestartSystem` resets shell/core health, replacement timers, flash state and break time alongside
the existing collider/history reset and wave-root cleanup. Scene reload restores authored state.
`GauntletProgressionBuilder.BuildShell` rebuilds only level 15, preserving existing tuning assets
and earlier sequence entries. Balance, final geometry/art/audio and whole-game performance targets
remain future work. See [Shell validation](../Validation/Shell.md) for verification and playtests.

## Knock Into Place And Gauntlet_16 (TRACK-001..005)

Gauntlet_16 follows the shell level in Bootstrap, selection and Build Settings. Its open 32 x 34m
arena contains one orange block on a 10m rail from z=-5 to z=5, side arrows and a blue socket.
Two objective-owned wave sequences reuse the existing bounded pool: 12 Baselines begin after
two seconds and two Explosives join after ten seconds. Both replenish until physical docking.
Previous level content is unchanged. `GauntletProgressionBuilder.BuildTrack` rebuilds only level 16
and its assets; it preserves existing tuning assets and reapplies geometry/wave recipes.

`TrackObjectAuthoring`/`TrackObjectBaker` add track settings, motion state and an infinite-mass
kinematic body to the existing barricade solid. `TrackObjectSettings.asset` owns five net hits,
0.35-second slide duration, launch eligibility and optional push damage. `TrackSolidSettings.asset`
owns the existing rebound, replenishment and impact-flash tuning. The solid's one-hit flag is only
an unfinished/completed marker; impacts never decrement it. The collider remains intact after docking.

`BarricadeImpactSystem` retains its launched-body broadphase and nearest-blocker casts, including
relative sweeps against the current moving track pose. Ordinary rebound and Explosive detonation
are unchanged. `TrackObjectHitResolution` converts signed track direction into a clamped integer
destination. Impact and explosion share the existing source/launch history. `ExplosionResolutionSystem`
dispatches in-range blasts using the vector from blast origin to current object position. The
common cleanup bounds history by source lifetime/relaunch. Direct punches skip the track target
without confirming cooldown by themselves. Existing aim selection, propagated aim correction,
homing and short trajectory preview accept its barricade target data until docking.

After swept impact detection, `TrackObjectMotionSystem` writes kinematic velocity toward a smoothstep
pose. A new hit restarts easing from the actual position toward the updated destination; no hit
waits for an earlier step to finish. Unity Physics integrates the body. In post-physics,
`TrackCharacterPushSystem` resolves swept overlap for non-launched characters by testing free
side positions and then both ends against solid geometry. Ordinary enemy locomotion remains
velocity-owned; these writes are obstacle penetration corrections. Launched bodies retain the
shared rebound path. The authored track has clearance on all sides; arbitrary enclosed custom
tracks are not validated. Optional damage queues existing `DamageRequest` or player health events,
deduplicated by character across a continuous slide. `TrackPushContactSystem` preserves pre-physics
swept contact eligibility so successful solver separation cannot erase optional damage. Mid-slide
retargets preserve the damage history; stationary/cancelled motion cannot deal push damage.

Player corrections pass through `PlayerEcsBridge.ReceiveObstacleDisplacement` to `PlayerController`,
which owns the GameObject transform. The system also refreshes the ECS player snapshot for later
post-physics consumers. Neither player MonoBehaviour queries or stores enemies. `TrackObjectArrivalSystem`
then removes numerical rail drift, stops velocity, locks at the physical endpoint and marks the
shared objective complete. Existing replenishment and completion systems stop supply and report the
win without survivor cleanup. `TrackSocketPresentationSystem` colors socket pieces green on locking.

`TrackNavigationSystem` owns a runtime replacement for the immutable baked navigation grid. It adds
the current block AABB and rebuilds after a quarter-cell displacement or a movement-state change.
Moving footprints include that threshold as padding. Existing navigation detects a changed blob,
cancels stale searches and revalidates paths; safe spawn checks use the same footprint. The system
disposes only its own blobs, restores the original when the object disappears, and handles scene
unload. This intentionally bounded single-object implementation avoids changing global navigation
ownership. The system is Burst compiled; its `CrowdPunch.TrackNavigation` profiler marker includes
rebuild work. A warm Editor check at the 14-root cap measured 0.234ms mean / 0.242ms maximum
over 30 forced footprint rebuilds. Managed fallback before asynchronous Burst compilation measured
about 17ms; these are isolated rebuild timings, not whole-frame or player-build guarantees.

`GameRestartSystem` resets pose, destination, timers, velocity and push history alongside shared
hit history and crowd teardown. Scene reload restores the baked state. The three unresolved
eligibility/damage edge cases are exposed as provisional options, with defaults and alternatives
documented in `OpenQuestions.md`; no wider design question is resolved by these choices.
See [Track validation](../Validation/TrackObject.md) for evidence, limitations and playtests.

## Progressive campaign track routes (2026-10-11)

TRACK-006 updates the eight campaign track levels, leaving the legacy straight gauntlet
and level 16 introduction intact. Levels 19/26/32/39/46/54/69 now have 20/30/40/50/60/70/80m
routes made from 10m legs with alternating 90-degree turns. The 2m hit step gives 10-40 net
forward hits. Floors, enclosing walls, bounds, entry aprons, navigation participation anchors,
rail/arrows/socket visuals, crowd banks and hazard positions are authored to clear the full
route. Existing support populations, replacement policies and hazard timing are retained.
`CampaignTrackRouteBuilder` applies the upgrade in place and chapter authoring invokes the
same route recipe; asset and scene identities remain stable.

`TrackObjectAuthoring.corners` is an optional ordered Transform array. `TrackObjectBaker`
validates horizontal, finite segments and stores positions plus cumulative arc distance in
an immutable `TrackPathPoint` buffer. Empty corners bake a two-point straight path.
`TrackObject.Length` is total route distance; its original Direction is the straight fallback
and first-segment direction. `TrackPathGeometry` resolves positions, local forward/reverse
hit directions and the next corner. At a vertex, forward impacts use the outgoing tangent;
reverse impacts use the incoming tangent. Eligibility and per-launch deduplication are unchanged.

The motion, arrival, relative launched-body sweep, pre-physics push-contact and post-physics
character-displacement systems all use the same route positions. Motion caps each fixed-step
distance at the next vertex before continuing, so physics and sweeps traverse one leg at a
time. Even when easing time expires with queued hits, arrival keeps moving until the target
arc distance is physically reached; an intermediate corner cannot lock or complete the level.
Block rotation remains fixed, preserving the existing box-collision and navigation-footprint
semantics. Retry resets arc distance/target to zero against the immutable baked route. Shared
navigation ownership, hybrid bridges and system ordering are unchanged. See
[progressive track verification](../Verification/ProgressiveTracks.md).

## Progressive barricade rooms (2026-10-11)

BARRICADE-007 extends campaign levels 22/34/42/56/62/74 to 2/3/4/5/6/7 room-and-gate
segments. Level 13 retains one original gate. Each repeated segment keeps its original
40/44/44/42/44/46m length and width. BARRICADE-009 now varies earlier sectors while
preserving each final sector's original rocks, hazards and gate difficulty.
A continuous convex floor and enclosing perimeter span each complete chain; side terrain
seals every 2m gate behind its exposed front. Level 62 keeps x=8 northeast openings.
There is one final exit and every gate must break before reaching it can complete the level.

`CampaignBarricadeRoomBuilder` upgrades saved scenes in place and is also used by chapter
recipes. Each room owns one `EnemyWaveSequenceAuthoring` pointing to its gate and a separate
wave asset with translated spawn rectangles. The campaign now has 176 wave assets; scene
IDs and all 81 build-scene entries are stable. Only the first owner retains the shared
GroundHazardPolicyAuthoring; hazard copies reference their own room sequence. Campaign-owned
navigation settings extend bounded path capacity for the longer halls. Odd spacing-grid
width aligns cells with integer-x gate centres so normal enemy regions stay connected
through the 2m openings. Shared enemy settings and navigation budgets are unchanged.

`BarricadeRoomAuthoring`/`BarricadeRoomBaker` add pure ECS `BarricadeRoom` bounds, predecessor
entity and activation byte to the crowd owner. `BarricadeRoomActivationSystem` runs after
PlayerBridgeSystem and before EnemyWaveSpawnSystem, activating on player entry only after
the preceding gate breaks. EnemyWaveSpawnSystem skips dormant owners entirely, so their
spawn clocks start on entry. Existing wave ownership, bounded slot reuse and safe room-local
respawn placement remain in their original systems. Breaking a gate disables only its
members' replenishment; survivors remain hostile inside their original spawn room.

BARRICADE-008 uses `EnemyWaveOwnership.Sequence` to read that owner's `BarricadeRoom`.
`BarricadeRoomMovement` derives an inset movement rectangle from the activation bounds.
EnemyChaseSystem uses it for reserve distribution and surround/contact targets, drops
off-room pursuit commitments, and excludes off-room enemies from pressure-slot and
closest-exploder selection. Invalid cached global wander targets are reassigned locally.
EnemyNavigationSystem and GroundHazardNavigationSystem resolve blocked destinations
inside the same rectangle. Their bounded A* requests reject detours into another room
when starting inside; displaced bodies may route back in. EnemyMovementSystem brakes
normal steering at its edge and removes outward active velocity without writing positions.
Launched/recovering physics is unchanged, as is global movement for owners without rooms.
See [spawn-room movement verification](../Verification/BarricadeRoomMovement.md) for
the behavioral tests, baked-world observations and crowd profiling.

`CampaignBarricadeSectorBuilder` edits the six saved chains in place and is called by the
room/chapter recipes. Earlier sectors use ascending small side-pocket/offset-island
footprints, rising bounded populations and reduced periodic hazards. Final geometry,
hazards, spawn banks and gate tuning are retained; final mixes add one Ranged, Dasher and
Armored to two Exploders plus replenishing Baselines within the original population cap.
Reapplying room authoring to an existing chain updates its variants instead of cloning
the easier first sector over the final template. Waves retain their asset GUIDs and
room-local ownership. The ordinary armor safeguard is disabled for these replenishing
encounters because existing bounded Baseline slots already provide renewable ammunition.
Ranged positioning preserves local coverage when the player leaves and clamps in-room
distance-band targets. Ranged attacks cancel off-room wind-up. Dasher decisions preserve
coverage/reentry outside their room and voluntary dash velocity stops before the inset
room boundary. These guards do not restrict launched-body physics or other encounter types.
See [sector verification](../Verification/BarricadeSectors.md) for exact populations,
final-layout preservation checks, live progression and profiling.

GauntletCompletionSystem aggregates all barricade conditions rather than taking whichever
wall is visited last. Intermediate gates complete their local destruction condition; only
the final one requires the final exit. BarricadePresentationSystem stores reported hit
sequence per gate in BarricadeFeedbackState, preventing repeated feedback when several gates
are present. GameRestartSystem resets these feedback states and room activation alongside
all gates, spawn queues and hit histories. MonoBehaviours gain no enemy queries. See
[room-chain verification](../Verification/BarricadeRooms.md).

## Protected Point And Gauntlet_18 (PROTECT-001..004)

Implemented: Gauntlet_18, "Hold the Line", follows the Wizard level in Bootstrap, level
selection and Build Settings. The 32 x 96m rectangular floor has a noncolliding turquoise
8 x 4m ground zone at z=-46. Spawn rectangles occupy the opposite end, z=37..45.
`ProtectedPointAuthoring` and its Baker attach the objective and selection buffer to the
existing wave-sequence entity. `ProtectedPointSettings.asset` exposes threshold (one) and
new-attack selection cap (three). Zone coordinates/size are authored in the SubScene.
The three CP18 wave assets own exact composition, batches and cadence; puzzle ammunition
supply and persistent-hazard gating are disabled. The first wave has a three-second entry delay.

`ProtectedPointPrioritySystem` runs after player bridging and wave spawning, before chase
and Wizard casting. It maintains the closest eligible in-range roots in a small sorted
`ProtectedPointAttacker` buffer, filtered by wave owner and run generation. Distances use XZ;
ties use entity index. Melee range is the larger of contact-attempt range and physical contact
reach; Ranged/Wizard use engagement range and Dashers use their preparation band. Staggered
Armored enemies cannot take slots. Selection is O(enemies * configured cap), with no all-pairs query.

`EnemyChaseSystem` reuses separation and writes the zone destination for unselected enemies.
Selected melee retains normal contact cadence; committed Baseline attempts finish. Ranged
and Wizard positioning holds selected attackers in place and otherwise preserves zone intent;
their attack systems gate new wind-ups/casts. Dasher positioning keeps zone intent until
selected, after which existing preparation, dash and recovery own movement. Existing navigation
and physics velocity steering are reused. Facing follows movement while advancing. Contact
damage and Explosive player-contact detonation obey selection; launched impacts and area
effects retain ordinary resolution. Committed attacks may finish after losing selection.

Last in post-physics, after recovery, bounds and lifetime handling, `ProtectedPointBreachSystem` checks Active root centres
against the inclusive XZ rectangle. Launched and recovering bodies can be moved back out
before becoming Active. A breach decrements shared undefeated accounting, increments the
current wave's resolved count, and ECB-destroys the root and linked visuals immediately.
Moving Wizard zones are removed with their owner; detached zones keep their existing lifetime.
No death explosion or replacement is generated. After removal, the managed system requests
the existing one-step pre-physics rebuild guard because destroyed Dashers can own unique
colliders still referenced by the preceding collision world. Spawn progression stops at the breach threshold.

`GauntletCompletionSystem` checks failure before completion and reports it once through
`GauntletFailureRegistry`. `GauntletSequence.RunFailed` drives the existing pause menu's
"PROTECTED ZONE BREACHED" / "Retry Level" result. MonoBehaviours never inspect enemy entities.
Scene retry restores authored state; `GameRestartSystem` also resets breach/selection state
alongside its existing wave teardown. Level transitions consume stale failure signals.

**Crowd Punch > Levels > Build Protected Point Gauntlet 18** rebuilds only this level and
registers it, preserving the dedicated threshold/cap settings while reapplying the layout and
wave recipe. Earlier scenes and shared archetype tuning are preserved. Difficulty, final art,
full-run duration and camera suitability for this longer court remain future validation work.
See [Protected point validation](../Validation/ProtectedPoint.md) for measured checks and playtests.

## Wizard And Gauntlet_17 (WIZARD-001..007)

Wizard is an explicit sixth standard `EnemyArchetypeKind`. `WizardEnemySpawnSettings`
bakes `WizardSettings` through the shared spawn profile, and `EnemySpawnInitialization`
adds its dedicated `WizardCastState`. Normal contact damage excludes Wizards. Punches,
aim assist, preview, health, launch, recovery and pooling use the existing enemy systems.
Wizard movement tuning overrides its movement components; shared Ranged tuning is not used.

`WizardCastSystem` owns cooldown, probability checks, telegraph and active duration. It
supports `WizardSettings.CastWheneverInRange`, default false, to bypass probability and
check interval after cooldown while retaining player availability and engagement range gates. It
creates a first-class `WizardZone` entity with an ECB-remapped reference in the cast state.
`WizardPositioningSystem` owns range-band decisions and optional casting stops. It writes
intent only. `WizardHazardAvoidanceSystem` adds separation from other Wizards' reserved
radii and independent zones before navigation; stopped casters and committed Dashers
retain their intentional movement ownership. Existing navigation and physics steering
remain responsible for movement. `EnemyFacingSystem` applies Wizard turn tuning while
facing the player; launched facing still follows velocity.

A zone stores its source, baked settings, explicit Cast/Impact kind, position, expiry, follow/active flags and scene/
wave ownership. Its `WizardZoneTarget` buffer stores independent entry/tick clocks and
player protection per target. `WizardSettings.CastRadius` and `ImpactRadius` are separate
Inspector fields. `WizardZone.Radius` selects the radius by kind for damage membership,
ground visuals and avoidance. Casting probability and caster reservations use Cast Radius.
Membership is refreshed every fixed step, including between
damage ticks; leaving removes the record. Physics broadphase candidates are expanded for
body travel since broadphase construction, then filtered by exact XZ radius plus the
target's physical radius. Cast zones check the player only. Impact zones exclude the source,
armor stages, bosses and non-enemy puzzle objects from enemy effects. Impact-zone force writes post-physics velocity; strong force uses the shared launch
transition only when starting a new normal-enemy flight. A dashing Dasher receives damage
without force, and an elite receives force without launching.

`EnemyDamageResolution` is the shared health/request resolver used by pre-physics
`DamageApplicationSystem` and same-step zone ticks before recovery. It preserves deferred
defeat, armor protection and one-shot death requests. No explosive detonation request is
created by zone damage. `WizardPlayerHit` is a singleton buffer written by the Burst zone
system and drained by managed `WizardPlayerHitSystem`. `PlayerEcsBridge.WizardHitReceived`
delivers damage and impulse to `PlayerHealth`, bypassing global invulnerability while
retaining the existing accepted-damage/knockback path. MonoBehaviours never hold enemies.

`EnemyLaunchState.ContinuousFlight` advances on a genuine entry into `Launched`.
`LaunchSequence` still advances for re-punches as before. `WizardImpactZoneSystem` consumes
one special impact per launched Wizard continuous flight. It also creates the same zone
when a launched enemy strikes an active/recovering Wizard; `WizardIncomingImpactHistory`
allows one zone per incoming source flight per Wizard and resets on Wizard pool/restart.
An incoming enemy preserves its monotonic `ContinuousFlight` number across pooling, so
the next genuine launch of the same entity remains distinguishable from the earlier one.
Moving zones are cancelled by launch or defeat; fixed zones keep their own timer through
source death, pooling, recovery and recasting. Scene/run ownership and `GameRestartSystem`
remove old zones on unload/reset. Respawn and restart reset cast state.

`EnemyWizard.prefab` uses Blob/Wizard.fbx and the established sampled ECS skinning path.
`EnemyAnimationProfile.Wizard` selects Idle, Walk, looping Dance for both cast phases,
the established launched pose and Death. Root motion is disabled. Rebuild its controller,
samples, materials, prefab and settings reference with **Crowd Punch > Enemies > Rebuild
Wizard Prefab**. `WizardZonePresentationSystem` draws a shared procedural disc using
`Resources/WizardZone.mat` and the additive URP `WizardZone.shader`; the full radius and
outer ring pulse during telegraph and brighten while active. The Wizard's body remains
violet through shared readability data. No extra HUD element is introduced.

Wave assets expose optional Wizard Baseline supply, supply delay, persistent-zone waiting
and an alive-Wizard cap (zero means unlimited). These features default off for existing
waves. Supply reuses existing safe placement, ownership and wave-count accounting and
cancels a pending replacement when the last living Wizard dies. Weighted selection
rerolls eligible non-Wizard profiles at the cap and reserves capacity for outstanding
guaranteed Wizard allocations; invalid minimum/cap combinations are rejected in baking.

**Crowd Punch > Levels > Build Wizard Gauntlet 17** builds Gauntlet_17, its matching SubScene,
two wave assets and progression references. It introduces 6 Baselines + 1 Wizard, then
12 Baselines + 2 Wizards in an open clipped court. Both waves enable delayed Baseline
supply and wait for defeated enemies plus expired zones; Wizards themselves are finite.
See [Wizard validation](../Validation/Wizard.md) for checks and remaining playtests.

## Trail Enemy And Gauntlet_19 (TRAIL-001..007)

Trail appends archetype value 7, stays Normal tier, and uses Baseline health. Its spawn profile
references `Data/Settings/Enemies/TrailEnemySettings.asset`, a dedicated `TrailEnemySettings`
ScriptableObject. Shared profile baking explicitly depends on that asset; spawning adds
`TrailSettings` and `TrailEmitter`. Per-archetype separation overrides support Trail. Contact
damage excludes it. The health-bar presentation bridge never publishes a Trail health bar.
Ordinary punch, preview, homing, collision propagation and recovery paths are reused.

`TrailCirclingSystem` runs after chase and elite support, before hazard avoidance/navigation.
It writes a short tangential travel goal plus radial correction, preserves crowd separation,
reverses periodically and respects an elite's projectile reservation. Navigation owns obstacle
handling and `EnemyMovementSystem` owns velocity steering. No normal enemy transform movement
or separate physics motor is introduced.

Post-physics `TrailExpirySystem` removes expired or unloaded encounter data. `TrailEmissionSystem`
runs after ground reconciliation/explosions and before recovery. It samples actual horizontal
travel in Active/Launched only, emits capsule sections using an ECB, and projects endpoints onto
upward-facing static surfaces below the body. Section spacing is bounded by width to keep paths
continuous. Stationary/recovering/defeated states break the emission anchor. Each section snapshots
width, damage, timing, color, immunity/avoidance, launch ownership and chain depth.

All baked enemies have `EnemyLifetime`, incremented at pooling and restart. A detached `TrailSource`
stores source entity + lifetime, scene/sequence/run/wave ownership, latest expiry and a
`TrailDamageTarget` buffer. Sections share this record across normal/launch transitions. Source
death, pooling and reuse never remove or mutate old sections. The record outlives its enemy and
expires when its last section does. `TrailWaveCleanupSystem` runs after defeat counting and removes
sections and source clocks immediately once a fully spawned finite encounter has no undefeated enemies.
It does not mistake empty spawn-batch gaps for wave clearance or interrupt objective-owned replenishing crowds.
Scene/run invalidation and `GameRestartSystem` remove both
records and sections, plus pending player hits.

`TrailDamageSystem` runs after emission and Wizard-zone damage, before recovery. It snapshots eligible
enemies' current post-physics positions, radius, lifetime and armor state into a temporary 4m spatial
grid; exact capsule overlap uses XZ and target radius. Each target enters one grid cell, so broadphase
candidate enumeration cannot duplicate a target. This avoids repeated EntityManager lookups per section
and does not depend on pre-integration physics broadphase positions.
Armor stages/protection, defeated/pooled enemies, bosses and non-enemy objects are excluded. One
source/target-lifetime timer survives exit/reentry and prevents overlapping or adjacent sections
from multiplying damage. Player uses a separate entry in that same source buffer with interval
at least the trail-specific protection duration; independent source records stack. Damage uses
the shared resolver with no velocity writes, launch transition or explosive request. Earlier
pending damage resolves separately before trail damage so it cannot acquire trail credit.

`EnemyDamageState` records last damage and lethal damage source lifetime, ownership and chain
depth. Lethal trail credit survives deferred defeat and later source reuse without altering the
target's launch ownership. The repository has no numeric score/reward economy; these fields retain
player kill/chain attribution for the existing/future consumers without inventing progression.
`TrailPlayerHitSystem` drains an ECS hit buffer into `PlayerEcsBridge.TrailDamageReceived`.
`PlayerHealth` accepts damage independently from ordinary invulnerability, with zero impulse.

`TrailAvoidanceSystem` runs after other hazard decisions and before navigation. A Burst parallel
job filters damage eligibility, cheaply rejects distant section bounds, and adds the strongest
local capsule repulsion to movement/navigation separation. It ignores launched bodies, committed
Dashers and explicitly anchored/stopped behavior. The None mode leaves intent alone. It never
makes trails solid or changes launched-body collision physics.

`EnemyTrailPrefabBuilder` samples Fish Idle/Walk/Death into the established CPA3 skinning path,
using the common Baseline animation state mapping and a stable green body tint. Root motion is
disabled and the physical capsule provides collision contacts. `TrailPresentationSystem` batches
all ground capsules into one reusable mesh/draw, with green normal and orange launched colors.
Each retains full width while alpha fades through the last 35% of its lifetime.

**Crowd Punch > Levels > Build Trail Gauntlet 19** authors only Slippery Circuit: an open 32 x 34m
clipped court, 6 Baselines + 1 Trail then 12 Baselines + 2 Trails. Waves use guaranteed counts,
no ammunition supply, and no Trail expiry gate. Trail cleanup on defeat lets wave advancement and final
completion proceed without waiting; the optional Wizard-zone hazard gate remains unchanged.
Bootstrap/build/selector append 19 after Hold the Line.
Starting tuning is 1.2m/5s/4 damage normal, 2m/6s/8 damage launched, .75s ticks, .35s player
protection, 8m circling distance and 4s reversals. See [Trail validation](../Validation/Trail.md).

## Dino Pillars And Gauntlet_22 (PILLAR-001..006)

`DinoBossAuthoring` and `FallingPillarAuthoring` bake the boss and exactly three scene-owned
pillars. `DinoBossSettings.asset` is the dedicated tuning source; `DinoTuning` and
`PillarTuning` contain only ECS data and immutable collider blobs. The boss owns `DinoBoss`,
`Health` measured in required pillar hits, dynamic Unity Physics capsule/mass/velocity and
no ordinary enemy launch components. Ordinary punch/explosion/body-damage paths therefore
cannot launch or damage it. `PillarDamageResolution` alone changes boss health and stagger.

`DinoCycleSystem` advances chase/warning/burst/stagger before physics. `DinoMotionSystem`
turns toward the player, uses capsule sweeps and surface tangents around static obstacles,
and steers horizontal velocity with bounded acceleration. Burst turning is slower;
warning retains normal chase. `DinoContactSystem` locks only the ground axis after physics,
publishes cooldown-limited player contacts through the existing bridge, and gives ordinary
crowd bodies outward velocity without damage or launch-state changes.

`PillarToppleSystem` runs after launch homing and grounding, sweeping Player-owned launched
enemy colliders before the physics build. A nearer blocking contact remains authoritative.
Toppling locks direction at impact, clears per-fall histories, reserves the triggering
body's pass-through contact so launch mode cannot steal its launch, and swaps the solid box for
an immutable zero-filter box before the solver. It never changes the triggering body.
`PillarFallMotionSystem` rotates the scripted pillar around its base with an accelerating
fall. `PillarFallContactSystem` runs after physics and before recovery/replenishment. It
collects enemy contacts in a Burst job by subdividing the rotating box arc and target
motion with conservative capsule padding,
records each enemy once per fall and separate player/boss flags, and resolves shared enemy
damage/deferred defeat. The alternate launch response starts explicit `EnvironmentImpact`
launches owned by `Environment`; normal propagation inherits this ownership. These launches
remain dangerous to the player and fail the Player-only pillar trigger.

`PillarRegenerationSystem` hides fallen geometry after its brief linger. Successful pillars
stay consumed; misses wait for their delay and player/boss clearance. Ordinary occupants
receive outward velocity; collision is restored only after they actually clear the space.
This avoids teleporting physics-driven enemies into an upright obstacle.

Upright pillars join aim-assist candidates and launch homing. Falling/waiting/consumed
pillars and immune Dino are ineligible. `DinoAnimationSystem` uses generated Dino Walk,
Run, HitReact and Death samples through the established GPU skinning path. Presentation
colors distinguish warning, burst, stagger, impact and regeneration; no ground marker or
additional HUD is created. `EnemyHealthBarBridgeSystem` reuses the existing boss bar.

`BossCrowdSequence`, spawning and replenishment now recognize Dino as an owner. The
Gauntlet_22 wave asset supplies eight Baselines with a three-second respawn delay.
`DinoTuning.EnemiesPerPillar` and `PillarCrowdRadius` additionally reserve two local
Baseline bodies per unconsumed pillar, default radius 4m. `PillarCrowdSupplySystem` uses the
wave's baked Baseline profile and normal ECB spawn initialization; each fixed slot owns
one `PillarCrowdMember`, `BossCrowdMember` and `EnemyWaveOwnership`. It counts pooled or
launched occupants too, so firing bodies cannot create unlimited replacements.
`PillarCrowdPositioningSystem` runs after ordinary chase intent and before navigation,
preserves ordinary crowd goals, speeds, contact cadence and separation when they fit within
the pillar radius. Outside goals are clipped to that circle; displaced Active bodies return
to its nearest inner edge. Small arc waypoints avoid crossing through the upright shaft.
Local bodies participate in the ordinary crowd's chase-pressure allocation. The shared
`EnemyMovementSystem` removes outward radial motor velocity at the local boundary, keeping
tangential and inward movement. Launched/Recovering motion is untouched; consumed pillars
release the constraint along with the local replenishment policy.
`BossCrowdPlacement` safely respawns these bodies in their pillar sector instead of the
general wave regions, with the wave's existing respawn delay. Space blocked by the player,
boss or crowd retries later. Consumed pillars release survivors to normal AI and disable
their respawn. Existing wave-owned reset and unloading remove every local body; resetting
the sequence recreates exactly the configured slot count.
`GauntletCompletionSystem` uses Dino defeat as authoritative regardless of crowd survivors.
`DinoEncounterReset` restores health, timers, velocity, all pillar colliders/transforms,
hit histories and sampled playback; additive unloading removes their SubScene entities.
The authored sequence and Build Settings append Dino Pillars after Rolling Blob.
Rebuild via **Crowd Punch > Levels > Build Dino Pillars Gauntlet 22**; existing boss/wave
settings are preserved. See [Dino pillar validation](../Validation/DinoPillars.md).

## Fixed Ground Hazards And Gauntlet_23 (GROUND-001..007)

`GroundHazardAuthoring` bakes XZ circle or yaw-oriented rectangle footprints,
per-patch damage/tick tuning, periodic durations/offset and first-wave introduction.
Dimensions are explicit world metres; transform scale and height do not resize damage.
`GroundHazardPolicyAuthoring` adds scene policy to the wave sequence from the dedicated
`Data/Settings/GroundHazardSettings.asset`: Active-only avoidance and Wait Safely by default.
Warning-and-Active and Cross As Last Resort remain exposed alternatives.

Pre-physics `GroundHazardCycleSystem` runs before wave spawning, resetting patch epochs
when sequence wave index or run generation changes. Epochs begin at the wave's pre-spawn
delay. Later-wave patches are introduced then; introduced inactive patches stay visible.
Spawn capture evaluates authored offsets when initialization/reset/advancement precedes
the cycle system, preventing a one-update unsafe placement gap. Initial random, wave,
shared spawn creation, soft restart and pooled respawn paths reject warning/active
footprints independently of avoidance policy. Failed placement retains existing retries.

`GroundHazardNavigationSystem` runs after terrain navigation and before both enemy motors.
It retains the immutable terrain grid and adds a dynamic edge mask per clearance class,
rebuilt by a Burst parallel job only when hazardous membership changes. It does not
rebuild solid connectivity, create collision geometry or alter physics velocity. A Burst
`IJobEntity` writes voluntary `DesiredMovement`, using a bounded FIFO and separate
`GroundHazardRoute`/`GroundHazardWaypoint` data. Shared A* accepts optional edge masks;
terrain callers keep previous behavior. Hazard searches use the existing navigation
asset's expansion, slot, queue and path limits, with a separate budget from terrain.
Radius-inclusive sweeps validate edges, shortcuts and braking probes. Activation invalidates
cached routes/searches. Caught Active bodies sample 32 analytical footprint exits,
preserving static clearance and other patches. Armor does not exempt avoidance. Launch,
recovery, committed lunges/Dashers, stationary casters and reserved elite projectiles
preserve existing movement ownership.

Wait Safely brakes while routes are pending/unavailable. Cross As Last Resort keeps
terrain's original movement only after an exhaustive unreachable result. Expansion/path
limits and invalid anchors remain safe waits. Native scratch survives inactive cycles
and is disposed on arena unload/world teardown. Pooling/restart invalidate versions and
buffers; scene-owned roots disappear on unload.

Post-physics `GroundHazardDamageSystem` runs after Trail damage and before recovery. Each
baked enemy owns one `GroundHazardDamageClock`, shared across all patches and keyed by
`EnemyLifetime`; exit, overlap and activation never clear it. Simultaneous overlaps select
the highest-damage patch, with the longer interval breaking ties. Shared damage resolution
preserves armor, pending-damage attribution and launched deferred defeat, without impulse
or explosive requests. Current Player-owned launched victims receive Player lethal credit;
other hazard damage uses Environment ownership. Existing launch ownership stays intact.

One player clock and `GroundHazardPlayerHit` singleton buffer feed the dedicated
`GroundHazardPlayerHitSystem`/`PlayerEcsBridge.GroundHazardDamageReceived` event.
`PlayerHealth` accepts damage through the existing damage-only path, bypassing ordinary
invulnerability and retaining dash/movement. MonoBehaviours do not query enemies.
Restart clears pending hits/clocks and patch epochs. `GroundHazardPresentationSystem`
draws matching footprints with a shared procedural mesh/material: slate inactive,
full-area pulsing amber warning and bright red active. No additional HUD is created.

`GroundHazardAppearanceAuthoring` and its baker optionally add the data-only
`GroundHazardAppearance` component. The six Chapter Three patches opt into Quarry
through `ChapterThreeQuarryHazards`; the chapter objective recipe applies it on
construction too. The ground and particle presentation systems read this component. The ground
property block selects metre-scaled basalt cells, cold/amber/heated palettes and
the existing phase pulse in the shared hazard shader. The original circle/rectangle
clipping and draw transform remain authoritative. Untagged hazards retain the
original presentation; cycle, damage, navigation and player-bridge systems are unchanged.

`GroundHazardParticlePresentationSystem` batches camera-facing cartoon puffs, two-tone
flame tongues and embers into one dynamic mesh using the shared vertex-colour effect
shader. `GroundHazardParticleGeometry` derives bounded emission lanes from the same
phase/wave clock: two faint cooling wisps while inactive, denser smoke during warning,
and flames/embers only while active. No particle entities, enemy queries, gameplay
state or MonoBehaviour bridge are added. Flames clear immediately in safe states;
all particle geometry clears for unintroduced patches and unload. The owned material draws after the footprint without
casting shadows. Footprint clipping, damage, navigation and baked lighting are unchanged.

**Crowd Punch > Levels > Build Ground Hazards Gauntlet 23** authors Hot Footing after Dino
Pillars: a nature-kit 32 x 34m clipped court, two permanent patches and two periodic patches
introduced in wave two. Editable waves contain 12 and 20 finite Baselines, with no supply
or persistent-hazard completion gate. Broad side corridors and safe spawn ranges remain.
Initial tuning is 12 damage every .75s; periodic timing is 3s inactive / 1.5s warning /
2.5s active, with 0s/2s offsets. See [Ground hazard validation](../Validation/GroundHazards.md).

## Legacy Environment Examples (Gauntlet_28-31)

`EnvironmentExamplesBuilder` creates four development-only art examples after legacy 27:
Sunlit Village Gate (barricade), Emberworks Foundry (rail), Overgrown Observatory
(rotating cover), and Frostbound Vault (shell). The sources are legacy 13/16/14/15.
Each example has its own main scene, ECS SubScene, copied wave assets and project-owned
URP materials under `Data/EnvironmentExamples`. Bootstrap's legacy array contains the
new entries; campaign catalogs, save identifiers and player build registration are unchanged.

`EnvironmentExampleScenery` assembles imported KayKit, Quaternius and Kenney models.
Static non-colliding scenery, paving and two bounded ambient particle systems live in
each main scene and unload with it. Existing floor/obstacle/objective collision and
spawning stay in the SubScene. Timber facing uses the existing barricade presentation
components, and the foundry machine is an ECS child of the existing moving objective.
The imported FBX root orientations are preserved and source assets are not modified.

The create command refuses to overwrite existing examples. `Refresh Example Scenery
28-31` replaces their generated scenery roots and reapplies the example layout/facing
treatment; manual edits inside those generated roots should be made after that command.
These are OQ-015 experiments, not an accepted change to campaign art direction or to
BARRICADE-001..005, TRACK-001..005, COVER-001..005 or SHELL-001..006 behavior.

The environment finish adds a 600 m visual landscape, surrounding settlements/forest/
industry/fortress silhouettes, service paths and ground dressing to examples 28-31.
`EnvironmentExampleFinish`, `EnvironmentExampleLandscape`, `EnvironmentExampleGroundDetail`
and `EnvironmentExampleRendering` extend the editor recipe. The old flat presentation
backdrop is inactive in these four SubScenes; gameplay floor and physics are unchanged.

`ExampleEnvironmentLighting` owns presentation settings during an example's additive
scene lifetime. Bootstrap remains the active scene, so this component explicitly applies
the sky, trilight ambient fill and distance fog, enables camera HDR/post-processing/SMAA,
and temporarily selects a project-owned URP asset with soft cascaded shadows and
half-resolution SSAO. It restores the prior camera, atmosphere and quality-pipeline
settings on unload. Each example has one global Volume at priority 100 with a separate
ACES/bloom/colour/vignette profile. No depth of field or motion blur is used. All new
scenery is non-colliding, main-scene presentation; no enemy entity access is introduced.
Imported assets, the default renderer/pipeline and campaign scene assets are unchanged.

## Art Lab (Editor only)

`ArtLabWindow` compares five presets in legacy 28-31 using the existing IMGUI editor
tool pattern. `ArtLabSession` coordinates temporary main-scene/player material copies,
a priority-200 Volume, chapter-relative lighting and camera/freeze controls. It applies
after `ExampleEnvironmentLighting`, whose scene-lifetime ownership remains unchanged.
Original references/settings are restored when preview ends or the example unloads.

`ArtLabPreview` is the editor-only presentation bridge. `ArtLabMaterialSystem` runs in
GamePresentationGroup and replaces RenderMeshArray material references through an ECB,
preserving meshes, material/mesh ranges, deformation data and per-entity colour properties.
Prefab and disabled entities are included so later spawns inherit the preview. The managed
SystemBase is intentional for Unity material/array caching; no Unity object references
are added to ECS components and no MonoBehaviour queries enemies. A throttled scan catches
streamed arrays; style changes force an immediate refresh. Simulation systems are unchanged.

Generated URP shader variants preserve the installed EnemySkinning/Sidekick vertex and
surface logic, replacing only forward lighting. All preview bridge/system code is guarded
by UNITY_EDITOR; presets, window and authoring live in Editor folders. See
`Docs/Verification/ArtLab.md` for controls and limits.

## Production Full Cel Presentation (2026-10-16)

VISION-006 resolves OQ-015. Game-owned surface materials now reference fixed
`CrowdPunch/Cel/Environment`, `CrowdPunch/Cel/Enemy`, or `CrowdPunch/Cel/Player`
shaders. Their generated passes preserve the original URP material properties,
EnemySkinning compute deformation and Sidekick vertex logic. The shared lighting
include fixes production shading to Full Cel at compile time; Art Lab globals cannot
change a production material. No material replacement system is required in builds.

`Data/Presentation/FullCelPost.asset` is the runtime VolumeProfile copied from the selected
preset: Neutral tonemapping, contrast 5, saturation 4, exposure .25, bloom .06 with
threshold 1.1 and scatter .65, and vignette .12. Campaign main scenes carry a global
Volume at priority 150; Bootstrap provides the same profile at priority 0. Chapter
white balance remains inherited. The camera enables HDR, post-processing and SMAA.
`CelRendering.asset`/`CelRenderer.asset` promote the example rendering configuration to
all quality levels, including its contact shading and shadow settings.

The production pipeline disables soft shadows and uses a single 8192 directional shadow
map across 110 m, avoiding cascade resolution changes and improving near-object edge density.
Dash FOV feedback defaults to zero in both `CombatFeedbackSettings` and its Resources asset:
URP refits the directional shadow projection when FOV changes, making hard edges crawl.
Dash displacement, camera follow, impact kick and shake retain their existing behavior.
`ChapterOneVillageRoofShadows` authors a shared two-plane, shadows-only roof mesh for
the village's tiled gable roofs. The detailed visible tiles no longer cast a serrated
outline. Terracotta roof materials opt out of receiving cast shadows so the proxy cannot
self-shadow the tiles; directional cel lighting and contact shading still define their
form. These proxies have no colliders or LOD switching and are retained by the village recipe.
`CelShadowDetailAuthoring` applies hard-only quality settings, authors LOD0 overrides on
MeshRenderer and SkinnedMeshRenderer components in game prefabs/scenes, and flattens any
LODGroups to their highest-detail renderers with no distance culling. Source model imports
are preserved. Unity clamps its global mesh LOD threshold to .01, so the explicit renderer
overrides provide the fixed-detail policy. All six quality tiers retain the production
pipeline and include LOD0 in builds. No runtime renderer scan or ECS ownership change is used.
The explicit repair command is `Crowd Punch > Art Direction > Apply Hard Shadows and Full Detail`;
the Full Cel migration and Chapter One authoring recipes also preserve these settings.

`CelLevelLighting` scopes each campaign/legacy main scene's saved ambient palette and sun
to its additive lifetime. It applies the selected 1.08 ambient multiplier and restores the
previous environment on unload. Existing polished examples retain `ExampleEnvironmentLighting`
as their sole atmosphere owner, with the same multiplier and production pipeline reference.
Neither component queries ECS or changes simulation ownership.

`CelPresentationAuthoring` migrates game-owned material assets in place, creates owned
copies of referenced imported/package materials, remaps renderers in game prefabs and
scenes, and wires the camera and presentation volumes. Model sources, particle shaders,
hazard shaders, gameplay tuning and collisions are preserved. The explicit migration is
repeatable; it does not silently run on imports. Art Lab remains an optional Editor tool;
its Current setting now means the authored Full Cel baseline.

## Chapter One Village Environments

Campaign 01-10 now use the sunlit border village setting (VISION-004/005/006).
`ChapterOneVillageBuilder` dresses the existing saved scenes without rebuilding encounters.
`ChapterOneVillageAssets` owns model fitting and copied cel materials;
`ChapterOneVillageLandscape` authors bounds-aware meadow meshes;
`ChapterOneVillageScenery` composes village houses, orchards, markets, a mill, gardens,
watchtowers and the Gatekeeper keep. The newly imported Kenney Castle Kit supplies
the fortified landmarks alongside the existing village, nature, dungeon and prop kits.

Scenery is static, non-colliding main-scene presentation. Collision meshes, original
transforms, waves, objectives, navigation and boss authoring remain in their original
SubScenes. Surface materials are chapter-owned; the old flat backdrop renderer is hidden.
Paving textures are projected onto the original authored floor, including the round boss
arena. There is no extra paving layer. The landscape stays below the playable floor
across the entire 100 m wave arena.

`ChapterOneVillageSurfaces` assigns the imported StylizedTextures maps to chapter-owned
`CrowdPunch/Cel/Village Surface` materials. World-space projection supports UV-less
authored floors and imported atlas geometry without relying on their original texture UVs. This opaque
URP shader supports DOTS instancing, shadow casting, depth and depth normals, three-band
directional lighting, screen-space contact shading and fog. Meadow materials store each
arena's bounds and blend earth paths and broad colour variation per pixel; no geometry
colour bands or overlapping path slabs are used. Base material recipes have no runtime
controller; baked-only visibility is scoped by `BakedShadowLighting` as described below.
Existing player/enemy deformation is unchanged.

`ChapterOneVillageDetails` clusters grass, flowers, household props and garden details
outside combat bounds and approach lanes. `ChapterOneVillagePlanting` keeps complete
ground-plant bounds clear of the meadow shader's dirt corridors, including feathered
and noisy edges. It relocates offending plants to the nearest green verge without
intersecting buildings, fences or props, and preserves their ground-height offsets.
This final editor-only pass also applies to future village rebuilds.
`ChapterOneVillageGrounding` then samples the rendered meadow triangles under each
ground-level model's base. It seats floating roots, rock bases and ground props with
a small overlap, preserving market-table produce and raised-bed planting. The pass
works with both the original meadow mesh and its baked-shadow tiles, adds no runtime
components or colliders, and also applies after path-clearance relocations.
Wider house footings avoid coplanar wall faces.
`ChapterOneVillageHouseBody` authors shared, closed wall/gable shells for the two house
heights, fitted inside the tiled roof's curved underside. Houses no longer combine a
box wall with unrelated imported gable pieces. Cream plaster uses a subtle procedural
wash instead of projecting a decorative wall atlas. Mills place their annex beside
the tower with a recessed passage and a clear sail plane, avoiding shared wall faces.
Chapter ambient fill is reduced to retain visibly shaded faces; the shared Full Cel
post-processing profile is unchanged.

`CelLevelLighting` optionally owns a chapter skybox and distance fog in addition to
its ambient palette and sun. This opt-in defaults off for existing chapters and restores
the previous sky and fog settings on additive unload. The shared Full Cel Volume remains
at priority 150; a chapter profile at 160 overrides only white balance (temperature 4).
No new runtime ECS or material-scanning system is introduced.

The explicit `Crowd Punch > Art Direction > Dress Chapter One - Sunlit Village` command
replaces the generated scenery root and reapplies these visual settings to stages 01-10.
Hand edits inside that root should be made after running the recipe. Generated assets
live in `Assets/CrowdPunch/Data/ChapterOneVillage`; imported models are not edited.

## Chapter Three quarry presentation (2026-10-19, VISION-004..006)

Saved Campaign 21-30 main scenes own noncolliding quarry/aqueduct scenery through
`ChapterThreeQuarryBuilder`, `ChapterThreeQuarryAssets` and
`ChapterThreeQuarryScenery`. The builder refuses to overwrite dressed scenery;
`RefreshAppearance` updates owned materials/profile and explicit receiver copies;
`RefreshSurfaces` updates only material recipes and rock bindings, retaining each
receiver's shadow mask and chart transform. Chapter Three opts into continuous 3D
pigment, separate cliff/rock recipes and locally imported textures; other chapters
keep the shader's existing mapping. Neither refresh replaces transforms or baked
UV2 geometry. Original gameplay authoring,
physics meshes, encounters and character materials remain unchanged.
`ChapterThreeQuarryObjectives` adds segmented masonry, cargo fittings and gate-owned
exit cues through the existing ECS presentation components and systems. No runtime
ownership boundary or system ordering changes. Exit/socket bakers declare material
dependencies and convert authored colours to linear RGB only when the material opts
into painted ECS tint.

`ChapterThreeProtectedPointVisuals` skins Campaign 24's existing protected marker
with turquoise paving, limestone edging and a low cargo-stamp inlay. Its recipe uses
the authored `ProtectedPointAuthoring` centre/size directly; all added pieces remain
inside that rectangle and add no colliders, shadows or runtime state. Original zone,
priority, breach and wave ownership remain unchanged.

The production painted shader provides pigment, all receiver/DOTS passes and scoped
decorative cutaways. Chapter-owned sky, post profile, UV2 copies, masks and receiver
materials live under `Data/ChapterThreeQuarry`. The explicit campaign bake pipeline
bakes each main scene with its SubScene; existing `BakedShadowLighting` owns the
runtime zero-shadow-distance policy, probe lifetime and restoration. See
[Chapter Three verification](../Verification/ChapterThreeQuarry.md).

## Chapter Two foundry presentation (2026-10-18, VISION-004..006)

`ChapterTwoFoundryBuilder` refreshes the presentation of saved Campaign 11-20 scenes.
`ChapterTwoFoundryAssets`, `ChapterTwoFoundryScenery` and `ChapterTwoFoundryObjectives`
own materials, exterior compositions and objective bindings respectively. The generated
`Chapter Two - Emberworks Foundry` root is replaceable; keep hand-authored additions in
separate roots. It does not run encounter builders or change gameplay authoring.

The existing floor receives world-projected paving. Fixed noncolliding service yards,
workshops, tanks and chimneys live in the main scene; objective skins remain in the
gameplay SubScene. Segmented metal gates/targets reuse `VillageGatePiece` presentation;
shell fragments, cargo fittings, sockets and exit cues use their existing ECS state.
No runtime MonoBehaviour queries enemies and no new gameplay state is introduced.

`CampaignCargoTrackVisuals` owns the level-19 foundry and level-26 quarry transfer
carts and matching rail fittings. Chapter objective recipes call the same scoped
builder. Noncolliding cart children inherit the existing track entity's translation;
chapter-owned painted materials keep pigment attached to the cargo. Original rail
geometry, receiver charts, routes, collider and settings remain unchanged. Socket
enamel continues to use `TrackSocketVisual` and its existing locked-state tint.

The foundry composition uses joined structural bays and returning wings around an
open handling yard, with connected service paving and loading shelters.
`ChapterTwoFoundryInfrastructure` builds noncolliding docks, drains and constant-diameter
pipe routes with rounded knees and flange joints. Imported factory models use uniform
envelope fitting, preserving their authored aspect ratios; long utility runs are owned
geometry instead of stretched imported pipes. Neither construction adds combat colliders.

`ChapterTwoFoundryBlocks` builds the workshop bays and closes the surrounding factory
streets on all four sides, with a lower delivery frontage and a second skyline row.
This supports the existing orbiting camera, including views from arena edges. Joined
bays leave expansion joints, roofs clear their deck surfaces, door jamb footings stop
beside the opening, and service paving meets edge to edge to avoid competing depth
surfaces. The backdrop remains fixed, noncolliding scene geometry.
The shared cel renderer uses full-resolution interleaved-gradient contact shading
instead of per-render blue-noise sampling. Its effective radius, intensity and sample
count are preserved; this affects contact shading in every scene using `CelRenderer`.

`CampaignShadowBake` accepts explicit main-scene paths and an owned output directory,
processes each main scene with its SubScene, and enables `BakedShadowLighting` only after
usable shadowmask output. `ChapterOneShadowBake` retains its existing 1-10 API as a
wrapper. The receiver binder accepts an output folder; Chapter Two UV2 mesh copies,
receiver materials and settings live in `Data/ChapterTwoFoundry/BakedShadows`. Static
flags are assigned to fixed renderers, not propagated onto their ancestors; moving
receivers retain probes and no realtime caster shadows. Existing scene-scoped lighting,
pipeline and probe restoration remains shared.

Chapter Two's approved painted-background treatment is authored by `ChapterTwoPaintedLook`.
The chapter builder invokes it for stages 11-20. Its `PaintedFoundry` shader and
`Data/ChapterTwoFoundry/PaintedLook` materials/profile retain the existing UV2
shadowmask receiver contract, including per-chart ECS floor materials. Painted washes,
roof seams and glazing are surface shading; no runtime renderer scans or enemy changes
are involved. Each level's priority-150 Volume uses the chapter-owned copy of FullCelPost,
and sky/fog remains scoped by `CelLevelLighting`. Haze begins beyond the arena's diagonal
extent plus 30 metres (minimum 52 metres), preserving large-yard combat readability.
Static SubScene materials resolve their original source tags when selecting the painted
palette, while retaining individual baked mask charts. Other chapters retain their
materials and the shared FullCelPost profile. Assets moved from the level-11 pilot
retain their GUIDs; the approved reference level needs no re-authoring or rebake.

`CameraFollow` supplies its final look target to the required `CameraSceneryCutaway`
component on the Bootstrap camera. This component publishes camera/focus/clearance
globals for that camera's render and clears them afterwards; other cameras see the
complete scenery. It performs no physics casts, renderer scans, material instancing
or ECS queries. The painted shader uses `SceneryCutaway.hlsl` in its forward, depth
and normal passes to open a feathered, ordered-coverage corridor between camera and
player. ShadowCaster and Meta passes retain the full fixed environment.

`ChapterTwoPaintedLook` opts main-scene decoration into this camera cutaway with
dedicated material variants. Ground, paving and every gameplay SubScene material
remain excluded, preserving visible collision boundaries, objectives and enemies.
Geometry, camera framing, colliders, baked charts and level layouts are unchanged.

## Chapter One Painted Village Refresh (2026-10-19, VISION-005/006)

`ChapterOnePaintedLook` applies the approved Chapter Two pigment language to Campaign
01-10 without rebuilding scenery. Chapter-owned `ChapterOneVillage/PaintedLook`
material variants retain the original village palette, projected meadow/path bounds,
imported UV colour atlases, culling and explicit shadowmask/UV2 bindings. The shared
`PaintedFoundry` shader supports opt-in UV colour and material culling; existing foundry
materials keep their defaults. Floors and fixed SubScene receivers use solid variants;
main-scene decoration uses the existing scoped `CameraSceneryCutaway` contract. Moving
and destructible gameplay renderers retain their original materials and ECS feedback.

The priority-150 chapter profile carries the approved restrained painted grading;
the village's priority-160 warmth, sky, haze, sun and baked-only policy are preserved.
The explicit `Apply Painted Look to Chapter One` command updates appearance in place.
Future village dressing also invokes this treatment, retaining it after a deliberate
scenery rebuild. No transforms, geometry, colliders, gameplay authoring or runtime
systems are added by the refresh. Verification: `Docs/Verification/ChapterOneVillage.md`.

## Chapter One Baked Shadows (2026-10-18)

`ChapterOneShadowBake` processes each main scene together with its gameplay SubScene using
Unity's Progressive lightmapper and native shadowmasks. Fixed mesh geometry is marked for
static batching, GI and static visibility. Meshes are copied into chapter ownership and
given UV2; imported meshes and collision mesh references are preserved. Changing geometry
is excluded by its authoring components, including barricade visuals, rotating cover and
boss parts. These exclusions preserve ECS transform/state ownership (VISION-004/006).

`ChapterOneShadowTerrain` splits the existing meadow triangles into sixteen visual tiles,
preserving positions and normals. The four inner tiles receive denser lightmap charts;
distant hills use lower density. No collision terrain or arena geometry is replaced.
`ChapterOneShadowReceivers` writes baked mask texture/UV-transform material bindings for
static village-shader receivers in the SubScenes. These survive Entities baking without
a runtime material scan. Generated lightmaps ignore texture quality mip limits to keep
Entities Graphics' texture-array copies valid on every quality tier.

The village shader has a Meta pass and samples baked visibility through UV2 while retaining
world-projected surface detail and directional cel bands. Baked visibility has a hard
threshold. The shared character/environment cel lighting reads probe/lightmap visibility
when the level selects baked-only shadows; existing deformation paths are preserved.

`BakedShadowLighting` owns a temporary URP asset clone with shadow distance zero, the
shadowmask mode and a scoped shader flag for the level's loaded lifetime. Unload restores
the prior pipeline, mode and flag. The Mixed sun supplies directional cel lighting; all
cast visibility in these levels comes from the bake. Moving/destructible objects are
not permanent casters, and no real-time character shadow pass runs. Light probes support
moving receivers. The component holds an owned copy of the saved probe data and reasserts
its reference if additive ECS SubScene streaming clears the global probe set. Unload
restores the preceding probe set. This requires only a reference comparison, with no
renderer or entity scan. Screen-space contact shading remains part of the production renderer.

Rebake after scenery, sun or fixed-layout changes with `Crowd Punch > Art Direction >
Bake Chapter One Shadows`. The village dressing command invalidates its old baked-only
policy and restores realtime fallback until the explicit bake completes. Other chapters
keep their existing lighting policy. No ECS runtime system or enemy query was added.
