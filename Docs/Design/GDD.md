# Crowd Punch — Codex Game Design Document

Status: Working design baseline  
Last updated: 2026-10-21

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

### VISION-006 — Full Cel Art Direction

Status: Must

Use the approved Art Lab Full cel shading look as the default for environment surfaces,
player and enemy shading, lighting, and post-processing throughout the campaign and game
prefabs. Preserve chapter-specific colours, readable effect ownership, existing animation
and deformation, and the distinct visual cues of hazards and objectives. The default must
work in player builds without the Editor's Art Lab preview.
Use hard cast shadows that complement the cartoony surfaces. Keep geometry at its
highest level of detail and disable automatic LOD switching to avoid detail and shadow popping.
Chapter One's fixed environment uses baked hard shadows and static geometry. Its runtime
shadow maps are disabled to remove camera-distance popping and crowd shadow-rendering cost.
Moving characters, rotating cover and destructible objects must not leave baked caster
shadows behind when their state changes.

## Core Loop And Run Structure

### LOOP-001 — Moment-To-Moment Loop

Status: Must

The player approaches or is pressured by a crowd, chooses position and launch direction, strikes an enemy, observes the physical chain reaction, and repositions to exploit the changed crowd state.

### LOOP-002 — Run Goal

Status: Must

The campaign contains 81 levels: the existing eight chapters of ten plus a one-level ninth chapter containing the final encounter (FINALE-001..007). Every tenth level in Chapters 1-8 is a boss. Introduce Gatekeeper, Chicken, Rolling Blob and Dino in Chapters 1-4 and revisit them in that order in Chapters 5-8. Introduce the original eight enemy archetypes, objectives and static hazards by level 40; the four additional enemies follow LOOP-009. Each level has one main objective. Use distinct authored enclosed layouts; retain each boss's existing arena geometry for introduction and rematch. Compact layouts are the default, with protected-point lanes, TRACK-006's progressive routes, BARRICADE-007's room chains and LOOP-008's large crowd arenas as exceptions. The accepted encounter table is [Campaign80Proposal.md](Campaign80Proposal.md); implementation proceeds in chapter batches.

Existing numbered gauntlet placements and single-introduction restrictions elsewhere in this document describe the preserved legacy encounters. They do not constrain campaign placement or repetition. Mechanic eligibility, damage, ownership, ammunition and lifecycle rules remain mandatory. Preserve all 27 legacy encounters outside campaign progression and player builds; retain them in the existing Level Play Editor window without modifying campaign saves. Campaign wave and objective/boss tuning must not overwrite legacy assets or shared ordinary-enemy profiles.

### LOOP-003 — Run Duration

Status: Should

Most ordinary non-boss encounters target 1-2 minutes; introductions may be shorter. Large crowd levels follow LOOP-008. Boss introductions target 2-3 minutes and harder rematches 3-5 minutes. All durations are balance targets, not timers or enforced minimums. The former 15-20 minute total-run target is superseded.

### LOOP-004 — Failure

Status: Must

Zero player health fails only the current level. Retry restores that level and full player health while preserving completed levels and chapter unlocks. Every level entry restores full health. Existing objective failure rules also fail only the current level.

### LOOP-005 — Short Interruptions

Status: Should

Any pause, selection, or non-action interruption during a run should be very short.

### LOOP-006 — Gauntlet Transition

Status: Must

Ordinary campaign completion advances to the next level. Chapter completion shows Continue and chapter selection; final campaign completion offers replay access. Completing a replay returns to level selection with Retry and Next Level available when loadable, and never lowers progress. Transitions remain short, clear previous encounter state, and place the player at the authored entry point. During chapter-by-chapter development, unlocks may refer to future content, but unavailable scenes must never be loaded or mistaken for full campaign completion.

### LOOP-007 - Campaign Persistence And Menus

Status: Must

Launch into a main menu with Continue, chapter/level selection and New Campaign. Confirm before clearing progress. Save completed levels and sequential unlocks locally; boss completion unlocks the next chapter. Continue resumes the next unfinished level. Previously unlocked levels remain replayable. Preserve keyboard/mouse and controller usability. Save format and stable identifiers are implementation choices. No cloud synchronization, economy or additional meta-progression is required.

Game settings may optionally start the game with all available levels unlocked. This option defaults to off, does not mark levels complete or alter saved unlock progress, and preserves Continue's next-unfinished-level behavior. Unavailable content remains unselectable.

### LOOP-008 - Large Crowd Level And Difficulty Rhythm

Status: Must

Each of Chapters 1-8 contains one 100 x 100 metre level with a larger enemy crowd and wave clearing as its only objective. Target longer than three minutes through sustained combat. The approved placement is levels 8, 18, 28, 38, 48, 58, 68 and 78, followed by shorter breathers before the bosses. Initial targets are 3.5-4.5 minutes in Chapters 1-4 and 4-5 minutes in Chapters 5-8. Counts and cadence are playtest tuning; no survival timer or forced delay is required.

Difficulty grows through composition, positioning, geometry, spawn directions, cadence and hazards, with relief between peaks and after bosses. Ordinary enemy health, damage and speed remain consistent. Larger counts in these eight arenas complement this encounter design rather than becoming the sole difficulty mechanism.

### LOOP-009 - Later Enemy Introductions And Recurrence

Status: Must

Introduce LinkedPair in Chapter 5 at level 41, Dragon in Chapter 6 at 52, Snake in Chapter 7 at 61 and Ninja in Chapter 8 at 71. Each remains present in every subsequent chapter. Use the accepted 2026-10-14 compositions and recurrence in [Campaign80Proposal.md](Campaign80Proposal.md): 15 finite wave-clear encounters, with Baselines as the only supporting archetype in the four introductions, followed by mixed practice and the chapter's large arena. Keep all 80 level identifiers, objectives, boss order, saves and unlocks intact.

Use the existing LINK-001..007, DRAGON-001..007, SNAKE-001..007 and NINJA-001..006 gameplay and shared settings. Count LinkedPair in pairs, with two independently defeated endpoints per pair. Count Snake in heads for clearance; its attached and detached orbs never become separate completion objectives. Mixed-wave composition replaces Baselines to preserve the previous initial physical-body budget, including current six-segment Snake tails. Keep the existing two-concurrent-ambush Ninja cap. Counts and duration targets remain playtest tuning under LOOP-003/008; no new completion waits, forced delays or shared stat escalation are introduced. The approved large waves at 48/58/68/78 spawn together and clear before the next begins.

This campaign integration is additional scope to the original legacy-only enemy implementation slices. Preserve Gauntlet_24..27's recipes, assets, Level Play access and exclusion from campaign progression/player builds. Their original placement instructions do not exclude the four enemy prefabs from campaign encounters authorized here.

## Final Encounter - Level 81

The accepted detailed handoff is [FinalEncounter.md](FinalEncounter.md), from the design conversation dated 2026-10-14. These rules deliberately append to the original 80-level campaign; numerical values remain editable tuning.

### FINALE-001 - Campaign And Arena

Status: Must

Append stable cp-081 in a one-level ninth chapter, unlocked by level 80. Preserve all previous IDs, scenes and saved progress. Final campaign completion requires level 81 victory. Use a 100 x 100 metre arena and the supplied Goleling_Evolved model. Continue, chapter selection, replay and Level Play support the shorter chapter. Target 6-8 minutes and roughly 6-8 downings without enforcing either.

### FINALE-002 - Anchor Lifecycle

Status: Must

Exactly four anchors are simultaneously usable during flight: rotating cover, explosion shell/core, short bidirectional rail, and narrow launched-hit target. Any one completed anchor cancels all pending airborne attacks and tethers the boss to a nearby valid landing. Used anchors remain spent until flight resumes after the fourth use, when the full set resets. Unfinished damage and rail progress persist. All anchors pause during falling, downed and takeoff phases; no queued downing or extended window is permitted. Rail completion requires physical docking.

### FINALE-003 - Boss Cycle And Attacks

Status: Must

The immune flying boss smoothly follows above/ahead and uses telegraphed ground strikes and committed sweeping lanes. Its landing damages and pushes player/enemies, leaving anchors unaffected. Landing begins a fixed vulnerability window with an initial stun, then alternating telegraphed directional slams and radial shockwaves. Grounded boss remains stationary and unlaunchable. Takeoff is harmless and ends vulnerability. Three health-based stages increase airborne attack demands and specialist pressure without changing ordinary enemy stats.

### FINALE-004 - Anchor Ownership

Status: Must

Only player-owned launched bodies, chain descendants and explosions from player-owned launched sources advance anchors. Unlaunched proximity blasts and enemy/boss-owned bodies do not count. Direct punches only damage the exposed shell core. The shell-breaking blast cannot also damage the exposed core; core body impact and explosion remain separate. Reuse applicable per-launch histories and target/rail impact-blast deduplication.

### FINALE-005 - Boss Damage And Rebound

Status: Must

Only the downed vulnerability window accepts player-owned body, chain and associated explosion damage. Direct punches never damage the boss. Each body deals impact damage once per launch; re-punch starts a new eligible launch. Exploders can deal both impact and explosion damage. Ordinary bodies rebound and retain their ordinary launch behavior; outgoing rebounds aimed directly at the player are deflected sideways. Boss health persists across cycles, without per-downing damage caps.

### FINALE-006 - Bounded Supply

Status: Must

Replenish a bounded crowd throughout the fight, including every current enemy type over time. Keep reliable Baselines and Exploders near unfinished anchors and useful ammunition during downings. Account for two endpoints per LinkedPair and physical Snake tail bodies. Finale-owned population, per-type caps, stage recipes, delays and spawn banks are editable; shared ordinary profiles remain unchanged. OQ-001 hardware/FPS targets remain unresolved.

Crowd supply increases at each boss health stage, treating the existing full population as the final-stage maximum. Starting tuning reserves 37, 50 and 66 physical bodies respectively. Each anchor bank grows from 4 Baselines/1 Exploder to 6/1, then 8/2; specialist additions remain staged. This phase scaling was requested on 2026-10-14.

### FINALE-007 - Victory, Retry And Feedback

Status: Must

Boss death immediately ends hostile combat, cancels attacks and replenishment including pending allocations, neutralizes survivors/effects, then presents a short victory before campaign completion. No survivor clear is required. Retry restores full player/boss health, flight, anchors, histories, cover clocks, rail state and supply. Scene exit cleans all encounter state. Provide a brief nonblocking opening instruction and in-world active/paused/spent/damaged feedback.

The final boss has a long red health bar centered at the top of the screen, matching the player's thin bar style with no text or background. Clearly distinguish vulnerability with a cyan boss glow and a pulsing ground ring. This HUD exception and its revised appearance were requested on 2026-10-14.

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

