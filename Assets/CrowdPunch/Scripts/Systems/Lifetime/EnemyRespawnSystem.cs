using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Lifetime
{
    /// <summary>
    /// Returns invalid enemies to spawn positions instead of destroying them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(OutOfBoundsSystem))]
    public partial struct EnemyRespawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RespawnRequest>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double elapsedTime = SystemAPI.Time.ElapsedTime;
            ArenaBounds arenaBounds = SystemAPI.GetSingleton<ArenaBounds>();
            PlayerSnapshot playerSnapshot = SystemAPI.HasSingleton<PlayerSnapshot>()
                ? SystemAPI.GetSingleton<PlayerSnapshot>()
                : default;

            foreach ((RefRW<RespawnRequest> respawnRequest,
                         RefRW<LocalTransform> transform,
                         RefRW<Health> health,
                         RefRW<HealthBar> healthBar,
                         RefRW<DesiredMovement> desiredMovement,
                         RefRW<WanderDestination> wanderDestination,
                         RefRW<PhysicsVelocity> physicsVelocity,
                         Entity enemy) in
                     SystemAPI.Query<RefRW<RespawnRequest>,
                             RefRW<LocalTransform>,
                             RefRW<Health>,
                             RefRW<HealthBar>,
                             RefRW<DesiredMovement>,
                             RefRW<WanderDestination>,
                             RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                physicsVelocity.ValueRW = default;
                desiredMovement.ValueRW = default;
                wanderDestination.ValueRW = default;

                if (respawnRequest.ValueRO.IsPooled == 0)
                {
                    transform.ValueRW.Position = GetPoolPosition(arenaBounds);
                    health.ValueRW.Current = health.ValueRO.Max;
                    healthBar.ValueRW.Normalized = health.ValueRO.Normalized;
                    respawnRequest.ValueRW.IsPooled = 1;
                    DisableTransientState(ref state, enemy);
                }

                if (elapsedTime < respawnRequest.ValueRO.RespawnAt)
                {
                    transform.ValueRW.Position = GetPoolPosition(arenaBounds);
                    continue;
                }

                uint seed = (uint)math.max(1, enemy.Index + 1)
                    ^ ((uint)math.max(1, enemy.Version + 1) * 747796405u)
                    ^ ((uint)math.max(1, (int)math.round((float)elapsedTime * 1000f)) * 2891336453u);
                Random random = Random.CreateFromIndex(seed);

                transform.ValueRW.Position = GetRespawnPosition(ref random, arenaBounds, playerSnapshot);
                respawnRequest.ValueRW = default;
                SystemAPI.SetComponentEnabled<RespawnRequest>(enemy, false);
            }
        }

        private void DisableTransientState(ref SystemState state, Entity enemy)
        {
            if (SystemAPI.HasComponent<DamageRequest>(enemy))
            {
                SystemAPI.SetComponentEnabled<DamageRequest>(enemy, false);
            }

            if (SystemAPI.HasComponent<DeathRequest>(enemy))
            {
                SystemAPI.SetComponentEnabled<DeathRequest>(enemy, false);
            }

            if (SystemAPI.HasComponent<ExternalImpulse>(enemy))
            {
                SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, false);
            }

            if (SystemAPI.HasComponent<KnockbackRecovery>(enemy))
            {
                SystemAPI.SetComponentEnabled<KnockbackRecovery>(enemy, false);
            }
        }

        private static float3 GetPoolPosition(ArenaBounds arenaBounds)
        {
            return arenaBounds.Center - new float3(0f, math.max(25f, arenaBounds.Extents.y + 25f), 0f);
        }

        private static float3 GetRespawnPosition(ref Random random, ArenaBounds arenaBounds, PlayerSnapshot playerSnapshot)
        {
            float2 center = arenaBounds.Center.xz;
            float2 extents = math.max(arenaBounds.Extents.xz, new float2(0f));
            float playerAvoidDistance = math.max(6f, playerSnapshot.Radius + 5f);
            float playerAvoidDistanceSq = playerAvoidDistance * playerAvoidDistance;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float3 position = GetRandomEdgePosition(ref random, arenaBounds.Center.y, center, extents);
                if (!playerSnapshot.IsAvailable
                    || math.distancesq(position.xz, playerSnapshot.Position.xz) >= playerAvoidDistanceSq)
                {
                    return position;
                }
            }

            return GetFarthestEdgePosition(arenaBounds.Center.y, center, extents, playerSnapshot);
        }

        private static float3 GetRandomEdgePosition(ref Random random, float y, float2 center, float2 extents)
        {
            int edge = random.NextInt(0, 4);
            float x = random.NextFloat(center.x - extents.x, center.x + extents.x);
            float z = random.NextFloat(center.y - extents.y, center.y + extents.y);

            if (edge == 0)
            {
                x = center.x - extents.x;
            }
            else if (edge == 1)
            {
                x = center.x + extents.x;
            }
            else if (edge == 2)
            {
                z = center.y - extents.y;
            }
            else
            {
                z = center.y + extents.y;
            }

            return new float3(x, y, z);
        }

        private static float3 GetFarthestEdgePosition(float y, float2 center, float2 extents, PlayerSnapshot playerSnapshot)
        {
            if (!playerSnapshot.IsAvailable)
            {
                return new float3(center.x + extents.x, y, center.y);
            }

            float3 bestPosition = new float3(center.x + extents.x, y, center.y);
            float bestDistanceSq = math.distancesq(bestPosition.xz, playerSnapshot.Position.xz);

            float3 candidate = new float3(center.x - extents.x, y, center.y);
            SelectFarthest(candidate, playerSnapshot.Position.xz, ref bestPosition, ref bestDistanceSq);

            candidate = new float3(center.x, y, center.y + extents.y);
            SelectFarthest(candidate, playerSnapshot.Position.xz, ref bestPosition, ref bestDistanceSq);

            candidate = new float3(center.x, y, center.y - extents.y);
            SelectFarthest(candidate, playerSnapshot.Position.xz, ref bestPosition, ref bestDistanceSq);

            return bestPosition;
        }

        private static void SelectFarthest(float3 candidate, float2 playerPosition, ref float3 bestPosition, ref float bestDistanceSq)
        {
            float distanceSq = math.distancesq(candidate.xz, playerPosition);
            if (distanceSq > bestDistanceSq)
            {
                bestPosition = candidate;
                bestDistanceSq = distanceSq;
            }
        }
    }
}
