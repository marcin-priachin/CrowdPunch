using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    public static class GroundHazardSpawnClearance
    {
        public static NativeList<GroundHazard> Capture(EntityManager em)
        {
            var hazards = new NativeList<GroundHazard>(Allocator.Temp);
            using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<GroundHazard, GroundHazardState>();
            using var query = builder.Build(em);
            using var patches = query.ToComponentDataArray<GroundHazard>(Allocator.Temp);
            using var states = query.ToComponentDataArray<GroundHazardState>(Allocator.Temp);
            for (int i = 0; i < patches.Length; i++)
            {
                var h = patches[i]; var s = states[i];
                var phase = s.Phase; int waveIndex = s.WaveIndex;
                if (h.Sequence != Entity.Null)
                {
                    if (!em.HasComponent<EnemyWaveSequence>(h.Sequence)) continue;
                    var wave = em.GetComponentData<EnemyWaveSequence>(h.Sequence); waveIndex = wave.CurrentWaveIndex;
                    if (s.Initialized == 0 || wave.RunGeneration != s.RunGeneration || waveIndex != s.WaveIndex) phase = GroundHazardGeometry.Phase(h, 0);
                }
                else if (s.Initialized == 0) phase = GroundHazardGeometry.Phase(h, 0);
                if (waveIndex >= h.FirstWave && phase != GroundHazardPhase.Inactive) hazards.Add(h);
            }
            return hazards;
        }
        public static bool Allowed(EntityManager em, float2 position, float radius)
        {
            using var hazards = Capture(em);
            return GroundHazardGeometry.Clear(hazards.AsArray(), position, position, radius);
        }
    }
}
