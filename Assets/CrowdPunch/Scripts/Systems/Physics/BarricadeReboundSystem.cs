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
            foreach (var (wall, rebounds) in SystemAPI.Query<RefRO<Barricade>, DynamicBuffer<BarricadeRebound>>())
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
                    SystemAPI.SetComponent(contact.Source, velocity);
                    launch.HomingTarget = Entity.Null;
                    launch.BelowUsefulMomentumSeconds = 0;
                    SystemAPI.SetComponent(contact.Source, launch);
                }
                rebounds.Clear();
            }
        }
    }
}
