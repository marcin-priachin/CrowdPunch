using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(WizardPositioningSystem))]
    [UpdateAfter(typeof(RangedEnemyPositioningSystem))]
    [UpdateAfter(typeof(DasherDecisionSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct WizardHazardAvoidanceSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool defending = SystemAPI.HasSingleton<ProtectedPoint>();
            var attackers = defending ? SystemAPI.GetSingletonBuffer<ProtectedPointAttacker>(true).AsNativeArray() : default;
            using var hazards = new NativeList<WizardZone>(Allocator.Temp);
            foreach (var zone in SystemAPI.Query<RefRO<WizardZone>>())
                if (zone.ValueRO.Follow == 0 && SystemAPI.Time.ElapsedTime < zone.ValueRO.ExpiresAt) hazards.Add(zone.ValueRO);
            foreach (var (settings, cast, pose, launch, entity) in SystemAPI.Query<RefRO<WizardSettings>, RefRO<WizardCastState>,
                RefRO<LocalTransform>, RefRO<EnemyLaunchState>>().WithNone<RespawnRequest>().WithEntityAccess())
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active && (settings.ValueRO.AlwaysReserveRadius || cast.ValueRO.IsCasting))
                    hazards.Add(new WizardZone { Source = entity, Position = pose.ValueRO.Position, Settings = settings.ValueRO });
            if (hazards.Length == 0) return;
            foreach (var (pose, launch, agent, navigation, movement, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>,
                RefRO<NavigationAgent>, RefRW<NavigationIntent>, RefRW<DesiredMovement>>().WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active) continue;
                if (defending && (SystemAPI.HasComponent<RangedEnemySettings>(entity) || SystemAPI.HasComponent<WizardSettings>(entity))
                    && (ProtectedPointAttacker.Contains(attackers, entity)
                        || SystemAPI.HasComponent<WizardCastState>(entity) && SystemAPI.GetComponent<WizardCastState>(entity).IsCasting
                        || SystemAPI.HasComponent<RangedAttackState>(entity) && SystemAPI.GetComponent<RangedAttackState>(entity).Phase == RangedAttackPhase.WindUp)) continue;
                if (SystemAPI.HasComponent<DasherState>(entity) && SystemAPI.GetComponent<DasherState>(entity).Phase != DasherPhase.Positioning) continue;
                if (SystemAPI.HasComponent<WizardCastState>(entity) && SystemAPI.GetComponent<WizardCastState>(entity).StopsMovement(SystemAPI.GetComponent<WizardSettings>(entity))) continue;
                float3 away = 0;
                foreach (var hazard in hazards)
                {
                    if (hazard.Source == entity) continue;
                    float3 delta = pose.ValueRO.Position - hazard.Position; delta.y = 0;
                    float radius = math.max(0, hazard.Radius) + agent.ValueRO.Radius + 1;
                    float distance = math.length(delta);
                    if (distance < radius && distance > .0001f) away += delta / distance * (1 - distance / radius) * 5;
                }
                if (math.lengthsq(away) <= 0) continue;
                navigation.ValueRW.Separation += away;
                movement.ValueRW.Direction = math.normalizesafe(movement.ValueRO.Direction + away);
                movement.ValueRW.Speed = math.max(movement.ValueRO.Speed, 1.5f);
                navigation.ValueRW.Speed = math.max(navigation.ValueRO.Speed, 1.5f);
            }
        }
    }
}
