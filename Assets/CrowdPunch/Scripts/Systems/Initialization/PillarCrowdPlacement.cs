using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Systems.Initialization
{
    internal static class PillarCrowdPlacement
    {
        internal static bool Needed(in FallingPillar pillar,in DinoBoss boss,in DinoTuning tuning)
            => tuning.EnemiesPerPillar>0 && boss.Phase!=DinoPhase.Defeated && pillar.HitBoss==0 && pillar.Phase!=PillarPhase.Consumed;

        internal static float3 Anchor(in PillarTuning pillar,in DinoTuning boss,int slot,float y)
        {
            float angle=(slot+.5f)*2*math.PI/math.max(1,boss.EnemiesPerPillar);
            float radius=math.max(boss.PillarCrowdRadius,pillar.Width*.71f+1.5f);
            return pillar.InitialPosition+new float3(math.cos(angle)*radius,y-pillar.InitialPosition.y,math.sin(angle)*radius);
        }

        internal static bool TryFind(in PillarTuning pillar,in DinoTuning boss,int slot,float clearance,float navigationRadius,
            PhysicsWorldSingleton physics,PlayerSnapshot player,NavigationGrid grid,NativeList<float4> occupied,
            ref Random random,out float3 position)
        {
            using var accepted=new NativeList<float4>(Allocator.Temp);
            float3 anchor=Anchor(pillar,boss,slot,pillar.InitialPosition.y+3);
            float baseAngle=math.atan2(anchor.z-pillar.InitialPosition.z,anchor.x-pillar.InitialPosition.x);
            float radius=math.distance(anchor.xz,pillar.InitialPosition.xz);
            position=default;
            for(int attempt=0;attempt<32;attempt++)
            {
                float angle=baseAngle+(attempt==0?0:random.NextFloat(-math.PI,math.PI)/math.max(1,boss.EnemiesPerPillar));
                float r=radius+(attempt==0?0:random.NextFloat(-.4f,.4f));
                position=pillar.InitialPosition+new float3(math.cos(angle)*r,3,math.sin(angle)*r);
                if(NavigationGeometry.SpawnAllowed(grid,position.xz,navigationRadius)
                    && EnemyWaveSpawnSystem.IsSafe(physics,player,position,clearance,0,occupied,accepted)) return true;
            }
            return false; // Retry later instead of overlapping the player, boss, crowd or solid geometry.
        }
    }
}
