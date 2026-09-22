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
            UnityEngine.Color color=a.sharedMaterial!=null?a.sharedMaterial.color:UnityEngine.Color.white;
            if(a.sharedMaterial!=null) DependsOn(a.sharedMaterial);
            float4 tint=new float4(color.r,color.g,color.b,color.a);
            AddComponent(e,new BossVisualOwner { Value=GetEntity(owner,TransformUsageFlags.Dynamic), Color=tint });
            AddComponent(e,new URPMaterialPropertyBaseColor { Value=tint });
        }
    }
}
