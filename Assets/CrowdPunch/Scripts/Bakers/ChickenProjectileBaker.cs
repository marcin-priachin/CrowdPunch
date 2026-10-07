using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;
namespace CrowdPunch.Bakers
{
    public sealed class ChickenProjectileBaker : Baker<ChickenProjectileAuthoring>
    {
        public override void Bake(ChickenProjectileAuthoring a)
        {
            var e=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChickenProjectile>(e); AddComponent<PunchAimAssistTarget>(e);
            AddBuffer<ChickenProjectileHit>(e);
            AddComponent(e,new URPMaterialPropertyBaseColor { Value=new float4(1,.2f,.03f,1) });
        }
    }
}
