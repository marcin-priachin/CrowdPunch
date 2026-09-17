# Static obstacles and enemy navigation

This slice preserves the GameObject player / Entities enemy boundary, continuous velocity steering, local separation (COMBAT-013), distributed pressure (COMBAT-016), separate spacing and defeat bounds (COMBAT-017), and ground-plane physics (COMBAT-018). It adds static rectangular solids and route assistance. It does not change combat damage, progression, or unresolved design questions.

## Authoring and data ownership

`SolidObstacleAuthoring` exposes integer footprint dimensions, a placement unit in world metres, and height. Place obstacles beneath the arena's `NavigationArenaAuthoring`. The custom Inspector snaps the footprint to the spacing-bounds origin and resizes a child blocking silhouette. Keep the obstacle transform axis aligned and unit scale. Change the footprint fields, rather than stretching the root transform. Decorative child meshes may vary inside the blocking silhouette; do not add a second collider to the visual child.

`SolidObstacleBaker` creates one static Unity Physics box matching that footprint. Its Default collision category blocks enemy colliders and the existing `PlayerObstacleCollisionSystem` bridge sweep. It has no velocity component. The player remains GameObject-owned. Ground and decorative geometry are not navigation obstacles unless explicitly authored as solids.

`NavigationArenaBaker` uses the actual world XZ spacing rectangle from `ArenaAuthoring`, independently of defeat bounds. It rasterises only explicit solid footprints into a blob with three clearance classes. Placement units and navigation cell size are separate: changing resolution does not resize obstacles. Coordinates use the rectangle minimum as the grid origin; actor transforms remain continuous.

Each class rounds an enemy's horizontal collider radius up, then adds the safety margin. Prefab baking derives capsule/sphere horizontal radius from their transformed shape; other collider shapes use conservative bounds. Oversized enemies are rejected instead of using insufficient clearance. Cell centres, every traversable edge, direct segments, and shortcuts use the same conservative rectangle inflation and inset arena boundary. Eight-direction edges additionally forbid diagonal corner cutting. Square corner inflation intentionally leaves more room than the exact rounded Minkowski boundary. Narrow passages can therefore be physically passable but unavailable to a larger navigation class.

The immutable blob stores rectangle footprints, class-major connected-region IDs, and eight edge bits per cell. Region zero is blocked. Baking flood-fills each clearance class once; simulation never rebuilds the grid. There is a 65,536-cell authoring limit. Future hazard costs can extend the cost representation independently of solid connectivity; no hazard simulation or dynamic occupancy is implemented.

## Tactical intent and system order

The pre-physics flow is:

1. `PlayerBridgeSystem` publishes the player snapshot.
2. `EnemyChaseSystem` selects coverage, surround, and contact goals and calculates the existing per-archetype separation input.
3. Ranged, Dasher, Elite punch, and Elite crowd-support systems publish their explicit tactical overrides.
4. `EnemyNavigationSystem` resolves direct travel or a route, combines its travel direction with the published local separation, and validates the resulting terrain clearance.
5. `EnemyMovementSystem` steers `PhysicsVelocity`; `DasherMovementSystem` retains committed dash ownership. Combat impulses and Unity Physics follow their existing ordering.

The navigation attributes explicitly follow **all** intent writers and precede both movement systems. `NavigationIntent` carries destination, speed, arrival distance, goal kind, separation, and Hold/Travel/Committed mode. `DesiredMovement` remains the final direction/speed consumed by movement, and is also written with legacy steering by AI for disabled-assistance comparison. Navigation never infers a destination from that blended direction and never writes transforms or physics velocity.

`NavigationPathState` owns requested/resolved destinations, version, request/progress times, travel state, and waypoint index. A `NavigationWaypoint` buffer stores the bounded cached route. `NavigationAgent` stores the actual horizontal body radius. Runtime components contain no UnityEngine object references.

## Routes and resource bounds

Safe direct movement is preferred. A blocked segment queues A* against the existing reachable region. FIFO admission and round-robin node expansion share a global update budget across a fixed number of concurrent searches. Search nodes, heap, and stamps are persistent native scratch storage; there is no managed per-enemy search object or per-frame grid rebuild. The entire navigation update is Burst compiled. Direct checks, bounded smoothing, steering, and goal resolution still have a per-actor cost outside the A* expansion budget.

The scheduler distinguishes an unfinished search from exhausted-open-set unreachability and from expansion/path-storage limits. Unfinished searches retain their heap and node state. Queue capacity, concurrent slots, per-search expansions, and per-enemy stored waypoints are separately bounded. A valid previous route can continue while its replacement waits; otherwise movement brakes. No budget failure authorizes travel through a wall.

Every request carries the full Entity handle and goal version. Admission and completion reject stale identity/version/state. Meaningful goal movement, invalid route segments, substantial displacement, lack of progress, or a new navigation grid can trigger replanning. Small direct-goal movements track continuously when safe. Repath cooldown, deterministic request offsets, stuck duration, and failure delay prevent transient crowd congestion from producing frame-by-frame retries. Path following skips waypoints only through clearance-safe shortcuts.

