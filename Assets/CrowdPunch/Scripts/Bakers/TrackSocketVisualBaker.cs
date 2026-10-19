using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class TrackSocketVisualBaker : Baker<TrackSocketVisualAuthoring>
    {
        public override void Bake(TrackSocketVisualAuthoring a)
        {
            if (a.trackObject == null) return;
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var material = GetComponent<MeshRenderer>().sharedMaterial;
            DependsOn(material);
            var color = material.color;
            if (material.HasProperty("_UseEntityColor") && material.GetFloat("_UseEntityColor") > .5f)
                color = color.linear;
            var rgba = new float4(color.r, color.g, color.b, color.a);
            AddComponent(e, new TrackSocketVisual { Object = GetEntity(a.trackObject, TransformUsageFlags.Dynamic), OpenColor = rgba });
            AddComponent(e, new URPMaterialPropertyBaseColor { Value = rgba });
        }
    }
}
