using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Applies pending damage, then resolves immediate or launch-deferred defeat.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PunchDetectionSystem))]
    public partial struct DamageApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Health>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DamageRequest>>().WithAll<Enemy>().WithAll<Health>().WithAll<EnemyLaunchState>().WithAll<EnemyDamageState>()
                .WithNone<BossPart>().WithEntityAccess())
                EnemyDamageResolution.ApplyPending(state.EntityManager, entity, SystemAPI.Time.ElapsedTime);
        }
    }
}
