# Ten-gauntlet encounter progression

Authored 2026-09-07. This table was recorded before implementation. These are provisional encounter choices authorized for this slice, not resolutions of OQ-001, OQ-007, OQ-013, OQ-015, OQ-016, or OQ-018. Duration estimates are playtest targets, not measurements. The target total is about 16 minutes (LOOP-003).

B = Baseline, R = Ranged, X = Explosive, D = Dasher, E = Elite. All use the existing, unchanged profiles. A plus sign joins an overlapping stage; a semicolon means a complete clear and a short recovery window. Counts are exact, not random special-threat rolls.

| Level | Learning / tactical goal | Layout (interior metres) | Composition / pacing | Target duration | Escalation |
|---|---|---|---|---|---|
| 01 First Line | Move, rotate camera to aim, punch a foreground body through the crowd | 26 x 32 launch court, central long stripe and open side aprons; south entry | 1B; 8B; 10B. Three-second clear breaks. Peak 10 | 45-60 s | One target, then choosing a direction through several targets |
| 02 Side Step | Combine camera-facing aim with lateral movement / committed dash | 38 x 28 wide court, two parallel banks with a broad transverse dash route | 6B west + 6B east after 3 s; 14B in two batches four seconds apart. Peak 14 | 55-75 s | Pressure changes side; reposition to put one bank behind another |
| 03 Far Bank | Launch a nearby body at a ranged threat instead of chasing blindly | Tapered court, 34-wide south / 26-wide north, 36 deep | 6B + 1R; 12B + 2R. Ranged banks arrive after bodies. Peak 14 | 65-85 s | First projectile threat, then target choice between two shooters |
| 04 Fuse Court | Hit orange with a launched body while outside its 7 m blast | Clipped 40 x 32 court with cross lanes and open retreat corners | 10B + 1X; 16B + 1X. Only one explosive at a time. Peak 17 | 75-95 s | A rewarding chain target also closes distance and threatens the player |
| 05 Runway | Sidestep the committed dash; launch the recovering Dasher down a crowd lane | 28 x 44 long court, 24 m attack runway and lateral aprons | 12B + 1D; 18B + 1D from the opposite end. Peak 19 | 80-100 s | First fast committed threat; direction reversal tests camera and dash control |
| 06 Two Problems | Choose between a shooter and an approaching explosive | Sheared 34 x 34 court, opposite offset banks and diagonal cross-shot | 16B + 1R + 1X; 20B + 1R + 1X. Specials enter separately. Peak 22 | 90-110 s | Two familiar threat roles compete for the same useful launch body |
| 07 Borrowed Fist | Disrupt elite setup and feed crowd impacts into its durable body | Open 38 x 38 square, wide cross lanes and clear edges for replenishment | 20B in two batches; 22B + 1E. Four-second elite reveal break. Peak 23 | 100-120 s | First replenishing encounter; clearing ammunition alone cannot finish it |
| 08 Oblique Angles | Keep a lateral escape while aiming across offset threat banks | Wide clipped 46 x 32 court, staggered shooting lanes | 18B + 1R + 1D; 22B + 2R. Staggered threat arrivals. Peak 24 | 105-125 s | Ranged trajectory and Dasher commitment must both be read during positioning |
| 09 Relay | Finish useful chains before a finite reinforcement arrives | Clipped 44 x 40 court, three entry banks around an open centre | 16B + 10B/1X after 6 s + 1R; 22B + 1D/1R. Peak 28 | 110-130 s | First substantial timed crowd overlap; clearing early preserves space |
| 10 Crowd Punch | Apply the full shot-selection loop, ending with crowd-supported elite | Clipped 48 x 44 amphitheatre, converging lanes and a roomy central exchange | 24B/1D; 28B/1R/1X; 24B/1R/1D/1E. Three- and four-second act breaks. Peak 30 | 130-150 s | Three distinct tests, then durable priority target with two support threats |

Design basis: VISION-001 through VISION-005, LOOP-001/002/006, PLAYER-001 through PLAYER-005, COMBAT-001/003/005/016/017, ENEMY-001 through ENEMY-013, INFO-001/002. No functional boss exists: this task ends with the existing elite, as explicitly authorized, and does not claim to implement MVP-002 or resolve OQ-007.

The actual baseline has six pressure slots, 3-5 m separation, 8 m/s return-to-distribution movement, and 0.65 s contact preparation. Authored formations are therefore initial approaches, not stationary puzzles. Introductory courts stay small enough that distributed bodies remain plausible downstream targets. The player can choose an angle during commitment and recovery, and can dash around the six approaching bodies to shoot outward through the others. There are no frozen enemies or profile overrides.

The camera orbits at the existing (0, 30, -28) offset and 60-degree field of view. Layouts use low perimeter walls, continuous flat floors, and convex interiors. There are no internal pillars, mazes, holes, navigation assumptions, or projectile-cover claims. Ranged projectiles ignore arena geometry. Floor stripes identify useful directions without forcing a shot or adding target markers.

