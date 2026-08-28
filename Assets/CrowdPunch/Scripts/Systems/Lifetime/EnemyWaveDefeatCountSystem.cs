using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(DefeatedEnemyLifecycleSystem))]
    public partial struct EnemyWaveDefeatCountSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            ComponentLookup<EnemyWaveSequence> sequences = SystemAPI.GetComponentLookup<EnemyWaveSequence>();
            foreach ((RefRW<EnemyWaveOwnership> ownership, RefRO<EnemyLaunchState> launchState) in
                     SystemAPI.Query<RefRW<EnemyWaveOwnership>, RefRO<EnemyLaunchState>>())
            {
                if (ownership.ValueRO.DefeatCounted != 0 || launchState.ValueRO.Phase != EnemyLaunchPhase.Defeated)
                    continue;
                Entity owner = ownership.ValueRO.Sequence;
                if (!sequences.HasComponent(owner)) continue;
                EnemyWaveSequence sequence = sequences[owner];
                if (ownership.ValueRO.RunGeneration != sequence.RunGeneration
                    || ownership.ValueRO.WaveIndex != sequence.CurrentWaveIndex)
                    continue;
                ownership.ValueRW.DefeatCounted = 1;
                sequence.DefeatedCount++;
                sequences[owner] = sequence;
            }
        }
    }
}
