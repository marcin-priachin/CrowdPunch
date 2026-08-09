# Crowd Punch — Open Design Questions

Last updated: 2026-08-02

These questions are deliberately unresolved. Agents must not infer answers from prototype code, inspector values, placeholder assets, or genre convention.

## Priority 1 — Needed For The Next Playable Slice

### OQ-001 — Prototype Success Criteria

What crowd size, frame-rate target, target hardware, and minimum chain-reaction length should the first representative physics test prove?

### OQ-002 — First Enemy Set

Which four enemy types best prove crowd composition without creating excessive per-enemy complexity?

### OQ-003 — First Two Weapons

Which two weapons provide meaningfully different launch geometry while preserving one coherent physical combat language?

## Priority 2 — Needed For A Complete MVP Run

### OQ-007 — Boss Interaction

What crowd-mediated physical opportunity damages the first boss, and how does the boss change the crowd state during the fight?

### OQ-008 — Route Structure

What meaningful choice distinguishes the map's two or three paths: enemy composition, effect source, weapon opportunity, risk, reward, or route geometry?

### OQ-009 — Progression Model

What changes during a run, what persists between runs, and what—if anything—is meta-progression?

### OQ-010 — Weapon Ownership

Are weapons permanent unlocks, temporary pickups, run-start choices, replaceable equipment, limited-use opportunities, or another model?

### OQ-011 — Effect Grammar

What is the smallest shippable effect set, and what general transformation/event table governs collisions between those effects?

### OQ-012 — Elite Role

Does the MVP require elites in addition to four normal enemy types and the boss? If so, what decision do they add that a larger normal enemy cannot?

### OQ-013 — Encounter Pacing

How are traversal, crowd encounters, recovery, choices, and the boss distributed across a 15–20 minute run?

## Priority 3 — Validate After The Core Is Fun

### OQ-014 — Combination Reference UI

How many discoverable combinations exist before a compact reference becomes more helpful than environmental learning?

### OQ-015 — Art Direction

What visual style best preserves silhouettes, launch direction, effect ownership, and large-crowd performance?

### OQ-016 — Camera

What camera angle, distance, and dynamic behavior best preserve positioning accuracy while showing enough of a large crowd?

### OQ-017 — Input Targets

Which controller and keyboard/mouse schemes are primary, and what aim assistance is appropriate for each?

### OQ-018 — Difficulty Scaling

Should difficulty grow mainly through crowd composition, density, speed, route risk, effect interactions, or boss behavior?

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
