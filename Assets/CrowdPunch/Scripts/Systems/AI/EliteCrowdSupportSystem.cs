using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
                        // The projectile anchors the setup while the elite alone closes the gap behind it.
                        movement = default;
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
