using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Presentation
{
    internal static class AlienSpikeTrajectoryPrediction
    {
        internal static void Trace(EntityManager em, Entity entity, CollisionWorld world, AlienSpikeShotContact collector,
            AlienSpikeProjectile shot, float3 position, in PunchSpecification punch, in PlayerSnapshot player,
            in EnemyLaunchSettings guidance, float maximumSeconds, int steps, float timestep,
            NativeList<float3> points, NativeList<LaunchTrajectoryImpact> impacts, NativeList<LaunchTrajectoryContact> contacts)
        {
            if (timestep <= 0 || steps <= 0) return;
            AlienSpikeProjectilePunch.Redirect(em, entity, ref shot, position, punch);
            points.Add(position);
            float remaining = math.min(maximumSeconds, shot.Remaining);
            for (int i = 0; i < steps && remaining > 0; i++)
            {
                float dt = math.min(timestep, remaining);
                if (shot.HomingTarget == Entity.Null)
                {
                    dt = math.min((steps - i) * timestep, remaining);
                    i = steps - 1;
                }
                AlienSpikeProjectilePunch.Guide(em, entity, ref shot, position, guidance, dt);
                float3 displacement = shot.Velocity * dt;
                var hit = AlienSpikeShotContact.Sweep(world, collector, shot, player, position, displacement);
                position += displacement * hit.MaxFraction;
                points.Add(position);
                if (hit.Kind != AlienSpikeContactKind.None)
                {
                    contacts.Add(new LaunchTrajectoryContact { EndPointIndex = points.Length - 1, Position = position });
                    if (hit.Kind == AlienSpikeContactKind.Enemy && !ArmorHitResolution.IsProtected(em, hit.Target))
                        impacts.Add(new LaunchTrajectoryImpact { Position = position, Direction = shot.Velocity });
                    return;
                }
                remaining -= dt;
            }
        }
    }
}

