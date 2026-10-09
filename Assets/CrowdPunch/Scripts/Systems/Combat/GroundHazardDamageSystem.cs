using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Profiling;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(TrailDamageSystem)), UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct GroundHazardDamageSystem : ISystem
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.GroundHazardDamage");
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity(typeof(GroundHazardPlayerClock));
            state.EntityManager.AddBuffer<GroundHazardPlayerHit>(entity);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            state.Dependency.Complete();
            double now = SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var hits = SystemAPI.GetSingletonBuffer<GroundHazardPlayerHit>(); hits.Clear();
            using var active = new NativeList<GroundHazard>(Allocator.Temp);
            Entity sequence = Entity.Null; uint generation = 0;
            foreach (var (patch, status) in SystemAPI.Query<RefRO<GroundHazard>, RefRO<GroundHazardState>>())
            {
                sequence = patch.ValueRO.Sequence; generation = status.ValueRO.RunGeneration;
                if (status.ValueRO.Introduced != 0 && status.ValueRO.Phase == GroundHazardPhase.Active && patch.ValueRO.Damage > 0) active.Add(patch.ValueRO);
            }
            var playerClock = SystemAPI.GetSingleton<GroundHazardPlayerClock>();
            if (playerClock.Sequence != sequence || playerClock.RunGeneration != generation)
                playerClock = new GroundHazardPlayerClock { Sequence = sequence, RunGeneration = generation };
            if (active.Length == 0) { SystemAPI.SetSingleton(playerClock); return; }
            foreach (var (pose, agent, life, clock, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLifetime>, RefRW<GroundHazardDamageClock>>()
                    .WithAll<Enemy, Health, EnemyLaunchState>().WithNone<RespawnRequest, BossPart>().WithEntityAccess())
            {
                ref var timer = ref clock.ValueRW;
                if (timer.Lifetime != life.ValueRO.Generation) timer = new GroundHazardDamageClock { Lifetime = life.ValueRO.Generation };
                if (now + 1e-6 < timer.NextHitAt || !SelectHit(active.AsArray(), pose.ValueRO.Position.xz, agent.ValueRO.Radius, out var h)) continue;
                EnemyDamageResolution.ApplyPending(em, entity, now);
                var launch = em.GetComponentData<EnemyLaunchState>(entity);
                if (em.GetComponentData<Health>(entity).Current <= 0 || launch.Phase == EnemyLaunchPhase.Defeated) continue;
                if (em.HasComponent<EnemyArmor>(entity))
                {
                    var armor = em.GetComponentData<EnemyArmor>(entity);
                    if (armor.Stages > 0 || now < armor.ProtectedUntil) continue;
                }
                timer.NextHitAt = now + h.DamageInterval;
                bool playerOwned = launch.Phase == EnemyLaunchPhase.Launched && launch.Owner == EnemyLaunchOwner.Player;
                em.SetComponentData(entity, new DamageRequest { Amount = h.Damage });
                em.SetComponentEnabled<DamageRequest>(entity, true);
                EnemyDamageResolution.ApplyPending(em, entity, now, playerOwned ? EnemyLaunchOwner.Player : EnemyLaunchOwner.Environment,
                    entity, playerOwned ? launch.FeedbackChainDepth : 0, life.ValueRO.Generation);
            }
            if (SystemAPI.HasSingleton<PlayerSnapshot>())
            {
                var player = SystemAPI.GetSingleton<PlayerSnapshot>();
                if (player.IsAvailable && now + 1e-6 >= playerClock.NextHitAt && SelectHit(active.AsArray(), player.Position.xz, player.Radius, out var h))
                { hits.Add(new GroundHazardPlayerHit { Damage = h.Damage }); playerClock.NextHitAt = now + h.DamageInterval; }
            }
            SystemAPI.SetSingleton(playerClock);
        }
        // Overlaps choose the strongest patch, with the longer interval breaking damage ties.
        // This is deterministic and never multiplies damage by patch count.
        public static bool SelectHit(NativeArray<GroundHazard> hazards, Unity.Mathematics.float2 point, float radius, out GroundHazard selected)
        {
            selected = default;
            foreach (var h in hazards)
                if (GroundHazardGeometry.Overlaps(h, point, radius) &&
                    (h.Damage > selected.Damage || h.Damage == selected.Damage && h.DamageInterval > selected.DamageInterval)) selected = h;
            return selected.Damage > 0;
        }
    }
}
