using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct TrackSocketPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (visual, color) in SystemAPI.Query<RefRO<TrackSocketVisual>, RefRW<URPMaterialPropertyBaseColor>>())
                if (SystemAPI.HasComponent<TrackObjectState>(visual.ValueRO.Object))
                    color.ValueRW.Value = SystemAPI.GetComponent<TrackObjectState>(visual.ValueRO.Object).Locked != 0
                        ? new float4(.15f, 1, .4f, 1) : visual.ValueRO.OpenColor;
        }
    }
}
