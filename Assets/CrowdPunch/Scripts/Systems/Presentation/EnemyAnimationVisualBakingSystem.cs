using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid.Baking;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // Entities Graphics creates separate rendering entities for skinned submeshes.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct EnemyAnimationVisualBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (animation, additional) in SystemAPI.Query<RefRO<EnemyAnimation>, DynamicBuffer<AdditionalEntitiesBakingData>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                commands.AddComponent(animation.ValueRO.Owner, new EnemyLandingAnimation
                {
                    Duration = animation.ValueRO.Samples.Value.Durations[EnemyAnimationSamples.ImpactMotion]
                });
                foreach (var child in additional)
                {
                    Entity entity = child.Value;
                    if (!SystemAPI.HasComponent<RenderBounds>(entity)) continue;
                    commands.AddComponent(entity, new EnemyVisualOwner { Value = animation.ValueRO.Owner });
                    commands.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
                }
            }
            commands.Playback(state.EntityManager);
        }
    }
}