Player punches use configurable range-based aim assistance limited by a configurable maximum horizontal angle from player facing. When an enemy first enters the live punch volume, cast a ray from that enemy along the player's facing direction for the configured assist range. A ray-hit enemy inside the angular limit becomes that source enemy's locked launch target; later valid ray-hit enemies replace the lock, while a ray miss retains it only while it remains within both limits. If the initial ray has no enemy target, use the eligible enemy within the range and angular limits with the smallest planar angle from the facing direction as the initial lock. Protected Armored enemies do not originate a launch preview, but remain valid targets for aim assistance and launched-body homing from another body. The lock clears when the source leaves the punch volume. Launch and preview both point from the source toward its current locked target. An assist range of zero disables this behavior. Aim assistance changes launch direction only and does not expand or redirect the punch volume.

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

### PLAYER-008 — Punch Cooldown Feedback

Status: Must

Represent punch cooldown on the player model's `hand_r` bone rather than with a world-space punch-range shape. The hand scales from its authored cooldown multiplier to its ready multiplier as cooldown recovers. A hand-bound particle aura grows in intensity through cooldown and changes to its ready color when recovery completes. A second hand-bound particle effect flashes once at completion. The player holds the configured Ready Frame until an accepted punch input immediately skips to Contact Frame, before hit/miss acknowledgement. The cooldown animation then plays forward from the configured punch contact frame to the end of the clip, wraps to its beginning, and plays forward to the ready frame. Both segments fit within the selected cooldown duration. These cues are presentation only and follow the outcome-specific cooldown rule in PLAYER-009.

### PLAYER-009 — Punch Cooldown

Status: Must

Punches use a configurable hit cooldown when the punch hits at least one eligible enemy or an intact barricade, and a separately configurable miss cooldown when it connects with neither. Each cooldown begins when the matching punch result is acknowledged. A punch connecting with a protected Armored enemy or intact barricade activates the hit cooldown despite applying no damage, push, stagger, or launch to it. A zero duration disables that outcome's cooldown. Hitting multiple targets with one punch starts cooldown only once. Punch input rejected by cooldown does not alter dash movement.

### PLAYER-010 — Supported Input Methods

Status: Must

The MVP must be fully playable with both keyboard/mouse and gamepad. Movement, camera rotation, punch, and dash must remain available on both input methods. Input-specific tuning, including whether punch aim assistance differs between schemes, remains unresolved under `OQ-017`.

### PLAYER-011 — Cursor Capture

Status: Must

During active gameplay, the mouse cursor must be locked and hidden. Pausing must release and show the cursor for menu interaction, and resuming must lock and hide it again.

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

While a launched enemy has a target selected either by player aim assistance or propagated-launch aim correction, its horizontal velocity homes toward that target at the configured maximum degrees per second. Homing preserves horizontal speed and vertical velocity, stops when the target is no longer a living `Active` or `Recovering` enemy, and a turn rate of zero disables it.

### COMBAT-013 — Active Enemy Separation

Status: Must

Active enemies continuously try to maintain local separation from nearby active enemies while wandering and charging. Separation influences movement intent but must not disable enemy-enemy collisions, override launch physics, or prevent crowd compression caused by the arena, the player, or other physical forces.

### COMBAT-014 — Re-Punching Non-Active Enemies

Status: Must

A `Launched` enemy remains a valid player-punch target even at zero health while defeat is deferred. A living `Recovering` enemy also remains valid. A new punch starts a new launch sequence and replaces the prior launch's cause, damage, recovery timing, propagation inspection data, linear velocity, and angular velocity with the new punch data and impulse. Existing momentum must not add to or cancel the re-punch; the result begins from rest as if the enemy were stationary. Zero-health enemies in any phase other than `Launched` remain ineligible.

### COMBAT-015 — Launched Bodies Damage The Player

Status: Must

A launched body can damage the player as a physical projectile regardless of whether the player or an enemy originated its current launch sequence. It uses the same originating launch damage, minimum impact-impulse threshold, impulse-scaled damage multiplier and cap, and once-per-continuous-source-launch rule used when a launched body damages an ordinary enemy. After calculating impact damage, apply a configurable damage reduction only when the current launch owner is Player, including propagated player-owned chains. Elite-owned, boss-owned and other non-player launches receive no ownership reduction. Then clamp the final player damage to a separate configurable maximum health-point amount per launched-body impact, for every launch owner. This player-only cap must not alter damage to enemies or independent explosion splash. Expose both settings in Game Runtime Settings: the reduction is a zero-to-one fraction (zero preserves full damage; one prevents player-owned body damage), and the impact cap is nonnegative (zero prevents all launched-body damage to the player). The starting 50% reduction and 25-health-point cap are provisional tuning. Ordinary player invulnerability remains in force. Launch ownership continues to propagate through qualifying enemy-enemy launch collisions. A player punch on an already-launched body begins a new player-owned launch sequence and resets its once-per-launch player-impact eligibility. Independent rules such as explosions remain independently capable of damaging the player.

### COMBAT-016 — Arena-Wide Crowd Pressure

Status: Must

Ordinary melee enemies remain distributed across the arena while a limited configurable number of the closest eligible enemies actively approach and surround the player. The pressure allocation must retain arena-wide launch opportunities rather than drawing the whole crowd into one group. Ranged positioning, committed Dasher behavior, continuous Armored pursuit, and elite crowd support override this baseline allocation, and local enemy separation continues to influence movement in both roles.

### COMBAT-018 — Ground-Plane Enemy Physics

Status: Must

Enemies may fall from their initial spawn height until they reach the ground. After landing, enemy motion is constrained to the XZ plane at their grounded height; punches and collision responses must not lift them. A player punch that hits an enemy before it lands snaps that enemy to ground level and immediately applies the same constraint. Horizontal collision momentum is preserved. This vertical constraint also applies while launched; aim correction and homing do not override it.

### COMBAT-017 — Separate Arena And Defeat Bounds

Status: Must

Enemy spacing and arena-wide distribution use an authored movement volume that is independent from the authored out-of-bounds defeat volume. Leaving the spacing volume does not itself defeat an enemy; leaving the defeat volume requests pooling for respawning enemies or terminal defeat for fixed encounter enemies. The two volumes may use different centers and sizes so launched bodies can travel beyond the ordinary crowd area before being removed.

## Linked Pair Enemy

### LINK-001 - Flexible Paired Attack

Status: Must

Two normal endpoints have independent Baseline health from their spawn profile and a visible flexible tether. They straddle and sweep the player using normal obstacle navigation and crowd physics. The tether shortens freely; tension acts at its maximum length. Intact endpoints have no ordinary walking/recovery contact attack. Actual endpoint defeat breaks the tether and leaves the survivor as an ordinary independent enemy.

### LINK-002 - Shared Launch

Status: Must

A punch (including dash-punch), qualifying incoming launched body, or explosion launches both endpoints together. The struck endpoint leads and its partner trails and swings; both inherit the triggering launch ownership. Re-punching either flying endpoint switches the leader and replaces the pair's launch with player ownership. An explosion selects the endpoint nearest the blast as leader, travelling away from it; each endpoint takes its own eligible splash damage. Bodies keep existing collision-chain and target-specific interactions, aim assistance and initial-direction preview. Do not prescribe side-by-side translation, centre spinning, or a full swing-path preview.

### LINK-003 - Link Contact And Cover

Status: Must

The walking, launched and recovering link damages the player regardless of launch ownership. First contact is immediate, followed by interval ticks. One per-pair, per-victim clock controls all link hits, including bypass of ordinary player hit invulnerability. Endpoint-body damage keeps its own ordinary rules. Fast sweeps must not tunnel through victims. Solid obstacles block link damage; walking endpoints reposition to clear the connection. A flying snag keeps the tether and dissipates momentum; constraints must never pull bodies through solids.

### LINK-004 - Launched Link Enemy Responses

Status: Must

Walking/recovering links pass through enemies without effects. A launched link uses configurable DamageAndLaunch (default) or DamageAndPush on ordinary unlaunched victims. New flights inherit pair ownership and chain data. Already-launched victims receive damage only, preserving velocity and ownership. Intact armor ignores all link effects and never loses armor; preserve armor protection windows and existing protected Snake-part gates. Elites receive damage and push, never launch. Exploders detonate in either mode. Boss and puzzle targets receive no link effect; endpoint bodies keep their existing rules. Simultaneous endpoint hits and pair-on-pair propagation resolve coherently once per pair event.

### LINK-005 - Shared Flight And Recovery

Status: Must

The pair remains flying while either endpoint retains useful momentum, until both settle or the shared flight limit expires. Both recover together; surviving pairs remain connected and resume their attack. Zero-health endpoints remain attached while launch defeat is deferred, and defeat only when flight ends. The recovering link remains dangerous to the player.

### LINK-006 - Configurable Tuning And Presentation

Status: Must

Use dedicated pair settings for maximum length, tension, movement and straddle/sweep positioning, damaging width, player/enemy damage, tick interval, launch/push strength, shared flight limit/settling and recovery. Initial numeric values, visuals, effects and animations are delegated tuning. Use the supplied Mushnub model and its clips. Endpoint health remains in the spawn profile. Only launch/push are agreed alternate behavior modes.

### LINK-007 - Legacy Gauntlet_27

Status: Must

Introduce the pair in legacy Gauntlet_27 through Level Play, outside campaign progression and player builds. Two finite waves contain exactly 1 pair + 6 Baselines, then 2 pairs + 12 Baselines. Ordinary enemy clearance advances/completes the waves, including surviving independent endpoints. No replenishment. Use a compact arena with solid cover and flanking space. Retry restores intact healthy pairs, Baselines and clean clocks/flight state. Bounds, pooling, cleanup and scene exit leave no orphan links or blocked defeat accounting.

## Barricade Objective

### BARRICADE-001 - Break And Exit

Status: Must

Add Gauntlet_13 only, after Gauntlet_12. Its objective is to destroy one solid barricade with shared durability, then reach the exposed exit. Surviving enemies need not be cleared and remain hostile. Use a broad, unobstructed approach. Existing gauntlets retain their encounters and layouts.

### BARRICADE-002 - Counted Hits

Status: Must

Launched enemy bodies and explosions reduce a fixed hit count, initially three. Each enemy counts once per continuous launch; a re-punch starts a new eligible launch. Different bodies in the same chain each count. An exploder's body impact and associated explosion together count once. Eligible launch ownership is configurable in a settings asset, defaulting to all launched bodies, including elite-owned and boss-owned bodies and their descendants. Explosions can count independently of whether their source was launched. Direct player punches do no barricade damage but consume cooldown (PLAYER-009).

### BARRICADE-003 - Physical Response

Status: Must

Bodies rebound from an intact barricade and remain eligible for ordinary crowd collisions and propagation. The destroying body continues through the broken barricade. The intact barricade blocks player traversal; destruction exposes the exit. Rebound strength is configurable tuning.

### BARRICADE-004 - Existing Aiming And Feedback

Status: Must

An intact barricade participates as a target in the existing enemy aim-assist mechanics, with the same ray selection, lock retention, angular and range limits, and fallback selection. Launch and short initial trajectory preview agree (PLAYER-003/004); no separate barricade alignment mechanic is introduced. Propagated aim correction and launched-body homing also accept it. Broken barricades cease to be targets. Show distinct visual damage stages and impact/destruction effects, without a health bar. Show one brief nonblocking opening instruction through the existing tutorial presentation. Previewing the rebound path is not required.

