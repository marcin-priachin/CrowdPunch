# Wizard accepted design

Accepted 2026-10-01 from conversation `6abe10dd-838c-83ed-8b66-0308a282a96d`.
This records the final agreed summary, including the continuous-launch correction (Q160).
It is design, not verification evidence. See GDD WIZARD-001..007 and `../Validation/Wizard.md`.

## Agreed decisions

### 1. Core Wizard role

- New enemy archetype: **Wizard**.
- Model: `Assets/CrowdPunch/Models/UltimateMonsters/Blob/Wizard.fbx`.
- Wizard is a **medium-range area-denial enemy** whose main threat is a periodically activated radial damage zone.
- It has **no normal contact/melee attack**.
- Direct player punches behave normally:
  - normal punch damage;
  - normal launch;
  - full aim assist;
  - trajectory preview.
- Wizard has **baseline-enemy health** by default.
- Wizard gets its own explicit archetype/state/components rather than piggybacking on Ranged/Exploder logic.

### 2. Normal cast

Final cast sequence:

`Cooldown → probability checks → Telegraph → Active → Cooldown`

Defaults, all exposed in Wizard settings:

- initial/post-cast/post-recovery cooldown: **2.0 s**;
- cast probability evaluated every **0.5 s**;
- engagement range: **12 m**;
- telegraph duration: **1.0 s**;
- active duration: **3.0 s**;
- zone radius: **4.0 m**.

Cast probability is additive and clamped to 100%:

`base + proximity bonus + approach bonus`

Defaults:

- base chance: **15%**;
- proximity bonus: up to **+35%**;
- approach bonus: up to **+35%**;
- proximity scales linearly from 0 at 12 m to maximum at 4 m;
- approach bonus uses actual player velocity projected toward the Wizard;
- full approach bonus reached at **4 m/s** approach speed.

There is **no randomized cooldown** anymore.

The Wizard:

- may start cast checks without line of sight;
- keeps cooldown counting even while the player is outside engagement range;
- once it commits to a cast, the cast continues even if the player moves away;
- continuously faces the player during telegraph and active phases.

### 3. Movement

Wizard has dedicated movement tuning rather than reusing Ranged settings.

Defaults:

- preferred range: **6–9 m**;
- approach speed: **3.0 m/s**;
- retreat speed: **4.0 m/s**;
- acceleration: **10 m/s²**;
- braking: **10 m/s²**;
- turn speed: **10**.

Cooldown blocks only casting; positioning continues normally.

Casting movement mode is configurable:

- **Stop during telegraph + active — default**
- Stop during telegraph only
- Keep moving throughout

The Wizard and other enemies try to keep distance from one another.

Wizard-specific hazard-spacing behavior is configurable:

- always reserve the full hazard radius;
- **reserve only during Telegraph + Active — default**.

Launched enemies ignore this Wizard-spacing avoidance entirely.

### 4. Normal damage zone

The normal zone:

- follows the Wizard while active;
- uses flat **XZ-plane/cylindrical** gameplay checks;
- affects:
  - player;
  - normal enemies;
  - elites;
  - other Wizards;
- does **not** affect BossParts;
- does not affect non-enemy puzzle targets, barricades, track objects, cover, etc.;
- ignores line of sight and cover.

The source Wizard is immune to its own zone.

Other Wizards are not immune.

Overlapping Wizard zones **stack independently**.

Armored enemy:

- while armor remains: completely immune to Wizard-zone damage/effects;
- after armor breaks: affected normally.

Exploder:

- takes Wizard-zone damage normally;
- zone damage itself does **not** trigger its explosion.

Dasher while actively dashing:

- takes damage normally;
- its committed dash trajectory is not changed by Wizard-zone force.

Recovering enemies:

- remain valid zone targets.

### 5. Per-target damage timing

This changed from a zone-wide tick schedule to **per-zone, per-target timing**.

For each target entering each zone:

- immediate damage on entry;
- then that target gets its own timer;
- subsequent hits every **0.5 s** from that target's entry time.

