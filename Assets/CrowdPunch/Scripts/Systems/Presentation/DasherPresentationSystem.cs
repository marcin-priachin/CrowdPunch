using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>Grey-box state language: pulsing warning, bright motion streak colour, and dim recovery.</summary>
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct DasherPresentationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            float pulse = 0.5f + 0.5f * math.sin((float)SystemAPI.Time.ElapsedTime * 24f);
            foreach ((RefRW<URPMaterialPropertyBaseColor> color, RefRW<PostTransformMatrix> shape,
                         RefRO<DasherState> dash, RefRO<EnemyLaunchState> launch) in
                     SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, RefRW<PostTransformMatrix>, RefRO<DasherState>, RefRO<EnemyLaunchState>>())
            {
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched)
                {
                    color.ValueRW.Value = new float4(1f, 0.85f, 0.2f, 1f);
                    shape.ValueRW.Value = float4x4.Scale(new float3(0.55f, 0.8f, 3.2f));
                }
                else if (dash.ValueRO.Phase == DasherPhase.Preparing)
                    color.ValueRW.Value = new float4(0.65f + 0.35f * pulse, 0.12f, 0.12f, 1f);
                else if (dash.ValueRO.Phase == DasherPhase.Dashing)
                {
                    color.ValueRW.Value = new float4(0.95f, 0.95f, 1f, 1f);
                    shape.ValueRW.Value = float4x4.Scale(new float3(0.55f, 0.8f, 3.2f));
                }
                else if (dash.ValueRO.Phase == DasherPhase.Recovering)
                    color.ValueRW.Value = new float4(0.16f, 0.16f, 0.2f, 1f);
                else color.ValueRW.Value = new float4(0.38f, 0.38f, 0.42f, 1f);
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched && dash.ValueRO.Phase != DasherPhase.Dashing)
                    shape.ValueRW.Value = float4x4.Scale(new float3(0.8f, 1.15f, 1.45f));
            }
        }
    }
}