### BARRICADE-005 - Bounded Replenishing Crowd

Status: Must

Continuously replenish a configurable bounded crowd of Baseline melee enemies with occasional Explosives while the barricade is intact. Stop pending and future replenishment immediately when it breaks; surviving enemies continue their normal behavior. Crowd composition, replenishment timing, visual effect parameters, and exit placement use configurable implementation defaults. Do not add a second harder barricade level or a finite-wave fallback ammunition system.

### BARRICADE-006 - Campaign Gate Width

Status: Must

Chapter 1's introductory campaign barricades retain their wide targets. From campaign level 13 onward, break-and-exit barricades must be 2 metres wide. Match the visible plate and damage-stage visuals to the collider. Solid terrain must seal the approach on both sides, connecting to the arena perimeter so the exit can only be reached through the gate opening after destruction. This campaign rule does not resize legacy gauntlets or other objective types.

### BARRICADE-007 - Progressive Campaign Room Chains

Status: Must

Every successive break-and-exit campaign level after level 13 adds one room-length segment
separated by another barricade. Levels 13/22/34/42/56/62/74 use 1/2/3/4/5/6/7 gates:
room -> barricade -> exit; room -> barricade -> room -> barricade -> exit; and so on.
Repeat each level's original room length, retain its width and encounter theme, and adjust
the enclosing layout and bounds to fit. Every gate follows BARRICADE-006's 2m width and
sealed side terrain, with an exposed front so terrain cannot shield damaging impacts.

Breaking an intermediate gate opens the next room without completing the level. Every
gate must be broken and the player must reach the sole exit beyond the final gate;
surviving enemies remain hostile and need not be cleared. Preserve the per-gate hit rules,
physical response, aiming, feedback and bounded replenishment in BARRICADE-002..005.
The original single-gate legacy introduction and campaign levels 4/6/13 remain unchanged.

The original repeated-layout defaults are superseded by BARRICADE-009's ascending sector
layouts and crowd mixes. A room starts its initial spawn delay only after the preceding gate is
broken and the player enters; activation persists on backtracking. Each broken gate stops
only its own room's supply. Retry resets the full level, including every gate and dormant
room. These staging/layout choices implement the requested room chain without adding a
second objective, survivor-clearing requirement or HUD counter.

### BARRICADE-008 - Spawn Room Movement

Status: Must

In progressive barricade levels, enemies' normal distribution, pursuit and navigation
stay within the segment where they spawned, including after its barricade breaks.
Enemies do not pursue the player into another room or take another room's attack slots.
Survivors remain hostile when the player returns. Replenishment retains the same room.
Physical launches and collision displacement may cross an opened gate; after recovery,
normal movement returns the body to its original segment without teleporting it.

### BARRICADE-009 - Distinct Ascending Sectors

Status: Must

The multi-sector campaign barricade levels must use different layouts with increasing
difficulty toward the final sector. Keep the final sector's current layout, terrain,
static hazards and gate difficulty; its enemy composition may change. Earlier sectors
must be easier, with broader clear routes, fewer/smaller obstacles, less hazardous ground
and lighter enemy pressure. Add enemy variety using existing profiles, retaining ample
replenishing Baselines for launching into gates and armor. The final sector has the
strongest crowd mix. Preserve campaign enemy introduction order, BARRICADE-008's spawn-room
movement, staged activation, bounded replenishment, all gate impact rules, retry and exit.

Implementation defaults use Baseline/Exploder crowds first, then Ranged, Dasher and Armored
specialists as pressure increases. Final populations retain their existing 14/14/16/14/16/18
limits in levels 22/34/42/56/62/74. Each earlier sector has a smaller increasing population;
final gates retain three hits in level 22 and four in later levels. Earlier hazard patches
are smaller periodic warnings, while the final patches remain unchanged. No extra enemy
health bars, survivor-clearing requirement or secondary objective is added.

## Rotating Cover Objective

### COVER-001 - Central Protected Target

Status: Must

Add Gauntlet_14 after Gauntlet_13, with one central target using the existing barricade hit-count,
eligible launch-source settings, direct-punch immunity and damage feedback. A solid enclosure
keeps the player and walking enemies outside. Launched bodies can reach the target through
one rotating opening. Earlier gauntlet encounters and layouts remain unchanged.

### COVER-002 - Configurable Rotation

Status: Must

Expose continuous constant-speed rotation (default), rotate/pause/repeat, and continuous rotation
with periodic reversals. Expose successful-hit responses: no change (default), briefly pause,
or increase rotation speed with accumulated damage. Opening width, rotation rates, durations,
required hits and wave composition are configurable. Exact starting values are implementation tuning.

### COVER-003 - Return Shots

Status: Must

Shield impacts redirect launched bodies toward the player's position sampled at impact, without
homing. Multiply the incoming speed by a configurable float, default 1.0. Preserve the existing
continuous launch, ownership, collision history, damping and recovery state; do not reapply a full
launch impulse or reset recovery. Bodies retain ordinary crowd collisions, chain reactions and
player damage. The implementation clarification on 2026-09-28 explicitly preserves the existing
momentum-based travel model rather than introducing an explicit remaining-distance budget.

### COVER-004 - Explosions And Aiming

Status: Must

Exploders reflect from cover without detonating there; ordinary collision behavior resumes on the
return path. Explosions outside the enclosure cannot damage the protected target. An exploder
must be launched through the opening into the enclosure; its target impact and explosion share
one counted hit. Reuse normal target aim assistance even while covered, without steering around
the shield. Keep the existing initial trajectory preview, without rotation or reflection prediction.

### COVER-005 - Persistent Progress And Completion

Status: Must

Require multiple successful hits with a configurable count and no timeout or progress reset.
Use the existing bounded replenishing-wave infrastructure while the target lives. Target destruction
immediately completes the level through normal gauntlet completion; neither reaching an exit nor
clearing survivors is required. Restart/retry restores the target, cover motion, crowd and completion.

## Shell Objective

### SHELL-001 - Crack The Shell Introduction

Status: Must

Append Gauntlet_15 after the existing levels. One stationary central target is accessible from
all sides. Use only Baseline enemies and Explosives. Destroying the exposed core completes
the level immediately regardless of surviving enemies; no exit or cleanup is required.

### SHELL-002 - Permanent Explosion-Only Shell

Status: Must

The shell takes three exploder explosions by default, configurable in settings. Any explosion
whose radius reaches the target counts, including unlaunched exploders. Direct punches and
launched-body impacts do no shell damage. Punches connect for normal cooldown and blocked-hit
feedback. Shell damage persists and breaking it permanently exposes the core. The breaking
explosion must not damage the newly exposed core; a fresh attack is required.

### SHELL-003 - Ordinary Core Damage

Status: Must

The core defaults to the Baseline enemy's maximum health, with its own configurable health.
An already exposed core takes ordinary damage from direct punches, launched bodies and
explosions. Each body can deal impact damage once per continuous launch. A launched exploder's
impact and explosion are separate damaging events against an already exposed core.

### SHELL-004 - Solid Target And Existing Aiming

Status: Must

Both phases block player and enemy movement. Launched non-exploders rebound and continue
flying; launched exploders detonate on target impact. Reuse the same aim-assist rules as enemies
in both phases, including the existing initial-direction preview. Treat the target as solid;
do not extend preview functionality to predict a bounce.

### SHELL-005 - Replenishment In Both Phases

Status: Must

Maintain a configurable bounded Baseline population in both phases. While the shell is intact,
provide two exploders and replenish the pair only when none remain, after a configurable delay.
Do not top up when one survives. Once the shell breaks, cancel guaranteed exploder replacements
while retaining existing exploders. Core destruction stops replenishment through level completion.

### SHELL-006 - In-World Durability

Status: Must

Use a chunky placeholder shell, a clearly distinct core, and visual damage progression in both
phases without health bars. Include a brief opening hint explaining explosions and distinct
successful shell-hit feedback. Geometry, colors, effects, baseline population, bounce strength
and remaining timing values are implementation tuning, subject to playtesting.

## Knock Into Place Objective

### TRACK-001 - Track And Completion

Status: Must

Append Gauntlet_16, with one movable object and one destination at opposite ends of a fixed,
bidirectional track. The object locks permanently and completes the level immediately when
it physically reaches the destination, regardless of surviving enemies. Targeting the final
step alone does not secure completion; opposite hits can still change its destination mid-slide.

### TRACK-002 - Eligible Hits

Status: Must

Launched-enemy impacts and any explosion in range move the object; direct punches have no effect.
Expose launch-source eligibility, defaulting to any launched enemy including elite/boss launches,
with a player-launched-only option. Each enemy counts once per launch; relaunching permits another
hit. A launched exploder's impact and associated blast contribute at most one step together.
Every valid hit contributes the same fixed distance, independent of impact strength or blast range.

### TRACK-003 - Accumulation And Motion

Status: Must

Expose required net forward hits, default five, and short smooth slide duration with a firm stop.
Use the hit direction's component along the track: shallow angles still contribute a whole step,
exactly perpendicular hits contribute nothing, and explosions push away from their origin.
Accumulate hits during movement and immediately retarget, including stops and reversals. Clamp
the destination at either endpoint and discard excess progress rather than banking it.

### TRACK-004 - Physical Obstacle And Aiming

Status: Must

Block player and enemy traversal and account for changing position in navigation. The moving
object pushes characters aside without being stopped by them. Expose push without damage
(default) and push with damage. Launched enemies retain the existing obstacle-impact behavior.
Use the same aim-assist mechanics as enemies, including the existing short initial-direction preview.

### TRACK-005 - Introduction And Readability

Status: Must

Use an open arena with ample space at both sides and endpoints. Reuse bounded replenishing waves
while the objective is unfinished, starting with Baselines and introducing Explosives later.
Use a chunky block on a sliding base, a visible rail/groove, arrows toward the goal and a marked
end socket that changes appearance when locked. Do not add a remaining-hit counter, extra
objectives or a survivor-clearing requirement. Exact geometry, timing and composition remain tuning.

The final conversation delegates implementation details for optional damage cadence, safe displacement,
numerical tolerance and edge-case eligibility. Provisional implementation options are recorded in
`OpenQuestions.md`; their defaults do not establish additional accepted design requirements.

### TRACK-006 - Progressive Campaign Routes

Status: Must

Each successive campaign level with the knock-into-place objective adds one original
10m rail length. Levels 16/19/26/32/39/46/54/69 therefore use 10/20/30/40/50/60/70/80m
routes. The introduction remains straight; later routes contain left or right turns.
Adjust each arena layout, entry, support spawn banks and hazards to leave clear shooting
and return space around every segment and corner. Preserve TRACK-001..005's bidirectional
progress, eligible impacts, physical docking and bounded replenishment.

Implementation tuning uses 2m per eligible hit, five net forward hits per original length,
and alternating 90-degree turns along mirrored staircase routes. These are delegated
geometry/step choices for the requested length progression, subject to playtesting.
At an exact corner, the outgoing leg accepts forward shots and the incoming leg accepts
reverse shots; hits continuing into the space beyond either leg do not advance it.
The block follows each rail segment through physics without cutting across a corner.

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