If a target exits:

- its tracking entry is removed immediately.

If it re-enters:

- it is treated as a completely fresh entry;
- immediate damage again;
- new tick schedule.

This applies even if the Wizard's moving zone causes a brief exit/re-entry.

At normal cast activation, targets already standing inside the telegraph radius are treated as newly entering:

- immediate hit;
- then normal per-target ticks.

Multiple overlapping zones track the same player/enemy independently.

Player Wizard-hit invulnerability is therefore **per zone**, not global.

### 6. Damage values

Defaults:

- player damage per hit/tick: **10**;
- enemy damage per hit/tick: **8**;
- tick interval: **0.5 s**;
- player Wizard-hit invulnerability: **0.5 s**.

All exposed in Wizard settings.

Wizard-zone damage can kill enemies normally.

Wizard-zone kills are treated as **enemy/environment damage**, not player kill/reward/chain credit.

### 7. Zone force

Configurable mode:

- damage only;
- **damage + small outward push — default**;
- damage + strong knockback capable of launching normal enemies.

Defaults:

- enemy small push: **3 m/s**;
- enemy strong knockback: **12 m/s**;
- player small push: **2 m/s**;
- player strong knockback: **8 m/s**.

Force applies:

- on initial entry hit;
- on every later damage tick.

Push direction is away from the zone center.

If target is effectively at the exact center:

- apply damage;
- skip the push rather than choosing an arbitrary direction.

Small push:

- displacement/impulse only;
- does not transition enemies into `Launched`.

Strong knockback:

- normal enemies can genuinely enter `Launched`;
- Elite gets strong physical push but never becomes `Launched`;
- an already-launched enemy has its existing launch velocity redirected/modified rather than starting a new launch;
- a genuine new launch of a Wizard, regardless of source, enables its special launched-Wizard behavior.

Player dash does not provide immunity:

- damage and force still apply normally while dashing.

### 8. Cast interruption

Damage alone does **not** cancel casting.

Casting is cancelled when the Wizard genuinely loses control, such as:

- entering `Launched`;
- entering recovery;
- death;
- another explicit control-loss state.

External pushes that do not cause such a state do not interrupt the cast.

Therefore:

- ordinary damage without launch → cast continues;
- Exploder damage/push → cast continues unless it causes loss of control;
- another Wizard's zone → damage/push applies, but cast continues unless it launches/controls the Wizard.

If Wizard enters `Launched` while its normal moving zone is active:

- moving zone disappears immediately.

After recovery:

- Wizard gets the normal **2 s cooldown** before cast checks resume.

### 9. Launched Wizard special behavior

Wizard behaves like a normal launched projectile for ordinary collision rules.

Additionally:

**On the first qualifying collision of each genuine launch, it creates a stationary Wizard damage zone.**

Qualifying collision:

- live enemy;
- player;
- BossPart.

Not qualifying:

- walls;
- ordinary environment;
- defeated/dying cleanup-state enemies.

Armored enemies still qualify even with intact armor.

Elites qualify.

Boss head and hands qualify, while their normal boss-specific launched-enemy interaction also happens unchanged.

The special effect:

- exactly **one stationary zone per launch**;
- re-launch impulses while already `Launched` do not reset the allowance;
- allowance resets only after leaving `Launched` and later entering a new genuine launch.

### 10. Stationary impact zone

On the first qualifying launched-Wizard collision:

- center = Wizard position at collision time, projected onto ground plane;
- activation is **immediate**;
- no telegraph;
- same radius as normal zone;
- same damage;
- same tick interval;
- same force mode/values;
- same active duration;
- same visuals as normal active zone.

It remains fixed at the collision point and is completely independent from the Wizard afterward.

It continues for its full duration even if:

- Wizard keeps flying;
- Wizard recovers;
- Wizard casts again;
- Wizard dies.

The source Wizard is immune to its own stationary zone.

Other Wizards can be damaged by it.

It stacks independently with all other Wizard zones.

