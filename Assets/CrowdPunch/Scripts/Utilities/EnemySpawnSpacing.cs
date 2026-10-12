using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Utilities
{
    // Placement spacing is a planar centre distance, independent of collider clearance.
    public static class EnemySpawnSpacing
    {
        public struct Occupant
        {
            public Entity Entity;
            public float3 Position;
            public float MinimumDistance;
        }

        public static float Minimum(in EnemyMovementSettings settings) => math.max(0f, settings.SeparationDistanceMin);

        public static NativeList<Occupant> Capture(EntityManager em, bool includeReservations = true)
        {
            var occupied = new NativeList<Occupant>(Allocator.Temp);
            using var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Enemy, LocalTransform, EnemyMovementSettings>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities);
            using var query = builder.Build(em);
            using var enemies = query.ToEntityArray(Allocator.Temp);
            foreach (var enemy in enemies)
            {
                bool pending = em.HasComponent<EnemySpawnWarning>(enemy);
                if (pending && !includeReservations) continue;
                if (!pending && (em.HasComponent<Disabled>(enemy)
                    || em.HasComponent<RespawnRequest>(enemy) && em.IsComponentEnabled<RespawnRequest>(enemy))) continue;
                occupied.Add(new Occupant { Entity = enemy,
                    Position = em.GetComponentData<LocalTransform>(enemy).Position,
                    MinimumDistance = Minimum(em.GetComponentData<EnemyMovementSettings>(enemy)) });
            }
            return occupied;
        }

        public static bool IsClear(float3 position, float minimumDistance, NativeList<Occupant> occupied,
            Entity ignore = default)
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                var other = occupied[i];
                if (ignore != Entity.Null && other.Entity == ignore) continue;
                float required = math.max(minimumDistance, other.MinimumDistance);
                if (math.distancesq(position.xz, other.Position.xz) < required * required) return false;
            }
            return true;
        }

        public static float2 LimitApproachVelocity(float2 position, float2 velocity, float2 reservation,
            float distance, float brakingAcceleration)
        {
            float2 offset = reservation - position;
            float length = math.length(offset);
            if (length <= .0001f) return velocity;
            float2 toward = offset / length;
            float allowed = math.sqrt(2f * math.max(0f, brakingAcceleration) * math.max(0f, length - distance));
            return velocity - toward * math.max(0f, math.dot(velocity, toward) - allowed);
        }
    }
}
