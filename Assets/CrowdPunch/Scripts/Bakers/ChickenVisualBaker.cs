using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;
using UnityEngine;
namespace CrowdPunch.Bakers
{
    public sealed class ChickenVisualBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer a)
        {
            var owner=GetComponentInParent<ChickenBossAuthoring>();
            if(owner==null) return;
            var e=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e,new ChickenVisualOwner { Value=GetEntity(owner,TransformUsageFlags.Dynamic) });
            AddComponent(e,new URPMaterialPropertyBaseColor { Value=new float4(1) });
        }
    }
}
