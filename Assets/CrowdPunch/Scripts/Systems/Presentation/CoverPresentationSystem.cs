using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct CoverPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (panel, transform, scale) in SystemAPI.Query<RefRO<CoverPanel>, RefRW<LocalTransform>, RefRW<PostTransformMatrix>>())
            {
                if (!SystemAPI.HasComponent<RotatingCover>(panel.ValueRO.Cover)) continue;
                var cover = SystemAPI.GetComponent<RotatingCover>(panel.ValueRO.Cover);
                CoverGeometry.Panel(cover, panel.ValueRO.Index, out var position, out var rotation, out var size);
                transform.ValueRW.Position = position;
                transform.ValueRW.Rotation = rotation;
                scale.ValueRW.Value = float4x4.Scale(size);
            }
        }
    }
}
