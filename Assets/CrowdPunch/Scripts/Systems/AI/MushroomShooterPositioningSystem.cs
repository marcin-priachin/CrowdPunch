using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(RangedEnemyPositioningSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    [UpdateBefore(typeof(Movement.EnemyMovementSystem))]
    public partial struct MushroomShooterPositioningSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MushroomShooterSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<ArenaGroundPlane>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            float ground = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var sources = SystemAPI.GetComponentLookup<MushroomShotSource>(true);
            SystemAPI.TryGetSingleton<NavigationGrid>(out var grid);
            if (SystemAPI.TryGetSingleton<NavigationRuntimeSettings>(out var navigation) && navigation.Enabled == 0)
                grid = default;
            foreach (var (movement, intent, shooter, ranged, attack, pose, agent, entity) in SystemAPI
                .Query<RefRW<DesiredMovement>, RefRW<NavigationIntent>, RefRO<MushroomShooterSettings>,
                    RefRO<RangedEnemySettings>, RefRO<RangedAttackState>, RefRO<LocalTransform>, RefRO<NavigationAgent>>()
                .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (SystemAPI.GetComponent<EnemyLaunchState>(entity).Phase != EnemyLaunchPhase.Active) continue;
                if (attack.ValueRO.Phase == RangedAttackPhase.WindUp)
                {
                    movement.ValueRW = default;
                    intent.ValueRW = default;
                    continue;
                }
                float radius = math.max(.01f, ranged.ValueRO.ProjectileRadius);
                float shotY = ground + math.max(radius + .02f, shooter.ValueRO.Height);
                float3 direction = MushroomShotGeometry.Aim(pose.ValueRO, player.Position, shotY,
                    sources.TryGetComponent(entity, out var source), source, out float3 origin);
                float distance = math.max(0f, math.dot(player.Position - origin, direction));
                if (!MushroomShotGeometry.FindCover(world, enemies, origin, direction, distance, radius, ground, out _)) continue;
                float minimum = math.max(0f, math.min(ranged.ValueRO.PreferredMinimumDistance, ranged.ValueRO.PreferredMaximumDistance));
                float maximum = math.max(minimum, math.max(ranged.ValueRO.PreferredMinimumDistance, ranged.ValueRO.PreferredMaximumDistance));
                float3 destination = FindFiringAngle(world, enemies, grid, pose.ValueRO.Position, player.Position.xz,
                    minimum, maximum, origin.y, radius, agent.ValueRO.Radius, ground);
                float speed = math.max(0f, ranged.ValueRO.ApproachSpeed);
                movement.ValueRW = new DesiredMovement
                {
                    Direction = math.normalizesafe(destination - pose.ValueRO.Position), Speed = speed
                };
                intent.ValueRW = NavigationIntent.Travel(destination, speed, .35f, float3.zero);
            }
        }

        private static bool Blocked(in CollisionWorld world, ComponentLookup<Enemy> enemies, float2 origin,
            float2 target, float y, float radius, float ground)
        {
            float2 delta = target - origin;
            float distance = math.length(delta);
            if (distance <= .001f) return false;
            var direction = delta / distance;
            return MushroomShotGeometry.FindCover(world, enemies, new float3(origin.x, y, origin.y),
                new float3(direction.x, 0f, direction.y), distance, radius, ground, out _);
        }

        private static float3 FindFiringAngle(in CollisionWorld world, ComponentLookup<Enemy> enemies, NavigationGrid nav,
            float3 position, float2 player, float minimum, float maximum, float y, float radius, float agentRadius, float ground)
        {
            // Probe both sides on the closest preferred spacing, then try a shorter range.
            float2 away = math.normalizesafe(position.xz - player, new float2(0f, 1f));
            float heading = math.atan2(away.y, away.x);
            float spacing = math.max(2f, math.clamp(math.distance(position.xz, player), minimum, maximum));
            int clearance = -1, region = 0;
            if (nav.Data.IsCreated)
            {
                ref var grid = ref nav.Data.Value;
                clearance = NavigationGeometry.ClearanceClass(ref grid, agentRadius);
                region = NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, position.xz, clearance), clearance);
            }
            for (int ring = 0; ring < 2; ring++)
            for (int probe = 1; probe <= 16; probe++)
            {
                float angle = heading + (probe % 2 == 1 ? 1 : -1) * ((probe + 1) / 2) * (math.PI / 8f);
                float2 candidate = player + new float2(math.cos(angle), math.sin(angle))
                    * (ring == 0 ? spacing : math.max(2f, minimum * .7f));
                if (region > 0)
                {
                    ref var grid = ref nav.Data.Value;
                    if (NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, candidate, clearance), clearance) != region)
                        continue;
                }
                else if (Blocked(world, enemies, position.xz, candidate, y, agentRadius, ground)) continue;
                if (!Blocked(world, enemies, candidate, player, y, radius, ground)) return new float3(candidate.x, position.y, candidate.y);
            }
            return new float3(player.x, position.y, player.y);
        }
    }
}
