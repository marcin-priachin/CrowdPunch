# Crowd Punch — Open Design Questions

Last updated: 2026-10-09

These questions are deliberately unresolved. Agents must not infer answers from prototype code, inspector values, placeholder assets, or genre convention.

## Priority 1 — Needed For The Next Playable Slice

### OQ-001 — Prototype Success Criteria

What crowd size, frame-rate target, target hardware, and minimum chain-reaction length should the first representative physics test prove?

### OQ-003 — First Two Weapons

Which two weapons provide meaningfully different launch geometry while preserving one coherent physical combat language?

## Priority 2 — Needed For A Complete MVP Run

### OQ-009 — Progression Model

What changes during a run, what persists between runs, and what—if anything—is meta-progression?

### OQ-010 — Weapon Ownership

Are weapons permanent unlocks, temporary pickups, run-start choices, replaceable equipment, limited-use opportunities, or another model?

### OQ-011 — Effect Grammar

What is the smallest shippable effect set, and what general transformation/event table governs collisions between those effects?

### OQ-013 — Encounter Pacing

How are gauntlet encounters, short transitions, recovery, and the boss distributed across a 15–20 minute run?

## Priority 3 — Validate After The Core Is Fun

### OQ-014 — Combination Reference UI

How many discoverable combinations exist before a compact reference becomes more helpful than environmental learning?

### OQ-015 — Art Direction

What visual style best preserves silhouettes, launch direction, effect ownership, and large-crowd performance?

### OQ-016 — Camera

What camera angle, distance, and dynamic behavior best preserve positioning accuracy while showing enough of a large crowd?

### OQ-017 — Input Targets

Which controller and keyboard/mouse schemes are primary, and should the accepted range-based punch aim assistance vary between them?

### OQ-018 — Difficulty Scaling

Should difficulty grow mainly through crowd composition, density, speed, gauntlet layout, effect interactions, or boss behavior?

### OQ-019 — Audio Language

Which sounds communicate a successful launch, propagated collision, effect transformation, player danger, and exhausted chain without becoming cacophonous?

## Decision Record Template

When a question is resolved:

1. Add or revise the relevant requirement in `GDD.md`.
2. Record the decision and rationale below.
3. Remove the question from the active list only after the GDD contains the authoritative rule.

```md
### 2026-MM-DD — OQ-XXX

Decision: ...

Rationale: ...

GDD rules: COMBAT-XXX, INFO-XXX
```

## Resolved Decisions

### 2026-10-09 - Ground Hazards And Gauntlet_23

Decision: Adopt the final agreed summary in Ground Hazard Design, conversation
6ac8f571-f0a4-83ed-bdf9-c5b4b25b9c73. GROUND-001..007 record both shapes and activation
modes, shared victim clocks, horizontal damage only, player protection bypass, armor
immunity, no hazard death explosions, current-launch kill attribution, real routing,
safe spawning, wave/retry resets and finite two-wave completion. Defaults are Active-only
avoidance and Wait Safely; both alternatives remain configurable.

Numbers, layout, enemy counts, minor visuals and configuration structure are delegated.
No blocking design question remains. Balance/readability and OQ-001 hardware/performance
targets still require playtesting. Rejected: warning damage, force/slow/launch, player
protection, armor stripping, overlap multiplication, reentry resets and death explosions.

Implementation choice: simultaneous overlaps select the highest-damage patch, with the
longer interval breaking ties. One victim timer remains authoritative. Wave cycle epochs
begin when the sequence enters that wave's pre-spawn delay; periodic patches for later
waves are introduced at that epoch. These remain implementation details, not new rules.

### 2026-10-09 - Local Pillar Crowd

Correction: Local enemies may move like ordinary enemies throughout `pillarCrowdRadius`.
The area limits normal movement, rather than assigning permanent standing positions.
Normal pressure selection, chase/contact cadence and separation remain shared with other
Baselines. Launch/recovery physics and local replenishment continue unchanged.

