# Barricade spawn-room movement verification

Date: 2026-10-11. Requirement: BARRICADE-008.

## Implementation

Normal AI uses the `BarricadeRoom` belonging to `EnemyWaveOwnership.Sequence`,
including after its gate breaks. Reserve targets cover that room's interior.
Off-room players do not attract pursuers or consume that room's pressure slots.
Surround/contact destinations and blocked terrain/hazard alternatives remain local.
Terrain and hazard A* requests originating inside cannot detour through another room.
Active movement brakes at the room edge; physics positions are never clamped.
Launched/recovering bodies remain free to cross opened gates and route back afterward.
Owners without a room retain ordinary arena behavior. No scene or tuning changes are
required: the six progressive campaign levels already bake owner bounds.

## Automated verification

- Unity compilation succeeded with Burst enabled.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1`: zero errors, 344 existing warnings.
- Barricade filter: 35 passed, including 16 new spawn-room movement cases.
- Enemy arena distribution: 13 passed.
- Navigation system: 13 passed; navigation/ground-hazard geometry and routing: 29 passed.
- Armored enemy: 20 passed; Dino pillar: 31 passed; protected point: 17 passed.

The new tests exercise stale global distribution targets, spread throughout the local
room, adjacent-player pursuit cancellation, next-room pressure allocation, backtracking,
displaced-body reentry without teleporting, braking at walking/return/charge speeds,
unmodified launched/recovering velocity, non-room fallback, blocked local destinations,
and rejection of a route that requires leaving the territory.
The hazard regression suite initially exposed a Burst-only null reference from a default
argument passed by reference. The bounds calculation no longer takes that unused argument;
the affected suites passed again with Burst enabled.

## Live baked-world verification

Played through Bootstrap with a temporary completed campaign save; the normal save was
not modified. Controlled setup activated every room, waited for its bounded initial
crowd, opened intermediate gates and placed the player in the final room. Real AI,
navigation, hazards and physics advanced. Player/living enemy health was restored for
observation; one injected shot used extra health to isolate recovery from combat death.
This is a controlled runtime check, not human combat or pacing evidence.

- Level 22: 28 roots; 719 sampled updates, up to 28 active enemies. 26 bodies moved more
  than 1m. Off-room survivors used local coverage targets with the gate open.
- Level 74: 126 roots; 720 sampled updates, up to 126 active enemies. All 126 bodies moved
  more than 1m. Every sampled travel destination and successful resolved navigation goal
  remained inside its owner's room, including hazard routing in the seven-room hall.
- Both levels: a physically launched body crossed the first opened gate, recovered and
  returned to its original room while retaining ownership. The first level-22 body died
  after crossing; repeating with extra health verified recovery independently.
- Old global wander destinations were deliberately injected and replaced when coverage
  resumed. Pursuing enemies' unused wander caches were excluded from that assertion.

Editor profiler observations (milliseconds; no target-device performance claim):

| Level | Main Thread median / max | Navigation median / max | Hazard navigation median / max |
| --- | --- | --- | --- |
| 22 | 16.640 / 26.032 | 0.022 / 0.066 | 0.003 / 0.082 |
| 74 | 16.600 / 25.852 | 0.074 / 0.302 | 0.253 / 0.814 |

Raw probe and build logs remain in ignored `Temp/BarricadeRoomMovementValidation/`.
OQ-001's target hardware and performance criteria remain unresolved.
