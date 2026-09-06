using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    /// <summary>Renderer-owned baking allows elite child meshes to read their parent's enemy state.</summary>
    public sealed class EnemyVisualBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer authoring)
        {
            EnemyAuthoring owner = GetComponentInParent<EnemyAuthoring>();
            if (owner == null) return;
            Entity visual = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(visual, new EnemyVisualOwner { Value = GetEntity(owner, TransformUsageFlags.Dynamic) });
            AddComponent(visual, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
        }
    }
}
