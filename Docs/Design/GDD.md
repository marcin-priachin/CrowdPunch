# Crowd Punch — Codex Game Design Document

Status: Working design baseline  
Last updated: 2026-08-01

## How To Read This Document

Each accepted rule has a stable ID and a strength:

- **Must** — required unless the design is explicitly revised.
- **Should** — intended default; deviation needs a stated design reason.
- **May** — allowed or optional.
- Unresolved decisions live in `OpenQuestions.md` and must not be invented during implementation.

Numeric tuning values in the current prototype are not design requirements unless this document says otherwise.

## Vision

### VISION-001 — Core Fantasy

Status: Must

The player is a powerful close-range fighter facing crowds of much smaller enemies. The central pleasure is not simply defeating enemies with punches; it is launching bodies through the crowd and deliberately creating useful collision chains.

### VISION-002 — Core Skill

Status: Must

The player reads crowd geometry, positions for an angle, and launches an enemy in a chosen direction so that the resulting body interacts with other enemies, effects, hazards, or objectives.

### VISION-003 — Emergent Complexity

Status: Must

Mechanical complexity should emerge primarily from enemies colliding with one another, effect propagation, and combinations of crowd states. Controls and systems outside the crowd interaction should remain clean and simple.

### VISION-004 — Physical Coherence

Status: Must

Player attacks, enemies, knockback, collisions, and propagated effects must belong to one coherent physical simulation. Crowd reactions must not read as disconnected scripted damage events.

### VISION-005 — Readable Chaos

Status: Must

Large crowds may make the screen chaotic, but the player's own action, launch direction, important threats, and major chain reactions must remain legible.

## Core Loop And Run Structure

### LOOP-001 — Moment-To-Moment Loop

Status: Must

The player approaches or is pressured by a crowd, chooses position and launch direction, strikes an enemy, observes the physical chain reaction, and repositions to exploit the changed crowd state.

### LOOP-002 — Run Goal

Status: Must

A run culminates in reaching and defeating a boss. The route to the boss should offer two or three paths rather than one strictly linear corridor.

### LOOP-003 — Run Duration

Status: Should

The MVP should target a complete run of approximately 15–20 minutes.

### LOOP-004 — Failure

Status: Must

The player has health and loses the run when it reaches zero. Taking damage makes positioning a risk decision rather than a cost-free optimization puzzle.

### LOOP-005 — Short Interruptions

Status: Should

Any pause, selection, or non-action interruption during a run should be very short.

## Player And Controls

### PLAYER-001 — Immediate Fundamentals

Status: Must

The player should be able to use every fundamental mechanic from the beginning of a run. Weapons may be an exception because their acquisition and permanence are unresolved.

### PLAYER-002 — Player Movement

Status: Must

The player can move freely and use a directional dash for rapid repositioning. While dashing, the player faces the committed dash direction rather than camera-forward.

### PLAYER-003 — Attack Direction Preview

Status: Must

Before an attack, when an enemy is inside the player's punch volume, show a short semitransparent line from that enemy along its initial post-hit trajectory. Show one line per enemy that the punch would affect. Each line covers only the first few metres and must not attempt to predict the full collision chain.

### PLAYER-004 — Directional Certainty

Status: Must

When an attack is committed, the player should not be uncertain about its initial launch direction. Later deviations caused by real collisions are desirable physical outcomes, not aiming ambiguity.

### PLAYER-005 — Punch During Dash

Status: Must

- Punch input during dash is buffered.
- A punch requested before the dash midpoint is buffered and begins once normalized dash progress reaches `0.5`.
- A punch requested at or after the midpoint begins immediately when normal punch eligibility permits it.
- A punch begun during a dash uses the committed dash direction as its attack direction.
- Dash punches have independently configurable launch strength and damage from normal punches while retaining the normal targeting and resolution pipeline.
- Beginning the dash punch ends dash movement.
- A buffered punch is discarded if the dash ends or is interrupted before it can execute, and cannot carry into a later dash.
- Merely contacting an enemy while dashing does not automatically attack it.

### PLAYER-006 — Conventional Combos

Status: Must Not

Do not add conventional multi-button or timing-string attack combos. Combination depth should come from crowd interactions.

### PLAYER-007 — Clean Supporting Mechanics

Status: Must

Movement, aiming, weapons, progression, and other non-crowd mechanics must not accumulate complexity merely to match the depth produced by crowd combinations.

### PLAYER-008 — Punch Area Feedback

Status: Must

When a normal or dash-buffered punch begins, show a semitransparent world-space shape matching the committed punch area for `0.5` seconds. The shape retains the punch's committed world-space origin and direction for its full display interval, including the committed dash direction for a dash punch.

## Combat And Crowd Physics

### COMBAT-001 — Enemies As Projectiles

Status: Must

An enemy launched by the player becomes a physical projectile capable of affecting other enemies through collision.

### COMBAT-002 — Enemy-Enemy Collision

Status: Must

Enemies physically collide with one another. Collision outcomes must preserve believable momentum and produce gameplay consequences rather than visual overlap only.

