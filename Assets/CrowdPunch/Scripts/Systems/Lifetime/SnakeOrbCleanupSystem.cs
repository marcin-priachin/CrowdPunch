using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(EnemyRespawnSystem)), UpdateAfter(typeof(EnemyWaveDefeatCountSystem))]
    public partial struct SnakeOrbCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (segment, launch, entity) in SystemAPI.Query<RefRO<SnakeSegment>, RefRO<EnemyLaunchState>>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                var s = segment.ValueRO;
                bool stale = s.Encounter.Sequence != Entity.Null
                    && (!SystemAPI.HasComponent<EnemyWaveSequence>(s.Encounter.Sequence)
                        || SystemAPI.GetComponent<EnemyWaveSequence>(s.Encounter.Sequence).RunGeneration != s.Encounter.RunGeneration);
                bool deadHead = !SystemAPI.HasComponent<SnakeHead>(s.Head)
                    || SystemAPI.GetComponent<SnakeHead>(s.Head).Generation != s.Generation
                    || SystemAPI.IsComponentEnabled<RespawnRequest>(s.Head)
                    || SystemAPI.GetComponent<EnemyLaunchState>(s.Head).Phase == EnemyLaunchPhase.Defeated;
                if (stale || s.Detached == 0 && deadHead || s.Detached != 0 && launch.ValueRO.Phase != EnemyLaunchPhase.Launched)
                    commands.DestroyEntity(entity);
            }
            commands.Playback(state.EntityManager);
        }
    }
}
