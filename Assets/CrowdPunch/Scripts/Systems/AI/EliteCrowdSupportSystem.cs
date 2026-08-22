using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Lets active normal enemies stage shots and clear the firing lane for a living elite.</summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(ElitePunchSystem))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Movement.EnemyMovementSystem))]
    public partial class EliteCrowdSupportSystem : SystemBase
    {
        private EntityQuery normalEnemyQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSnapshot>();
            normalEnemyQuery = GetEntityQuery(
                ComponentType.ReadOnly<Enemy>(),
                ComponentType.ReadOnly<EnemyTier>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<DesiredMovement>(),
                ComponentType.ReadOnly<EnemyMovementSettings>(),
                ComponentType.ReadOnly<EnemyLaunchState>());
        }

        protected override void OnUpdate()
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable)
            {
                return;
            }

            using NativeArray<Entity> normalEnemies = normalEnemyQuery.ToEntityArray(Allocator.Temp);
            using NativeList<SupportElite> elites = new NativeList<SupportElite>(Allocator.Temp);

            foreach ((RefRO<LocalTransform> eliteTransform, RefRO<ElitePunchSettings> settings,
                         RefRO<ElitePunchState> punchState, RefRO<EnemyLaunchState> launchState, Entity elite) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ElitePunchSettings>, RefRO<ElitePunchState>,
                             RefRO<EnemyLaunchState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                if (launchState.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    continue;
                }

                elites.Add(new SupportElite
                {
                    Entity = elite,
                    Position = eliteTransform.ValueRO.Position,
                    Settings = settings.ValueRO,
                    SelectedProjectile = punchState.ValueRO.Target
                });
            }

            for (int eliteIndex = 0; eliteIndex < elites.Length; eliteIndex++)
            {
                SupportElite elite = elites[eliteIndex];
                Entity projectile = IsActiveNormal(elite.SelectedProjectile)
                    && GetClosestEliteIndex(
                        EntityManager.GetComponentData<LocalTransform>(elite.SelectedProjectile).Position,
                        elites) == eliteIndex
                    ? elite.SelectedProjectile
                    : FindClosestActiveNormal(eliteIndex, normalEnemies, elites);
                if (projectile == Entity.Null)
                {
                    continue;
                }

                float3 projectilePosition = EntityManager.GetComponentData<LocalTransform>(projectile).Position;
                float3 stagingPosition = FindStagingPosition(
                    elite.Entity,
                    projectile,
                    elite.Position,
                    projectilePosition,
                    player.Position,
                    elite.Settings,
                    normalEnemies);

                for (int index = 0; index < normalEnemies.Length; index++)
                {
                    Entity enemy = normalEnemies[index];
                    if (!IsActiveNormal(enemy)
                        || GetClosestEliteIndex(
                            EntityManager.GetComponentData<LocalTransform>(enemy).Position,
                            elites) != eliteIndex)
                    {
                        continue;
                    }

                    LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(enemy);
                    EnemyMovementSettings movementSettings = EntityManager.GetComponentData<EnemyMovementSettings>(enemy);
                    DesiredMovement movement = EntityManager.GetComponentData<DesiredMovement>(enemy);

                    if (enemy == projectile)
                    {
                        movement = GetStagingMovement(
                            transform.Position,
                            stagingPosition,
                            movementSettings.MoveSpeed,
                            elite.Settings.PositionTolerance);
                        ElitePunchReservation reservation = EntityManager.GetComponentData<ElitePunchReservation>(enemy);
                        if (reservation.Owner == elite.Entity)
                        {
                            reservation.IsStaged = movement.Speed <= 0f ? (byte)1 : (byte)0;
                            EntityManager.SetComponentData(enemy, reservation);
                        }
                    }
                    else if (TryGetCorridorExitDirection(
                                 transform.Position,
                                 projectilePosition,
                                 player.Position,
                                 elite.Settings.CrowdCorridorRadius,
                                 out float3 exitDirection))
                    {
                        movement.Direction = exitDirection;
                        movement.Speed = math.max(0f, movementSettings.MoveSpeed);
                    }

                    EntityManager.SetComponentData(enemy, movement);
                }
            }
        }

        private Entity FindClosestActiveNormal(
            int eliteIndex,
            NativeArray<Entity> normalEnemies,
            NativeList<SupportElite> elites)
        {
            Entity closest = Entity.Null;
            float closestDistanceSq = float.MaxValue;
            for (int index = 0; index < normalEnemies.Length; index++)
            {
                Entity enemy = normalEnemies[index];
                if (!IsActiveNormal(enemy))
                {
                    continue;
                }

                float3 enemyPosition = EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                if (GetClosestEliteIndex(enemyPosition, elites) != eliteIndex)
                {
                    continue;
                }

                float distanceSq = math.distancesq(
                    elites[eliteIndex].Position.xz,
                    enemyPosition.xz);
                if (distanceSq < closestDistanceSq
                    || distanceSq == closestDistanceSq && (closest == Entity.Null || enemy.Index < closest.Index))
                {
                    closest = enemy;
                    closestDistanceSq = distanceSq;
                }
            }

            return closest;
        }

        private bool IsActiveNormal(Entity entity)
        {
            return entity != Entity.Null
                && EntityManager.Exists(entity)
                && EntityManager.HasComponent<EnemyTier>(entity)
                && EntityManager.GetComponentData<EnemyTier>(entity).Value == EnemyCombatTier.Normal
                && EntityManager.HasComponent<EnemyLaunchState>(entity)
                && EntityManager.GetComponentData<EnemyLaunchState>(entity).Phase == EnemyLaunchPhase.Active
                && (!EntityManager.HasComponent<RespawnRequest>(entity)
                    || !EntityManager.IsComponentEnabled<RespawnRequest>(entity));
        }

        private float3 FindStagingPosition(
            Entity elite,
            Entity projectile,
            float3 elitePosition,
            float3 projectilePosition,
            float3 playerPosition,
            ElitePunchSettings settings,
            NativeArray<Entity> enemies)
        {
            float clearance = math.max(0.1f, settings.CrowdCorridorRadius);
            if (IsApproachLaneClear(
                    elite,
                    projectile,
                    elitePosition,
                    projectilePosition,
                    playerPosition,
                    clearance,
                    settings.DesiredPunchDistance,
                    enemies))
            {
                return projectilePosition;
            }

            float3 shotDirection = ElitePunchSystem.HorizontalDirection(projectilePosition, playerPosition);
            float3 perpendicular = new float3(-shotDirection.z, 0f, shotDirection.x);
            for (int ring = 1; ring <= 2; ring++)
            {
                float distance = clearance * ring;
                for (int index = 0; index < 8; index++)
                {
                    float3 direction = GetStagingSampleDirection(index, shotDirection, perpendicular);
                    float3 candidate = projectilePosition + direction * distance;
                    candidate.y = projectilePosition.y;
                    if (math.distancesq(candidate.xz, elitePosition.xz) < clearance * clearance
                        || HasWorldObstruction(projectilePosition, candidate)
                        || !IsApproachLaneClear(
                            elite,
                            projectile,
                            elitePosition,
                            candidate,
                            playerPosition,
                            clearance,
                            settings.DesiredPunchDistance,
                            enemies))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return projectilePosition;
        }

        private bool IsApproachLaneClear(
            Entity elite,
            Entity projectile,
            float3 elitePosition,
            float3 projectilePosition,
            float3 playerPosition,
            float clearance,
            float desiredPunchDistance,
            NativeArray<Entity> enemies)
        {
            float3 desiredElitePosition = ElitePunchSystem.DesiredPosition(
                projectilePosition,
                playerPosition,
                desiredPunchDistance);
            desiredElitePosition.y = elitePosition.y;
            if (HasWorldObstruction(elitePosition, desiredElitePosition))
            {
                return false;
            }

            float clearanceSq = clearance * clearance;
            for (int index = 0; index < enemies.Length; index++)
            {
                Entity blocker = enemies[index];
                if (blocker == elite || blocker == projectile || !EntityManager.Exists(blocker)
                    || EntityManager.HasComponent<RespawnRequest>(blocker)
                    && EntityManager.IsComponentEnabled<RespawnRequest>(blocker))
                {
                    continue;
                }

                EnemyLaunchState blockerState = EntityManager.GetComponentData<EnemyLaunchState>(blocker);
                if (blockerState.Phase == EnemyLaunchPhase.Defeated)
                {
                    continue;
                }

                float3 blockerPosition = EntityManager.GetComponentData<LocalTransform>(blocker).Position;
                if (math.distancesq(blockerPosition.xz, projectilePosition.xz) < clearanceSq
                    || DistanceSqToSegment(blockerPosition, elitePosition, desiredElitePosition) < clearanceSq)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasWorldObstruction(float3 start, float3 end)
        {
            if (math.distancesq(start, end) <= 0.0001f)
            {
                return false;
            }

            if (!SystemAPI.HasSingleton<PhysicsWorldSingleton>())
            {
                return false;
            }

            RaycastInput input = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = uint.MaxValue,
                    CollidesWith = ~(1u << 7),
                    GroupIndex = 0
                }
            };
            return SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld.CastRay(input);
        }

        public static DesiredMovement GetStagingMovement(
            float3 position,
            float3 destination,
            float moveSpeed,
            float tolerance)
        {
            float3 offset = destination - position;
            offset.y = 0f;
            float distance = math.length(offset);
            if (distance <= math.max(0f, tolerance))
            {
                return default;
            }

            return new DesiredMovement
            {
                Direction = offset / math.max(0.0001f, distance),
                Speed = math.max(0f, moveSpeed)
            };
        }

        public static float3 GetStagingSampleDirection(
            int index,
            float3 shotDirection,
            float3 perpendicular)
        {
            switch (index)
            {
                case 0: return perpendicular;
                case 1: return -perpendicular;
                case 2: return math.normalizesafe(perpendicular + shotDirection);
                case 3: return math.normalizesafe(-perpendicular + shotDirection);
                case 4: return math.normalizesafe(perpendicular - shotDirection);
                case 5: return math.normalizesafe(-perpendicular - shotDirection);
                case 6: return shotDirection;
                default: return -shotDirection;
            }
        }

        public static float DistanceSqToSegment(float3 point, float3 start, float3 end)
        {
            float2 segment = end.xz - start.xz;
            float lengthSq = math.lengthsq(segment);
            float time = lengthSq <= 0.0001f
                ? 0f
                : math.saturate(math.dot(point.xz - start.xz, segment) / lengthSq);
            return math.distancesq(point.xz, start.xz + segment * time);
        }

        private static int GetClosestEliteIndex(float3 normalPosition, NativeList<SupportElite> elites)
        {
            int closestIndex = -1;
            float closestDistanceSq = float.MaxValue;
            for (int index = 0; index < elites.Length; index++)
            {
                float distanceSq = math.distancesq(normalPosition.xz, elites[index].Position.xz);
                if (distanceSq < closestDistanceSq
                    || distanceSq == closestDistanceSq
                    && (closestIndex < 0 || elites[index].Entity.Index < elites[closestIndex].Entity.Index))
                {
                    closestIndex = index;
                    closestDistanceSq = distanceSq;
                }
            }

            return closestIndex;
        }

        private struct SupportElite
        {
            public Entity Entity;
            public float3 Position;
            public ElitePunchSettings Settings;
            public Entity SelectedProjectile;
        }

        public static bool TryGetCorridorExitDirection(
            float3 position,
            float3 shotStart,
            float3 shotEnd,
            float corridorRadius,
            out float3 exitDirection)
        {
            float2 corridor = shotEnd.xz - shotStart.xz;
            float corridorLengthSq = math.lengthsq(corridor);
            float radius = math.max(0f, corridorRadius);
            if (corridorLengthSq <= 0.0001f || radius <= 0f)
            {
                exitDirection = float3.zero;
                return false;
            }

            float projection = math.dot(position.xz - shotStart.xz, corridor) / corridorLengthSq;
            if (projection <= 0f || projection >= 1f)
            {
                exitDirection = float3.zero;
                return false;
            }

            float2 closestPoint = shotStart.xz + corridor * projection;
            float2 away = position.xz - closestPoint;
            if (math.lengthsq(away) >= radius * radius)
            {
                exitDirection = float3.zero;
                return false;
            }

            float2 perpendicular = math.normalizesafe(new float2(-corridor.y, corridor.x));
            float side = math.dot(away, perpendicular) < 0f ? -1f : 1f;
            float2 horizontalDirection = math.normalizesafe(away, perpendicular * side);
            exitDirection = new float3(horizontalDirection.x, 0f, horizontalDirection.y);
            return true;
        }
    }
}
