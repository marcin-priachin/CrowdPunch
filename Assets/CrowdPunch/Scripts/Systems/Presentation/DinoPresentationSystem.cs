using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile,UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct DinoPresentationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            float now=(float)SystemAPI.Time.ElapsedTime;
            foreach(var (owner,color) in SystemAPI.Query<RefRO<DinoVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<DinoBoss>(owner.ValueRO.Value)) continue;
                var b=SystemAPI.GetComponent<DinoBoss>(owner.ValueRO.Value); var t=SystemAPI.GetComponent<DinoTuning>(owner.ValueRO.Value);
                float3 tint=b.Phase==DinoPhase.Warning?t.WarningColor*(1+.2f*math.sin(now*14)):
                    b.Phase==DinoPhase.Burst?t.BurstColor:b.Phase==DinoPhase.Stagger?t.StaggerColor:
                    b.Phase==DinoPhase.Defeated?new float3(.2f):t.ChaseColor;
                color.ValueRW.Value=new float4(tint,1);
            }
            foreach(var (owner,color) in SystemAPI.Query<RefRO<PillarVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<FallingPillar>(owner.ValueRO.Value)) continue;
                var p=SystemAPI.GetComponent<FallingPillar>(owner.ValueRO.Value);
                float flash=math.saturate(1-(now-(float)p.LastEvent)*3);
                float3 tint=p.Phase==PillarPhase.Falling?math.lerp(new float3(1,.65f,.25f),new float3(1.8f,1.6f,1),flash):
                    math.lerp(new float3(.7f,.8f,.85f),new float3(.4f,1.5f,1.3f),flash);
                color.ValueRW.Value=new float4(tint,1);
            }
        }
    }
}
