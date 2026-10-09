using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateBefore(typeof(EnemyWaveSpawnSystem))]
    public partial struct GroundHazardCycleSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (patch, status) in SystemAPI.Query<RefRO<GroundHazard>, RefRW<GroundHazardState>>())
            {
                var h = patch.ValueRO; ref var s = ref status.ValueRW;
                bool available = h.Sequence == Entity.Null || SystemAPI.HasComponent<EnemyWaveSequence>(h.Sequence);
                var wave = h.Sequence == Entity.Null || !available ? default : SystemAPI.GetComponent<EnemyWaveSequence>(h.Sequence);
                if (s.Initialized == 0 || s.RunGeneration != wave.RunGeneration || s.WaveIndex != wave.CurrentWaveIndex)
                {
                    s.Initialized = 1; s.RunGeneration = wave.RunGeneration; s.WaveIndex = wave.CurrentWaveIndex; s.WaveStartedAt = now;
                }
                s.Introduced = available && wave.CurrentWaveIndex >= h.FirstWave ? (byte)1 : (byte)0;
                s.Phase = s.Introduced == 0 ? GroundHazardPhase.Inactive : GroundHazardGeometry.Phase(h, now - s.WaveStartedAt);
            }
        }
    }
}
