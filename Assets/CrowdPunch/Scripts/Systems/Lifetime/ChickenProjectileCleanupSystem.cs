using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(ChickenDamageSystem))]
    public partial struct ChickenProjectileCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var (shot,e) in SystemAPI.Query<RefRO<ChickenProjectile>>().WithEntityAccess())
                if(!SystemAPI.HasComponent<ChickenBoss>(shot.ValueRO.Boss)
                    || SystemAPI.GetComponent<ChickenBoss>(shot.ValueRO.Boss).Phase==ChickenPhase.Defeated) commands.DestroyEntity(e);
            commands.Playback(state.EntityManager); commands.Dispose();
        }
    }
}
