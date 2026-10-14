using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionResolutionSystem)), UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(WizardZoneSystem)), UpdateAfter(typeof(PillarFallContactSystem))]
    [UpdateAfter(typeof(LinkedPairDamageSystem))]
    [UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct SnakeImpactSeverSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (head, links, entity) in SystemAPI.Query<RefRW<SnakeHead>, DynamicBuffer<SnakeTailLink>>().WithEntityAccess())
                SnakeSevering.Resolve(state.EntityManager, entity, ref head.ValueRW, links, commands);
            commands.Playback(state.EntityManager);
        }
    }
}