Wave recipes use existing exact minimum counts summing to the normal total, existing timed activation for finite stage assembly, and cumulative-clear activation at every recovery break and final wave. Only the elite's own normal cohort replenishes, using the same instances, at the established cadence; elite stages do not overlap a following act. The finale replenishes at most one R and one D and never replenishes explosives. Safe initial placement remains the existing collision-tested pipeline; later-level sequences require 8 m additional player clearance. Spawns use broad rectangles so camping one point cannot exclude the entire region.

## Implemented content and integration

- Ten main scenes and ten matching ECS SubScenes are saved under `Assets/CrowdPunch/Scenes/Gauntlets`. The first four scene/SubScene GUIDs were retained; their layouts and encounters were replaced. Bootstrap still owns the player, camera, UI, and additive sequence.
- Thirty-nine wave assets live under `Assets/CrowdPunch/Data/Settings/Waves/Progression`, named `CP<level>_<wave>_<purpose>`. Normal minimum counts sum to the normal total, with zero random excess weight. Existing wave inspection and editing tools remain usable.
- Shared ground material and unchanged enemy/projectile prefabs are reused. Ten convex floor meshes and shared environment materials live under `Data/GauntletLayouts`. Each floor is continuous, its top is Y=-1, and its entry transform is Y=0.5. Spawn centres are Y=2. Movement bounds are inset; defeat bounds extend four metres beyond each footprint's axis-aligned envelope, from Y=-8 to Y=10.
- The low visible rails have taller collision faces (top Y=2) because the existing hybrid movement cast is centred at player Y + radius + 0.02. Play-mode inspection caught and corrected escape over the initial lower collision faces. Neither the player collision algorithm nor global physics tuning changed.
- A rendering-only backdrop at Y=-2.1 hides pooled bodies beneath the compact floors. It has no collider, spawn region, or gameplay role; the perimeter and convex floor still define usable space.
- Levels 1-2 show a ten-second opening hint using the existing canvas. The instructions match the actual actions: WASD/left stick, mouse/right-stick camera aim, LMB/west face button punch, RMB/right shoulder dash. Fundamental controls are never gated.
- The existing pause menu now fits ten named level buttons in two columns. Final encounter completion displays Run Complete and Play Again there. Retry delegates to the existing additive reload; loading a level resets the completion flag, player health, entry, and punch state.
- The ten old wave assets had no remaining serialized scene/prefab/asset references after replacement and were removed with their metadata. Shared dependencies and the four original scene identities were retained. The previous encounters remain available in Git history.

## Spawning and lifecycle changes

No wave schema, enemy variant, AI override, or new runtime spawning framework was needed. Different profile-specific banks are separate timed wave assets; cumulative-clear gates join them into one stage. This preserves legacy random/authored spawning and every existing wave activation mode.

Three existing bookkeeping gaps needed correction for LOOP-006 and ENEMY-011:

1. Fired ranged projectiles are independent runtime entities. `GameRestartSystem` now destroys their roots and linked children on reset, as it already does for old wave bodies.
2. Generic pooling resets an elite's launch phase to Active. `EliteWaveReplenishmentSystem` now also rejects an ownership record with terminal `DefeatCounted`, preventing a pooled dead elite from re-enabling normal replenishment. The established same-wave replenishment policy and cadence are preserved.
3. A replenishing normal can enter the pool directly on leaving defeat bounds, without entering Defeated. `EnemyWaveDefeatCountSystem` now runs after bounds handling and counts an enabled pooling request as well as terminal launch state. The existing respawn path restores its counters if it returns. If the elite dies before that return, the pooled normal no longer strands cumulative completion at one remaining enemy. This was reproduced during the full sequence probe and is covered in both pending-pool and already-pooled regression cases.

`GauntletProgressionBuilder` provides an explicit Editor rebuild recipe with an overwrite warning. Saved scenes/assets are the runtime content and normal Inspector tuning is independent of that recipe. `GauntletSequenceSmokeCheck` is an opt-in Editor validation tool, never part of player builds.

## First human playtest priorities

1. In levels 1-2, watch whether the player discovers camera-facing aim and a lateral dash, and whether a chosen downstream target saves punches. If bodies disperse before a novice reads the shot, adjust the spawn rectangle and stage size first; do not freeze or slow enemies. The formations are not expected to persist.
2. Record clear time, damage taken, retries, and useful multi-body hits by stage. Duration targets above are provisional. Add or remove a bounded baseline batch to adjust sustained length without increasing special count or changing enemy health.
3. Check the first ranged, explosive, and Dasher reveals from both entry and an aggressively advanced player position. Increase the timed gap between the body bank and special arrival if attention is overloaded. Preserve player-clearance requirements and rectangles large enough to remain spawnable when camped.
4. In levels 7 and 10, check whether the elite's durable body and replenishment make prioritization apparent. If cleanup dominates after elite defeat, reduce the elite wave's normal cohort; if the fight is too empty, expand the cohort within the measured budget. Do not add elites or replenish explosives to lengthen it.
5. Inspect far-edge threats while orbiting and fighting near each perimeter. Adjust court dimensions, entry positions, or threat-bank locations before changing the shared camera. Geometry does not provide ranged cover.
6. Recheck peak active and retained entity counts after tuning. Defeated roots stay pooled until scene restart/unload, so active count alone understates query and rendering costs. Validate target hardware separately; OQ-001 remains open.