## Protected-Point Level

### PROTECT-001 - Layout And Progression

Status: Must

Append Gauntlet_18 after Gauntlet_17. Its arena is 32 x 96 metres, with an 8 x 4 metre
marked ground zone centred against one short border. Enemies spawn near the opposite
short border. This deliberately longer defense arena is an exception to the compact
gauntlet layout default in LOOP-002. Reuse existing player controls and combat.

Clarification (2026-10-10): Elites are excluded from every level containing a protected
zone, including campaign defense encounters. Use only normal enemy archetypes in
these levels; Elite staging and replenishment rules do not apply.

### PROTECT-002 - Objective Priority And Player Attacks

Status: Must

All participating enemies prioritize reaching the protected zone. Only the closest eligible
active enemies with the player in their attack range may begin player attacks, up to an
exposed limit defaulting to three. Zero disables new player attacks. Route blocking is not
required. This replaces ordinary crowd-pressure allocation, Explosive proximity exceptions,
Armored unconditional pursuit, and Ranged/Wizard/Dasher distance-band positioning in this mode.
Ranged enemies and Wizards advance toward the zone unless selected to attack, when they stop.
Dashers dash only against the player, never as a way to reach the objective.

Later clarification (2026-10-03): already committed attacks finish when a closer enemy takes
the slot; the closest-enemy limit gates new attacks. This includes attack preparation already
begun, Ranged wind-up, Wizard casts and committed dashes. Ordinary launch/defeat interruption
still applies. Consequently, finishing attacks can temporarily exceed the selection limit.

### PROTECT-003 - Breaches And Defeat

Status: Must

Only enemies in the shared `Active` phase count as breaches. `Launched`, `Recovering` and
`Defeated` enemies do not. Breaching enemies disappear and are resolved for wave progression.
An enemy still inside the zone when it becomes `Active` can breach then. Expose a breach
threshold in settings, default one: the first breach causes defeat. Failure takes precedence
over simultaneous wave completion. Restart restores the objective, waves and crowd.

### PROTECT-004 - Finite Wave Schedule

Status: Must

Use the following editable starting composition:

| Enemy | Wave 1 | Wave 2 | Wave 3 |
| --- | ---: | ---: | ---: |
| Baseline | 14 | 16 | 20 |
| Explosive | 2 | 4 | 4 |
| Ranged | 0 | 2 | 3 |
| Dasher | 0 | 2 | 3 |
| Armored | 0 | 0 | 1 |
| Wizard | 0 | 0 | 1 |
| Total | 16 | 24 | 32 |

Spawn four enemies every three seconds. After all enemies from a wave are defeated or
have breached, wait five seconds before starting the next wave. Batch size, interval,
inter-wave delay and composition are tunable in wave assets. Clearing all three waves
without reaching the breach threshold wins. No timed-survival goal or replenishing supply
is required. Persistent Wizard zones retain their ordinary lifetime but do not delay clearance.

## Enemies And Information

Protected-point levels use PROTECT-002's objective priority instead of the ordinary pursuit
and positioning rules below. Other archetype combat and interruption rules still apply.

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

The Dasher maintains distance, stops for a readable configurable telegraph, samples and locks its direction when the dash begins, and travels without steering until its maximum distance. Static-obstruction contacts redirect the committed dash through the same physics response used while launched; the resolved direction becomes the new locked dash direction instead of ending the dash. Player and ordinary-enemy contact does not end the dash. It then enters a readable recovery window. Dash-path enemy avoidance is one authored policy: none, between the Dasher and player, or between and behind the player.

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

While at least one non-defeated elite is active in the arena, active normal enemies cooperate with the nearest active elite to make its projectile punch easier to execute. Normal enemies with armor remaining are ineligible, including stale selections and area punch resolution. After armor breaks, ordinary eligibility applies. The closest eligible active normal enemy to the elite is always selected as its projectile regardless of spawn order or its distance from the player, and that choice is re-evaluated during setup at the elite's retarget interval. Before waiting, the projectile adjusts to the nearest sampled staging position whose path and resulting elite-to-behind-projectile approach lane are not blocked by static geometry or another non-defeated enemy. If the selected projectile is beside an obstacle, it must keep moving far enough away to provide clearance for the elite's full behind-projectile aiming position and approach lane before the elite approaches. It stops only once that lane is clear, anchoring a stable setup position; until then, the elite waits and does not consume its setup timeout. The elite then repositions behind it within punch tolerance and steers through a side waypoint whenever a direct approach would cross the target's collision clearance, avoiding premature target contact before alignment. After launching a projectile, the elite retains its attack cooldown but uses that interval to approach the next closest eligible active normal rather than resuming ordinary player chase. Other active normal enemies steer laterally out of the projectile-to-player shot corridor. Launched, recovering, defeated, disabled, and pooled enemies do not perform this support movement. With multiple active elites, each normal enemy supports the nearest elite, with entity index used only as a deterministic exact-distance tie break.

### ENEMY-010 — Explosive Proximity Pressure

Status: Must

An active unexploded explosive enemy inside its authored contact-attempt range continuously approaches the player even when it is not selected by the ordinary crowd-pressure limit. During pursuit it retains its configured active-enemy separation distance and weight, including per-archetype overrides. If separation prevents progress toward the player, only the closest eligible pursuing unexploded explosive may ignore separation to reach the player. Outside that range it follows normal arena-wide distribution, pressure allocation, and contact-attempt cadence. Protected-point attacker selection remains authoritative.

### ENEMY-011 — Elite Wave Crowd Replenishment

Status: Must

While at least one elite from an authored wave remains non-defeated, defeated or out-of-bounds normal enemies from that same wave return through the normal pooling lifecycle so the wave maintains its authored normal-enemy count. Elites do not replenish. Once the wave's last elite is defeated, its pooled normal enemies stop returning and the surviving normals can be defeated to complete the wave normally.

### ENEMY-012 — Baseline Crowd Enemy

Status: Must

The Baseline enemy is the default ordinary crowd body. It uses arena-wide pressure allocation, active-enemy separation, contact damage, and the shared health, launch, collision, recovery, defeat, pooling, and wave rules without an additional archetype-specific attack.

### ENEMY-013 — Explosive Detonation

Status: Must

An unexploded Explosive detonates when it collides with another enemy while either participant is `Launched`, or when it reaches the player while still `Active`. The explosion resolves immediately, deals configured damage throughout its radius, applies the configured knockback for the affected target tier, launches ordinary affected enemies through the shared launch lifecycle, may trigger other unexploded Explosives in range during the same resolution, damages and knocks back the player when in range, and defeats the exploding enemy. An Explosive can detonate only once.

### ENEMY-014 - Armored Normal Enemy

Status: Must

Armored is a normal archetype available in EnemySpawnSettings and weighted/guaranteed EnemyWaveSettings entries. It starts with two shield stages, displayed as two overhead shield icons. While stages remain, player punches connect for hit-confirmed cooldown but cause no damage, launch, stagger or push, and no trajectory preview originates from that enemy. It remains a valid aim-assist/homing target for other launched bodies. Elites cannot select or launch protected Armored enemies; special launch paths, including direct boss scattering, must not bypass armor.

A meaningful launched-enemy impact removes one stage regardless of player, elite or boss launch origin; propagated chains and launched Dashers qualify. Explosions also remove one stage. Ordinary crowd contact and all other ordinary damage do not. Both accepted shield hits briefly interrupt pursuit and cause modest recoil without entering Launched or losing health. The second hit removes the last shield; the indicator then disappears and the enemy becomes launchable. A subsequent eligible hit uses its existing ordinary launch/damage rules. Shield hits never accumulate health damage. Preserve deferred defeat, recovery and pooling; a survivor stays unarmored after recovery and can be punched and re-punched normally.

Count each source body once per continuous source launch against a target. A short configurable protection window suppresses additional shield loss and health damage, including after the last shield disappears. An explosive body's impact and its associated explosion count as one event against each Armored target in either order; repeated explosion evaluation cannot add another stage loss or damage outcome. Fresh spawning, pooled reuse and restart restore both stages and clear transient state.

Active Armored enemies continuously pursue the player with obstacle navigation and local separation, independently of baseline pressure slots, surround positions, wind-ups or lunge schedules. Start near baseline charging speed. Stagger suspends pursuit; launch, recovery, defeat, pooling and unavailable-player states suppress movement normally. Chase steering must preserve launch and recoil velocity. Ordinary contact damage and player pushback apply. Use Orc_Skull.fbx and embedded sampled animations, with a stable body tint and a compact overhead indicator showing one icon per remaining armor stage. The indicator disappears when armor breaks or the enemy is unavailable; no runtime Animator per crowd enemy.

### ENEMY-015 - Armored Introduction And Ammunition

Status: Must

Gauntlet_12 follows the Gauntlet_11 boss. Introduce one Armored with Baseline launch ammunition, then several Armored with a supporting Baseline crowd. Retain compact arena styling, navigation and space for repeated aligned shots.

An opted-in encounter supplies one Baseline when undefeated protected Armored enemies remain and no other usable ammunition remains. Broken-armor enemies count as ammunition when otherwise punchable. Respect existing bodies and pending spawns, safe placement, encounter ownership, defeat counters and cleanup. Repeat only as needed; stop when no protected enemies need support. Replacements must neither complete a wave early nor prevent its eventual completion, and must not survive restart or scene unloading.

### WIZARD-001 - Role And Baseline Rules

Status: Must

Wizard is a normal, baseline-health, medium-range area-denial archetype using
`Models/UltimateMonsters/Blob/Wizard.fbx`. It has no contact attack. Normal punch damage,
launching, aim assistance, initial-direction preview, collisions, recovery, deferred death
and pooling remain available. Wizard state and tuning are explicit ECS data.
The full final accepted decisions and rejected alternatives are recorded in [Wizard](Wizard.md).

### WIZARD-002 - Casting And Positioning

Status: Must

Use cooldown -> periodic probability checks -> telegraph -> active -> cooldown. Defaults:
2s initial/post-cast/post-recovery cooldown, .5s checks, 12m engagement, 1s telegraph,
3s active duration and 4m cast radius. Cast Radius and Impact Radius are separate settings.
Chance is clamped base 15% plus proximity up to 35%
(linear from 12m to 4m) plus actual player approach velocity up to 35% (full at 4m/s).
Cooldown counts outside engagement; no line-of-sight check or cooldown randomization.
Optional Cast Whenever In Range (default off) starts telegraph as soon as cooldown is ready
and the available player is in engagement range, bypassing probability and check interval.
Committed casts continue out of range and face the player throughout. Damage/push alone
does not interrupt; launch, recovery, defeat or explicit control loss cancels the moving zone.

Dedicated movement defaults: preferred 6-9m, approach 3m/s, retreat 4m/s, acceleration and
braking 10m/s2, turn response 10. Casting defaults to stopping during both phases; alternatives
stop only in telegraph or keep moving throughout. Positioning continues during cooldown.
Normal separation remains. Reserve Wizard hazard radius during Telegraph/Active by default,
with an always-reserve option; avoid other Wizards' moving and stationary hazards. Launched
bodies ignore this avoidance. Avoidance cannot move a committed stationary caster.

