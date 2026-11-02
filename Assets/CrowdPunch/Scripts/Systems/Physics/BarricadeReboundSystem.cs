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
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(ExplosionResolutionSystem))]
    public partial struct BarricadeReboundSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (wall, rebounds, target) in SystemAPI.Query<RefRO<Barricade>, DynamicBuffer<BarricadeRebound>>().WithEntityAccess())
            {
                foreach (var contact in rebounds)
                {
                    if (wall.ValueRO.HitsRemaining <= 0 || !SystemAPI.HasComponent<EnemyLaunchState>(contact.Source)) continue;
                    var launch = SystemAPI.GetComponent<EnemyLaunchState>(contact.Source);
                    if (launch.Phase != EnemyLaunchPhase.Launched || launch.LaunchSequence != contact.LaunchSequence) continue;
                    var transform = SystemAPI.GetComponent<LocalTransform>(contact.Source);
                    // A discrete solver can tunnel at extreme launch speeds. Correct penetration only along
                    // the contact normal; normal movement and tangential collision response remain solver-owned.
                    transform.Position = LaunchContactResponse.OutsideSurface(transform.Position, contact.ContactCenter, contact.Normal);
                    SystemAPI.SetComponent(contact.Source, transform);
                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(contact.Source);
                    velocity.Linear = LaunchContactResponse.BarricadeVelocity(velocity.Linear, contact.IncomingVelocity,
                        contact.Normal, wall.ValueRO.ReboundMultiplier);
                    if (SystemAPI.HasComponent<FinaleBoss>(target) && SystemAPI.HasSingleton<PlayerSnapshot>())
                    {
                        float angle = SystemAPI.GetComponent<FinaleTuning>(target).ReboundSafetyDegrees;
                        velocity.Linear = LaunchContactResponse.FinaleSafety(velocity.Linear, transform.Position,
                            SystemAPI.GetSingleton<PlayerSnapshot>().Position, contact.Normal, angle);
                    }
                    SystemAPI.SetComponent(contact.Source, velocity);
                    SnakeProjectileCollision.RecordRebound(state.EntityManager,contact.Source,contact.IncomingVelocity,contact.Normal);
                    LaunchAimPrediction.StopHoming(ref launch);
                    launch.RicochetSurface = target;
                    launch.RicochetNormal = contact.Normal;
                    CommittedLaunchPlan.Contact(state.EntityManager, contact.Source, launch, target, contact.Normal);
                    launch.BelowUsefulMomentumSeconds = 0;
                    SystemAPI.SetComponent(contact.Source, launch);
                }
                rebounds.Clear();
            }
        }
    }
}