Decision: Keep enemies around the Dino encounter's pillars with the amount configured in
boss settings (PILLAR-006). Implement a bounded local Baseline group per unconsumed pillar,
separate from the general wave crowd. Local bodies keep ordinary launch/recovery physics,
return to their pillar afterwards, and safely respawn nearby. Consumed pillars release
survivors and stop their local replenishment. Default two per pillar; general wave composition
and respawn delay remain wave-owned.

### 2026-10-08 - Dino Pillars And Gauntlet_22

Decision: Adopt the final agreed summary in Boss Pillar Design, conversation
6ac79a98-f608-83ed-8503-1a550fa5810c. PILLAR-001..006 record continuous pursuit,
slower-turning bursts, pillar-only damage, Player-owned toppling, locked direction,
once-per-fall moving contacts, safe miss regeneration and complete lifecycle.
Default toward-boss falling, damage-and-push response, and three successful hits;
both alternative modes and one/two-hit requirements remain configurable.
Numbers, layout, visuals, clips and collision safeguards are delegated. No blocking
question remains; balance and OQ-001 crowd/target-hardware criteria still need playtesting.

Rejected: regenerating successful pillars, explosion/non-Player toppling, ordinary boss
damage, solid fallen obstacles, pillar ground markers and an extra standalone test level.

### 2026-10-07 - Rolling Blob And Gauntlet_21

Decision: Adopt the final summary and explicit answers in Boss Rolling Design,
conversation 6ac6ab4f-c4d8-83ed-9ea0-83f31ae8bd34. ROLL-001..007 record committed
cycle timing, six configurable modes, protected rolls, resistance-aware Boss-owned
launches, any-source vulnerable explosions, combined hits, stage clamping and lifecycle.
No blocking gameplay question remains. Exact balance, arena, colors, animations and
collision safeguards are delegated; 2-3 minute balance and OQ-001 targets still need validation.

Rejected: hit-driven timer changes, protection-free hits, stage skipping, resistance
overrides, direct boss punches and an extra vulnerability countdown.

### 2026-10-07 - Chicken Body Rebounds

Decision: The implementation-chat correction requires launched enemies striking the Chicken
to deflect sideways instead of rebounding directly at the player. Reuse the first boss's
solver-speed-preserving deflection and clear the contacted boss homing lock (CHICKEN-004).
This does not remove COMBAT-015's persistent danger from launched bodies and their chains.

### 2026-10-07 - Chicken Boss And Gauntlet_20

Decision: Adopt the agreed summary and explicit decisions in Boss Design Options,
conversation 6ac52668-a844-83eb-b4aa-6ee463abf246. CHICKEN-001..008 record fleeing,
single/paired stages, punchable bouncing shots, dangerous returned shots/chains, ownership
gates, any-source explosion damage, combined Exploder hits and immediate boss-owned completion.
The supplied Chicken.fbx is present. Gauntlet_19 already existed, so the next encounter is 20.

Rejected: rush markers/warnings, proximity cancellation of committed shots, homing after
wall bounce, direct-punch damage, boss-owned body damage, safe redirected shots/chains,
enemy-contact shot consumption, retained returned shots on boss contact and survivor cleanup.
Numbers, arena and minor visuals were delegated. No blocking Chicken design question remains;
2-3 minute balance, art/audio, camera and representative performance targets still need validation.

### 2026-10-06 - Clear Trails On Wave Defeat

Decision: The implementation-chat correction supersedes the original Trail expiry gate.
When all spawned enemies in a finite wave are defeated, clear its remaining trails and
shared damage clocks immediately. Trails must not block wave advancement or final completion
(TRAIL-007). Keep independent lifetimes while enemies remain and preserve Wizard-zone rules.

### 2026-10-06 - Trail Enemy And Gauntlet_19

Decision: Adopt the final agreed design in conversation 6ac3dbb3-6c8c-83eb-b491-f5d389645abf.
TRAIL-001..007 record circling/reversal, persistent source-lifetime sections, horizontal
damage-only overlap, shared source/target timers, independent source stacking, player-specific
protection, immunity/avoidance alternatives, launch ownership credit and finite 6+1 / 12+2 waves.
Both trail types damage enemies and the player by default. Own-source immunity and damaging-trail
avoidance are defaults. Fish.fbx was supplied in this implementation chat.

