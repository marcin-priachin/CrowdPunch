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
            foreach(var (owner,color) in SystemAPI.Query<RefRO<ChickenVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<ChickenBoss>(owner.ValueRO.Value)) continue;
                color.ValueRW.Value=new float4(1);
            }
            foreach(var (shot,color) in SystemAPI.Query<RefRO<ChickenProjectile>,RefRW<URPMaterialPropertyBaseColor>>())
                // Both are danger colors. Returned shots never use a safe/green cue.
                color.ValueRW.Value=shot.ValueRO.Redirected!=0?new float4(1f,.42f,.08f,1):new float4(.7f,.22f,1f,1);
        }
    }
}
