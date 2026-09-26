using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class BarricadeVisualBaker : Baker<BarricadeVisualAuthoring>
    {
        public override void Bake(BarricadeVisualAuthoring a)
        {
            if (a.barricade == null) return;
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var renderer = GetComponent<MeshRenderer>();
            var color = renderer.sharedMaterial.color;
            float4 rgba = new float4(color.r, color.g, color.b, color.a);
            AddComponent(e, new URPMaterialPropertyBaseColor { Value = rgba });
            AddComponent(e, new BarricadeVisual { Barricade = GetEntity(a.barricade, TransformUsageFlags.Dynamic),
                Color = rgba, Position = a.transform.localPosition, Scale = -1,
                DebrisDirection = a.debrisDirection, CrackStage = (byte)a.crackStage });
        }
    }
}