Rejected: normal contact attacks, trail force/slow/launch, height restrictions, dash immunity,
trail-triggered Exploder detonation and Baseline replenishment. Numerical tuning, placeholder
colors and suitable model animations were delegated. No significant Trail design question remains.
OQ-001 performance targets, art/audio, broader pacing and progression remain unresolved.

### 2026-10-03 - Protected Point And Gauntlet_18

Decision: Adopt the final decisions from Codex chat 01a1025f-eb6f-76e2-8618-83032c5bf7df:
32 x 96m arena, marked 8 x 4m end zone, opposite-end spawning, closest three in-range
player attackers, Active-only disappearing breaches, first-breach defeat by default,
and finite 16/24/32 waves in batches of four every three seconds with five-second cleared-wave pauses.
The later answer in the implementation chat preserves already committed attacks when selection changes.
PROTECT-001..004 are authoritative. The earlier route-blocking-only alternative is superseded.

Implementation details: XZ root-centre zone inclusion, existing per-archetype attack ranges,
three-second opening delay, player entry 12m from the defended border, and the existing pause
menu for defeat/retry. Zone geometry belongs to scene authoring; threshold and attack cap
belong to a dedicated settings asset. No replenishment or Wizard-hazard completion gate is
enabled in these finite waves. Balance, art, camera and OQ-001 performance targets remain
playtest work; this does not resolve the broader run-duration or progression questions.

### 2026-10-03 - Optional Guaranteed Wizard Casting

Decision: Expose Cast Whenever In Range, default off (WIZARD-002). When enabled, a ready
Wizard starts telegraph immediately with an available player in engagement range,
bypassing cast chance and check interval. Cooldown and the committed cast lifecycle still apply.

### 2026-10-02 - Independent Wizard Zone Radii

Decision: Cast zones and zones caused by impacts have separate radius settings (WIZARD-002/005).
Both retain the previously authored radius during migration. Cast probability uses Cast Radius;
each zone's damage bounds, visuals and avoidance use that zone kind's radius.

### 2026-10-01 - Wizard And Gauntlet_17

Decision: Adopt the final Wizard design in conversation 6abe10dd-838c-83ed-8b66-0308a282a96d,
including per-target zone timers and the Q160 correction preserving already-launched state.
The later 2026-10-01 correction makes cast zones player-only and adds detached impact zones
when a launched enemy strikes an active or recovering Wizard; GDD WIZARD-003/005 governs both.
GDD WIZARD-001..007 and [Wizard](Wizard.md) record the accepted requirements and rejected alternatives.
Wizard is the sixth standard archetype. No significant Wizard gameplay question remains open.

Implementation choices: 32 x 34m clipped court, two-second optional ammunition delay, fixed
Wizard counts expressed through existing guaranteed-minimum profiles. Cap authoring rejects
impossible guaranteed counts and capped weighted waves without an eligible non-Wizard fallback.
Ground graphics reuse a procedural mesh and additive material; sampled Dance uses the existing
profile-specific animation slot. These are implementation details, not broader design decisions.
OQ-001 performance targets, final art/audio and whole-run pacing remain unresolved.


### 2026-09-29 - Knock Into Place And Gauntlet_16

Decision: Adopt the final agreed design in conversation 6abb87ae-bbe4-83eb-9af8-9f1b3890742a.
One bidirectional rail object advances by fixed steps from launched bodies and explosions,
defaults to five net forward hits, retargets immediately, discards excess endpoint hits, and
locks/completes on physical arrival. Preserve solid collision, ordinary launch rebound, shared
aiming, bounded replenishment, Baseline-first introduction, and rail/socket placeholder visuals.

Rejected: free movement, forward-only progress, direct-punch movement, strength-scaled steps,
double-counted explosive impact/blast, banked hits, ignoring mid-slide hits, pass-through
characters, survivor cleanup, extra objectives, extra counters and predictive rebound previews.

