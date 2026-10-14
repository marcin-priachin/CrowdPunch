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
                    float penetration = math.dot(transform.Position - contact.ContactCenter, contact.Normal);
                    if (penetration < 0)
                    {
                        transform.Position -= contact.Normal * penetration;
                        SystemAPI.SetComponent(contact.Source, transform);
                    }
                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(contact.Source);
                    velocity.Linear.xz = math.reflect(contact.IncomingVelocity, contact.Normal).xz * wall.ValueRO.ReboundMultiplier;
                    if (SystemAPI.HasComponent<FinaleBoss>(target) && SystemAPI.HasSingleton<PlayerSnapshot>())
                    {
                        float2 toward = math.normalizesafe(SystemAPI.GetSingleton<PlayerSnapshot>().Position.xz - transform.Position.xz);
                        float speed = math.length(velocity.Linear.xz);
                        float angle = SystemAPI.GetComponent<FinaleTuning>(target).ReboundSafetyDegrees;
                        if (math.dot(math.normalizesafe(velocity.Linear.xz), toward) > math.cos(math.radians(angle))) {
                            float2 side = new float2(-toward.y,toward.x);
                            // Choose the tangent facing out of the boss's surface, never back into the solid.
                            if (math.dot(side,contact.Normal.xz) < 0) side = -side;
                            velocity.Linear.xz = math.normalizesafe(side + contact.Normal.xz * .2f) * speed;
                        }
                    }
                    SystemAPI.SetComponent(contact.Source, velocity);
                    SnakeProjectileCollision.RecordRebound(state.EntityManager,contact.Source,contact.IncomingVelocity,contact.Normal);
                    launch.HomingTarget = Entity.Null;
                    launch.BelowUsefulMomentumSeconds = 0;
                    SystemAPI.SetComponent(contact.Source, launch);
                }
                rebounds.Clear();
            }
        }
    }
}
