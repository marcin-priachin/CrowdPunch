using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

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
            var pairManager = state.EntityManager;
            bool hasFloor = SystemAPI.HasSingleton<ArenaGroundPlane>();
            bool pairHasGround = hasFloor && SystemAPI.HasSingleton<PhysicsWorldSingleton>();
            float pairFloor = hasFloor ? SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight : 0;
            var pairWorld = pairHasGround ? SystemAPI.GetSingleton<PhysicsWorldSingleton>() : default;
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            // LINK-007: reserve and activate a pair atomically, including when one endpoint is obstructed.
            foreach (var pair in SystemAPI.Query<RefRW<LinkedPair>>())
            {
                var p = pair.ValueRO;
                if (!SystemAPI.HasComponent<EnemySpawnWarning>(p.A) || !SystemAPI.HasComponent<EnemySpawnWarning>(p.B)) continue;
                var a = SystemAPI.GetComponent<EnemySpawnWarning>(p.A); var b = SystemAPI.GetComponent<EnemySpawnWarning>(p.B);
                a.SecondsRemaining -= SystemAPI.Time.DeltaTime; b.SecondsRemaining -= SystemAPI.Time.DeltaTime;
                SystemAPI.SetComponent(p.A, a); SystemAPI.SetComponent(p.B, b);
                if (a.SecondsRemaining > 0 || b.SecondsRemaining > 0 || !PairClear(p.A, a) || !PairClear(p.B, b)) continue;
                float spawnDuration = math.max(Activate(p.A, a), Activate(p.B, b));
                if (spawnDuration > 0)
                {
                    // Ordinary recovery skips intact pairs; their shared clock must own Spawn too.
                    p.Phase = EnemyLaunchPhase.Recovering;
                    p.RecoveryRemaining = spawnDuration;
                    pair.ValueRW = p;
                }
                Reserve(p.A, a); Reserve(p.B, b);
            }
            foreach (var (warning, enemy) in SystemAPI.Query<RefRW<EnemySpawnWarning>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                if (SystemAPI.HasComponent<LinkedPairEndpoint>(enemy)
                    && SystemAPI.HasComponent<LinkedPair>(SystemAPI.GetComponent<LinkedPairEndpoint>(enemy).Pair)) continue;
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
                Activate(enemy, w);
                spacing.Add(new EnemySpawnSpacing.Occupant { Entity = enemy, Position = w.Position,
                    MinimumDistance = SystemAPI.HasComponent<EnemyMovementSettings>(enemy)
                        ? EnemySpawnSpacing.Minimum(SystemAPI.GetComponent<EnemyMovementSettings>(enemy)) : 0f });
            }
            commands.Playback(state.EntityManager);

            float Activate(Entity enemy, EnemySpawnWarning warning)
            {
                commands.SetEnabled(enemy, true);
                commands.RemoveComponent<EnemySpawnWarning>(enemy);
                var em = pairManager;
                if (!hasFloor || !em.HasComponent<EnemySpawnAnimation>(enemy)
                    || !em.HasComponent<PhysicsCollider>(enemy) || !em.HasComponent<EnemyLaunchState>(enemy)) return 0;
                var spawn = em.GetComponentData<EnemySpawnAnimation>(enemy);
                var collider = em.GetComponentData<PhysicsCollider>(enemy);
                if (spawn.UseAnimation == 0 || spawn.Duration <= 0f || !collider.IsValid) return 0;
                var transform = em.GetComponentData<LocalTransform>(enemy);
                var bounds = collider.Value.Value.CalculateAabb(new RigidTransform(transform.Rotation, transform.Position), transform.Scale);
                float height = pairFloor + transform.Position.y - bounds.Min.y;
                transform.Position = new float3(warning.Position.x, height, warning.Position.z);
                commands.SetComponent(enemy, transform);
                commands.SetComponent(enemy, new EnemyGroundConstraint { Height = height, HasGround = 1, IsLocked = 1 });
                commands.SetComponent(enemy, new PhysicsVelocity());
                commands.SetComponent(enemy, new DesiredMovement());
                var launch = em.GetComponentData<EnemyLaunchState>(enemy);
                launch.Phase = EnemyLaunchPhase.Recovering;
                launch.RecoverySecondsRemaining = spawn.Duration;
                commands.SetComponent(enemy, launch);
                commands.SetComponentEnabled<EnemySpawnAnimation>(enemy, true);
                return spawn.Duration;
            }

            bool PairClear(Entity enemy, EnemySpawnWarning warning)
            {
                var em = pairManager;
                if (em.HasComponent<EnemyWaveOwnership>(enemy)) {
                    var owner = em.GetComponentData<EnemyWaveOwnership>(enemy);
                    if (!em.HasComponent<EnemyWaveSequence>(owner.Sequence) || em.GetComponentData<EnemyWaveSequence>(owner.Sequence).RunGeneration != owner.RunGeneration) return false;
                }
                float radius = em.HasComponent<NavigationAgent>(enemy) ? em.GetComponentData<NavigationAgent>(enemy).Radius : warning.Radius;
                if (!GroundHazardGeometry.Clear(hazards.AsArray(), warning.Position.xz, warning.Position.xz, radius)) return false;
                if (player.IsAvailable && math.distancesq(player.Position.xz, warning.Position.xz) < math.square(player.Radius + radius)) return false;
                if (pairHasGround && !EnemyGroundSpawnClearance.IsClear(pairWorld.CollisionWorld, warning.Position, radius, pairFloor)) return false;
                return !em.HasComponent<EnemyMovementSettings>(enemy) || EnemySpawnSpacing.IsClear(warning.Position,
                    EnemySpawnSpacing.Minimum(em.GetComponentData<EnemyMovementSettings>(enemy)), spacing, enemy);
            }
            void Reserve(Entity enemy, EnemySpawnWarning warning) => spacing.Add(new EnemySpawnSpacing.Occupant {
                Entity = enemy, Position = warning.Position,
                MinimumDistance = pairManager.HasComponent<EnemyMovementSettings>(enemy)
                    ? EnemySpawnSpacing.Minimum(pairManager.GetComponentData<EnemyMovementSettings>(enemy)) : 0 });
        }
    }
}
