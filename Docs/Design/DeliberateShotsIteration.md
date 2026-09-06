# Deliberate shots: first playable experiment

Date: 2026-09-06. Comparison baseline: `2207c7f` (clean working tree at investigation start).
This is an implementation experiment authorized by the open design brief, not a replacement for
the GDD or a resolution of its open questions. No weapons, boss rules, effect catalogue, progression,
input scheme, or final art direction are introduced.

## Diagnosis and evidence

The existing loop already contains useful target incentives: explosives trigger area damage and
further explosions; launched Dashers penetrate ordinary enemies; ranged attacks cancel when their
source is launched; and an elite's wave replenishes normal enemies until its last elite dies.
These are intentional mechanics (ENEMY-002, 007, 011, 013), not unfinished substitutes to remove.

The authored conditions made those opportunities expensive or difficult to read:

| Observation from the baseline | Consequence / hypothesis |
| --- | --- |
| Bootstrap camera offset `(0,6,-38)`, look offset `(0,5,0)`; the captured baseline frame has compressed ground depth and a large sky/wall area. | Crowd geometry and relative range are hard to compare. The frame is visual evidence, not a human readability test. |
| Ground uses a high-frequency texture; normal enemies are red capsules; ranged and explosive variants share the capsule silhouette. Ranged wind-up has gameplay state but no corresponding body presentation. | Background detail competes with actors; threats often require watching individual movement. |
| Punch volume is 4 m wide and 8 m long. Aim assist range is 999999 m with a 45-degree cone. Propagation correction is 90 m, with no angular cap; homing continues at 30 degrees/s. | A wide punch often affects several bodies, and downstream direction can change substantially without a physical collision. A useful initial choice can be diluted by automatic steering. |
| First punches add an Active enemy's incoming velocity; the preview shows the impulse direction alone. An existing test explicitly preserves that momentum rule. | A moving body can initially depart at a different angle from its arrow. This iteration intentionally revises that behavior for normal player launches (PLAYER-004); it is not treated as an accidental implementation bug. |
| Baseline pressure is capped at six. Its speed is 16 m/s inside effectively unlimited charge range, with 2-second pursuing contact attempts, no preparation, and a 3.5 m surround ring. Player movement is 12 m/s. | The whole crowd does not chase, but the local decision space still collapses quickly. This is a hypothesis about play, not proof of player frustration. |
| Explosions have a 15 m radius, greater than the 8 m punch reach. Ranged projectiles travel 64 m/s and engage from 80 m. | Immediate explosive contacts often threaten the initiating player; distant ranged threats leave little post-fire correction time. |
| Direct punch damage is 10. Ordinary health is 5; elite health is 250. Collision damage caps at 0.75 times originating punch damage. | A strong body impact caps at 7.5 against the elite, less than a direct punch. Setup costs have little damage payoff on the durable target. |
| Presentation publishes a text state label for every launched/recovering normal body, including exhausted-health projectiles. | The biggest chains also create the most repeated text. |

Recent changes were checked: hit-confirmed cooldown (`2207c7f`), accepted roster and rules
(`098c29d`, `609ef7e`), gauntlet selection/content (`d4d5cb6`), gamepad support (`5d8d90b`),
and the current wave allocator. In particular, current guaranteed spawns are **interleaved and
distributed proportionally across a wave**, unlike the old architecture description. Gauntlet 3's
200-enemy wave, seed, batch size, timing, weights, and minimums remain unchanged.

Apparent data issues were separated from accepted rules: the Dasher preferred maximum distance
was 1 while its minimum was 6; the first iteration gives it a valid 8-14 m band. Unlimited assist,
large correction radius, long contact pursuit and extreme ranged speeds were treated as provisional
tuning. The existing re-punch, deferred defeat, ownership, collision deduplication, dash-punch and
wave-lifetime behavior were retained.

## Approaches considered

| Direction | Complaints addressed | Cost and tradeoff |
| --- | --- | --- |
| Hold-to-aim slowdown / target lock mode | Buys decision time and separates a chosen target. | Moderate input/bridge/UI cost on both schemes; makes aiming a separate mode and risks routinely suspending crowd pressure. It still needs a gameplay payoff. |
| New objective objects or marked priority enemies | Supplies explicit reasons to aim and a visible destination. | New content and rule cost; risks a universal priority and normal-enemy marker noise. Existing elite replenishment already supplies a goal. |
| Readable commitments and stronger existing body-shot payoffs (selected) | Exposes local opportunities, creates finite positioning windows, and improves the consequences of choosing a body and trajectory. | Small ECS/presentation addition plus authored tuning. Physical chains lose automatic correction, so long-chain reliability and controller precision need particular attention. |

