using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(CommittedLaunchReboundSystem))]
    [UpdateAfter(typeof(EnemyRicochetAimSystem))]
    [UpdateAfter(typeof(BarricadeReboundSystem))]
    [UpdateAfter(typeof(CoverReflectionSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct AuthoritativeLaunchFinishSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            foreach (var (path, launch, velocity, pose, points, source) in
                SystemAPI.Query<RefRW<AuthoritativeLaunchPath>, RefRO<EnemyLaunchState>, RefRW<PhysicsVelocity>,
                    RefRO<LocalTransform>, DynamicBuffer<CommittedLaunchPathPoint>>().WithEntityAccess())
            {
                if (launch.ValueRO.LaunchSequence != path.ValueRO.LaunchSequence || launch.ValueRO.Phase != EnemyLaunchPhase.Launched
                    || launch.ValueRO.Owner != EnemyLaunchOwner.Player || path.ValueRO.NextPoint >= points.Length
                    || em.HasComponent<RespawnRequest>(source) && em.IsComponentEnabled<RespawnRequest>(source)
                    || math.distancesq(pose.ValueRO.Position, path.ValueRO.ScheduledPosition) > .0025f)
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                var point = points[path.ValueRO.NextPoint].Value;
                float3 desired = path.ValueRO.ExitVelocity;
                Entity surface = path.ValueRO.Contact;
                // COVER-003 samples the player at impact. A changed return direction takes
                // precedence over the old preview; the existing reflection already set velocity.
                if (path.ValueRO.ExpectedContact != 0 && em.HasComponent<RotatingCover>(surface)
                    && math.distancesq(velocity.ValueRO.Linear.xz, desired.xz) > .000001f)
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                if (path.ValueRO.ReleaseAfterContact != 0)
                {
                    bool reflected = em.HasComponent<RotatingCover>(surface)
                        || em.HasComponent<Barricade>(surface) && em.GetComponentData<Barricade>(surface).HitsRemaining > 0;
                    desired = reflected ? velocity.ValueRO.Linear : path.ValueRO.IncomingVelocity;
                }
                if (surface != Entity.Null && SystemAPI.TryGetSingleton<PlayerSnapshot>(out var player) && player.IsAvailable)
                {
                    float3 safe = desired;
                    if (em.HasComponent<FinaleBoss>(surface))
                        safe = LaunchContactResponse.FinaleSafety(desired, pose.ValueRO.Position, player.Position, path.ValueRO.ContactNormal,
                            em.GetComponentData<FinaleTuning>(surface).ReboundSafetyDegrees);
                    else if (em.HasComponent<ChickenBoss>(surface) || em.HasComponent<RollingBoss>(surface)
                        || em.HasComponent<BossPart>(surface) && em.GetComponentData<BossPart>(surface).Kind == BossPartKind.Head)
                        safe = BossHeadBounceSystem.Redirect(desired, pose.ValueRO.Position,
                            em.GetComponentData<LocalTransform>(surface).Position, player.Position, source.Index);
                    if (math.distancesq(safe, desired) > .000001f)
                    { velocity.ValueRW.Linear = safe; AuthoritativeLaunchPlan.Release(em, source); continue; }
                }
                velocity.ValueRW.Linear = desired;
                if (path.ValueRO.ReleaseAfterContact != 0)
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                if (path.ValueRO.ReachedPoint != 0)
                {
                    if (path.ValueRO.ExpectedContact != 0)
                        SnakeProjectileCollision.RecordRebound(em, source, path.ValueRO.IncomingVelocity, point.Normal);
                    path.ValueRW.NextPoint++;
                    if (path.ValueRO.NextPoint >= points.Length)
                    {
                        // The displayed endpoint is the end of this launch, including a budget-limited path.
                        velocity.ValueRW.Linear = float3.zero;
                        velocity.ValueRW.Angular = float3.zero;
                        if (em.HasComponent<DasherState>(source))
                        {
                            var dash = em.GetComponentData<DasherState>(source);
                            dash.PreservedLaunchedVelocity = float3.zero; em.SetComponentData(source, dash);
                        }
                        AuthoritativeLaunchPlan.Release(em, source);
                    }
                }
            }
        }
    }
}
