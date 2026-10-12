using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Initialization
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    [UpdateAfter(typeof(GroundHazardCycleSystem))]
    [UpdateBefore(typeof(EnemyWaveSpawnSystem))]
    public partial struct EnemySpawnWarningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<EnemySpawnWarning>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            // Activation tests physical occupants. Append each activation immediately so
            // legacy authored reservations that overlap cannot mutually wait forever.
            using var spacing = EnemySpawnSpacing.Capture(state.EntityManager, false);
            using var hazards = GroundHazardSpawnClearance.Capture(state.EntityManager);
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            foreach (var (warning, enemy) in SystemAPI.Query<RefRW<EnemySpawnWarning>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnemyWaveOwnership>(enemy))
                {
                    var owner = SystemAPI.GetComponent<EnemyWaveOwnership>(enemy);
                    if (!SystemAPI.HasComponent<EnemyWaveSequence>(owner.Sequence)
                        || SystemAPI.GetComponent<EnemyWaveSequence>(owner.Sequence).RunGeneration != owner.RunGeneration)
                    {
                        commands.DestroyEntity(enemy);
                        continue;
                    }
                }
                warning.ValueRW.SecondsRemaining -= SystemAPI.Time.DeltaTime;
                if (warning.ValueRO.SecondsRemaining > 0) continue;
                var w = warning.ValueRO;
                float radius = SystemAPI.HasComponent<NavigationAgent>(enemy)
                    ? SystemAPI.GetComponent<NavigationAgent>(enemy).Radius : w.Radius;
                // GROUND-006: a patch may become dangerous during the warning. Keep the
                // reservation and marker at the advertised point until it is safe again.
                if (!GroundHazardGeometry.Clear(hazards.AsArray(), w.Position.xz, w.Position.xz, radius)) continue;
                if (player.IsAvailable && math.distancesq(player.Position.xz, w.Position.xz)
                    < math.square(player.Radius + radius)) continue;
                if (SystemAPI.HasSingleton<ArenaGroundPlane>() && SystemAPI.HasSingleton<PhysicsWorldSingleton>()
                    && !EnemyGroundSpawnClearance.IsClear(SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                        w.Position, radius, SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight)) continue;
                if (SystemAPI.HasComponent<EnemyMovementSettings>(enemy)
                    && !EnemySpawnSpacing.IsClear(w.Position,
                        EnemySpawnSpacing.Minimum(SystemAPI.GetComponent<EnemyMovementSettings>(enemy)), spacing, enemy)) continue;
                commands.SetEnabled(enemy, true);
                commands.RemoveComponent<EnemySpawnWarning>(enemy);
                spacing.Add(new EnemySpawnSpacing.Occupant { Entity = enemy, Position = w.Position,
                    MinimumDistance = SystemAPI.HasComponent<EnemyMovementSettings>(enemy)
                        ? EnemySpawnSpacing.Minimum(SystemAPI.GetComponent<EnemyMovementSettings>(enemy)) : 0f });
            }
            commands.Playback(state.EntityManager);
        }
    }
}
