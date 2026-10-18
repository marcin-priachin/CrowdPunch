using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Bakers
{
    public sealed class VillageExitCueBaker : Baker<VillageExitCueAuthoring>
    {
        public override void Bake(VillageExitCueAuthoring a)
        {
            if(a.gate==null) return;
            var entity=GetEntity(TransformUsageFlags.Dynamic);
            float4 locked=new float4(a.lockedColor.r,a.lockedColor.g,a.lockedColor.b,1);
            AddComponent(entity,new VillageExitCue {Gate=GetEntity(a.gate,TransformUsageFlags.Dynamic),
                LockedColor=locked,OpenColor=new float4(a.openColor.r,a.openColor.g,a.openColor.b,1),
                Phase=a.phase,Arrow=a.arrow?(byte)1:(byte)0});
            AddComponent(entity,new URPMaterialPropertyBaseColor {Value=locked});
        }
    }
}