Separation retains the original active-neighbour collections and profile override rules. The final steering direction is checked over look-ahead/braking distance; blocked directions reduce safe speed or fall back to the route direction. Physics remains the final safeguard. If physics pushes a body inside the navigation margin, a bounded escape uses actual collider clearance to regain a valid nearby centre. There is no crowd hard occupancy or actor grid snapping.

## Behavior integration

- Baseline and explosive enemies retain pressure caps, stable coverage assignments, surround slots, and contact behavior. Invalid goals resolve through deterministic per-entity alternatives; coverage can use distributed reachable arena cells instead of collapsing onto one nearest fallback. Committed lunges and windups keep their original modes (COMBAT-016, ENEMY-010).
- Ranged enemies select reachable preferred-distance ring candidates for approach/retreat and retain Hold. A wall-blocked retreat can travel around the distance band instead of repeatedly choosing an outward vector (ENEMY-001).
- Dashers use the same distance-band navigation only while positioning. Preparation, straight committed dashes, existing obstacle stopping, and recovery remain under their existing systems (ENEMY-005).
- Elites publish punch setup/side-detour and staging destinations, preserving reservations, closest-normal selection, line-up checks, windup, and cancellation. Exact unreachable setup positions fail into existing cooldown/cancellation; they are not silently replaced by arbitrary distant coverage positions (ENEMY-009).
- Non-Active and pooled entities invalidate navigation without changing their motion. Returning Active resolves from the actual physics position. Pooling, soft restart, grid replacement, and scene unload clear or invalidate cached state (LOOP-006).

## Spawning and lifecycle

The participation anchor identifies the playable connected region separately for each clearance class. By default, baking uses the world XZ centre of the enemy spacing bounds. If blocked, it selects the nearest grid cell centre clear for all three configured classes, with deterministic grid-order ties. This selection runs only during baking. `NavigationArenaAuthoring.overrideParticipationAnchor` enables an explicit world XZ point for layouts whose centre is in the wrong region; invalid overrides are reported and never silently relocated. Existing serialized anchor coordinates are ignored unless the override is enabled. No common valid point produces a single authoring error; adjust geometry or clearance settings. Bounds-derived selection cannot infer which disconnected region the designer intends.

Initial random, authored, wave, and respawn paths reject insufficient clearance and disconnected locations. Wave placement retains its existing Unity Physics clearance and same-update neighbour checks. A bounded static feasibility pass at wave start reports an impossible configured profile/range once and puts that wave in the inspectable Invalid state. Ordinary dynamic congestion retains bounded attempts and throttled retries.

Initial spawners consume their source components via ECB after processing, rather than permanently disabling the spawn system. Newly loaded scene inputs can therefore initialize once. Respawn samples inset edges, since an exact spacing-bound edge is not collider-clear. Soft restart uses bounded valid random samples and resets path versions; invalid random placement stays pooled.

`EnemyNavigationSystem` intentionally runs without a grid RequireForUpdate gate so it can dispose search scratch and empty requests during unload. A changed blob or grid Entity recreates scratch and invalidates surviving routes. World destruction disposes native storage. Baked blob lifetime belongs to Entities scene/baking ownership.

## Settings and inspection

Use `Assets/CrowdPunch/Data/Settings/NavigationSettings.asset`. All ScriptableObject fields are baked: edit outside Play mode and rebake/reload the arena. Grid resolution, margin/classes, budgets, and capacities are not advertised as live controls. The Navigation editor window offers an explicitly live assistance toggle and Scene drawing toggle; these are temporary runtime comparison controls. Disabling assistance preserves legacy AI steering while solid collision and spawn clearance remain active.

Conservative defaults: 1 m cells, 0.12 m margin, 0.5/0.85/1.5 m base radius classes; 0.2 s direct cadence; 0.25 m waypoint arrival; 0.7 m minimum look-ahead; eight smoothing candidates; 1.5 m destination threshold; 0.6 s repath cooldown plus up to 0.3 s staggering; 2.5 s stuck duration / 0.3 m progress; 3 s failure delay; 512 global expansions, 4,096 per search, four concurrent searches, 128 waypoints, and 4,096 queued requests.

Open **Crowd Punch > Navigation > Inspect**. Select a clearance class to see blocked cells and reachable regions. Goal lines, resolved destination discs, pending requests, routes, and current waypoint are color coded; counters expose queue/search sizes, expansions, failures, limits, replans, stale results, and rejected spawns. `CrowdPunch.Navigation` is the Profiler CPU marker. `NavigationTimingSystem` records its last sampled frame cost for optional diagnostics. Debugging is editor-only and adds nothing to the normal HUD. **Benchmark Current Crowd** provides a completed-system-update comparison; its report explicitly excludes whole-frame claims.

The dedicated playable setup, measured validation, and combat limitations are recorded in [Navigation validation](../Validation/NavigationValidation.md).