### WIZARD-003 - Zone Membership And Damage

Status: Must

Cast zones follow their caster and affect the player only. Detached impact zones affect
the player, normal enemies, Elites and other Wizards. Use cylindrical XZ overlap including
target physical radius, ignoring cover and height. Exclude the source, BossParts and
puzzle/environment objects from impact-zone effects. Intact armor is immune. Exploder
impact-zone damage alone never requests detonation. Recovering enemies remain impact targets.

Every zone is an ECS entity, with per-target timing in its own DynamicBuffer. Refresh
membership each fixed simulation update using nearby spatial candidates and exact XZ tests.
Entry (including activation) hits immediately, then every .5s from that target's entry.
Exit or invalidation removes tracking; reentry hits immediately with fresh timers. Overlapping
zones stack independently. Defaults are 10 player damage, 8 enemy damage and .5s player
protection per zone, independent of ordinary/global invulnerability. Zone kills receive no
player kill/reward/chain credit. All values are configurable.

### WIZARD-004 - Force And Deferred Death

Status: Must

Modes are damage only, small outward push (default), and strong knockback. Default enemy
speeds are 3/12m/s and player speeds 2/8m/s for small/strong force. Apply on each accepted
entry/tick; at the center damage applies but force has no arbitrary direction. Small push
never launches. Strong force can launch normals; Elites only receive physical push. An
already-launched body keeps its existing launch bookkeeping while velocity changes.
Committed Dashers take damage but keep their trajectory. Player dash grants no immunity.
Lethal launched targets retain deferred death and remain force targets, with no further HP loss.

### WIZARD-005 - Launched Impact Zone

Status: Must

Each genuine entry into Launched grants one stationary zone on its first collision with a
non-defeated enemy, player, or BossPart. Armored and Elite targets qualify; walls/environment
and defeated cleanup bodies do not. Re-punch or redirection while already launched does not
renew this allowance, although ordinary re-punch bookkeeping still resets under COMBAT-014/015.
An active or recovering Wizard hit by a launched enemy also creates an immediate detached
zone. A given incoming enemy flight can trigger each Wizard once; a later genuine flight can
trigger it again. Launched Dasher hits use their swept gameplay collision path. If the Wizard
is already launched, its own flight allowance governs the contact.

The zone appears immediately at the Wizard's impact position projected to ground, without
telegraph, and uses its separate Impact Radius (default 4m). It copies cast-zone duration,
player damage/timing/force and visuals.
Unlike a cast zone, an impact zone also damages and pushes eligible enemies using the
existing enemy settings.
It remains independent through source recovery, recasting or death, while preserving source
immunity. Ordinary impact damage and the zone entry hit both apply; global player protection
from the collision cannot suppress the zone hit or its creation. Scene exit/restart clears zones.

### WIZARD-006 - Readability And Animation

Status: Must

Use permanent violet tint, soft/pulsing telegraph and brighter active treatment on both body
and full-radius ground disc with clear ring. Overlaps blend additively. Stationary zones use
the same active treatment. Dance loops continuously through telegraph and active, interrupting
immediately for launch/recovery. Use embedded Idle/Walk/Death as appropriate, with root motion
disabled and ECS movement authoritative. No new HUD or normal-enemy health bars.

### WIZARD-007 - Waves And Gauntlet_17

Status: Must

Append a compact Wizard introduction without extra hazards: wave one is 6 Baselines + 1 Wizard;
wave two is 12 Baselines + 2 Wizards. Wait for every enemy and persistent zone before advancing.
Wizards do not replenish. Use a brief nonblocking opening hint about damaging zones and
launching enemies into Wizards, without revealing their launched special effect.

Expose reusable wave options, disabled by default: ensure a Baseline is available while a
Wizard remains; wait for persistent hazards before advancing; maximum live Wizards (unlimited
default). Live Active/Launched/Recovering Baselines count as ammunition. Delayed replacements
use ordinary safe placement and wave ownership/counting, cancel if the last Wizard dies before
spawn, and remain if already spawned. Weighted Wizard rolls at cap reroll eligible other
weighted archetypes without delaying/skipping the spawn. Gauntlet_17 enables both safeguards
and uses fixed authored Wizard counts. Later waves can use Wizard as an ordinary weighted type.

### TRAIL-001 - Circling Crowd Weapon

Status: Must

Trail is a standard enemy with Baseline health. It circles the player at medium distance and periodically reverses direction. It has no normal contact attack. Ordinary punches, dash-punches, launching, propagation, recovery, aim assistance and trajectory previews remain available.

### TRAIL-002 - Persistent Ground Sections

Status: Must

Only moving Active or Launched Trail enemies produce sections. Stationary, Recovering and Defeated enemies produce none. Launched trails project onto ground beneath the flight path. Sections activate immediately, expire independently after a fixed lifetime, and retain their original damage, immunity and ownership through source launch, recovery, death or pooling. Pooled lifetimes have distinct source identities. Restart and scene exit clear trails and damage tracking.

### TRAIL-003 - Damage And Timing

Status: Must

Trail overlap uses XZ distance and target radius, ignoring target height. Trail deals damage only, without push, slow or launch. First eligible contact damages immediately; continued contact ticks at the configured interval. Every target shares one cooldown per source lifetime across all sections, including normal and launched sections. Exit/reentry retains that cooldown; overlapping sections cannot multiply damage. Different sources stack independently. Player protection is trail-specific; ordinary invulnerability and dash do not block trail damage.

### TRAIL-004 - Eligibility And Credit

Status: Must

Intact armor is immune; elites receive damage. Boss parts and puzzle/environment objects are unaffected. Damage alone never detonates Exploders, launches targets or propagates a launch chain. Player-owned launched sections receive player kill and chain-reward credit; normal and enemy-owned launched sections receive none. Shared launched deferred-defeat rules remain in force.

### TRAIL-005 - Configurable Alternatives

Status: Must

A dedicated settings asset exposes enemy damage from launched sections only or both section types (default both); source immunity to own sections only, all trails of this enemy type, or none (default own only); and no avoidance or avoidance of damaging trails (default avoidance). Launched enemies and committed Dashers ignore avoidance. Existing obstacle navigation remains relevant. Normal and launched width, lifetime and damage are separate settings. Damage interval, player protection, circling distance, movement speeds and reversal interval are tunable. Starting numeric values are delegated implementation/playtest tuning.

### TRAIL-006 - Readability

Status: Must

Both trail types share one visual style with distinct colors, show the full damaging width and fade toward expiry. Use Blob/Fish.fbx supplied by the user, with suitable existing animations. Placeholder colors and animation choices are delegated. Add no individual normal-enemy health bars or other HUD elements.

### TRAIL-007 - Gauntlet_19

Status: Must

Append one open compact arena as Gauntlet_19 after Hold the Line. Its editable finite waves contain six Baselines plus one Trail, then twelve Baselines plus two Trails. No Baseline replenishment occurs. Once all enemies are defeated, remove remaining trails and their damage tracking immediately. Trails do not delay wave advancement or final completion. Sections otherwise retain their independent lifetimes under TRAIL-002.

### INFO-001 — No Persistent Normal-Enemy UI

Status: Must

Do not attach persistent health bars, names, targeting markers, or other persistent UI to normal enemies. Armored is the sole exception for a compact indicator with one icon per remaining armor stage; it never shows a health bar, and its indicator disappears when armor breaks. Other normal enemies may show a temporary health bar for one second after receiving damage; another damaging hit refreshes that interval.

### INFO-002 — Minimal HUD

Status: Must

Keep the HUD to information needed for immediate decisions. Prefer world animation, silhouette, color, motion, and effects over additional HUD elements.

### INFO-003 — Elite Exception

Status: May

Elites and bosses may display dedicated UI when the information cannot be communicated reliably in-world.

### INFO-004 — Threat Readability

Status: Must

Enemy types and dangerous states must remain distinguishable at crowd scale without requiring the player to inspect individual units.

Enemies in `Active` or `Recovering` face the player's current horizontal position, independently of movement direction. During a Baseline contact charge, face actual horizontal movement instead; when stationary, use movement intent or the committed charge direction. This facing does not steer the charge. While `Launched`, they use the Flying animation and face their current movement direction, including changes during flight. Launch ends with the Falling Flat Impact animation, including when defeat was deferred until launch ended. Facing does not steer committed dash movement or launched velocity. `Defeated` retains its existing rotation behavior.

Entering `Launched` does not change an enemy's archetype body color. Launch readability comes from movement, the Flying animation, and existing silhouette behavior. Other authored feedback, including damage flashes, attack telegraphs, recovery dimming, and Dasher shape changes, remains phase-specific.

The Baseline uses `Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Variant.prefab`
and authored clips from the Goblin Trooper Modular Pack animation folder. Its walk/run,
charge running, damage, flight, landing, recovery and defeat are communicated
through sampled animation with a stable textured Full Cel appearance. Do not add runtime
body tint pulses, hit flashes, idle bobbing or procedural squash/shake to this model.
The mace is visual equipment; contact damage and launch physics retain their existing rules.
Baseline charge preparation uses idle with no wind-up or attack pose. During the charge,
loop the supplied Run Forward In Place on the whole body; bypass the directional jog-leg
layer until the charge ends. Keep the existing charge timing and contact-damage rules.
For both Goblin Baseline and Ranged, Launched holds Jump In Place at original source
frame 6 (zero-based; 0.2 seconds at 30 fps). Rotate the visual 90 degrees top-forward
around the physics owner's centre so the head points along the current launch direction.
Hold the pose for the entire launch, including repunches; body and equipment stay aligned.
Owner yaw still follows actual horizontal velocity. Keep physics, collision, launch duration
and landing/recovery rules unchanged. Die supplies landing/defeat and reverse-rise poses
because the pack has no dedicated Falling Flat Impact or get-up clip.

The Ranged enemy uses `Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Wizard Variant.prefab`
with the same Goblin animation folder and stable textured Full Cel treatment. Use Cut Attack
for ranged anticipation and projectile release, with authored damage, flight, landing,
recovery and defeat poses instead of runtime tint changes or procedural shake/deformation.
Its equipment is visual; ranged projectile and shared physics rules remain authoritative.
Preserve the model scale authored in the supplied prefab; do not fit it to a scripted
target size or stretch its proportions at spawn. Ranged settings expose Projectile Launch
Frame in the original Cut Attack frame range (zero-based). Wind Up Duration remains time
until the shot; retime anticipation to reach the selected frame when the projectile fires,
then play the rest of the authored clip once.

For both Goblin Baseline and Ranged enemies, active movement blends the directional jog
clips in `Assets/CrowdPunch/Animation/` on the legs only. Retain the original Goblin upper
body animation, including attacks while moving. Blend movement relative to facing, so
retreat and strafing use the appropriate leg motion. Spawn, flight, landing, recovery and
defeat keep their original full-body poses. Preserve authored model scale and gameplay movement.

### INFO-005 — Trajectory Scope

