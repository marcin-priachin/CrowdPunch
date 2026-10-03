using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    [UpdateAfter(typeof(Initialization.EnemyWaveSpawnSystem))]
    [UpdateBefore(typeof(EnemyChaseSystem)), UpdateBefore(typeof(WizardCastSystem))]
    public partial struct ProtectedPointPrioritySystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<ProtectedPoint>(); state.RequireForUpdate<PlayerSnapshot>(); }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var objective = SystemAPI.GetSingleton<ProtectedPoint>();
            var owner = SystemAPI.GetSingletonEntity<ProtectedPoint>();
            var sequence = SystemAPI.GetComponent<EnemyWaveSequence>(owner);
            var selected = SystemAPI.GetBuffer<ProtectedPointAttacker>(owner);
            selected.Clear();
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (objective.Failed || !player.IsAvailable || objective.MaximumPlayerAttackers <= 0) return;
            foreach (var (pose, launch, contact, ownership, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<EnemyContactDamageSettings>, RefRO<EnemyWaveOwnership>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || ownership.ValueRO.Sequence != owner
                    || ownership.ValueRO.RunGeneration != sequence.RunGeneration) continue;
                if (SystemAPI.HasComponent<EnemyArmor>(entity) && SystemAPI.Time.ElapsedTime < SystemAPI.GetComponent<EnemyArmor>(entity).StaggerUntil) continue;
                float distanceSq = math.distancesq(pose.ValueRO.Position.xz, player.Position.xz);
                float minimum = 0;
                float range = math.max(contact.ValueRO.AttemptDistance, player.Radius + contact.ValueRO.ContactRadius);
                if (SystemAPI.HasComponent<RangedEnemySettings>(entity)) range = SystemAPI.GetComponent<RangedEnemySettings>(entity).EngagementRange;
                if (SystemAPI.HasComponent<WizardSettings>(entity)) range = SystemAPI.GetComponent<WizardSettings>(entity).EngagementRange;
                if (SystemAPI.HasComponent<DasherSettings>(entity))
                {
                    var dash = SystemAPI.GetComponent<DasherSettings>(entity);
                    minimum = math.min(dash.PreparationMinimumDistance, dash.PreparationMaximumDistance);
                    range = math.max(dash.PreparationMinimumDistance, dash.PreparationMaximumDistance);
                }
                if (distanceSq < minimum * minimum || distanceSq > range * range) continue;
                int index = 0;
                while (index < selected.Length && (selected[index].DistanceSq < distanceSq
                    || selected[index].DistanceSq == distanceSq && selected[index].Enemy.Index < entity.Index)) index++;
                if (index >= objective.MaximumPlayerAttackers) continue;
                selected.Insert(index, new ProtectedPointAttacker { Enemy = entity, DistanceSq = distanceSq });
                if (selected.Length > objective.MaximumPlayerAttackers) selected.RemoveAt(selected.Length - 1);
            }
        }
    }
}
