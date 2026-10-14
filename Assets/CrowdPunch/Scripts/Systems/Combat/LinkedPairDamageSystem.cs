using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateBefore(typeof(ExplosionResolutionSystem)), UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct LinkedPairDamageSystem : ISystem
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.LinkedPairDamage");
        private NativeParallelHashMap<Entity, PreviousTarget> previousTargets, currentTargets;
        private struct PreviousTarget { public float3 Position; public uint Lifetime; }
        public void OnCreate(ref SystemState state) {
            previousTargets = new NativeParallelHashMap<Entity, PreviousTarget>(1024, Allocator.Persistent);
            currentTargets = new NativeParallelHashMap<Entity, PreviousTarget>(1024, Allocator.Persistent);
            state.EntityManager.AddBuffer<LinkedPairPlayerHit>(state.EntityManager.CreateEntity());
            state.RequireForUpdate<LinkedPair>(); state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        public void OnDestroy(ref SystemState state) { previousTargets.Dispose(); currentTargets.Dispose(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto(); state.Dependency.Complete();
            var em = state.EntityManager; var hits = SystemAPI.GetSingletonBuffer<LinkedPairPlayerHit>(); hits.Clear();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var heads = SystemAPI.GetComponentLookup<SnakeHead>(true);
            var segments = SystemAPI.GetComponentLookup<SnakeSegment>(true);
            var armors = SystemAPI.GetComponentLookup<EnemyArmor>(true);
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            double now = SystemAPI.Time.ElapsedTime; float dt = SystemAPI.Time.DeltaTime;
            currentTargets.Clear();
            int count = SystemAPI.QueryBuilder().WithAll<Enemy>().Build().CalculateEntityCount();
            if (currentTargets.Capacity < count) currentTargets.Capacity = count;
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            using var cells = new NativeParallelMultiHashMap<int2, int>(math.max(1, SystemAPI.QueryBuilder().WithAll<Enemy>().Build().CalculateEntityCount()), Allocator.Temp);
            float padding = 0;
            foreach (var (pose, agent, life, velocity, launch, health, target) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLifetime>, RefRO<PhysicsVelocity>, RefRO<EnemyLaunchState>, RefRO<Health>>()
                .WithAll<Enemy>().WithNone<BossPart, RespawnRequest>().WithEntityAccess()) {
                if (SnakeHitRules.Protected(target, heads, segments) || armors.HasComponent(target)
                    && (armors[target].Stages > 0 || now < armors[target].ProtectedUntil)) continue;
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Defeated || health.ValueRO.Current <= 0) continue;
                float3 old = previousTargets.TryGetValue(target, out var previous) && previous.Lifetime == life.ValueRO.Generation
                    ? previous.Position : pose.ValueRO.Position;
                currentTargets.TryAdd(target, new PreviousTarget { Position = pose.ValueRO.Position, Lifetime = life.ValueRO.Generation });
                int index = candidates.Length;
                candidates.Add(new Candidate { Entity = target, Position = pose.ValueRO.Position, OldPosition = old,
                    Radius = agent.ValueRO.Radius, Lifetime = life.ValueRO.Generation });
                cells.Add((int2)math.floor(pose.ValueRO.Position.xz / 4), index);
                padding = math.max(padding, agent.ValueRO.Radius + math.distance(old.xz, pose.ValueRO.Position.xz));
            }
            foreach (var (pairRef, tuning, clocks, record) in SystemAPI.Query<RefRW<LinkedPair>, RefRO<LinkedPairTuning>, DynamicBuffer<LinkedPairClock>>().WithEntityAccess()) {
                var pair = pairRef.ValueRO; var t = tuning.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(em, pair.A, out _, out _) || !em.IsEnabled(pair.A) || !em.IsEnabled(pair.B)) continue;
                float3 a = em.GetComponentData<LocalTransform>(pair.A).Position, b = em.GetComponentData<LocalTransform>(pair.B).Position;
                float3 oldA = pair.HasPrevious != 0 ? pair.PreviousA : a, oldB = pair.HasPrevious != 0 ? pair.PreviousB : b;
                if (player.IsAvailable && LinkedPairGeometry.Sweep(oldA, oldB, a, b, player.Position - player.Velocity * dt,
                    player.Position, t.Width * .5f + player.Radius, world, enemies, out _, out _) &&
                    LinkedPairGeometry.Tick(clocks, Entity.Null, 0, now, t.TickInterval)) hits.Add(new LinkedPairPlayerHit { Damage = t.PlayerDamage });
                if (pair.Phase == EnemyLaunchPhase.Launched) {
                    float reach = padding + t.Width * .5f;
                    float2 lo = math.min(math.min(a.xz, b.xz), math.min(oldA.xz, oldB.xz)) - reach;
                    float2 hi = math.max(math.max(a.xz, b.xz), math.max(oldA.xz, oldB.xz)) + reach;
                    int2 min = (int2)math.floor(lo / 4), max = (int2)math.floor(hi / 4);
                    for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
                    foreach (int index in cells.GetValuesForKey(new int2(x, z))) {
                        var c = candidates[index];
                        if (c.Entity == pair.A || c.Entity == pair.B || !LinkedPairGeometry.Sweep(oldA, oldB, a, b,
                            c.OldPosition, c.Position, t.Width * .5f + c.Radius, world, enemies, out var point, out var motion)) continue;
                        if (!LinkedPairGeometry.Tick(clocks, c.Entity, c.Lifetime, now, t.TickInterval)) continue;
                        var source = em.GetComponentData<EnemyLaunchState>(pair.Leader);
                        float3 direction = math.normalizesafe(motion, math.normalizesafe(c.Position - point,
                            math.normalizesafe(em.GetComponentData<PhysicsVelocity>(pair.Leader).Linear, new float3(0, 0, 1)))); direction.y = 0;
                        Hit(em, c.Entity, source, pair.Leader, direction, t, now);
                    }
                }
                // Other pairs may have changed this record while resolving pair-on-pair propagation.
                pair = em.GetComponentData<LinkedPair>(record); pair.PreviousA = a; pair.PreviousB = b; pair.HasPrevious = 1;
                pairRef.ValueRW = pair;
                for (int i = clocks.Length - 1; i >= 0; i--) if (now >= clocks[i].NextHitAt) clocks.RemoveAt(i);
            }
            var swap = previousTargets; previousTargets = currentTargets; currentTargets = swap;
        }
        private struct Candidate { public Entity Entity; public float3 Position, OldPosition; public float Radius; public uint Lifetime; }

        internal static void Hit(EntityManager em, Entity target, EnemyLaunchState source, Entity leader, float3 direction, LinkedPairTuning t, double now)
        {
            if (Protected(em, target, now)) return;
            bool attached = SnakeHitRules.Attached(em, target);
            // Attached tips require a launch to detach; damage alone cannot bypass SNAKE-002.
            if (attached && t.Response == LinkedPairResponse.DamageAndPush) return;
            if (em.GetComponentData<EnemyTier>(target).Value == EnemyCombatTier.Boss) return;
            var launch = em.GetComponentData<EnemyLaunchState>(target);
            if (launch.Phase == EnemyLaunchPhase.Defeated) return;
            bool flying = launch.Phase == EnemyLaunchPhase.Launched;
            bool normal = EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(target));
            if (!flying) {
                if (normal && t.Response == LinkedPairResponse.DamageAndLaunch) {
                    EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.EnemyCollision, source.LaunchDamage, source.Owner);
                    launch.FeedbackChainDepth = math.min(64, source.FeedbackChainDepth + 1);
                    em.SetComponentData(target, launch);
                    em.SetComponentData(target, new PhysicsVelocity { Linear = direction * t.LaunchSpeed });
                    em.SetComponentEnabled<ExternalImpulse>(target, false);
                    LinkedPairLaunchResolution.Begin(em, target, launch, direction * t.LaunchSpeed);
                } else {
                    var velocity = em.GetComponentData<PhysicsVelocity>(target); velocity.Linear.xz += direction.xz * t.PushSpeed; em.SetComponentData(target, velocity);
                }
            }
            if (em.HasComponent<ExplosiveEnemyState>(target) && em.GetComponentData<ExplosiveEnemyState>(target).HasExploded == 0)
                em.SetComponentEnabled<ExplosiveDetonationRequest>(target, true);
            var damage = em.IsComponentEnabled<DamageRequest>(target) ? em.GetComponentData<DamageRequest>(target) : default;
            damage.Amount += t.EnemyDamage; em.SetComponentData(target, damage); em.SetComponentEnabled<DamageRequest>(target, true);
            if (!attached) EnemyDamageResolution.ApplyPending(em, target, now, source.Owner, leader, source.FeedbackChainDepth + 1, em.GetComponentData<EnemyLifetime>(leader).Generation);
        }
        private static bool Protected(EntityManager em, Entity target, double now) => SnakeHitRules.Protected(em, target)
            || em.HasComponent<EnemyArmor>(target) && (em.GetComponentData<EnemyArmor>(target).Stages > 0 || now < em.GetComponentData<EnemyArmor>(target).ProtectedUntil);
    }
}
