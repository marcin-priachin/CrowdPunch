using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyWaveDefeatCountSystem))]
    public partial struct TrailWaveCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<TrailSource>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var cleared = new NativeHashSet<Entity>(16, Allocator.Temp);
            foreach (var (source, entity) in SystemAPI.Query<RefRO<TrailSource>>().WithEntityAccess())
            {
                var owner = source.ValueRO.Sequence;
                if (!SystemAPI.HasComponent<EnemyWaveSequence>(owner) ||
                    SystemAPI.HasComponent<BossCrowdSequence>(owner) || SystemAPI.HasComponent<BarricadeCrowdSequence>(owner)) continue;
                var sequence = SystemAPI.GetComponent<EnemyWaveSequence>(owner);
                // An empty gap between spawn batches is not a cleared wave. Count deferred launched deaths normally.
                if (sequence.RunGeneration == source.ValueRO.RunGeneration && sequence.UndefeatedCount == 0 &&
                    (sequence.Phase == EnemyWaveRuntimePhase.AwaitingActivation || sequence.Phase == EnemyWaveRuntimePhase.Complete))
                    cleared.Add(entity);
            }
            if (cleared.Count == 0) return;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (section, entity) in SystemAPI.Query<RefRO<TrailSection>>().WithEntityAccess())
                if (cleared.Contains(section.ValueRO.Record)) commands.DestroyEntity(entity);
            foreach (var entity in cleared) commands.DestroyEntity(entity);
            commands.Playback(state.EntityManager);
        }
    }
}
