using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct ChickenPresentationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            double now=SystemAPI.Time.ElapsedTime;
            foreach(var (owner,color) in SystemAPI.Query<RefRO<ChickenVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<ChickenBoss>(owner.ValueRO.Value)) continue;
                var b=SystemAPI.GetComponent<ChickenBoss>(owner.ValueRO.Value);
                float3 tint=new float3(1,.83f,.45f);
                if(b.Phase==ChickenPhase.WindUp) tint=new float3(1.8f,.65f,.15f);
                else if(b.Phase==ChickenPhase.Rush) tint=new float3(1.4f,.4f,.12f);
                else if(b.Phase==ChickenPhase.Stagger) tint=new float3(2.5f);
                else if(b.Phase==ChickenPhase.Defeated) tint=new float3(.25f);
                else if(now<b.InvulnerableUntil) tint=new float3(.5f,1.5f,1.8f)*(1+.25f*math.sin((float)now*20));
                color.ValueRW.Value=new float4(tint,1);
            }
            foreach(var (shot,color) in SystemAPI.Query<RefRO<ChickenProjectile>,RefRW<URPMaterialPropertyBaseColor>>())
                // Both are danger colors. Returned shots never use a safe/green cue.
                color.ValueRW.Value=shot.ValueRO.Redirected!=0?new float4(1.4f,.05f,.7f,1):new float4(1.4f,.3f,.015f,1);
        }
    }
}
