using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid.Baking;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct ArenaDepthRoleBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Rebuild just these overrides so incremental renames also remove stale roles.
            var previous = SystemAPI.QueryBuilder().WithAll<ArenaDepthRole>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab).Build();
            state.EntityManager.RemoveComponent<ArenaDepthRole>(previous);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (data, submeshes, primary) in
                     SystemAPI.Query<RefRO<ArenaDepthRoleBakingData>, DynamicBuffer<AdditionalEntitiesBakingData>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab).WithEntityAccess())
            {
                if (SystemAPI.HasComponent<RenderBounds>(primary))
                    commands.AddComponent(primary, data.ValueRO.Role);
                foreach (var submesh in submeshes)
                    if (SystemAPI.HasComponent<RenderBounds>(submesh.Value))
                        commands.AddComponent(submesh.Value, data.ValueRO.Role);
            }
            commands.Playback(state.EntityManager);
        }
    }
}
