using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(TrailEmissionSystem))]
    public partial struct TrailExpirySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (section, entity) in SystemAPI.Query<RefRO<TrailSection>>().WithEntityAccess())
                if (now >= section.ValueRO.ExpiresAt || !SystemAPI.HasComponent<TrailSource>(section.ValueRO.Record) ||
                    !Valid(state.EntityManager, SystemAPI.GetComponent<TrailSource>(section.ValueRO.Record))) commands.DestroyEntity(entity);
            foreach (var (source, entity) in SystemAPI.Query<RefRO<TrailSource>>().WithEntityAccess())
                if (now >= source.ValueRO.ExpiresAt || !Valid(state.EntityManager, source.ValueRO)) commands.DestroyEntity(entity);
            commands.Playback(state.EntityManager);
        }

        public static bool Valid(EntityManager em, TrailSource source) =>
            (source.SceneOwner == Entity.Null || em.Exists(source.SceneOwner)) &&
            (source.Sequence == Entity.Null || em.HasComponent<EnemyWaveSequence>(source.Sequence) &&
                em.GetComponentData<EnemyWaveSequence>(source.Sequence).RunGeneration == source.RunGeneration);
    }
}
