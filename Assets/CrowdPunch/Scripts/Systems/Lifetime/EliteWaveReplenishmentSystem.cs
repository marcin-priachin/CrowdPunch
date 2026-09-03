using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    /// <summary>Allows elite-wave normals to return only while an elite from their wave remains alive.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(OutOfBoundsSystem))]
    public partial struct EliteWaveReplenishmentSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NativeList<EnemyWaveOwnership> livingEliteWaves = new NativeList<EnemyWaveOwnership>(Allocator.Temp);
            foreach ((RefRO<EnemyWaveOwnership> ownership, RefRO<EnemyTier> tier,
                         RefRO<EnemyLaunchState> launchState) in
                     SystemAPI.Query<RefRO<EnemyWaveOwnership>, RefRO<EnemyTier>, RefRO<EnemyLaunchState>>())
            {
                if (tier.ValueRO.Value == EnemyCombatTier.Elite
                    && launchState.ValueRO.Phase != EnemyLaunchPhase.Defeated)
                    livingEliteWaves.Add(ownership.ValueRO);
            }

            foreach ((RefRO<EnemyWaveOwnership> ownership, RefRW<EnemyRespawnSettings> respawnSettings) in
                     SystemAPI.Query<RefRO<EnemyWaveOwnership>, RefRW<EnemyRespawnSettings>>()
                         .WithAll<EliteWaveReplenishment>())
            {
                respawnSettings.ValueRW.Enabled = Contains(livingEliteWaves, ownership.ValueRO) ? (byte)1 : (byte)0;
            }

            livingEliteWaves.Dispose();
        }

        private static bool Contains(NativeList<EnemyWaveOwnership> livingEliteWaves, EnemyWaveOwnership normal)
        {
            for (int index = 0; index < livingEliteWaves.Length; index++)
            {
                EnemyWaveOwnership elite = livingEliteWaves[index];
                if (elite.Sequence == normal.Sequence
                    && elite.RunGeneration == normal.RunGeneration
                    && elite.WaveIndex == normal.WaveIndex)
                    return true;
            }

            return false;
        }
    }
}
