# Crowd Punch — Codex Game Design Document

Status: Working design baseline  
Last updated: 2026-08-03

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

The player can move freely and use a directional dash for rapid repositioning. Dash movement remains committed to its initial direction, while the player continues to face camera-forward.

### PLAYER-003 — Attack Direction Preview

Status: Must

Before an attack, when an enemy the player can launch is inside the player's punch volume, show a short semitransparent line from that enemy along its initial post-hit trajectory. This includes living enemies in `Active`, `Launched`, or `Recovering`; the indication must not depend on the enemy being `Active`. Show one line per enemy that the punch would affect. Each line covers only the first few metres and must not attempt to predict the full collision chain.

### PLAYER-004 — Directional Certainty

Status: Must

When an attack is committed, the player should not be uncertain about its initial launch direction. Later deviations caused by real collisions are desirable physical outcomes, not aiming ambiguity.

Player punches use configurable range-based aim assistance. When an enemy first enters the live punch volume, cast a ray from that enemy along the player's facing direction for the configured assist range. A ray-hit enemy becomes that source enemy's locked launch target; later valid ray-hit enemies replace the lock, while a ray miss retains it. If the initial ray has no enemy target, use the eligible enemy within range with the smallest planar angle from the facing direction as the initial lock. The lock clears when the source leaves the punch volume. Launch and preview both point from the source toward its current locked target. An assist range of zero disables this behavior. Aim assistance changes launch direction only and does not expand or redirect the punch volume.

### PLAYER-005 — Consistent Punch During Dash

Status: Must

- Punch input during a dash follows the same eligibility, timing, strength, damage, targeting, and resolution rules as punch input outside a dash.
- An eligible punch begins immediately and does not interrupt or otherwise alter dash movement.
- While dashing, camera-forward facing determines the punch direction under the normal punch rule, independently of the committed dash movement direction.
- Merely contacting an enemy while dashing does not automatically attack it.

### PLAYER-006 — Conventional Combos

Status: Must Not

Do not add conventional multi-button or timing-string attack combos. Combination depth should come from crowd interactions.

### PLAYER-007 — Clean Supporting Mechanics

Status: Must

Movement, aiming, weapons, progression, and other non-crowd mechanics must not accumulate complexity merely to match the depth produced by crowd combinations.

### PLAYER-008 — Punch Area Feedback

Status: Must

When a punch begins, show a semitransparent world-space shape matching the committed punch area for `0.5` seconds. The shape retains the punch's committed world-space origin and direction for its full display interval.

### PLAYER-009 — Punch Cooldown

Status: Must

Punches use a configurable cooldown that begins when a punch is committed. Punch input rejected by cooldown does not alter dash movement.

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

### COMBAT-010 — Enemy Health And Defeat

Status: Must

Ordinary enemies have current and maximum health. Valid damage reduces current health, clamped at zero. An enemy at zero health enters `Defeated`, no longer acts, attacks, receives ordinary damage, becomes launched, or participates as an active target, and is removed through the established pooling lifecycle. Numerical health and damage values are provisional tuning rather than final balance requirements.

### COMBAT-011 — Deferred Defeat While Launched

Status: Must

An enemy whose health reaches zero while `Launched` remains a physical launched projectile and may continue eligible launch propagation under the normal collision rules. It remains eligible for another player punch while launched; that punch starts a new launch sequence but cannot reduce health below zero. When its launch would otherwise end, it enters `Defeated` directly instead of `Recovering`; it must never return to `Active`. Launch propagation and damage are separate outcomes, so an ordinary enemy collision does not implicitly deal damage.

### COMBAT-012 — Launched-Enemy Collision Damage

Status: Must

A sufficiently forceful collision from one `Launched` enemy into an `Active` or `Recovering` enemy deals damage scaled from the player punch damage that originated the launch chain. Solver-estimated impulse determines eligibility and increases the damage multiplier up to a cap. Launch propagation and damage use independent thresholds and outcomes. During one continuous source launch, a source-target pair may deal collision damage once, while that source may damage multiple different targets. Collisions between two already-launched enemies do not deal this damage. Propagated enemies inherit the originating punch damage, and propagation is established before collision damage is evaluated so lethal damage on the target follows deferred-defeat rules. Numerical thresholds and multipliers are provisional.

When an enemy becomes `Launched` through ordinary enemy-to-enemy propagation, use its solver-produced horizontal velocity as the initial direction and search within the configured propagation aim-correction radius for a living `Active` or `Recovering` enemy. Rotate horizontal velocity toward the candidate with the smallest planar angle from that initial direction, preserving horizontal speed and vertical velocity. Distance and entity index resolve angular ties. The collision source and newly launched enemy are excluded. A radius of zero disables correction.

### COMBAT-013 — Active Enemy Separation

Status: Must

Active enemies continuously try to maintain local separation from nearby active enemies while wandering and charging. Separation influences movement intent but must not disable enemy-enemy collisions, override launch physics, or prevent crowd compression caused by the arena, the player, or other physical forces.

### COMBAT-014 — Re-Punching Non-Active Enemies

Status: Must

A `Launched` enemy remains a valid player-punch target even at zero health while defeat is deferred. A living `Recovering` enemy also remains valid. A new punch starts a new launch sequence and replaces the prior launch's cause, damage, recovery timing, propagation inspection data, linear velocity, and angular velocity with the new punch data and impulse. Existing momentum must not add to or cancel the re-punch; the result begins from rest as if the enemy were stationary. Zero-health enemies in any phase other than `Launched` remain ineligible.

### COMBAT-015 — Enemy-Owned Launched Bodies

Status: Must

