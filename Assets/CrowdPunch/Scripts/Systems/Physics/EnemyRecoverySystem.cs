using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>
    /// Advances enemy knockback recovery state after physics has simulated.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    public partial struct EnemyRecoverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<KnockbackRecovery>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<KnockbackRecovery> recovery, Entity enemy) in
                     SystemAPI.Query<RefRW<KnockbackRecovery>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                recovery.ValueRW.RemainingSeconds -= deltaTime;

                if (recovery.ValueRO.RemainingSeconds <= 0f)
                {
                    SystemAPI.SetComponentEnabled<KnockbackRecovery>(enemy, false);
                }
            }
        }
    }
}