## Implemented behavior and hypotheses

1. **Read the ground and the bodies** (VISION-005, PLAYER-003/004, INFO-001/002/004/005).
   The camera now looks down from `(0,30,-28)` with a 1 m target offset. The shared ground material
   is a matte solid background. Baseline bodies are subdued green, ranged bodies are taller blue,
   explosives are squat orange, and elite child meshes are purple. Existing Dasher streaks remain.
   Baseline, ranged and elite preparation pulses on the body; launched bodies brighten, recovery
   dims them, and actual damage briefly flashes white. Repeated state text and zero-health normal
   bars are removed; living damaged normals retain the permitted temporary bar, elites retain health UI.
   Five-metre arrow previews show only initial direction. Hypothesis: roles, imminent actions and
   consequences can be read while moving. Reject or revise if players still identify threats only
   after damage, confuse warnings with launches, or lose near-wall visibility under the higher camera.

2. **Sidestep a commitment, then take the shot** (VISION-002, LOOP-001, COMBAT-005/016).
   A baseline contact attempt brakes for 0.65 s, samples direction when that preparation ends,
   then attempts contact for 1.1 s without tracking a sidestep. It then returns to its surround role
   for the existing randomized 1-3 s interval. A committed lunge retains its existing pressure slot
   until it ends; total pressure remains six. Leaving range cancels preparation, and launch,
   defeat or loss of the player cancels the action. Contact itself remains dangerous, including
   walking into a preparing body. Separation and Unity Physics still affect movement; this is not
   a collision-free dash. Baseline approach speed is 10 m/s, ring radius 6 m, braking 40 m/s².
   Ranged wind-up is 1 s, shots travel 32 m/s, and range is 45 m. Dasher/elite wind-ups are 0.8 s.
   Explosive close pursuit still bypasses ordinary pressure allocation (ENEMY-010).
   Hypothesis: positioning wins a short local window without pausing simulation or clearing the arena.
   Reject if indiscriminate punching remains the only safe response, or waiting passively trivializes pressure.

3. **Choose a payload and destination with a useful payoff** (COMBAT-001/003/012/015, ENEMY-007/011/013).
   Punch width is 2.3 m, range remains 8 m, strength remains 90 and hit-confirmed cooldown is 1.25 s.
   Aim assistance is local (18 m, 12 degrees). Propagation correction and homing are authored to zero;
   their existing implementations and disable controls remain available. Initial assisted direction is
   still shared with the preview. A normal player-punched body now starts from rest in Active,
   Launched and Recovering states, removing incoming linear/angular momentum before the impulse.
   Elite knockback and enemy-originated punches retain their existing behavior. Subsequent motion
   is physics-owned except intentional Dasher rules. This deliberately revises the previous
   test-backed first-punch momentum rule to make moving targets follow the same initial direction
   contract as re-punched bodies (PLAYER-004, COMBAT-014).
   Strong collision damage now rises from 0.5 to at most 3 times originating punch damage; an impulse
   of 14.5 or greater reaches 30 damage, versus 10 for directly punching the elite. The same curve
   applies to launched bodies striking the player, preserving the danger of careless chains.
   Explosions have a 7 m radius, still enough to combine with nearby bodies, but allowing a contact
   ahead of the player to lie outside the blast. Baseline/ranged/explosive separation is 3-5 m;
   explosive-to-explosive separation is 10-16 m. A body into a nearby explosive can also trigger it
   without personally approaching it. These are physical clear-space and threat-removal rewards,
   not points. Larger chains affect more threats with one cooldown and can deliver multiple bodies
   to the elite; per-source damage remains capped and deduplicated.
   Reject if narrower targeting or uncorrected propagation makes deliberate shots miss too often,
   if the 7 m blast makes explosives irrelevant, or if ordinary impacts become universally preferable
   to the line penetration of a Dasher or the area payoff of an explosive.

