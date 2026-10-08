using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Physics
{
    [BurstCompile,UpdateInGroup(typeof(GamePrePhysicsGroup),OrderLast=true),UpdateAfter(typeof(PillarToppleSystem))]
    public partial struct PillarFallMotionSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach(var (pillar,tuning,pose) in SystemAPI.Query<RefRW<FallingPillar>,RefRO<PillarTuning>,RefRW<LocalTransform>>())
            {
                ref var p=ref pillar.ValueRW; var t=tuning.ValueRO;
                if(p.Phase!=PillarPhase.Falling) continue;
                p.PreviousAngle=p.Angle; p.Elapsed+=SystemAPI.Time.DeltaTime;
                float f=math.saturate(p.Elapsed/t.FallDuration);
                p.Angle=(math.PI*.5f)*f*f; // Acceleration reads as weight; the initial lean gives reaction time.
                pose.ValueRW.Rotation=PillarFallGeometry.Rotation(p.Direction,p.Angle);
            }
        }
    }
}
