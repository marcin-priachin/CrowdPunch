# Non-invasive runtime optimisations - 2026-11-13

Scope: approved optimisation items 1-3 only. Preserve PLAYER-003/004, INFO-005 and
VISION-005/006. No gameplay tuning, visual assets, forecast budgets, system ordering,
physics, surround planning, separation, shadow or VFX-density changes.

## Changes

- Remove redundant RecalculateBounds calls immediately after SetTriangles in the
  dotted-preview, combat-sprite, Dragon-flame, LinkedPair and ground-hazard particle
  renderers. SetTriangles retains its default bounds calculation. Vertex, colour,
  index, material and draw data are unchanged.
- Gather aim-assist candidate entities/poses, Gatekeeper heads and ray scratch only
  when an eligible enemy or Chicken shot needs enabled aim assistance. All original
  per-source eligibility and lock clearing still run, including zero assist range.
  One snapshot is shared for that update and disposed in a finally block; nothing
  is cached across simulation steps. Target ranking and iteration order are unchanged.
- PresentationBridgeSystem owns reusable native trajectory scratch. All seven
  containers are cleared before every trace, including invalid-source early returns,
  and disposed when the system is destroyed. Capacity survives between sources and
  frames. The temporary overload remains available for standalone callers.

## Verification

Unity 6000.3.10f1 compiled the runtime and Editor tests. The required
`dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero errors and
422 existing/package warnings after granting access to installed SDK directories.
The initial sandboxed attempt could not read the SDK directory. No new warnings
were reported for the changed runtime files. `git diff --check` passed.

223 focused Editor cases passed:

| Suite | Passed |
| --- | ---: |
| DirectLaunchTrajectoryTests | 41 |
| StableLaunchAimTests | 46 |
| LaunchTrajectoryComparisonTests | 20 |
| CommittedLaunchTests | 20 |
| AuthoritativeLaunchTests | 79 |
| RicochetAimTests | 13 |
| CombatVfxTests | 4 |

New regressions verify exact equality of path geometry, physics samples, impact
accents, first contacts, rebound plans and committed path points between fresh and
reused scratch, across different sources and an intervening early return. Aim checks
cover unavailable-preview clearing/resume and Chicken-only acquisition, leaving
the punch volume and re-entry. Existing cases cover zero assist range, target
retention/switching, boss priority, occlusion and committed-path behaviour.

## Local cost probes

Synthetic Editor measurements, frozen physics, no rendering or whole-frame/GPU
measurement. These are diagnostic samples, not target-hardware acceptance under
OQ-001, and should not be compared directly with older sessions.

The trajectory probe uses eight sources, the existing 160-step budget and ten
measured preview frames per strategy after warmup, alternating measurement order.
Both strategies use the same forecast code; only scratch allocation differs.

| Crowd bodies | Fresh scratch, ms/frame | Reused scratch, ms/frame |
| --- | ---: | ---: |
| 100 | 33.606 | 32.229 |
| 1,000 | 35.647 | 35.165 |

The small timing differences do not establish a reliable frame-rate improvement.
Reuse removes seven per-source scratch allocations after initialisation/growth;
the forecast's collision-query workload remains unchanged.

Aim measurements use 20 warmups and 100 measured updates per state, including the
source scan and world-time update, with a built collision broadphase:

| Bodies | Active | Preview unavailable | No source in volume | Assist disabled |
| --- | ---: | ---: | ---: | ---: |
| 100 | 0.3022 ms | 0.0569 ms | 0.1065 ms | 0.1339 ms |
| 1,000 | 1.1472 ms | 0.4575 ms | 0.9249 ms | 0.9219 ms |

These are post-change workload measurements, not before/after speedup claims.
Candidate copies and ray scratch are skipped in the latter three states, while
the source scan and required state clearing remain. No new full-game visual
capture or standalone performance run was performed.