Gauntlet 4 now authors 48 normals plus its existing one elite, with guaranteed ranged, Dasher and
explosive presence (4/3/3 respectively). Defeating its elite still stops replenishment via the existing
wave rule. This supplies a mixed goal encounter with meaningful durable-target impact payoff; it is
not the unresolved boss fight. Gauntlet 3 remains the unchanged 200-enemy representative encounter.

Situational choices to test: strike a stable baseline at an exposed elite; use a Dasher through a
line of threats; send an explosive into a cluster beyond its blast radius; interrupt a ranged wind-up
when the incoming shot blocks a setup; or take the less productive safe punch when boxed in.

## Tuning and comparison

| Location | Main controls |
| --- | --- |
| `Data/Settings/PlayerPunchSettings.asset` | Radius 1.15, range 8, cooldown 1.25, aim range 18, maximum assist angle 12. |
| `Data/Settings/GameRuntimeSettings.asset` | Pressure cap 6, correction radius 0, homing 0; damage base 0.5, impulse slope 0.2, cap 3. |
| `Data/Settings/Enemies/EnemySpawnSettings.asset` | Contact wind-up 0.65 (zero restores immediate pursuit), attempt duration 1.1, ring 6, charge multiplier 1.25, separation 3-5, braking 40. |
| Other `Data/Settings/Enemies/*.asset` | Ranged timing/speed/range, explosion radius and separation, Dasher distance band/preparation/recovery, elite wind-up. |
| `Scenes/Bootstrap.unity` | Main Camera / CameraFollow offset and Player / PunchTrajectoryPreview length, width, color. |
| `Materials/Ground.mat` | Background contrast. |
| `Data/Settings/Waves/Wave4.asset` | Mixed elite encounter size and guaranteed archetype counts. |

Paths above are relative to `Assets/CrowdPunch`. Body palette and modest silhouette proportions are
prototype constants in `EnemyReadabilitySystem` / `EnemySpawnInitialization`, avoiding another
configuration asset before visual language is validated.

Use Git comparison against `2207c7f` for the complete prior behavior; use a separate worktree for a
clean full A/B rather than overwriting this working tree. Set contact wind-up to zero or restore assist
and correction values for a focused mechanic comparison. Such partial toggles are not a full baseline.

## Validation and manual playtest

Automated checks and editor observations are recorded below as they complete. Scripted editor
execution does not establish subjective readability, gamepad feel, or whether lining up a shot is fun.

Completed checks:

| Check | Observed result |
| --- | --- |
| Runtime and editor C# builds, `--no-restore` | Pass. Existing package assembly conflicts and deprecated aspect warnings remain; no game-code compile errors. |
| Unity editor tests | All 37 pass, including cadence/commitment/payoff tests, three moving-body ECS detection-to-impulse cases, and existing launch homing, player-impact, elite-geometry and arena-distribution tests. The new integration cases verify assisted launch direction in Active/Launched/Recovering states and unchanged additive elite knockback. |
| Actual prefab baking / mixed elite scene | 49 spawned enemy roots and 49 correctly linked renderers; no Unity errors after correcting renderer-owned baking. |
| Controlled real-physics body shot into an elite | Elite health 250 -> 220 (30 damage). The matched direct punch gave 250 -> 240 (10 damage). Two immediate requests produced one accepted punch. |
| Moving body's live preview -> player punch -> physics | After settling the fixture, the body was made Active and given lateral velocity `(12,0,0)` at punch time. Its first observed launched horizontal velocity matched the visible assisted preview (measured angle 0 degrees, sampled 0.013 s after request); the elite again lost 30 health. Other enemies were disabled for this directional check. |
| Return-path risk and repositioning | Remaining on the body/elite line cost 30 player health from its rebound. Repeating the shot while moving sideways through virtual gamepad input preserved full player health. |
| Straight four-body chain, correction and homing disabled | All four bodies reached zero health while still Launched; three propagated bodies retained Player ownership and originating damage 10. |
| Body into an explosive and two adjacent bodies | Detonation flag set once, explosive Defeated, nearby bodies launched with explosion damage; player outside the 7 m blast remained at 100 health at the sampled time. This does not make later returning bodies safe. |
| Dasher through three ordinary enemies | All three were hit and launched; the Dasher remained launched and used its existing impact-damage rule. |
| Keyboard/mouse and gamepad dash-punch | Synthetic input on the existing bindings produced a launch while dash remained active. A separate narrow-volume mouse shot missed; aligning an eligible target produced the expected hit. This checks input plumbing, not human accuracy. |
| Hit versus miss cooldown | A repeated input during hit cooldown remained one request; after a confirmed miss the second request was accepted. |
| 200-body mixed encounter | Spawn cadence was accelerated in memory to reach all 200 bodies, leaving assets unchanged. Five simulated seconds / 301 rendered frames completed without Unity errors; all 200 were still Active at the first sample's end. |