Status: Must

Trajectory UI predicts the attack's initial direction only. It should not draw a complete future ricochet path or pretend to predict chaotic downstream collisions.

### INFO-006 - Enemy Spawn Warning

Status: Must

Before an enemy spawns or respawns, show a visible indication at its intended world-space
spawn location. The enemy appears at the indicated location after the warning. Keep this
feedback in the arena without adding HUD elements. Warning duration and appearance are
prototype tuning.

An enemy profile may opt into Spawn From Ground when its model has a configured spawn
animation. After the normal warning and safe-placement checks, place the physics body on
the arena floor at the reserved XZ coordinates and play the authored emergence once.
Normal movement and contact attacks wait for that animation; ordinary punch/launch rules
can interrupt it. Repeat on respawn/restart. Profiles without the option or a supported
clip keep the existing drop from spawn height. Enable this option for the Goblin Baseline
and Ranged Wizard using the supplied Spawn clip. Motion below the floor belongs to the visual pose; the
physics body stays grounded (COMBAT-018).

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

## First Boss Encounter

### BOSS-001 - Crowd-Mediated Head Damage

Status: Must

The boss consists of a head weak spot and two detached hands. Only player-owned launched enemy bodies, including propagated player launch chains, can damage the head. Direct punches, ordinary contact, ranged projectiles and explosion splash cannot. Each accepted hit starts configurable, clearly visible brief invulnerability while collision remains active. Suppress duplicate and sustained-contact damage within a source launch. The boss never enters the ordinary launch lifecycle. Openings emerge from physical shielding and hand behaviour, without scheduled vulnerability windows.

### BOSS-002 - Physical Hands And Committed Attacks

Status: Must

Hands physically intercept bodies, ignore player punches and cannot be permanently destroyed. Eligible launched impacts can briefly stagger them, with configurable duration and repeat protection plus clear blocked/stagger feedback. Ground slam, straight lunging punch and broad sweep each have readable anticipation, committed active movement with locked targeting, and recovery. One coordinated state owns each hand's motion; stagger and transitions cleanly cancel hitboxes. Recovery must permit deliberate shots with the current punch cooldown.

Each attack aims at the player's position sampled when its anticipation begins, including positions near the arena edge or behind the head. Moving after that commitment remains the player's way to dodge. Attack footprints and travel speed should make the hands a meaningful threat without removing readable anticipation or recovery.

### BOSS-003 - Boss Launch Ownership

Status: Must

Active hand strikes physically scatter ordinary enemies. Boss-generated launches and their descendants cannot damage the head. An active strike deliberately takes ownership of an already-launched body. Resolve incoming stagger eligibility before that ownership change; passive shielding must not erase it. A fresh player punch reclaims the body with a new player-owned launch. Preserve elite and ordinary ownership behaviour. Do not prevent self-damage by disabling hand-crowd physics or head collision.

### BOSS-004 - Three Stages

Status: Must

Use configurable thresholds initially near two-thirds and one-third health. Stage 1 alternates telegraphed attacks with a mostly exposed head. Stage 2 assigns one shielding hand and one attacker, with predictable role changes. Stage 3 coordinates hand sequences and more active shielding, retaining clear recovery and reaction time. Difficulty grows mainly through coordination, with modest configurable timing changes. Transitions occur once and cancel attacks visibly. A threshold-crossing hit clamps health at that threshold, discards overflow, and starts a protected transition; further hits cannot skip the next stage.

### BOSS-005 - Perimeter Movement And Reachability

Status: Must

Gauntlet 11 uses a round court with the same nature-kit terrain as the preceding gauntlets. A closed perimeter prevents the player from moving behind the boss, while the head remains exposed to launched enemies from inside the court. Use a configurable perimeter route or authored edge positions. Keep all parts in valid encounter bounds, move smoothly through corners, and coordinate travel with attacks so movement preserves promised recovery openings. The physical weak spot intersects existing ground-plane body trajectories. Preserve the normal camera and readable court. Moving hands are not baked navigation obstacles and do not trigger per-frame navigation-grid rebuilds.

### BOSS-006 - Bounded Supporting Crowd

Status: Must

EnemyWaveSettings owns supporting composition, population and replenishment using existing ordinary profiles. Defaults are mostly Baseline and at most one living non-Baseline ordinary enemy. Maintain a bounded supply of launchable bodies through pooling and safe placement without special duplication. Replenishment stops on boss defeat; surviving supporting enemies need not be cleared. Ordinary gauntlet wave behaviour remains unchanged.

### BOSS-007 - Eleventh Gauntlet And Lifecycle

Status: Must

The complete encounter is gauntlet 11 after the existing ten. Gauntlet 10 advances to it; head defeat completes that gauntlet once and advances to Gauntlet_12 through the existing sequence. Completing Gauntlet_12 uses normal final-level completion until further levels are authored. Supporting wave completion cannot win early. Build configuration, sequence, display names and selection include the boss. Restart, death/retry, selection, unloading and replay reset or remove parts, attacks, ownership, replenishment, feedback and completion while preserving existing player reset conventions.

### BOSS-008 - Presentation And Configuration

Status: Must

Use Assets/CrowdPunch/Models/UltimateMonsters/Blob/Orc.fbx for the head, preserving the original asset. Placeholder hands and procedural movement follow the toy-box style. Reuse the elite health-bar presentation for one head/boss bar, without hand bars, phase markers or additional boss HUD. Reuse proportionate combat feedback and readable world telegraphs. A dedicated baked ScriptableObject owns boss health, invulnerability, thresholds, movement, attacks, telegraphs, recovery, shielding and stagger; crowd tuning stays in wave settings.

### BOSS-009 - Encounter Duration

Status: Should

Target approximately 2-3 minutes through tuning and playtesting, without a forced timer.

## MVP

### MVP-001 — Map

Status: Must

A fixed sequence of small, closed gauntlet levels including the boss and continuing afterward. The MVP does not require open levels, branching routes, or seamless traversal between gauntlets.

### MVP-002 — Boss

Status: Must

One complete boss encounter that proves crowd-mediated boss interaction.

### MVP-003 — Enemy Roster

Status: Must

Seven standard enemy types: Baseline, Explosive, Ranged, Dasher, Armored, Wizard, and Trail. Elite enemies are an additional special encounter layer and do not count toward the seven standard enemy types. Their distribution across gauntlets remains encounter-pacing and tuning work.

### MVP-004 — Weapons

Status: Must

Two weapons. Their exact identities and acquisition model remain unresolved.

### MVP-005 — Complete Run

Status: Must

The MVP must support a start-through-boss-and-later-gauntlets-to-win-or-loss run rather than only a combat sandbox.

## Chicken Boss (Agreed 2026-10-07)

### CHICKEN-001 - Three-Stage Shot And Rush Cycle

Status: Must

The fleeing Chicken uses a stationary wind-up that tracks the player until release. Each shot
immediately commits a reachable rush away with a strong sideways offset, followed by a brief pause.
Stage one defaults to one shot, rush, pause; stage two to shot, rush, second shot from the new
position, rush, pause; stage three to paired shots with faster rushes and shorter pauses. Single
and paired patterns remain configurable per stage. Pair spacing must allow normal punch cooldown.
Do not add rush warnings or destination markers.

### CHICKEN-002 - Proximity, Solidity And Recovery

Status: Must

Expose finish-pause and interrupt-pause-and-flee responses, defaulting to interrupt and flee.
Proximity never cancels a committed wind-up or shot. The solid boss damages the player only
during rushes and pushes crowd bodies aside without dealing damage or initiating launches.
Accepted damage briefly staggers it, interrupts rushes, cancels pending shots, starts temporary
damage invulnerability, and begins a fresh attack cycle after recovery.

### CHICKEN-003 - Punchable Bouncing Projectiles

Status: Must

Shots travel at a fixed punchable height with a committed firing direction. Expose fire-time
position aim (default) and movement-leading aim. Walls reflect velocity while preserving speed.
A configurable bounce limit and lifetime remove shots. Punches use the existing volume, dash-punch
eligibility, hit-confirmed cooldown and assisted target selection. A punch replaces velocity with
a configurable return speed, resets lifetime and bounce count and begins a fresh launch. Returned
shots home using existing launch homing; the first wall bounce clears the lock until another punch.
Use the existing short initial-direction preview for shots in the punch volume.

### CHICKEN-004 - Projectile Contacts And Persistent Player Danger

Status: Must

Before and after redirection, shots damage and launch ordinary enemies and continue through them.
Each enemy can be hit once per projectile launch. Preserve armor and launch eligibility rules.
Projectiles and every launched-body chain remain dangerous to the player regardless of ownership.
Player contact consumes a shot even when player protection rejects damage. A returned shot is
consumed on boss contact whether or not boss invulnerability permits damage.

After a launched enemy physically strikes the Chicken, deflect any player-bound rebound
sideways away from the boss, preserving solver speed and vertical motion. Clear its boss
homing lock so it cannot steer straight back into the boss. Already-safe rebounds stay intact;
launched bodies remain dangerous to the player through subsequent physical interactions.

### CHICKEN-005 - Boss Damage Eligibility And Combined Exploder Hits

Status: Must

Returned shots and player-owned launched-enemy impacts, including their propagated chains, can
damage the Chicken. Direct punches, untouched boss shots and boss-owned body impacts/chains cannot.
Any nearby explosion can damage it, including unlaunched and boss-owned Exploders. An Exploder body
impact and its associated blast count as one combined hit using the higher eligible damage value.
Consume protected contacts to prevent delayed repeat hits from sustained contact.

### CHICKEN-006 - Encounter And Lifecycle

Status: Must

Append one introductory level after the current progression, selecting the next available number.
Use an open arena with a solid perimeter and no interior obstacles, plus a small replenishing
Baseline crowd. Population and composition belong in existing wave settings. Boss defeat immediately
completes the encounter, stops replenishment and clears its projectiles, without survivor cleanup.
Restart restores the boss, population, projectiles, timers and hit histories.

### CHICKEN-007 - Configuration And Presentation

Status: Must

Use the supplied `Assets/CrowdPunch/Models/UltimateMonsters/Blob/Chicken.fbx`, the existing elite-style
health bar, readable ownership colors that do not imply returned shots are safe, hit/invulnerability
feedback and one brief opening hint. A dedicated boss settings asset exposes health/damage, stage
thresholds/patterns, wind-up/spacing/pauses, rush/proximity, stagger/protection and projectile tuning.
Initial numbers, arena size and minor model presentation are delegated to implementation/playtesting.

### CHICKEN-008 - Duration Target

Status: Should

Target a roughly 2-3 minute fight through balance tuning, without a forced fight timer.

## Rolling Blob Boss (Agreed 2026-10-07)

### ROLL-001 - Committed Vulnerability Cycle

Status: Must

Cycle through vulnerable pause, vulnerable player-tracking wind-up, and protected roll.
Lock direction at roll start; do not steer between wall/obstacle redirects. Redirect immediately
without another wind-up. Surviving damage never changes the current action or timer.
Direct punches neither damage nor launch the boss; rolling rejects body and explosion damage.
Accepted damage grants brief visible protection. A body damages once per launch; re-punch renews eligibility.