After recovery, the Wizard may start another normal cast even while its stationary impact zone is still active.

If a normal active cast was interrupted by the launch:

- normal moving zone disappears;
- that launch still retains its one stationary-zone trigger.

### 11. Collision + impact-zone damage

The collision target receives:

- normal launched-Wizard collision damage;
- **plus** the impact zone's immediate entry hit.

For a player collision specifically, both are guaranteed even if the collision itself just activated player invulnerability.

Likewise, creation of the zone does not depend on whether the player was already invulnerable before impact.

### 12. Deferred-death launched enemies

Existing deferred-death behavior stays intact.

If a launched enemy reaches 0 HP from Wizard damage:

- it remains a usable projectile until the launch resolves;
- further Wizard-zone damage does not reduce health further or alter the deferred-death result;
- Wizard-zone force may still affect/redirect its flight.

### 13. Hazard avoidance

Other enemies should try to avoid:

- a Wizard's active moving zone;
- active stationary impact zones.

Wizards avoid zones belonging to other Wizards.

A Wizard ignores its own zone for avoidance.

However, a committed casting Wizard does not cancel or reposition out of another Wizard's hazard if its current cast movement mode says to stand still.

### 14. Animation

`Wizard.fbx` should be inspected for embedded animation clips.

- `Dance` is reserved for casting.
- `Dance` loops continuously through Telegraph + Active.
- It stops immediately if casting is interrupted.
- Normal launched/recovery presentation then takes over.
- Other suitable embedded clips should be used for idle/locomotion if available.
- Root motion disabled.
- ECS/gameplay movement remains authoritative.

### 15. Visual/readability treatment

Wizard permanent role tint:

- **purple/violet**.

Telegraph and active zone use the same color family:

- softer/pulsing violet telegraph;
- stronger/brighter/more saturated active zone.

Wizard body also:

- brightens/pulses during telegraph;
- gets stronger active-cast treatment during Active.

Ground visualization:

- procedural/simple project-native geometry;
- full-radius ground disc;
- clear outer ring;
- exact radius visible throughout Telegraph and Active.

Overlapping zones visually blend/add, making overlaps look more dangerous.

Stationary impact zones use exactly the same active-zone visual language.

### 16. ECS implementation decisions

Wizard should use a dedicated explicit cast state/component.

Every active Wizard zone should be a **first-class ECS entity**, including:

- moving normal zones;
- stationary impact zones.

Zone entity should contain the information required for:

- source Wizard;
- follow/fixed mode;
- position;
- radius;
- lifetime;
- damage/force behavior;
- ownership/immunity.

Per-target timing state:

- stored in a **DynamicBuffer on each zone entity**.

When a target leaves:

- remove its entry immediately.

Target detection:

- use existing ECS spatial/crowd-query infrastructure;
- gather nearby candidates;
- perform exact XZ overlap test afterward;
- update membership **every simulation update**.

Membership should account for target collision/physical radius rather than only target center.

### 17. Gauntlet_17

Wizard introduction level is:

**Gauntlet_17**

Structure:

**Wave 1**
- 6 Baseline
- 1 Wizard

**Wave 2**
- 12 Baseline
- 2 Wizards

Wave 2 begins only after:

- every Wave 1 enemy is dead;
- all persistent Wizard zones from Wave 1 have expired.

Wizard counts are fixed and never replenished.

Arena:

- Codex may design a simple Wizard-focused arena;
- enough space to read/exploit 4 m zones;
- compact enough for Wizards to remain relevant;
- no extra hazards;
- only minimal geometry/cover where useful.

### 18. Gauntlet_17 tutorial

Show one short non-blocking hint at level start.

It should explain:

- Wizard creates a damaging zone;
- launched enemies are useful against it.

It should **not reveal** the Wizard's special stationary-zone behavior when the Wizard itself is launched. That should be discovered naturally.

Hint disappears automatically when combat begins / after a short duration.

### 19. Baseline “ammunition” safeguard

