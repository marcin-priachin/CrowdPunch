using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem)), UpdateAfter(typeof(Physics.NinjaTeleportSystem))]
    [UpdateBefore(typeof(Combat.PunchDetectionSystem))]
    public partial struct NinjaMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NinjaState>(); state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (ninja, tuning, launch, transform, velocity, agent, entity) in
                     SystemAPI.Query<RefRW<NinjaState>, RefRO<NinjaTuning>, RefRO<EnemyLaunchState>, RefRW<LocalTransform>,
                         RefRW<PhysicsVelocity>, RefRO<NavigationAgent>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var n = ref ninja.ValueRW;
                n.HasDashSegment = 0;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active) continue;
                if (n.Phase == NinjaPhase.Positioning) continue;
                velocity.ValueRW.Linear.xz = float2.zero;
                velocity.ValueRW.Angular = float3.zero;
                if (n.Phase == NinjaPhase.ArrivalWarning || n.Phase == NinjaPhase.Dashing)
                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(n.LockedDirection, math.up());
                if (n.Phase != NinjaPhase.Dashing) continue;
                float remaining = math.max(0, tuning.ValueRO.MaximumDashDistance - n.DistanceTravelled);
                float requested = math.min(math.max(0, tuning.ValueRO.DashSpeed) * dt, remaining);
                float allowed = NinjaSolidSweep.Distance(world, enemies, entity, transform.ValueRO.Position,
                    n.LockedDirection, requested, agent.ValueRO.Radius);
                n.SegmentStart = transform.ValueRO.Position;
                n.SegmentEnd = n.SegmentStart + n.LockedDirection * allowed;
                n.HasDashSegment = 1;
                velocity.ValueRW.Linear.xz = n.LockedDirection.xz * allowed / math.max(.00001f, dt);
                // Recovering still retains this final swept segment until combat has evaluated it.
                if (allowed < requested - .001f || remaining <= requested + .001f || requested <= .00001f)
                    NinjaScheduling.Recover(ref n, tuning.ValueRO, SystemAPI.Time.ElapsedTime);
            }
        }
    }
}
