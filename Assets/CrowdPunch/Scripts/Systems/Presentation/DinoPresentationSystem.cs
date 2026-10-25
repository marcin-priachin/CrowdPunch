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
