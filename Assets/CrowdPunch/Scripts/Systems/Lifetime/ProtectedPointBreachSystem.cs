using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(Physics.EnemyRecoverySystem)), UpdateAfter(typeof(OutOfBoundsSystem))]
    [UpdateAfter(typeof(EnemyRespawnSystem))]
    public partial struct ProtectedPointBreachSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<ProtectedPoint>();
        public void OnUpdate(ref SystemState state)
        {
            var owner = SystemAPI.GetSingletonEntity<ProtectedPoint>();
            var objective = SystemAPI.GetSingleton<ProtectedPoint>();
            if (objective.Failed) return;
            var sequence = SystemAPI.GetComponent<EnemyWaveSequence>(owner);
            int previousBreaches = objective.Breaches;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pose, launch, ownership, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<EnemyWaveOwnership>>()
                .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !objective.Contains(pose.ValueRO.Position)
                    || ownership.ValueRO.Sequence != owner || ownership.ValueRO.RunGeneration != sequence.RunGeneration
                    || ownership.ValueRO.DefeatCounted != 0) continue;
                objective.Breaches++;
                sequence.UndefeatedCount = Unity.Mathematics.math.max(0, sequence.UndefeatedCount - 1);
                if (ownership.ValueRO.WaveIndex == sequence.CurrentWaveIndex) sequence.DefeatedCount++;
                // Destroy the root with its LinkedEntityGroup; no death effects or replenishment.
                commands.DestroyEntity(entity);
                if (SystemAPI.HasComponent<WizardCastState>(entity))
                {
                    var zone = SystemAPI.GetComponent<WizardCastState>(entity).MovingZone;
                    if (state.EntityManager.Exists(zone)) commands.DestroyEntity(zone);
                }
            }
            SystemAPI.SetComponent(owner, sequence);
            SystemAPI.SetSingleton(objective);
            commands.Playback(state.EntityManager);
            // Unique Dasher colliders can be freed on destruction. The previous physics world
            // must be rebuilt before next step's gameplay casts, just as during a level restart.
            if (objective.Breaches != previousBreaches)
                state.World.GetExistingSystemManaged<GamePrePhysicsGroup>()?.RequestPhysicsRebuild();
        }
    }
}
