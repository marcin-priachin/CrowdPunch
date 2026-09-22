using CrowdPunch.Components;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Lifetime
{
    internal static class BossCrowdPlacement
    {
        public static bool TryFind(EntityManager em, Entity body, PhysicsWorldSingleton world, PlayerSnapshot player,
            NavigationGrid grid, ref Random random, out float3 position)
        {
            position=default;
            var owner=em.GetComponentData<EnemyWaveOwnership>(body);
            if(!em.Exists(owner.Sequence)) return false;
            var sequence=em.GetComponentData<EnemyWaveSequence>(owner.Sequence);
            var waves=em.GetBuffer<EnemyWaveDefinition>(owner.Sequence);
            var ranges=em.GetBuffer<EnemyWaveSpawnRange>(owner.Sequence);
            var clearance=em.GetComponentData<EnemySpawnClearance>(body).Value;
            var radius=em.GetComponentData<NavigationAgent>(body).Radius;
            var occupied=new NativeList<float4>(Allocator.Temp);
            var accepted=new NativeList<float4>(Allocator.Temp);
            using(var query=em.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawnClearance>(),ComponentType.ReadOnly<LocalTransform>()))
            using(var entities=query.ToEntityArray(Allocator.Temp))
                foreach(var e in entities) if(e!=body)
                    occupied.Add(new float4(em.GetComponentData<LocalTransform>(e).Position,em.GetComponentData<EnemySpawnClearance>(e).Value));
            bool found=false;
            for(int i=0;i<sequence.PlacementAttemptsPerEnemy;i++)
            {
                position=EnemyWaveSpawnSystem.SelectPosition(ref random,waves[owner.WaveIndex],ranges);
                if(NavigationGeometry.SpawnAllowed(grid,position.xz,radius)
                    && EnemyWaveSpawnSystem.IsSafe(world,player,position,clearance,sequence.MinimumPlayerDistance,occupied,accepted))
                { found=true; break; }
            }
            occupied.Dispose(); accepted.Dispose(); return found;
        }
    }
}
