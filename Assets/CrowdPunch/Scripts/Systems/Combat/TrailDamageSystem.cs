using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Profiling;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(TrailEmissionSystem))]
    [UpdateAfter(typeof(WizardZoneSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct TrailDamageSystem : ISystem
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.TrailDamage");
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddBuffer<TrailPlayerHit>(state.EntityManager.CreateEntity());
            state.RequireForUpdate<TrailSection>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            state.Dependency.Complete();
            var em = state.EntityManager;
            var hits = SystemAPI.GetSingletonBuffer<TrailPlayerHit>(); hits.Clear();
            double now = SystemAPI.Time.ElapsedTime;
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            var armorLookup = SystemAPI.GetComponentLookup<EnemyArmor>(true);
            var lifetimeLookup = SystemAPI.GetComponentLookup<EnemyLifetime>(true);
            var records = SystemAPI.GetComponentLookup<TrailSource>(true);
            var targetTimers = SystemAPI.GetBufferLookup<TrailDamageTarget>();
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            float maximumRadius = 0;
            foreach (var (pose, agent, life, launch, health, archetype, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLifetime>,
                    RefRO<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyArchetype>>()
                    .WithAll<Enemy>().WithNone<BossPart, RespawnRequest>().WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0 || launch.ValueRO.Phase == EnemyLaunchPhase.Defeated) continue;
                bool armored = armorLookup.HasComponent(entity) &&
                    (armorLookup[entity].Stages > 0 || now < armorLookup[entity].ProtectedUntil);
                candidates.Add(new Candidate { Entity = entity, Position = pose.ValueRO.Position, Radius = agent.ValueRO.Radius,
                    Lifetime = life.ValueRO.Generation, Trail = archetype.ValueRO.Value == EnemyArchetypeKind.Trail, Armored = armored });
                maximumRadius = math.max(maximumRadius, agent.ValueRO.Radius);
            }
            // Index current post-physics positions. Height-independent membership needs no stale solver broadphase padding.
            using var cells = new NativeParallelMultiHashMap<int2, int>(math.max(1, candidates.Length), Allocator.Temp);
            const float cellSize = 4;
            for (int i = 0; i < candidates.Length; i++) cells.Add((int2)math.floor(candidates[i].Position.xz / cellSize), i);
            foreach (var sectionRef in SystemAPI.Query<RefRO<TrailSection>>())
            {
                var section = sectionRef.ValueRO;
                if (now >= section.ExpiresAt || !records.HasComponent(section.Record) || section.Damage <= 0) continue;
                var timers = targetTimers[section.Record];
                if (section.DamagesEnemies != 0)
                {
                    float reach = section.Width * .5f + maximumRadius;
                    int2 min = (int2)math.floor((math.min(section.Start.xz, section.End.xz) - reach) / cellSize);
                    int2 max = (int2)math.floor((math.max(section.Start.xz, section.End.xz) + reach) / cellSize);
                    for (int x = min.x; x <= max.x; x++)
                    for (int z = min.y; z <= max.y; z++)
                    foreach (int index in cells.GetValuesForKey(new int2(x, z)))
                    {
                        var candidate = candidates[index]; Entity target = candidate.Entity;
                        if (!TrailGeometry.CanDamage(section, target, candidate.Lifetime, candidate.Trail, candidate.Armored) ||
                            !TrailGeometry.Overlaps(section, candidate.Position, candidate.Radius) ||
                            !Tick(timers, target, candidate.Lifetime, now, section.TickInterval)) continue;
                        // Resolve any earlier request separately so its lethal damage cannot acquire trail credit.
                        EnemyDamageResolution.ApplyPending(em, target, now);
                        if (em.GetComponentData<Health>(target).Current <= 0) continue;
                        var request = new DamageRequest { Amount = section.Damage };
                        em.SetComponentData(target, request); em.SetComponentEnabled<DamageRequest>(target, true);
                        // Damage credit is detached from current source state and never alters target launch ownership.
                        EnemyDamageResolution.ApplyPending(em, target, now, section.Owner, section.Source, section.ChainDepth, section.SourceLifetime);
                    }
                }
                if (player.IsAvailable && TrailGeometry.Overlaps(section, player.Position, player.Radius) &&
                    Tick(timers, Entity.Null, 0, now, math.max(section.TickInterval, section.PlayerProtection)))
                    hits.Add(new TrailPlayerHit { Damage = section.Damage });
            }
            foreach (var timers in SystemAPI.Query<DynamicBuffer<TrailDamageTarget>>())
                for (int i = timers.Length - 1; i >= 0; i--)
                {
                    var timer = timers[i];
                    if (now >= timer.NextHitAt && (timer.Target == Entity.Null ||
                        !lifetimeLookup.HasComponent(timer.Target) || lifetimeLookup[timer.Target].Generation != timer.Lifetime))
                        timers.RemoveAt(i);
                }
        }

        private struct Candidate
        {
            public Entity Entity;
            public float3 Position;
            public float Radius;
            public uint Lifetime;
            public bool Trail, Armored;
        }

        public static bool Tick(DynamicBuffer<TrailDamageTarget> timers, Entity target, uint lifetime, double now, float interval)
        {
            for (int i = 0; i < timers.Length; i++)
            {
                var timer = timers[i];
                if (timer.Target != target || timer.Lifetime != lifetime) continue;
                if (now + 1e-6 < timer.NextHitAt) return false;
                timer.NextHitAt = now + math.max(.01f, interval); timers[i] = timer; return true;
            }
            timers.Add(new TrailDamageTarget { Target = target, Lifetime = lifetime, NextHitAt = now + math.max(.01f, interval) });
            return true;
        }
    }
}
