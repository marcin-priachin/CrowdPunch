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
    public partial struct CoverReflectionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (cover, contacts) in SystemAPI.Query<RefRO<RotatingCover>, DynamicBuffer<CoverReflection>>())
            {
                foreach (var contact in contacts)
                {
                    if (!SystemAPI.HasComponent<EnemyLaunchState>(contact.Source)) continue;
                    var launch = SystemAPI.GetComponent<EnemyLaunchState>(contact.Source);
                    if (launch.Phase != EnemyLaunchPhase.Launched || launch.LaunchSequence != contact.LaunchSequence) continue;
                    var transform = SystemAPI.GetComponent<LocalTransform>(contact.Source);
                    float penetration = math.dot(transform.Position - contact.ContactCenter, contact.Normal);
                    if (penetration < 0) transform.Position -= contact.Normal * penetration;
                    SystemAPI.SetComponent(contact.Source, transform);
                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(contact.Source);
                    velocity.Linear.xz = ReflectedVelocity(contact, cover.ValueRO.ReflectionMultiplier);
                    SystemAPI.SetComponent(contact.Source, velocity);
                    // Keep sequence, ownership, damage deduplication and recovery timers intact.
                    launch.HomingTarget = Entity.Null;
                    SystemAPI.SetComponent(contact.Source, launch);
                }
                contacts.Clear();
            }
        }

        public static float2 ReflectedVelocity(in CoverReflection contact, float multiplier)
            => math.normalizesafe(contact.PlayerPosition.xz - contact.ContactCenter.xz,
                math.normalizesafe(-contact.IncomingVelocity.xz, contact.Normal.xz))
                * math.length(contact.IncomingVelocity.xz) * math.max(0, multiplier);
    }
}