### ROLL-002 - Stages And Duration

Status: Must

Use three health stages with increasing speed and shorter pauses. Clamp damage at each stage
threshold and discard overflow. Stage changes preserve the committed action and timer; new tuning
applies next cycle. Target roughly 2-3 minutes through tuning without enforcing a fight timer.

### ROLL-003 - Configurable Modes

Status: Must

Expose physical reflection or collision-time re-aim (default re-aim); fixed-duration or bounce-count
roll ending with maximum-duration safeguard (default duration); Player-owned-only or any launched
body/chain eligibility (default Player-only); rolling-only or all-living-state player contact danger
(default rolling-only); every-living-state or vulnerable-state-only aim/homing targeting (default
every living state); and rolling-direction or outward crowd launch (default rolling direction).

### ROLL-004 - Crowd And Physical Contacts

Status: Must

Rolling contact damages and launches ordinary enemies, starting fresh Boss-owned launches even
for already-launched bodies. Player punches reclaim ownership. Passive contacts preserve ownership.
All launched bodies/chains remain dangerous to the player (COMBAT-015). Protected Armored bodies
receive pushback without shield/health loss; Elites receive damage/pushback without normal launch.
Reuse boss sideways rebound handling in every state, preserving solver speed and vertical motion
and clearing homing toward the contacted boss. Stationary contact remains solid and harmless by default.

### ROLL-005 - Explosions And Combined Hits

Status: Must

Vulnerable states accept explosions from any source, including unlaunched and Boss-owned Exploders.
An Exploder body impact and its associated blast count as one combined hit using existing maximum-damage
resolution. Protection and per-source launch/lifetime history prevent duplicate damage.

### ROLL-006 - Gauntlet_21 And Lifecycle

Status: Must

Add exactly Gauntlet_21 after Chicken Run. Use an enclosed arena with a few well-spaced solid interior
obstacles, broad lanes and a small bounded replenishing Baseline crowd. Boss defeat completes immediately
and stops replenishment; surviving support needs no cleanup. Retry, restart, selection and unloading
reset or remove boss timers/state/history, supporting crowd and completion state.

### ROLL-007 - Assets, Settings And Presentation

Status: Must

Use `Assets/CrowdPunch/Models/UltimateMonsters/Blob/GreenSpikyBlob.fbx`. A dedicated boss asset owns
health, thresholds, protection, wind-up/pause/roll timing, bounce limits, speed, contact damage,
launch/push and presentation tuning. Composition/replenishment remain wave-owned. Reuse one elite-style
boss health bar without vulnerability countdown. Distinguish pause, wind-up, rolling and temporary
hit protection; include one brief opening hint. Numbers, layout and minor presentation are delegated.

## Dino Pillar Boss (Agreed 2026-10-08)

### PILLAR-001 - Continuous Pursuit And Pillar-Only Defeat

Status: Must

Dino repeats normal chase, a brief warning while still chasing normally, and a speed burst
that continues steering with slower turning. Three health stages increase chase speed and
shorten chase intervals. Only falling pillars damage him; punches, launched bodies,
explosions and other attacks cannot damage or launch him. Each successful pillar contributes
one hit and briefly staggers him, cancelling a burst. Another pillar may hit during stagger
and refresh it. Required successful hits are configurable from 1 to 3, default 3.
Normal chase, warning and burst contact damage the player with a configurable cooldown.
Boss crowd contacts push without damage or starting launches.

### PILLAR-002 - Player-Owned Body Trigger And Solidity

Status: Must

Exactly three pillars begin upright and solid to everyone. One Player-owned launched-enemy
impact, including a propagated Player-owned chain, topples a pillar. Direct punches,
explosions alone and non-Player launches cannot topple it. The triggering body continues
through with its launch retained. Solid blocking ends when falling begins; falling and
fallen pillars never trap characters. Dino navigates around upright pillars.

### PILLAR-003 - Locked Direction And Moving Contacts

Status: Must

Expose incoming enemy travel direction and toward-boss fall modes, default toward boss.
Lock the direction at impact using the boss position at that moment. The rotating pillar
shape detects contact during its fall; each target receives at most one hit per fall.
Falling pillars damage the boss, player and ordinary enemies. Communicate direction through
the falling animation alone; do not add a ground marker.

### PILLAR-004 - Configurable Ordinary Impact Response

Status: Must

Expose damage-and-push without starting a launch (default), and damage-and-launch for
launchable ordinary enemies while pushing the player. Pillar-created launches and their
propagated chains are Environment-owned and cannot topple other pillars. Preserve established
launch resistance, armor and deferred-defeat rules. All launched bodies remain dangerous to
the player under COMBAT-015.

### PILLAR-005 - Consumption And Safe Regeneration

Status: Must

A successful boss hit permanently consumes that pillar. A missed pillar's fallen visual
disappears, then it regenerates after a configurable few-second delay. Regeneration waits
while the player or boss occupies its upright space and pushes ordinary enemies aside
without damage or initiating launches. Restore solid blocking only when the space is clear.

### PILLAR-006 - Targeting, Assets And Lifecycle

Status: Must

Upright pillars participate in existing launch aim assistance, homing and short trajectory
previews. Exclude the immune boss and unavailable pillars from targeting. Use
`Assets/CrowdPunch/Models/UltimateMonsters/Big/Dino.fbx` with supplied animations, one existing
boss health bar and a brief opening hint. Show readable warning, burst, stagger, pillar
impact and regeneration feedback in-world. A dedicated settings asset owns stage movement,
turning, cycle/stagger/contact timing, pillar geometry/fall/regeneration and damage/push/launch
strengths. General crowd composition and replenishment remain in existing wave settings.
The boss settings additionally expose the number of replenishing Baselines around
each unconsumed pillar, default two per pillar. These enemies use ordinary crowd movement,
separation, chase pressure and contact attacks freely within `pillarCrowdRadius`; do not
pin them to fixed standing slots. Limit normal movement to that local area. Launched bodies
retain normal physics and return after recovery; defeated local bodies respawn nearby through
safe placement. Missed pillars retain their local crowd while waiting to regenerate.
A successful pillar releases its survivors to normal crowd behavior and stops their local
replenishment. Boss defeat, retry and unloading also apply to this extra crowd.

Append exactly Gauntlet_22 after Rolling Blob: an enclosed otherwise open arena, three spaced
pillars and a small replenishing Baseline crowd. Boss defeat completes immediately and stops
replenishment; supporting survivors need not be defeated. Retry/restart restores boss,
pillars, crowd, timers, histories and completion. Unloading removes encounter-owned state
and effects. Numerical balance, layout, pillar visuals, clips, colors and collision safeguards
are delegated to implementation; no additional encounter mechanics are authorized.

## Ground Hazards (Agreed 2026-10-09)

### GROUND-001 - Fixed Shapes And Activation

Status: Must

Support fixed rectangle and circle patches, each authored as always active or periodic.
Periodic patches repeat inactive -> warning -> active. Inactive and warning phases deal
no damage. Each patch has its own durations and starting offset; every wave start and
retry resets cycles to their authored offsets. Expose damage, interval, dimensions,
inactive/warning/active durations and offset as editable tuning. Initial numbers are delegated.

### GROUND-002 - Horizontal Damage Only

Status: Must

Active patches damage the player and living Active, Launched and Recovering enemies by
horizontal overlap, ignoring height. Damage causes no slow, push, launch or trajectory
change. Launched lethal victims retain the shared deferred-defeat behavior (COMBAT-011).

### GROUND-003 - Shared Victim Cooldown And Player Protection

Status: Must

First eligible contact hits immediately, followed by the configured damage interval.
All ground patches share one cooldown per victim. Overlaps, exit/reentry, switching
patches and activation cycles cannot bypass an unexpired cooldown. Player dash and
ordinary hit invulnerability do not prevent hazard damage.

### GROUND-004 - Armor, Exploders And Kill Credit

Status: Must

Intact enemy armor prevents health damage and armor-stage loss. Armored enemies still
avoid patches. Hazard kills never trigger Exploder explosions. Lethal hazard damage
receives player chain credit only when the victim is currently in a Player-owned launch;
otherwise it receives environmental credit. Preserve established launch ownership.

### GROUND-005 - Voluntary Routing And Configurable Fallback

Status: Must

Enemies route around hazardous areas and try to leave when caught inside. Avoidance
changes voluntary movement only; launches and physical collisions can force bodies
into danger. Committed attacks, including Dasher charges and melee lunges already
underway, finish normally without avoidance. Launched bodies retain existing physics.
Expose Active-only avoidance (default) or Warning-and-Active avoidance, and Wait Safely
(default) or Cross As Last Resort when no safe route exists. Local repulsion alone does
not satisfy route finding.

### GROUND-006 - Safe Spawning And Lifecycle

Status: Must

Spawn enemies outside warning and active patches, regardless of avoidance policy.
Permanent patches leave safe routes and usable spawn space. Retry/restart and scene
unloading reset or remove encounter cycles, shared damage clocks and cached hazard routes.

### GROUND-007 - Gauntlet_23 And Presentation

Status: Must

Append exactly one compact Gauntlet_23 after Dino Pillars. Wave one introduces always-active
patches with finite Baselines; wave two adds periodic patches. Complete once every
finite-wave enemy is defeated; hazards never delay completion. Enemy counts and layout
are delegated. Introduced periodic patches remain visible while inactive, with distinct
inactive/warning/active appearances, a full-footprint pulsing warning and a brighter
active state. Colors and effects are delegated; no additional HUD is required.

## Snake Enemy (Agreed 2026-10-12, Revised 2026-10-15)

### SNAKE-001 - Pursuing Head And Physical Tail

Status: Must

A Birb head continuously pursues the player outside ordinary crowd-pressure allocation. Attached spherical orbs follow its recent path with a sinusoidal sideways wave. Reduce the wave near blocking geometry and keep attached bodies in navigable space. Attached orbs collide with enemies but ordinary contact causes neither damage nor launches. Both head and attached orbs damage the player on contact through existing player protection.

### SNAKE-002 - Tail-Tip Launch Progression

Status: Must

Only the last attached tail segment is vulnerable. Punches (including ordinary dash punches) and qualifying launched-body impacts from any owner can launch and detach that orb regardless of health. The head and every preceding attached orb remain protected. Preserve all predecessors; when the tip launches, the immediately preceding orb becomes vulnerable. Ordinary damage alone cannot destroy attached orbs. One simultaneous punch/explosion hit batch affects only the tip exposed at its start; it cannot peel multiple segments by processing order. Tail loss is permanent until reset/respawn. This supersedes the original middle-cut/disconnected-suffix behavior.

### SNAKE-003 - Detached Projectile Lifecycle

Status: Must

Detached orbs retain ordinary physical chains, launch ownership and launched-body player damage. Ignore their original head and all of its other parts, including detached siblings, until the orb bounces off something outside its original snake. A meaningful external solid collision enables original-snake collisions for the rest of that orb's lifetime; distance, elapsed time and routine floor support cannot enable them. Other snakes and unrelated bodies remain collidable immediately. This supersedes spatial-clearance rearming. Destroy detached orbs when launch ends; they never recover as melee enemies.

