# Launch impact preview - 2026-10-31

The later option-2 aiming pass adds bounded moving-target prediction and guidance.
See [Stable launch aiming](StableLaunchAiming.md) for current behavior and checks;
the original implementation record below documents the initial static preview.

Requirements: PLAYER-003/004, COMBAT-003, INFO-005.

## Implemented slice

The shared `Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset` has separate
Direct Launch Arrow and Predicted Collision Arrow controls. Length is in world
metres, width controls the shaft and proportionate head, and both colours include
alpha. Saved defaults: direct 5m / 0.36m / white at 0.5 alpha; collision 3m / 0.3m /
amber at 0.5 alpha. Collision Prediction Distance defaults to 18m and is independent
of displayed dimensions and aim-assist range. Setting either length to zero hides
only that arrow type. Inspector edits are read during play.

The ECS presentation bridge retains exact direct-launch aim-lock directions. Each
punched enemy collider is swept to its first blocking contact. Eligible victims
receive one arrow in their estimated outgoing direction, including ordinary
propagation aim correction. Self, triggers and Snake owner exclusions do not block
the cast. Solid cover, protected armor, non-launchable enemies and bodies already
launched or punched simultaneously do not advertise another launch. The estimate
honours propagation impulse thresholds and does not mutate gameplay state.

## Performed checks

- Unity 6000.3.10f1 imported both scripts and generated their metadata; compilation
  completed with no new errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v quiet` passed. Existing Unity
  package assembly-version and deprecated-API warnings remain.
- `LaunchImpactPreviewTests`: 15/15 passed. Includes nearest contact, off-centre
  normal direction, correction enabled/disabled, blocking walls versus triggers,
  lifecycle/protection, impulse/mass semantics, search distance, simultaneous
  punches, independent lengths/colours/alpha, clearing and target deduplication.
- Existing `SnakeEnemyTests` 44/44, `ArmoredEnemyTests` 20/20 and
  `PlayerPunchCooldownTests` 12/12 passed.
- Warmed Editor benchmark, 100 updates, eight converging punch sources:
  100 bodies averaged 1.106ms/update; 1,000 bodies averaged 3.796ms/update.
  This measures the presentation bridge and collider queries, excluding simulation
  steps and rendering. Positions are copied once into native arrays; expensive
  eligibility checks run only for spatially relevant improving correction choices.
  The initial managed lookup loop was replaced after its 1,000-body benchmark
  exposed excessive cost. These measurements are not a player-build frame budget.
- Rendered and inspected the two reusable arrow meshes with their actual material
  and saved RGBA colours in an isolated Unity preview scene. Confirmed distinct
  styles, filled silhouettes and uniform transparency. Temporary output:
  `Temp/LaunchImpactPreview.png`. Preview scene and owned objects were disposed.

## Limits and remaining human checks

This predicts the first straight-line body collision from the current arrangement,
not a recursive chain. Frictionless momentum transfer is an estimate; future target
motion, gravity, collision friction/restitution, multiple solver contacts, homing
turns and linked-pair swing can change it. Projectiles retain their existing direct
arrows; the extension applies to launched enemy bodies. Environmental rebound paths
remain outside this slice.

No manual campaign playthrough or target-hardware player-build profile was performed.
Check readability over actual crowded scenery and tune the two colours/alpha/widths
as desired. OQ-001 still owns final crowd-size, hardware and frame-rate targets.
The pre-existing Sidekick Tool Downloader editor-window error remains unrelated.