### COMBAT-003 — Chain Reactions

Status: Must

A player-caused launch may continue through enemy-enemy collisions as a chain reaction. The system should reward deliberately choosing the first target and direction.

### COMBAT-004 — Unified Resolution

Status: Must

Direct hits, launched bodies, environmental collisions, and effect collisions should use shared state and physics rules wherever practical. Avoid parallel special-case simulations that create contradictory outcomes.

### COMBAT-005 — Positioning Risk

Status: Must

Getting into a strong launch position can expose the player to damage. The safest position should not always be the tactically optimal one.

### COMBAT-006 — Normal Enemy Simplicity

Status: Must

Individual normal enemies should be mechanically and informationally simple. Interesting behavior should emerge from crowd composition, spatial relationships, and collisions rather than long per-enemy move lists.

### COMBAT-007 — Boss Damage Model

Status: Must

The boss is not defeated by repeatedly applying ordinary direct player damage. Boss damage must be mediated through the game's crowd-launching interaction, a boss-specific physical opportunity, or both.

### COMBAT-008 — Boss Legibility

Status: Must

Bosses and elite enemies may use dedicated UI or stronger telegraphs because they are rare, decision-critical targets.

### COMBAT-009 — Tuning Discipline

Status: Should

Introduce new tuning variables only when an existing rule cannot express the desired behavior. The game already depends on many physical variables; additional parameters must earn their complexity.

## Effects And Combinations

### EFFECT-001 — Collision Propagation

Status: Must

Effects carried by enemies propagate through qualifying physical collisions.

### EFFECT-002 — Different-Effect Collision

Status: Must

When enemies carrying different effects collide, the interaction either transforms an effect or creates a distinct event such as an explosion or storm.

### EFFECT-003 — General Rule Before Catalogue

Status: Must

Effect content must be designed from a small, consistent collision grammar. Do not implement isolated pair-specific reactions without first identifying the reusable rule they instantiate.

### EFFECT-004 — Combination Discoverability

Status: Should

Players should be able to understand a combination through concise audiovisual cause and effect. A separate explanation UI is optional and depends on the eventual number of combinations.

### EFFECT-005 — Combination UI Threshold

Status: May

If the final combination set becomes too large to learn naturally, provide a compact reference or discovery record. Do not add it during the MVP merely in anticipation of scale.

## Enemies And Information

### INFO-001 — No Normal-Enemy UI

Status: Must

Do not attach health bars, names, status icons, targeting markers, or other persistent UI to normal enemies. There will be too many of them, and such UI would add noise to an already busy screen.

### INFO-002 — Minimal HUD

Status: Must

Keep the HUD to information needed for immediate decisions. Prefer world animation, silhouette, color, motion, and effects over additional HUD elements.

### INFO-003 — Elite Exception

Status: May

Elites and bosses may display dedicated UI when the information cannot be communicated reliably in-world.

### INFO-004 — Threat Readability

Status: Must

Enemy types and dangerous states must remain distinguishable at crowd scale without requiring the player to inspect individual units.

### INFO-005 — Trajectory Scope

Status: Must

Trajectory UI predicts the attack's initial direction only. It should not draw a complete future ricochet path or pretend to predict chaotic downstream collisions.

## Progression And Weapons

### PROGRESSION-001 — Mechanics Before Modifiers

Status: Must

Progression must not withhold the game's fundamental crowd-manipulation mechanics merely to create unlocks.

### PROGRESSION-002 — Preserve Core Readability

Status: Must

Upgrades may change tactical possibilities, but must not obscure the connection between player input, launched target, physical collision, and resulting effect.

### WEAPON-001 — Weapon Simplicity

Status: Must

Weapons should remain easier to understand than the crowd combinations they enable. Avoid separate elaborate combo trees per weapon.

### WEAPON-002 — Weapon Permanence

Status: Unresolved

Whether weapons are permanent possessions, temporary run pickups, replace one another, or use another acquisition model is intentionally undecided. See `OpenQuestions.md`.

## MVP

### MVP-001 — Map

Status: Must

One playable map with two or three routes leading toward the boss.

### MVP-002 — Boss

Status: Must

One complete boss encounter that proves crowd-mediated boss interaction.

### MVP-003 — Enemy Roster

Status: Must

Four enemy types. Their exact identities and distribution remain unresolved.

### MVP-004 — Weapons

Status: Must

Two weapons. Their exact identities and acquisition model remain unresolved.

### MVP-005 — Complete Run

Status: Must

The MVP must support a start-to-boss-to-win-or-loss run rather than only a combat sandbox.

## Explicit Non-Goals For The Current Baseline

- Conventional attack combo strings.
- Individual UI for normal enemies.
- A large encyclopaedia before the number of effect combinations justifies it.
- Unlocking basic movement or core crowd-manipulation mechanics during a run.
- Direct ordinary attacks as the complete boss solution.
- Additional maps, bosses, enemy types, or weapons before the MVP loop is proven.