### SNAKE-004 - Head Protection Until Tail Exhaustion

Status: Must

Any attached orb protects the head from damage, launch, push and stagger; direct punches still consume cooldown. The head becomes vulnerable only after no attached orbs remain, then follows ordinary launch/damage/recovery and resumes pursuit. Tail removal does not automatically defeat it. The former AlwaysVulnerable option is superseded by the 2026-10-13 decision. Head defeat/bounds cleanup removes any remaining tail.

### SNAKE-005 - Configurable Explosions

Status: Must

Default LaunchAndSever: explosions can launch only the currently exposed tip in range. Also expose IgnoreAttachedOrbs. Protected inner orbs/head ignore the blast, so one explosion cannot remove the entire tail. Head eligibility follows SNAKE-004. Reuse existing explosion ownership and impulse conventions.

### SNAKE-006 - Assets, Aiming And Settings

Status: Must

Use Models/UltimateMonsters/Blob/Birb.fbx for the head. Preserve its atlas face/eye/beak details; recolor its body and the protected tail segments consistently in Birb blue. The vulnerable tip is gold, and the head body changes to the same gold after the last attached segment is removed. Within an attached snake, only the currently vulnerable tip receives ordinary aim assistance, can be targeted by aim assistance and shows the initial punch-direction preview when approached. The player cannot punch a tail part through its owner's head; an occluded part must also lose its aim lock and launch arrow until approached from a clear direction. When the tail is empty, eligibility transfers to the head. Give the vulnerable part a distinct color that moves with eligibility. Detached orbs retain their ordinary independent projectile/repunch rules and the same head obstruction. Tail silhouette, visible loss, color and existing combat feedback communicate the mechanic without dedicated UI. Expose both explosion modes, initial segment count, orb size, spacing, wave amplitude/frequency (with units), pursuit speed, contact damage, head health and protected/vulnerable colors. Numeric tuning and colors remain provisional; head health uses the spawn profile's Max Health.

### SNAKE-007 - Legacy Gauntlet_24

Status: Must

Introduce the enemy in legacy Gauntlet_24 through Level Play, outside campaign progression and player builds. Use one small finite wave of snakes and Baselines with configurable counts. Complete on head/Baseline defeat; tail entities and detached projectiles never count as independent objectives. Retry restores full tails, heads, health and Baselines and clears projectiles/exclusion state. Bounds and cleanup must not leave invisible bodies blocking completion. Campaign placement is outside this slice.

Source: agreed summary in Design Yeti Tail Enemy, conversation 6accfed5-75b4-83ed-8ac1-0798c630da8e. No consequential open question remains within this scope.

## Explicit Non-Goals For The Current Baseline

- Conventional attack combo strings.
- Persistent individual UI for normal enemies.
- A large encyclopaedia before the number of effect combinations justifies it.
- Unlocking basic movement or core crowd-manipulation mechanics during a run.
- Direct ordinary attacks as the complete boss solution.
- Open-world or large open-level traversal.
- Branching route selection between gauntlets.
- Additional maps, bosses, enemy types, or weapons before the MVP loop is proven.

## Ninja Enemy

### NINJA-001 - Distance Management And Ambush Sequence

Status: Must

The Ninja is a normal-tier assassin with Baseline health and no ordinary contact attack.
Maintain a 12-18m preferred distance, retreat at the player's normal movement speed, and
approach at configurable speed. Stop for a recognizable 0.75s preparation, then teleport
approximately 2m behind the player's position/facing sampled at the end of preparation.
Hold a locked attack pose for 0.2s, dash at 48m/s for at most 5m without tracking or steering,
then stop for 1.6s recovery before resuming positioning. A player already dashing is eligible.
Engagement range is checked only when preparation begins; leaving it does not cancel commitment.
No preparation line of sight is required.

### NINJA-002 - Safe Teleportation And Retry

Status: Must

Prefer directly behind the player; search a limited configurable rear arc if blocked.
Reject obstacle/boundary intersections, occupied space, active or warning ground hazards,
blocked destination-to-player dash paths, and disconnected arena sections. Sample the
destination only after preparation completes. An invalid destination returns to positioning
and retries after a short randomized delay without consuming the full attack cooldown.
Every retry repeats the complete preparation warning. Validation/search details are delegated.

### NINJA-003 - Intentional Dash And Player Risk

Status: Must

Lock the dash direction at teleportation toward the player's position at that moment.
Stationary arrival warning and committed dash never update that direction. Intentional dash
contact deals 20 player damage and knockback strength 18 at most once per dash. Shared 0.5s
player invulnerability rejects both damage and knockback from subsequent hits. Player contact
does not end the dash. Pass through ordinary unlaunched enemies without affecting them.
Solid static geometry ends the dash immediately into recovery, without bounce or slide.
Outside intentional dashes, use only the existing launched-body danger to the player.

### NINJA-004 - Scheduling And Concurrent Ambushes

Status: Must

Expose Periodic, Proximity, and Both trigger modes, defaulting to Both. Initial attack delay
is 3s; the shared minimum cooldown is 6s. Periodic scheduling uses 10s with +/-2s variation.
Proximity triggers on entering 6m, and maximum engagement distance is 20m. At most two Ninjas
may simultaneously prepare, teleport, warn on arrival, or dash. Recovery releases the slot.
Extra Ninjas continue positioning and retry after short random delays without losing readiness.
Interruptions start a fresh cooldown and scheduling must respect launch, recovery, pooling,
defeat and restart. Retry ranges and initialization/cooldown staggering are delegated.

### NINJA-005 - Ordinary Launch Combat And Presentation

Status: Must

Punches and launched-body impacts interrupt either warning or the dash. A launched Ninja uses
Baseline projectile damage, knockback, chain propagation, ownership, recovery and defeat rules,
with no enhanced Dasher impacts. Use Models/UltimateMonsters/Big/Ninja.fbx and suitable embedded
animations, a recognizable preparation, an arrival attack pose, and compact departure/arrival
smoke that preserves the character's short reaction window. Add no directional dash line,
screen-space warning or dedicated HUD. Audio is deferred; a later audio pass may add distinct
preparation and arrival sounds.

### NINJA-006 - Legacy Gauntlet_25 And Tuning

Status: Must

Append only legacy Gauntlet_25. Wave 1 contains one Ninja plus Baselines; clearing it activates
wave 2 with three Ninjas plus Baselines. Clear the second finite wave to complete. No replenishment.
Keep it available through Level Play Editor and outside campaign/player builds. Preserve existing
campaign progression and legacy content. Baseline counts, spawn positions, enclosed layout and
minor timing are delegated. Expose dedicated Ninja movement, scheduling, teleport and attack
settings with the defaults above; rear search arc, approach speed and retry intervals are delegated.


## Flame Dragon Enemy (Agreed 2026-10-13)

### DRAGON-001 - Positioning, Facing And Flame Lifecycle

Status: Must

Use Baseline health and ordinary launch, collision-chain, recovery, deferred-defeat,
spawning, pooling, aim-assistance and trajectory rules (COMBAT-010..018). No contact
attack or individual health bar. Approach/retreat to an authored preferred distance
band, retaining separation and obstacle navigation throughout warning, active flame
and cooldown. Turn toward the player at an authored maximum angular rate. Flame
follows actual position/facing, including ordinary velocity-facing collision redirects.
Repeat harmless warning, active burst, cooldown. Engagement range gates starting a
warning only; leaving range never cancels a committed warning/burst. Any launch owner
immediately replaces bursts with continuous flame, including zero-health deferred
flight. Recovering, Defeated and pooled dragons emit nothing. Surviving recovery starts
a full cooldown before a fresh warning, never resuming an interrupted burst.

### DRAGON-002 - Attached Cone And Solid Cover

Status: Must

Use a widening mouth-attached cone. Membership overlaps victim disks on XZ and ignores
height. Solid obstacles/walls block damage, push, warning and active visuals; enemy
bodies provide no shielding. Harmless ground warning and mouth wind-up follow the
moving dragon; active visuals show the same cover-blocked footprint. No persistent
trail, lingering burn, independent flame aim assistance or new HUD.

### DRAGON-003 - Victim Eligibility And Independent Hit Clocks

Status: Must

All active flames can damage the player regardless of source launch ownership, dash
or ordinary hit invulnerability. Enemy modes are Player-owned launches only (default,
including propagated launches), any launched ownership, or both bursts and launches.
Immediate first-contact damage repeats on one shared-interval clock per source/victim
lifetime. Exit/reentry and normal-to-launched transitions do not reset that clock;
independent dragons have independent clocks. Normal/launched flames have separate
length, angle, player/enemy damage and player/enemy push tuning.

### DRAGON-004 - Push, Protection And Attribution

Status: Must

Apply damage plus a small outward horizontal push to the player and eligible walking
enemies, including elites; this never launches or starts propagation. Already-launched
enemy victims receive damage with unchanged velocity, launch ownership, sequence and
collision history. Preserve COMBAT-011 deferred defeat. Intact armor and its shared
protection window block damage and push without stage loss. Flame never requests
Exploder detonation, including on flame kills; dragon-body collision remains ordinary.
Bosses and puzzle objects receive no damage/activation. Flame kill credit follows the
emitter's current launch ownership and chain depth, without changing victim ownership;
normal/other-owned flame receives no Player credit.

### DRAGON-005 - Configurable Immunity And Avoidance

Status: Must

Dragon immunity modes are own flame only (default) or all dragon flame. Enemy avoidance
modes are avoid eligible damaging active cones (default) or none. Harmless warnings
require no avoidance. Launched enemies and committed dashes retain their existing
movement. Route around solid cover with the existing navigation/physics pipeline.

### DRAGON-006 - Assets, Settings And Reset

Status: Must

Use Models/UltimateMonsters/Flying/Dragon_Evolved.fbx and suitable supplied animations.
Expose a dedicated Dragon settings asset with every mode above, warning/active/cooldown
seconds, engagement range, preferred distance band, approach/retreat speeds, explicit
angular rate, independent normal/launched lengths/angles/player-enemy damage/push, and
shared tick interval. Push units must be explicit. Initial numerical tuning, attack
staggering, suitable clips, mouth attachment, colors/effects, cover geometry, occlusion
algorithm and performance strategy are delegated. Retry, reuse, pooling and scene exit
must clear stale effects and victim clocks through ordinary encounter lifecycle.

### DRAGON-007 - Legacy Gauntlet_26

Status: Must

Add Flame Dragon introduction outside campaign progression and player builds, accessible
through existing Level Play. Exactly two finite sequential waves: 6 Baselines + 1 Dragon,
then 12 Baselines + 2 Dragons. Use a compact arena with solid cover, clear flanking routes
and safe spawn placement. Advance/complete through ordinary enemy clearance; no lingering
flame gate or new ammunition replenishment policy. Layout and timing are delegated.
Preserve prior legacy encounters, campaign content, tuning and saves (LOOP-002/006).