GDD rules: TRACK-001 through TRACK-005. The following implementation edge cases remain
provisional pending user clarification/playtesting; they are not silently promoted to design:

- Player-only filtering affects body impacts by default; all explosions still count. An optional
  setting can also reject blasts from currently non-player-owned launches; unlaunched blasts count.
- Only destination-changing hits spend eligibility by default. An optional setting also spends it
  on perpendicular or endpoint-clamped contacts.
- Optional damaging pushes default to one damage to player and enemies once per continuous slide.
  Retargeting without stopping stays one slide. Player damage can be disabled independently.
- Numerical direction tolerance is 0.00001 on the normalized planar dot product. A centered blast
  contributes no directional step. Safe displacement tries both sides then the ends; Gauntlet_16
  deliberately leaves those routes open. Arbitrarily enclosed custom tracks need separate validation.

Initial geometry (10m track in a 32 x 34m arena), 0.35-second slide, 12 Baselines at 2 seconds,
two Explosives at 10 seconds, and existing replenishment delay are implementation defaults.
Broader performance targets, final art and run pacing remain under their existing questions.

### 2026-09-29 - Shell And Gauntlet_15

Decision: Adopt the final agreed shell design in the referenced "shell - Brainstorm Puzzle Levels"
conversation (6abb746a-5ed4-83eb-a3bb-a379ba5d49f5). Three nearby exploder explosions permanently
break a central solid shell; the breaking blast cannot damage its core. The core has configurable
Baseline health and accepts ordinary punch/body/blast damage, with independent explosive impact
and blast damage. Replenish Baselines throughout; replenish two exploders only after zero remain
while the shell is intact. Core death completes immediately. Use existing aiming and preview,
blocked punches with cooldown, rebound for non-exploders, and in-world damage without bars.

Rejected: launched-only or direct-hit-only shell damage, one-hit/regenerating/timed shells,
restricted core attacks, combined impact/blast damage, survivor cleanup, exits, extra targets,
additional enemy types, health bars, and extending trajectory prediction.

GDD rules: SHELL-001 through SHELL-006. No significant open decision blocks this slice.
OQ-001 performance targets, broader pacing, final art and audio remain unresolved.

### 2026-09-28 - Rotating Cover And Gauntlet_14

Decision: Add a single central target protected by rotating cover, using Gauntlet_13's target
and the existing bounded replenishment infrastructure. Three rotation modes and three hit
responses are configurable. Cover returns bodies toward the impact-time player position without
homing; exploders reflect without detonating on cover. Outside blasts cannot damage the target,
and admitted explosive impact/blast count once. Preserve existing aim assist and preview.
Target destruction completes immediately, with no exit or cleanup requirement.

Implementation clarification: the repository has no launch-distance budget. The user selected
preserving impact-time momentum, damping and the same launch/recovery state over introducing
an explicit distance cap. A configurable speed multiplier defaults to 1.0. This supersedes the
chat's literal remaining-distance wording. Higher multipliers may increase physical travel distance.

GDD rules: COVER-001 through COVER-005. Initial geometry, rotation speed, hit count and crowd
composition were delegated to implementation/playtesting. No other open design question is resolved.

### 2026-09-26 - Barricade And Gauntlet_13

Decision: Implement only Gauntlet_13 with a reusable three-hit barricade, launched-body/explosion damage deduplicated per source launch, intact rebound and destroying-hit pass-through, existing aim assistance, a continuously replenishing bounded Baseline/Explosive crowd, and completion on reaching the exposed exit. Direct punches consume cooldown without damaging it. Stop replenishment on destruction; survivors stay hostile. Use visual damage stages without a health bar and one nonblocking opening hint.

Rationale: The level teaches launching the crowd into an environmental objective without introducing another attack model. Independently breakable sections, direct punch damage, a conventional health pool, double-counted explosive impact/blast, finite-wave fallback ammunition, neutralized survivors, separate barricade aim alignment, and an additional level were rejected in the final referenced design conversation.