Gauntlet_17 enables a reusable wave-system feature:

- while at least one Wizard remains alive, ensure at least one Baseline enemy is available.

Reusable setting:

- default **off**;
- enabled for Gauntlet_17.

“Available” means any live non-defeated Baseline:

- Active;
- Launched;
- Recovering.

If count reaches zero:

- use normal replenishment/spawn pipeline;
- normal delay/placement rules apply;
- safeguard merely prevents zero, it does not cap ordinary baseline count.

Safeguard-spawned enemies:

- are normal wave enemies;
- once spawned, must still be defeated.

If a replacement is queued but the last Wizard dies before it spawns:

- cancel the queued replacement.

If already spawned:

- it remains.

### 20. Persistent-hazard wave completion

Reusable wave option:

- **wait for persistent hazards to clear before advancing**;
- default off;
- enabled in Gauntlet_17.

### 21. General wave integration

Wizard becomes a normal weighted archetype for later levels, like Baseline/Ranged/Explosive/Dasher.

Gauntlet_17 simply uses authored fixed Wizard counts.

Add optional **per-wave maximum Wizards alive**:

- default = unlimited.

When the cap is reached and weighted selection rolls Wizard:

- reroll among other currently eligible weighted archetypes;
- do not delay or skip the spawn.

---

# Rejected alternatives

The major alternatives we explicitly rejected were:

- always-active Wizard aura;
- player-only AoE;
- making Wizard immune to other Wizards;
- allowing Wizard AoE to damage BossParts;
- letting Wizard AoE affect puzzle/environment targets;
- LOS/cover blocking the AoE;
- LOS requirement for deciding to cast;
- randomized cooldowns;
- cancelling cast merely because player leaves range;
- damage itself interrupting casts;
- giving Wizard a normal contact attack;
- disabling aim assist/trajectory preview for Wizard;
- shared/global zone tick cadence;
- global player invulnerability across all Wizard zones;
- waiting one tick before first damage;
- preserving tick timers across exits/re-entries;
- grace period for rapid zone-boundary re-entry;
- center-only target radius checks;
- making Wizard exceptionally tanky;
- suppressing Wizard-vs-enemy kills;
- making small push count as a launch;
- forcibly re-launching an already launched target on every strong-zone tick;
- making stationary impact zone follow the Wizard;
- giving impact zone another telegraph;
- cancelling impact zone when Wizard dies;
- spawning more than one impact zone from one launch;
- resetting impact-zone allowance on mid-flight redirects/relaunch impulses;
- environmental collisions triggering impact zone;
- excluding Elite/Armored/BossPart collisions from impact-zone triggering;
- making impact-zone damage replace ordinary collision damage;
- preventing a recovered Wizard from casting while its detached zone remains alive;
- forcing enemies to ignore stationary hazards;
- allowing hazard avoidance to break a committed stationary cast;
- root-motion-driven Wizard movement;
- using shared/global cast state instead of Wizard-specific state;
- hard-coding Gauntlet_17-specific wave behaviors rather than making reusable settings;
- revealing the launched-Wizard special mechanic in the tutorial;
- putting unrelated hazards into the Wizard introduction level.

---

# Open questions

There are effectively **no gameplay-design blockers left**.

The remaining decisions are implementation-level details Codex can reasonably determine from the repository, such as:

- exact component/type names;
- exact system ordering;
- how to integrate Wizard spatial queries with the repo's current spatial data structures;
- precise DynamicBuffer struct layout;
- whether moving and stationary zones share one component with a mode flag or use small companion components;
- exact procedural mesh/material implementation for the ground disc/ring;
- exact purple/violet material values;
- which non-`Dance` embedded Wizard clips map best to idle/movement;
- exact Gauntlet_17 arena dimensions and object placement;
- exact tutorial wording;
- exact serialized field/property names;
- any small adjustments required to preserve compatibility with existing saved `EnemySpawnSettings`/enum serialization.

So from a design/specification perspective, **the Wizard is ready for a Codex implementation prompt.**