A body launched by an enemy can damage the player as a physical projectile. It uses the same originating launch damage, minimum impact-impulse threshold, impulse-scaled damage multiplier and cap, and once-per-continuous-source-launch rule used when a launched body damages an ordinary enemy. Enemy ownership propagates through qualifying enemy-enemy launch collisions. A player punch on any launched body begins a new player-owned launch sequence and removes this launched-body threat to the player, including from later propagation in that sequence. Independent rules such as explosions remain independently capable of damaging the player.

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

### ENEMY-001 — Ranged Enemy Positioning

Status: Must

The ranged enemy maintains a provisional preferred distance band from the player: it retreats when too close, approaches when too far away, and avoids unnecessary forward movement inside the band. It retains normal crowd separation and cannot use normal movement while launched, recovering, defeated, disabled, or pooled. Numerical distance and movement values are provisional.

### ENEMY-002 — Ranged Enemy Attack

Status: Must

While active, in range, and off cooldown, the ranged enemy winds up and fires one visible physical projectile toward a fire-time prediction of the player's position based on the player's current movement velocity and the projectile's configured speed. A configurable movement-lead multiplier adjusts or disables that prediction. Configurable horizontal spread prevents groups from focusing every shot on exactly one point, and configurable per-shot cooldown variation reduces synchronized group cadence. The shot does not home after launch. Entering `Launched`, `Recovering`, or `Defeated`, or becoming disabled or pooled, cancels a pending wind-up. Numerical wind-up, cooldown, movement lead, spread, damage, and cadence values are provisional.

### ENEMY-003 — Ranged Projectile Counterplay

Status: Must

The ranged projectile follows a readable arc over the crowd at a consistent configured horizontal speed and continues along the same trajectory after crossing its sampled aim point until impact or cleanup below a provisional minimum altitude. It passes through ordinary enemies without damaging, pushing, launching, or otherwise affecting them, and damages the player at most once through the normal player damage and invulnerability rules. The player avoids the shot by moving out of its fixed trajectory after it is fired. Projectile speed, arc height, minimum altitude, collision radius, damage, and lifetime are provisional.

### ENEMY-004 — Ranged Enemy Baseline Rules

Status: Must

Except for distance-keeping and its ranged attack, the ranged enemy follows the baseline ordinary-enemy health, mass, launchability, collision-chain participation, recovery, deferred defeat, pooling, and respawn rules.

### ENEMY-005 — Dasher Commitment

Status: Must

The Dasher maintains distance, stops for a readable configurable telegraph, samples and locks its direction when the dash begins, and travels without steering until its maximum distance or a static obstruction. Player and ordinary-enemy contact does not end the dash. It then enters a readable recovery window. Dash-path enemy avoidance is one authored policy: none, between the Dasher and player, or between and behind the player.

### ENEMY-006 — Dasher Impacts

Status: Must

An intentional dash passes through enemies without damaging, knocking back, launching, or otherwise affecting them. It damages and knocks back the player at most once per dash. Player damage remains independently configurable from player knockback. Enemy contacts do not redirect or end the committed dash. Only a Dasher in the shared `Launched` phase affects and launches struck enemies.

### ENEMY-007 — Launched Dasher

Status: Must

A player punch takes priority over every Dasher phase and enters the normal `Launched` lifecycle. While launched, the Dasher uses its own readable dash motion language, deals separately configured collision damage and knockback, launches ordinary targets into the shared chain pipeline, and preserves its trajectory through ordinary enemies. Static geometry still resolves through normal launched physics. Elite and boss momentum preservation is separately configurable.

### ENEMY-008 — Dasher Baseline Rules

Status: Must

Except for its authored dash and launched-impact modifiers, the Dasher follows ordinary-enemy health, damage, deferred defeat, recovery, pooling, and respawn rules.

### ENEMY-009 — Elite Crowd Support

Status: Must

While at least one non-defeated elite is active in the arena, active normal enemies cooperate with the nearest active elite to make its projectile punch easier to execute. The closest eligible active normal enemy to the elite is always selected as its projectile regardless of spawn order or its distance from the player, and that choice is re-evaluated during setup at the elite's retarget interval. Before waiting, the projectile adjusts to the nearest sampled staging position whose path and resulting elite-to-behind-projectile approach lane are not blocked by static geometry or another non-defeated enemy. It stops only once that lane is clear, anchoring a stable setup position; until then, the elite waits and does not consume its setup timeout. The elite then repositions behind it within punch tolerance and steers through a side waypoint whenever a direct approach would cross the target's collision clearance, avoiding premature target contact before alignment. After launching a projectile, the elite retains its attack cooldown but uses that interval to approach the next closest eligible active normal rather than resuming ordinary player chase. Other active normal enemies steer laterally out of the projectile-to-player shot corridor. Launched, recovering, defeated, disabled, and pooled enemies do not perform this support movement. With multiple active elites, each normal enemy supports the nearest elite, with entity index used only as a deterministic exact-distance tie break.

### INFO-001 — No Persistent Normal-Enemy UI

Status: Must

Do not attach persistent health bars, names, status icons, targeting markers, or other persistent UI to normal enemies. There will be too many of them, and such UI would add noise to an already busy screen. A normal enemy may show a temporary health bar for one second after receiving damage; another damaging hit refreshes that interval.

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
- Persistent individual UI for normal enemies.
- A large encyclopaedia before the number of effect combinations justifies it.
- Unlocking basic movement or core crowd-manipulation mechanics during a run.
- Direct ordinary attacks as the complete boss solution.
- Additional maps, bosses, enemy types, or weapons before the MVP loop is proven.
