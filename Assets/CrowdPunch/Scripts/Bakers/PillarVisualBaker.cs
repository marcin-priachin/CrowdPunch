using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Bakers
{
    public sealed class PillarVisualBaker : Baker<UnityEngine.MeshRenderer>
    {
        public override void Bake(UnityEngine.MeshRenderer a)
        {
            var pillar=GetComponentInParent<FallingPillarAuthoring>(); if(pillar==null) return;
            var e=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e,new PillarVisualOwner { Value=GetEntity(pillar,TransformUsageFlags.Dynamic) });
            AddComponent(e,new Unity.Rendering.URPMaterialPropertyBaseColor { Value=new float4(1) });
        }
    }

}
