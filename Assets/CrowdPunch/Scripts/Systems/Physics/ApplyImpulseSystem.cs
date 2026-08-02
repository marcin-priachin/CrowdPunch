using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>
    /// Transfers gameplay impulses into Unity Physics velocity data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct ApplyImpulseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExternalImpulse>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRO<ExternalImpulse> externalImpulse, RefRW<PhysicsVelocity> physicsVelocity, Entity enemy) in
                     SystemAPI.Query<RefRO<ExternalImpulse>, RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                    physicsVelocity.ValueRW.Linear += externalImpulse.ValueRO.Value;
                SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, false);
            }
        }
    }
}
