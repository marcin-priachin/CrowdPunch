using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Profiling;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(EnemyFacingSystem))]
    public partial struct DragonFlameDamageSystem : ISystem
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.DragonFlameDamage");
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddBuffer<DragonPlayerHit>(state.EntityManager.CreateEntity());
            state.RequireForUpdate<DragonTuning>(); state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            state.Dependency.Complete(); var em = state.EntityManager;
            var hits = SystemAPI.GetSingletonBuffer<DragonPlayerHit>(); hits.Clear();
            double now = SystemAPI.Time.ElapsedTime;
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var armors = SystemAPI.GetComponentLookup<EnemyArmor>(true);
            var dragons = SystemAPI.GetComponentLookup<DragonTuning>(true);
            var lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true);
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            float maxRadius = 0;
            foreach (var (pose, agent, life, health, launch, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLifetime>, RefRO<Health>, RefRO<EnemyLaunchState>>()
                    .WithAll<Enemy>().WithNone<BossPart, RespawnRequest>().WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0 || launch.ValueRO.Phase == EnemyLaunchPhase.Defeated) continue;
                if (em.HasComponent<EnemyTier>(entity) && em.GetComponentData<EnemyTier>(entity).Value == EnemyCombatTier.Boss) continue;
                if (SnakeHitRules.Protected(em, entity) || SnakeHitRules.Attached(em, entity)) continue;
                candidates.Add(new Candidate { Entity = entity, Position = pose.ValueRO.Position, Radius = agent.ValueRO.Radius, Generation = life.ValueRO.Generation });
                maxRadius = math.max(maxRadius, agent.ValueRO.Radius);
            }
            using var cells = new NativeParallelMultiHashMap<int2, int>(math.max(1, candidates.Length), Allocator.Temp);
            const float cell = 4;
            for (int i = 0; i < candidates.Length; i++) cells.Add((int2)math.floor(candidates[i].Position.xz / cell), i);
            foreach (var (tuning, flame, launch, pose, life, clocks, source) in
                SystemAPI.Query<RefRO<DragonTuning>, RefRO<DragonFlameState>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<EnemyLifetime>,
                    DynamicBuffer<DragonFlameClock>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (!DragonFlameGeometry.TryCone(source, pose.ValueRO, tuning.ValueRO, flame.ValueRO, launch.ValueRO, life.ValueRO.Generation, out var cone) || cone.Active == 0) continue;
                float reach = cone.Profile.Length + maxRadius;
                int2 min = (int2)math.floor((cone.Mouth.xz - reach) / cell), max = (int2)math.floor((cone.Mouth.xz + reach) / cell);
                for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
                foreach (int index in cells.GetValuesForKey(new int2(x, z)))
                {
                    var c = candidates[index];
                    bool armor = armors.HasComponent(c.Entity) && (armors[c.Entity].Stages > 0 || now < armors[c.Entity].ProtectedUntil);
                    if (!DragonFlameGeometry.EnemyEligible(cone, c.Entity, dragons.HasComponent(c.Entity), armor) ||
                        !DragonFlameGeometry.Exposed(cone, c.Position, c.Radius, world, enemies, cone.Tuning.GroundY + .25f) ||
                        !Tick(clocks, c.Entity, c.Generation, now, tuning.ValueRO.HitInterval)) continue;
                    // Pending ordinary impacts retain their own attribution; flame never requests Exploder detonation or a launch.
                    EnemyDamageResolution.ApplyPending(em, c.Entity, now);
                    if (em.GetComponentData<Health>(c.Entity).Current <= 0) continue;
                    em.SetComponentData(c.Entity, new DamageRequest { Amount = cone.Profile.EnemyDamage });
                    em.SetComponentEnabled<DamageRequest>(c.Entity, true);
                    EnemyDamageResolution.ApplyPending(em, c.Entity, now, cone.Launch.Owner, source, cone.Launch.FeedbackChainDepth, cone.Generation);
                    if (em.GetComponentData<EnemyLaunchState>(c.Entity).Phase == EnemyLaunchPhase.Launched) continue;
                    var velocity = em.GetComponentData<PhysicsVelocity>(c.Entity);
                    velocity.Linear += Push(c.Position, cone.Body, cone.Profile.EnemyPush);
                    em.SetComponentData(c.Entity, velocity);
                }
                if (player.IsAvailable && DragonFlameGeometry.Exposed(cone, player.Position, player.Radius, world, enemies, cone.Tuning.GroundY + .25f) &&
                    Tick(clocks, Entity.Null, 0, now, tuning.ValueRO.HitInterval))
                    hits.Add(new DragonPlayerHit { Damage = cone.Profile.PlayerDamage, Push = Push(player.Position, cone.Body, cone.Profile.PlayerPush) });
                for (int i = clocks.Length - 1; i >= 0; i--)
                    if (clocks[i].Target != Entity.Null && (!lifetimes.HasComponent(clocks[i].Target) || lifetimes[clocks[i].Target].Generation != clocks[i].Generation)) clocks.RemoveAt(i);
            }
        }
        private struct Candidate { public Entity Entity; public float3 Position; public float Radius; public uint Generation; }
        private static float3 Push(float3 target, float3 source, float speed)
        { float2 direction = math.normalizesafe(target.xz - source.xz); return new float3(direction.x, 0, direction.y) * speed; }
        public static bool Tick(DynamicBuffer<DragonFlameClock> clocks, Entity target, uint generation, double now, float interval)
        {
            for (int i = 0; i < clocks.Length; i++)
            {
                var clock = clocks[i]; if (clock.Target != target || clock.Generation != generation) continue;
                if (now + 1e-6 < clock.NextHitAt) return false;
                clock.NextHitAt = now + math.max(.01f, interval); clocks[i] = clock; return true;
            }
            clocks.Add(new DragonFlameClock { Target = target, Generation = generation, NextHitAt = now + math.max(.01f, interval) }); return true;
        }
    }
}
