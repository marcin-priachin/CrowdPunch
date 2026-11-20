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
        public const float ProjectileEmission = 1.3f;

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (owner,color) in SystemAPI.Query<RefRO<ChickenVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<ChickenBoss>(owner.ValueRO.Value)) continue;
                color.ValueRW.Value=new float4(1);
            }
            foreach (var (shot, color, emission) in SystemAPI.Query<RefRO<ChickenProjectile>,
                         RefRW<URPMaterialPropertyBaseColor>, RefRW<URPMaterialPropertyEmissionColor>>())
            {
                // Both are danger colors. Returned shots never use a safe/green cue.
                float3 tint = shot.ValueRO.Redirected != 0 ? new float3(1f,.42f,.08f) : new float3(.7f,.22f,1f);
                color.ValueRW.Value = new float4(tint, 1);
                emission.ValueRW.Value = new float4(tint * ProjectileEmission, 1);
            }
        }
    }
}
