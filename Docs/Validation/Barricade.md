# Barricade and Gauntlet_13 validation

Validated in Unity 6000.3.10f1 on 2026-09-26. Requirements: BARRICADE-001..005,
PLAYER-003/004/009, COMBAT-003/014 and LOOP-006.

## Implemented content and tuning

- Gauntlet_13, **13 Break Through**, is available through Bootstrap's existing level selector
  and follows Gauntlet_12. Existing gauntlet layouts and enemy profiles were not changed.
- One shared three-hit wall blocks the full width of a broad 28 x 36 m court. Destroy it,
  then reach the green exit behind it. Crowd clearance is not a completion condition.
- Launched bodies of all ownerships count by default. Each source entity/current launch
  counts once, including combined explosive impact and blast. A new launch can count again.
  Independent explosions count through their existing radial resolution.
- Intact impacts rebound at 0.85 horizontal speed. The destroying hit swaps the collider
  before simulation and passes through. Post-physics response preserves the contact timing;
  swept contact-plane correction prevents intact-wall tunneling at extreme speed.
- Direct punches report connection for existing cooldown without damaging the wall.
  The existing target lock, range/angle limits, propagation correction, homing and short
  initial-direction preview accept the wall. Broken walls become ineligible.
- The saved crowd is 14 Baselines + 2 Explosives, continually reusing the bounded 16 roots.
  Replenishment waits 2 seconds after ordinary pooling. It uses the authored front-court
  spawn range and existing player/physics clearance checks, and stops at destruction,
  including already pending pool deadlines. Surviving enemies retain normal combat behavior.
- Visual stages are an intact plate, one crack row, then two crack rows, with impact particles,
  0.18 s flashes and 0.65 s destruction debris. No health bar is added. One opening instruction
  uses the existing nonblocking tutorial presentation.

Tuning lives in `Assets/CrowdPunch/Data/Settings/BarricadeSettings.asset`; crowd size/composition
and initial timing live in `Data/Settings/Waves/Progression/CP13_01_Barricade_Crowd.asset`.
These are baked settings: allow the SubScene to rebake after editing. Visual placement and exit
position/radius are scene authoring. The explicit **Crowd Punch > Levels > Build Barricade
Gauntlet 13** recipe rebuilds only this level; it resets its generated scene and crowd recipe,
while retaining an existing barricade settings asset.

## Performed checks

- Unity compiled the final runtime and Editor scripts. All **200 Editor tests passed**,
  including existing armor, boss, navigation, launch, feedback and progression regressions.
  New tests cover identity deduplication, ownership filters, surface punch detection and
  cooldown confirmation, assist limits/invalidation, break-plus-exit completion, and
  intact penetration correction versus broken-wall pass-through.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal` completed with **0 errors**.
  It reported 454 warnings from the package/reference graph and existing deprecated aspect
  code. The ordinary parallel invocation failed without compiler diagnostics; the sequential
  invocation completed. Unity compilation remains the source of truth.
- `BarricadeEncounterPlayCheck.Start()` ran against the actual baked level and fixed-step
  physics. It verified 16 allocated crowd roots; an elite-owned rebound; that rebound launching
  another enemy through ordinary solver collision while retaining ownership; repeated-contact
  deduplication; explosive impact plus blast counting once; a boss-owned destroying shot
  passing beyond z=11; no completion on break alone; stopped replenishment including an
  injected pending pool deadline; living survivors; exit completion; and restart restoring
  wall, history and crowd. An injected pooled body respawned into the front court using the
  existing placement path. This check places bodies, injects velocities and restores player
  health; it proves integration, not natural encounter balance.
- Gameplay-camera captures were inspected for the intact wall and damage stages. Captures
  and machine-readable check output are under ignored `Temp/BarricadeValidation/`.

## Measured cost and limits

`BarricadeCrowdPerformanceCapture.Capture()` was run in the paused baked scene. It rebuilds
the real collision world at 16 and 128 roots, places a synthetic distributed launched crowd,
then measures the barricade sweep system for 20 warmups and 200 samples. This isolates the
new query cost; it does not measure a full frame, rendering, natural encounter duration or
worst-case simultaneous contacts. Most distributed bodies are rejected by the swept AABB.

| Distributed launched roots | Mean sweep update | p95 | Maximum |
|---|---:|---:|---:|
| 16 (authored allocation) | 0.0457 ms | 0.0481 ms | 0.2305 ms |
| 128 (synthetic stress) | 0.2106 ms | 0.2209 ms | 0.3050 ms |

The 26 presentation entities averaged about 0.036 ms per update. Before adding the AABB
rejection, the same distributed samples averaged 0.2412 ms and 2.4592 ms respectively.
The benchmark changes only Play Mode state; exit Play Mode afterwards. OQ-001's overall
hardware/frame-rate targets remain unresolved.

## Playtest still required

- Readability and feel of the three stages, rebound strength, and destroying-hit passage at
  ordinary and oblique angles, especially in a compressed crowd near the wall.
- How readily players discover aimed body shots using the existing center-targeted aim lock.
  Preview deliberately retains the existing short initial direction; no rebound path is drawn.
- Whether 14 Baselines, 2 Explosives and the 2-second pool delay provide useful ammunition
  without overwhelming the introductory objective. These are provisional defaults.
- Whether the green exit is clear after destruction and the still-hostile survivors make the
  final run to it satisfying. Test normal keyboard/mouse and gamepad play, death and restart.

No second level, separate alignment mechanic, health bar, direct-punch damage, or surviving
enemy neutralization was added. No major design decision was deferred; balance, final art
and subjective input feel remain playtest work rather than verified outcomes.
