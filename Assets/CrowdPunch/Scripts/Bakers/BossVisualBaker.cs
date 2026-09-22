using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class BossVisualBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer a)
        {
            var owner=GetComponentInParent<BossPartAuthoring>();
            if(owner==null) return;
            var e=GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e,new BossVisualOwner { Value=GetEntity(owner,TransformUsageFlags.Dynamic) });
            AddComponent(e,new URPMaterialPropertyBaseColor { Value=new float4(1) });
        }
    }
}