GDD rules: BARRICADE-001 through BARRICADE-005, PLAYER-003/004/009, LOOP-006. Rebound tuning, composition, effect parameters and exact exit placement are implementation defaults. The existing initial-direction preview is retained; it does not predict rebound. Broader pacing, art direction and performance targets remain open under their existing questions.

### 2026-09-24 - Armored And Post-Boss Progression

Decision: Add Armored as a fifth normal archetype with event-deduplicated shields, blocked-punch cooldown confirmation, continuous pursuit outside pressure allocation, and readable armor feedback. Introduce it in Gauntlet_12 with a one-at-a-time encounter ammunition safeguard. The Gauntlet_11 boss completes its level and advances to Gauntlet_12; bosses are not the end of the game.

Amendment: Replace the originally approved armor-stage color coding with a compact overhead indicator showing one shield per remaining stage. Keep a single stable model tint and transient impact feedback. Armored alone receives this exception to INFO-001's minimal normal-enemy UI rule.

Amendment: Start with two displayed shields. Each absorbs one accepted hit; after the second, remove the indicator and permit normal launching on a subsequent hit. No hidden protected stage remains after the indicator disappears.

Rationale: Repeated deliberate body shots teach the core crowd-projectile interaction. Shared damage/launch ownership preserves coherent outcomes and the safeguard prevents exhausted ammunition from stranding the encounter. This revises the earlier roster and boss-finale decisions; unrelated pacing, progression and art questions remain open.

GDD rules: ENEMY-014, ENEMY-015, PLAYER-003/004/009, COMBAT-016, INFO-001/004, LOOP-002/006, BOSS-007, MVP-001/003/005.


### 2026-09-22 - OQ-007

Decision: Gauntlet 11 is a head and two detached hands. Player-owned launched bodies and their propagated chains damage the head; boss-scattered bodies and their descendants do not. Hands physically shield, stagger without destruction, and perform committed slam, lunge and sweep attacks. Three health-based stages escalate coordination. A bounded wave-configured crowd replenishes while the boss lives; head defeat completes the gauntlet and advances to later authored content (revised 2026-09-24). Reuse the Orc Blob model and elite health-bar presentation.

Rationale: The encounter tests deliberate crowd-mediated shots through readable physical protection, while preserving player controls, ordinary enemy mechanics, and the existing hybrid ownership boundary. Threshold damage is clamped with overflow discarded so burst damage cannot skip a stage. The 2-3 minute target remains tuning work rather than a forced timer.

GDD rules: BOSS-001 through BOSS-009, MVP-002, MVP-005, LOOP-006.

### 2026-09-04 — OQ-002

Decision: The initial four standard MVP enemy types were Baseline, Explosive, Ranged, and Dasher; Armored was approved as a fifth on 2026-09-24. Elite enemies are a separate special encounter tier and do not count toward the standard enemy slots.

Rationale: These roles are already established in the playable design and provide distinct crowd functions: a neutral chain body, a collision-triggered area threat, positional ranged pressure, and a committed high-mobility threat. Keeping elites separate preserves a simple standard roster while allowing rarer crowd-manipulation threats.

GDD rules: ENEMY-001 through ENEMY-013, MVP-003

### 2026-08-30 — OQ-008

Decision: The run uses a fixed sequence of small, closed gauntlet levels leading to the boss. Open levels, branching routes, and seamless traversal between gauntlets are outside the MVP.

Rationale: Compact authored arenas concentrate play on crowd positioning and physical chain reactions while keeping level production and run pacing tractable.

GDD rules: LOOP-002, LOOP-006, MVP-001

### 2026-08-28 — Ranged Projectile Movement Lead

Decision: A ranged enemy predicts a fire-time horizontal intercept from the player's current movement velocity and the projectile's configured speed. An authored multiplier from zero to one blends between the player's sampled position and the full predicted intercept. The resulting target remains fixed after firing, so the projectile does not home.

Rationale: Movement-direction adjustment makes ranged enemies respond to an already-moving player while preserving readable projectile commitment and post-fire dodge counterplay.

GDD rules: ENEMY-002, ENEMY-003

