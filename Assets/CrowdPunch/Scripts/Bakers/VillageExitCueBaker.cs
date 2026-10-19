using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class VillageExitCueBaker : Baker<VillageExitCueAuthoring>
    {
        public override void Bake(VillageExitCueAuthoring a)
        {
            if(a.gate==null) return;
            var entity=GetEntity(TransformUsageFlags.Dynamic);
            var material=GetComponent<MeshRenderer>().sharedMaterial;
            DependsOn(material);
            bool linear=material.HasProperty("_UseEntityColor") && material.GetFloat("_UseEntityColor")>.5f;
            var lockedColor=linear?a.lockedColor.linear:a.lockedColor;
            var openColor=linear?a.openColor.linear:a.openColor;
            float4 locked=new float4(lockedColor.r,lockedColor.g,lockedColor.b,1);
            AddComponent(entity,new VillageExitCue {Gate=GetEntity(a.gate,TransformUsageFlags.Dynamic),
                LockedColor=locked,OpenColor=new float4(openColor.r,openColor.g,openColor.b,1),
                Phase=a.phase,Arrow=a.arrow?(byte)1:(byte)0});
            AddComponent(entity,new URPMaterialPropertyBaseColor {Value=locked});
        }
    }
}
