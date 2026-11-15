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
    public partial struct SporellaPositioningSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SporellaSettings>();
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
            SystemAPI.TryGetSingleton<NavigationGrid>(out var grid);
            if (SystemAPI.TryGetSingleton<NavigationRuntimeSettings>(out var navigation) && navigation.Enabled == 0)
                grid = default;
            foreach (var (movement, intent, shooter, ranged, burst, pose, agent, entity) in
                SystemAPI.Query<RefRW<DesiredMovement>, RefRW<NavigationIntent>, RefRO<SporellaSettings>,
                    RefRO<RangedEnemySettings>, RefRO<SporellaNormalBurst>, RefRO<LocalTransform>, RefRO<NavigationAgent>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (SystemAPI.GetComponent<EnemyLaunchState>(entity).Phase != EnemyLaunchPhase.Active) continue;
                if (burst.ValueRO.ShotsRemaining > 0)
                {
                    movement.ValueRW = default;
                    intent.ValueRW = default;
                    continue;
                }
                if (SystemAPI.GetComponent<RangedAttackState>(entity).Phase != RangedAttackPhase.Ready) continue;

                var shared = ranged.ValueRO;
                var tuning = shooter.ValueRO;
                float3 position = pose.ValueRO.Position;
                float distance = math.distance(position.xz, player.Position.xz);
                if (distance > shared.EngagementRange) continue;
                float radius = math.max(.01f, shared.ProjectileRadius);
                float y = SporellaShotGeometry.Height(ground, radius, tuning);
                float3 target = player.Position;
                var sweep = SporellaShotGeometry.Aim(position, target, math.forward(pose.ValueRO.Rotation), y, tuning);
                if (SporellaShotGeometry.HasClearShot(world, enemies, sweep, tuning.Range, radius, ground))
                    continue;
                float3 destination = FindAngle(world, enemies, grid, position, target, y, radius,
                    agent.ValueRO.Radius, ground, tuning, shared);
                float speed = math.max(0f, shared.ApproachSpeed);
                movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(destination - position), Speed = speed };
                intent.ValueRW = NavigationIntent.Travel(destination, speed, .35f, float3.zero);
            }
        }

        private static float3 FindAngle(in CollisionWorld world, ComponentLookup<Enemy> enemies, NavigationGrid nav,
            float3 position, float3 target, float y, float radius, float agentRadius, float ground,
            in SporellaSettings tuning, in RangedEnemySettings ranged)
        {
            float2 away = math.normalizesafe(position.xz - target.xz, new float2(0f, 1f));
            float heading = math.atan2(away.y, away.x);
            float min = math.max(2f, math.min(ranged.PreferredMinimumDistance, ranged.PreferredMaximumDistance));
            float max = math.max(min, math.max(ranged.PreferredMinimumDistance, ranged.PreferredMaximumDistance));
            float spacing = math.clamp(math.distance(position.xz, target.xz), min, max);
            int clearance = -1, region = 0;
            if (nav.Data.IsCreated)
            {
                ref var grid = ref nav.Data.Value;
                clearance = NavigationGeometry.ClearanceClass(ref grid, agentRadius);
                region = NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, position.xz, clearance), clearance);
            }
            for (int ring = 0; ring < 2; ring++)
            for (int probe = 1; probe < 16; probe++)
            {
                float angle = heading + (probe % 2 == 1 ? 1 : -1) * ((probe + 1) / 2) * (math.PI / 8f);
                float2 point = target.xz + new float2(math.cos(angle), math.sin(angle))
                    * (ring == 0 ? spacing : math.max(2f, min * .7f));
                float3 candidate = new float3(point.x, position.y, point.y);
                if (region > 0)
                {
                    ref var grid = ref nav.Data.Value;
                    if (NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, point, clearance), clearance) != region)
                        continue;
                }
                else if (SporellaShotGeometry.FindCover(world, enemies, new float3(position.x, y, position.z),
                    new float3(point.x, y, point.y), agentRadius, ground, out _)) continue;
                var sweep = SporellaShotGeometry.Aim(candidate, target, math.forward(), y, tuning);
                if (SporellaShotGeometry.HasClearShot(world, enemies, sweep, tuning.Range, radius, ground))
                    return candidate;
            }
            return new float3(target.x, position.y, target.z);
        }
    }
}
