using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class TrajectoryPreviewSurfaceBaker : Baker<TrajectoryPreviewSurfaceAuthoring>
    {
        public override void Bake(TrajectoryPreviewSurfaceAuthoring authoring)
        {
            var top = authoring.transform.TransformPoint(Vector3.up * authoring.heightOffset);
            var scale = authoring.transform.lossyScale;
            AddComponent(GetEntity(TransformUsageFlags.None), new TrajectoryPreviewSurface
            {
                Center = new float2(top.x, top.z),
                Radius = math.max(0f, authoring.radius) * math.max(math.abs(scale.x), math.abs(scale.z)),
                Height = top.y
            });
        }
    }
}