### 2026-08-23 — Propagated Launch Aim Correction

Decision: An enemy newly launched by ordinary enemy-to-enemy propagation corrects its solver-produced horizontal direction toward the smallest-angle living active or recovering enemy inside a configurable radius. Correction preserves horizontal speed and vertical velocity, and a zero radius disables it.

Rationale: Physical collision transfer remains the source of launch speed while modest directional correction makes intended crowd chains more reliable.

GDD rules: COMBAT-003

### 2026-08-22 — Punch Aim Assistance

Decision: For every enemy affected by a player punch, launch and trajectory preview use a persistent ECS-owned target lock. A ray from the source enemy along player facing selects or replaces the lock; misses retain it. If no ray target exists when aiming begins, the smallest-angle candidate within range supplies the initial lock. Leaving the punch volume clears the lock. Aim assistance does not alter the punch hit volume and a zero range disables it. Input-specific tuning remains unresolved under OQ-017.

Rationale: Launches remain deliberate and their previews remain honest while forgiving small directional aiming errors in dense crowds.

GDD rules: PLAYER-003, PLAYER-004

### 2026-08-20 — OQ-012

Decision: Elites are supported by the ordinary crowd. While an elite remains active, it always selects and periodically re-evaluates its closest eligible active normal as the projectile, without filtering that choice by player distance or spawn order. That projectile stops to anchor the setup, the elite repositions behind it, and other normal enemies clear the projectile-to-player firing corridor. After a launch, the elite keeps its cooldown but spends it approaching the next closest projectile rather than chasing the player.

Rationale: The elite adds a spatial crowd-management threat: ordinary enemies visibly arrange a shot for it, giving the player a readable opportunity to disrupt or exploit rather than merely fighting a larger normal enemy.

GDD rules: ENEMY-009

### 2026-08-09 — Zero-Health Launched Re-Punch

Decision: A zero-health enemy whose defeat is deferred while `Launched` remains eligible for player punches. Each re-punch begins a new launch sequence and extends its physical-projectile opportunity, but health remains clamped at zero and the enemy enters `Defeated` when that launch ends.

Rationale: Re-punching preserves player control over an enemy body while it remains part of the core launch simulation, regardless of whether its ordinary combat health is exhausted.

GDD rules: COMBAT-011, COMBAT-014

### 2026-08-02 — OQ-005

Decision: A solver-estimated impulse threshold independently determines whether one launched enemy damages an active or recovering target. Collision damage is a multiplier of the player punch damage that originated the launch chain; impulse increases that multiplier up to a cap, and propagated enemies inherit the originating value. A source-target pair can deal collision damage once per continuous source launch, while the source can damage multiple targets. Launched-versus-launched collisions are initially excluded. Launch propagation resolves before damage and uses its own threshold.

Rationale: This creates readable, physically grounded chain damage while preventing sustained-contact damage loops and preserving launch propagation as a distinct outcome.

GDD rules: COMBAT-002, COMBAT-003, COMBAT-004, COMBAT-009, COMBAT-011, COMBAT-012

### 2026-08-02 — OQ-004

Decision: Ordinary enemies use the minimal `Active`, `Launched`, `Recovering`, and `Defeated` lifecycle. Damage reduces health and zero health causes defeat, except that defeat is deferred while launched. A zero-health launched enemy remains an eligible physical projectile and enters `Defeated` directly when launch ends instead of recovering. Launch propagation does not itself imply damage.

Rationale: Deferring defeat preserves the launched body as the core physical projectile and keeps health resolution consistent without interrupting valid chain reactions.

GDD rules: COMBAT-001, COMBAT-003, COMBAT-004, COMBAT-010, COMBAT-011

### 2026-08-01 — OQ-006

Decision: When an enemy is inside the player's punch volume, show a short semitransparent line from the enemy in its initial post-hit direction. Show a line for each enemy the punch would affect, and do not predict later collisions.

Rationale: This gives immediate directional certainty at the decision point while keeping the preview local and honest about downstream crowd physics.

GDD rules: PLAYER-003, PLAYER-004, INFO-005
