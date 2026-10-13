using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem)), UpdateBefore(typeof(Physics.ApplyImpulseSystem))]
    public partial struct NinjaInterruptSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NinjaState>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (ninja, tuning, launch) in SystemAPI.Query<RefRW<NinjaState>, RefRO<NinjaTuning>, RefRO<EnemyLaunchState>>().WithNone<RespawnRequest>())
            {
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active || ninja.ValueRO.WasInactive != 0
                    && ninja.ValueRO.ObservedLaunchSequence == launch.ValueRO.LaunchSequence) continue;
                NinjaScheduling.Interrupt(ref ninja.ValueRW, tuning.ValueRO, SystemAPI.Time.ElapsedTime);
                ninja.ValueRW.WasInactive = 1;
                ninja.ValueRW.ObservedLaunchSequence = launch.ValueRO.LaunchSequence;
            }
        }
    }
}
