# Linked Pair Goblin presentation

2026-10-25. Requirements: LINK-001..007, INFO-004, VISION-006.

## Authored content

- `Assets/CrowdPunch/Prefabs/EnemyLinkedPair.prefab` now wraps `Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Linked Variant.prefab`. All source transform scales are retained, including its 1.3 root scale; no fitting/resizing pass runs.
- Textured `CrowdPunch/Cel/Enemy` materials retain the supplied palette, and the shared appearance pass preserves them. LOD switching is disabled. The Goblin animation profile bypasses procedural body tint, shaking and deformation.
- Trooper clips supply Idle, Take Damage, Spawn, Jump In Place frame 6 for flight, and Die/reverse-Die for defeat/recovery. Giant Walk/Run Forward In Place supply movement upper-body motion. The eight directional jogs from `Assets/CrowdPunch/Animation/EnemyMovement.controller` replace leg bones only.
- Samples correct floor penetration through translation only, including the actual +90 degree flight pose. Spawn retains its authored emergence motion. Collider dimensions remain unchanged.
- `LinkedPairEnemySpawnSettings.asset` enables Spawn From Ground. The pair recovery clock owns both emergence animations, then releases both endpoints together. Punching cancels emergence through the existing shared launch path.
- `LinkedPairHandAnchors` stores palm bone indices and marker bind positions. Tether rendering follows the final sampled skin at `+ L Hand` / `+ R Hand`, selecting the hand nearest the partner horizontally. Gameplay hit/cover/constraint geometry remains the existing planar span.
- The shared prefab/profile updates campaign Linked Pairs (introduced at level 41), finale supply and legacy Gauntlet 27. Encounter populations and tuning are unchanged.

## Verification

- Unity 6000.3.10f1 compilation passed.
- LinkedPair filter: 26/26 Editor checks passed, including independent health/shared launches, ticking/cover, recovery, warnings, hand choice, prefab scale/palette/clip wiring, and floor/culling bounds. The old Mushnub tilt test now checks Goblin clips with their actual flight rotation rather than the removed procedural tilt arc.
- EnemyGroundSpawn filter: 14/14 passed, including a new intact-pair shared Spawn completion regression.
- EnemyJogAnimation filter: 35/35 passed, including leg layering, upper-body preservation and state exclusions.
- Live legacy Gauntlet 27: observed both ground Spawn animations, return to Active, a real `PunchResolution` hit launching both endpoints, forced flight timeout into shared recovery, and return to Active. At Active, Launched and Recovering, actual tether mesh endpoints matched the selected animated hand markers within 1 mm. Runtime frames advanced from 449 to 993 during initial observation. No new baking/runtime gameplay errors appeared; the Editor retained its existing Synty window error and Pipeline command/timeout diagnostics.
- Inspected a live Game-view capture for textured cel shading and visible hand links. This is controlled Editor verification, not a controller/balance playtest or standalone build.
- Presentation microbenchmark at the authored maximum of three pairs: 0.0531 ms/update with 200 ordinary entities and 0.0536 ms/update with 1,000, after 20 warmups over 120 updates. This isolates hand lookup and tether mesh submission; it excludes animation evaluation, physics and GPU time and is not an FPS claim.
- `dotnet build Assembly-CSharp.csproj --no-restore` returned Build FAILED with zero diagnostics; Unity compilation is the build evidence.

Local transient evidence: `Temp/LinkedPairValidation/linked-tests.json`, `jog-tests.json`, `ground-spawn-tests.json`, `state-live.txt`, `presentation-live.txt`, `presentation-performance.txt`, and `Temp/LinkedPairDotnetBuild.log`. These files are not committed.

## Rebuild

Use **Crowd Punch > Enemies > Rebuild LinkedPair Prefab** outside Play Mode after source model/clip changes. **Apply Goblin Jog Legs** includes LinkedPair. Review hand switching, Spawn, launched pose and shared recovery through legacy Gauntlet 27 or campaign 41 after future animation changes.
