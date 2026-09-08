using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateAfter(typeof(OutOfBoundsSystem))]
    [UpdateBefore(typeof(DefeatedEnemyLifecycleSystem))]
    public partial struct EnemyWaveDefeatCountSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            ComponentLookup<EnemyWaveSequence> sequences = SystemAPI.GetComponentLookup<EnemyWaveSequence>();
            foreach ((RefRW<EnemyWaveOwnership> ownership, RefRO<EnemyLaunchState> launchState,
                         EnabledRefRO<RespawnRequest> respawnRequest) in
                     SystemAPI.Query<RefRW<EnemyWaveOwnership>, RefRO<EnemyLaunchState>, EnabledRefRO<RespawnRequest>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                // Replenishing normals can pool directly on leaving defeat bounds, without
                // entering Defeated. Count that absence too; the existing return path restores
                // ownership counters if the elite is still alive when the normal returns.
                if (ownership.ValueRO.DefeatCounted != 0
                    || (launchState.ValueRO.Phase != EnemyLaunchPhase.Defeated && !respawnRequest.ValueRO))
                    continue;
                Entity owner = ownership.ValueRO.Sequence;
                if (!sequences.HasComponent(owner)) continue;
                EnemyWaveSequence sequence = sequences[owner];
                if (ownership.ValueRO.RunGeneration != sequence.RunGeneration)
                    continue;
                ownership.ValueRW.DefeatCounted = 1;
                if (sequence.UndefeatedCount > 0)
                    sequence.UndefeatedCount--;
                if (ownership.ValueRO.WaveIndex == sequence.CurrentWaveIndex)
                    sequence.DefeatedCount++;
                sequences[owner] = sequence;
            }
        }
    }
}
