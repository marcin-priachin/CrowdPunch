using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(WizardHazardAvoidanceSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct TrailAvoidanceSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hazards = new NativeList<TrailSection>(Allocator.TempJob);
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var section in SystemAPI.Query<RefRO<TrailSection>>())
                if (now < section.ValueRO.ExpiresAt && section.ValueRO.Avoidance == TrailAvoidanceMode.DamagingTrails &&
                    section.ValueRO.DamagesEnemies != 0 && section.ValueRO.Damage > 0) hazards.Add(section.ValueRO);
            if (hazards.Length == 0) { hazards.Dispose(); return; }
            var job = new AvoidJob
            {
                Hazards = hazards.AsDeferredJobArray(), Now = now,
                Trails = SystemAPI.GetComponentLookup<TrailEmitter>(true),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                Wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                WizardSettings = SystemAPI.GetComponentLookup<WizardSettings>(true),
                Reservations = SystemAPI.GetComponentLookup<ElitePunchReservation>(true)
            }.ScheduleParallel(state.Dependency);
            state.Dependency = hazards.Dispose(job);
        }

        [BurstCompile, WithAll(typeof(Enemy)), WithNone(typeof(RespawnRequest))]
        private partial struct AvoidJob : IJobEntity
        {
            [ReadOnly] public NativeArray<TrailSection> Hazards;
            [ReadOnly] public ComponentLookup<TrailEmitter> Trails;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<WizardCastState> Wizards;
            [ReadOnly] public ComponentLookup<WizardSettings> WizardSettings;
            [ReadOnly] public ComponentLookup<ElitePunchReservation> Reservations;
            public double Now;
            private void Execute(Entity entity, in LocalTransform pose, in EnemyLaunchState launch,
                in EnemyLifetime life, in NavigationAgent agent, ref NavigationIntent navigation, ref DesiredMovement movement)
            {
                if (launch.Phase != EnemyLaunchPhase.Active ||
                    Dashers.HasComponent(entity) && Dashers[entity].Phase != DasherPhase.Positioning ||
                    Wizards.HasComponent(entity) && Wizards[entity].StopsMovement(WizardSettings[entity]) ||
                    Reservations.HasComponent(entity) && Reservations[entity].Owner != Entity.Null) return;
                bool armor = Armors.HasComponent(entity) && (Armors[entity].Stages > 0 || Now < Armors[entity].ProtectedUntil);
                float3 away = 0;
                foreach (var hazard in Hazards)
                {
                    if (!TrailGeometry.CanDamage(hazard, entity, life.Generation, Trails.HasComponent(entity), armor)) continue;
                    float radius = hazard.Width * .5f + agent.Radius + 1;
                    float2 point = pose.Position.xz;
                    // Cheap planar bounds rejection before capsule distance; no height gate.
                    if (math.any(point < math.min(hazard.Start.xz, hazard.End.xz) - radius) ||
                        math.any(point > math.max(hazard.Start.xz, hazard.End.xz) + radius)) continue;
                    float2 delta = point - TrailGeometry.Closest(hazard.Start.xz, hazard.End.xz, point);
                    float distance = math.length(delta);
                    if (distance >= radius) continue;
                    float2 fallback = math.normalizesafe(new float2(hazard.Start.z - hazard.End.z, hazard.End.x - hazard.Start.x), new float2(1, 0));
                    float2 push = math.normalizesafe(delta, fallback) * (1 - distance / radius) * 3;
                    // Adjacent sections describe one path: use the strongest local repulsion, not a section-count multiplier.
                    if (math.lengthsq(push) > math.lengthsq(away.xz)) away = new float3(push.x, 0, push.y);
                }
                if (math.lengthsq(away) == 0) return;
                navigation.Separation += away;
                movement.Direction = math.normalizesafe(movement.Direction + away);
                movement.Speed = math.max(movement.Speed, 1.5f);
                navigation.Speed = math.max(navigation.Speed, 1.5f);
            }
        }
    }
}