Profiling on an i9-14900KF / RTX 4090 in the Unity Editor, not a shipping performance target:
pre-physics gameplay averaged 0.800 ms (p95 1.475), post-physics gameplay 0.773 ms (p95 0.933).
A subsequent 181-frame sample recorded Unity Physics at 0.385 ms (p95 0.625), the chase-system
update at 0.034 ms (p95 0.051), and the new presentation-system update at 0.010 ms (p95 0.015).
System-update markers are not isolated worker-job timings; the standalone presentation worker
marker returned zero, so its worker cost is **not** established separately. The probe reset player
health to keep the workload running; these samples are not survival or difficulty measurements.
Hands-on interactive player testing was not available; no full human-controlled A/B run or shipping
hardware benchmark was performed. The live checks above were scripted through the Unity Editor.

The isolated collision fixtures placed subjects in Recovering to hold them for a reproducible
physics shot and disabled unrelated enemies; the final moving-body check explicitly switched the
source to Active with lateral velocity at punch time. The mixed 49/200-body checks ran normal AI separately.
The body/elite fixture placed the player at `(0,0.5,-20)`, the body at `(0,1,-14)`, and the elite at
`(0,0.5,0)` before allowing gravity/physics to step. These checks establish collision behavior and
damage payoff, not the success rate of aiming at a moving elite.

A camera-only comparison uses the **same frozen mixed-encounter frame**:
[previous low camera](PlaytestCaptures/camera-low.png) and
[implemented high camera](PlaytestCaptures/camera-high.png). Enemy positions and the new body/ground
presentation are identical in both captures. These are world-camera renders without the overlay HUD;
they isolate camera geometry, not the complete previous game or an interactive readability assessment.

An integration review also found that elite staging/corridor movement can supersede baseline
contact intent. Those overrides now cancel pending contact commitment, preventing a misleading
wind-up pulse for an attack that will not execute.

The temporary command-file editor probe was removed after validation. No health-reset, accelerated
spawn, disabled-enemy fixture, or virtual input device is shipped as part of the playable iteration.

Manual procedure (Bootstrap, then Pause menu level selector):

1. In Gauntlet 1, approach a baseline at the punch edge. Check that its arrow begins on the affected
   body; sidestep a wind-up and punch during the committed lunge. Repeat using gamepad movement,
   camera rotation, punch and dash. Check misses spend no cooldown and dash-punch stays immediate.
2. In Gauntlet 3, spend two minutes under mixed pressure. Before each of ten shots, name the chosen
   body and intended destination. Record whether another body was unintentionally hit, whether
   the initial direction matched the arrow, and whether setup cost health. Include near-wall positions.
3. Launch an explosive into a cluster beyond 7 m; compare with an immediate nearby explosion and a
   plain body into the same cluster. Check the origin and extent of damage are understandable.
4. Launch a Dasher through a line, then a baseline through a comparable line. Check ordinary body
   momentum transfer remains distinct from Dasher penetration and that a static obstruction stops both.
5. In Gauntlet 4, compare direct elite punches with body shots into it. Read the actual health loss
   and compare damage per setup second, health spent, and ranged interruptions. Then defeat the
   elite and confirm normal replenishment stops. Another target should sometimes be the better choice.
6. Repeat Gauntlet 3 long enough to reach representative crowd counts. Judge chain spectacle and
   clarity during a multi-explosion event; do not assess only the sparse first batch. Compare the same
   level on the baseline revision, and separate changed rules from the player's growing familiarity.

OQ-001 remains unresolved: 200 is the existing authored workload used here, not an agreed shipping
crowd-size or hardware target. Camera, art, encounter pacing and input-specific assist remain experiments
under OQ-013, OQ-015, OQ-016 and OQ-017. The biggest risk is trading automatic spectacle for physical
shot certainty without enough successful chains; measure both, not only survival.
