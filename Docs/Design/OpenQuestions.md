# Crowd Punch — Open Design Questions

Last updated: 2026-09-24

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
