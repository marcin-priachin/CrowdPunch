using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PunchDetectionSystem))]
    [UpdateAfter(typeof(AI.ElitePunchSystem))]
    [UpdateBefore(typeof(DamageApplicationSystem)), UpdateBefore(typeof(Physics.ApplyImpulseSystem))]
    public partial struct SnakePunchSeverSystem : ISystem
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
