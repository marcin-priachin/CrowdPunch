using CrowdPunch.Components;
using CrowdPunch.Utilities;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>Resets ECS-owned gameplay state without reloading scenes or SubScenes.</summary>
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial class GameRestartSystem : SystemBase
    {
        private uint lastRestartSequence;

        protected override void OnCreate()
        {
            RequireForUpdate<MatchState>();
            lastRestartSequence = GameRestartRegistry.Sequence;
        }

        protected override void OnUpdate()
        {
            uint restartSequence = GameRestartRegistry.Sequence;
            if (restartSequence == lastRestartSequence)
            {
                return;
            }

            lastRestartSequence = restartSequence;
            // Fired shots are independent of their shooter and are not owned by a SubScene.
            EntityQuery oldProjectiles = SystemAPI.QueryBuilder().WithAll<RangedProjectile>().Build();
            using (NativeArray<Entity> projectileRoots = oldProjectiles.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity projectileRoot in projectileRoots)
                    if (EntityManager.Exists(projectileRoot)) EntityManager.DestroyEntity(projectileRoot);
            }
            ResetWaveSequences();
            BossEncounterReset.Reset(EntityManager);
            foreach (var rebounds in SystemAPI.Query<DynamicBuffer<BarricadeRebound>>()) rebounds.Clear();
            foreach (var (wall, collider, history) in SystemAPI.Query<RefRW<Barricade>, RefRW<PhysicsCollider>, DynamicBuffer<BarricadeHitHistory>>())
            {
                wall.ValueRW.HitsRemaining = wall.ValueRO.RequiredHits;
                wall.ValueRW.HitSequence = 0;
                wall.ValueRW.LastHitTime = 0;
                collider.ValueRW.Value = wall.ValueRO.IntactCollider;
                history.Clear();
            }
            EntityQuery oldWaveEnemies = SystemAPI.QueryBuilder().WithAll<EnemyWaveOwnership>().Build();
            using (NativeArray<Entity> waveEnemyRoots = oldWaveEnemies.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity waveEnemyRoot in waveEnemyRoots)
                {
                    // Destroying roots individually lets Entities include every prefab-linked child in its
                    // LinkedEntityGroup. Query destruction rejects those children because ownership belongs
                    // only to the spawned root entity.
                    if (EntityManager.Exists(waveEnemyRoot))
                    {
                        EntityManager.DestroyEntity(waveEnemyRoot);
                    }
                }
            }
            Random random = Random.CreateFromIndex(1);
            NavigationGrid navigationGrid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;

            foreach ((RefRW<LocalTransform> transform,
                         RefRW<Health> health,
                         RefRW<HealthBar> healthBar,
                         RefRW<EnemyDamageState> damageState,
                         Entity enemy) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<Health>, RefRW<HealthBar>, RefRW<EnemyDamageState>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<AuthoredEnemyInitialPosition>(enemy))
                {
                    float3 position = SystemAPI.GetComponent<AuthoredEnemyInitialPosition>(enemy).Value;
                    transform.ValueRW = LocalTransform.FromPosition(position);
                }
                else if (SystemAPI.HasComponent<RandomEnemySpawnRegion>(enemy))
                {
                    RandomEnemySpawnRegion region = SystemAPI.GetComponent<RandomEnemySpawnRegion>(enemy);
                    float radius = SystemAPI.HasComponent<NavigationAgent>(enemy) ? SystemAPI.GetComponent<NavigationAgent>(enemy).Radius : 0;
                    float3 position = transform.ValueRO.Position; bool found = false;
                    for (int attempt = 0; attempt < 32; attempt++)
                    {
                        position = GetRandomSpawnPosition(ref random, region.Center, region.Radius);
                        if (NavigationGeometry.SpawnAllowed(navigationGrid, position.xz, radius)) { found = true; break; }
                    }
                    if (!found)
                    {
                        var navigation = SystemAPI.GetComponent<NavigationPathState>(enemy); navigation.Reset(); SystemAPI.SetComponent(enemy, navigation);
                        SystemAPI.GetBuffer<NavigationWaypoint>(enemy).Clear();
                        SystemAPI.SetComponent(enemy, new RespawnRequest());
                        SystemAPI.SetComponentEnabled<RespawnRequest>(enemy, true);
                        continue;
                    }
                    transform.ValueRW = LocalTransform.FromPosition(position);
                }

                health.ValueRW.Current = health.ValueRO.Max;
                healthBar.ValueRW.Normalized = health.ValueRO.Normalized;
                damageState.ValueRW = default;
                SystemAPI.SetComponent(enemy, new EnemyGroundConstraint());
                if (SystemAPI.HasComponent<NavigationPathState>(enemy))
                {
                    var navigation = SystemAPI.GetComponent<NavigationPathState>(enemy); navigation.Reset();
                    SystemAPI.SetComponent(enemy, navigation); SystemAPI.SetComponent(enemy, new NavigationIntent());
                    SystemAPI.GetBuffer<NavigationWaypoint>(enemy).Clear();
                }
                SystemAPI.SetComponent(enemy, new DesiredMovement());
                SystemAPI.SetComponent(enemy, new WanderDestination());
                SystemAPI.SetComponent(enemy, new PunchAimAssistTarget());
                if (SystemAPI.HasComponent<EnemyContactAttemptState>(enemy))
                {
                    SystemAPI.SetComponent(enemy, new EnemyContactAttemptState());
                }
                SystemAPI.GetBuffer<CollisionDamageHistory>(enemy).Clear();
                    if (SystemAPI.HasComponent<EnemyArmor>(enemy))
                    {
                        SystemAPI.SetComponent(enemy, EnemyArmor.Fresh);
                        SystemAPI.GetBuffer<ArmorHitHistory>(enemy).Clear();
                    }

                if (SystemAPI.HasComponent<EnemyImpactFeedback>(enemy))
                    SystemAPI.SetComponent(enemy, new EnemyImpactFeedback());
                if (SystemAPI.HasComponent<EnemyLaunchState>(enemy))
                {
                    SystemAPI.SetComponent(enemy, new EnemyLaunchState
                    {
                        Phase = EnemyLaunchPhase.Active
                    });
                }

                if (SystemAPI.HasComponent<PhysicsVelocity>(enemy))
                {
                    SystemAPI.SetComponent(enemy, new PhysicsVelocity());
                }

                ResetArchetypeState(enemy);
                ResetTransientState(enemy);
            }
            // Reset may run later than the request while the new SubScene's MatchState loads.
            World.GetExistingSystemManaged<GamePrePhysicsGroup>()?.RequestPhysicsRebuild();
        }

        private void ResetWaveSequences()
        {
            foreach ((RefRW<EnemyWaveSequence> sequence, Entity entity) in
                     SystemAPI.Query<RefRW<EnemyWaveSequence>>().WithEntityAccess())
            {
                sequence.ValueRW.RunGeneration++;
                if (sequence.ValueRW.RunGeneration == 0) sequence.ValueRW.RunGeneration = 1;
                sequence.ValueRW.RandomState = sequence.ValueRO.InitialSeed == 0 ? 1u : sequence.ValueRO.InitialSeed;
                sequence.ValueRW.CurrentWaveIndex = 0;
                sequence.ValueRW.SpawnedCount = 0;
                sequence.ValueRW.DefeatedCount = 0;
                sequence.ValueRW.AmmunitionSpawnedCount = 0;
                sequence.ValueRW.NextAmmunitionCheckAt = 0;
                sequence.ValueRW.UndefeatedCount = 0;
                sequence.ValueRW.EliteSpawnedCount = 0;
                sequence.ValueRW.EliteProfileCursor = 0;
                sequence.ValueRW.EliteProfileSpawnedInEntry = 0;
                sequence.ValueRW.NormalMinimumProfileCursor = 0;
                sequence.ValueRW.NormalMinimumRound = 0;
                sequence.ValueRW.NormalMinimumSpawnedCount = 0;
                sequence.ValueRW.NextActionAt = 0d;
                sequence.ValueRW.NextPlacementWarningAt = 0d;
                sequence.ValueRW.Phase = EnemyWaveRuntimePhase.PreWaveDelay;
                sequence.ValueRW.Initialized = 0;
                SystemAPI.SetComponentEnabled<EnemyWaveEncounterComplete>(entity, false);
            }
        }

        private void ResetArchetypeState(Entity enemy)
        {
            if (SystemAPI.HasComponent<RangedAttackState>(enemy))
            {
                RangedEnemySettings settings = SystemAPI.GetComponent<RangedEnemySettings>(enemy);
                SystemAPI.SetComponent(enemy, new RangedAttackState
                {
                    Phase = RangedAttackPhase.InitialDelay,
                    SecondsRemaining = math.max(0f, settings.InitialAttackDelay)
                        + GetRangedInitialDelayVariation(enemy, settings.InitialDelayVariation)
                });
                SystemAPI.SetComponent(enemy, new RangedPositioningState
                {
                    Mode = RangedPositioningMode.Hold
                });
            }

            if (SystemAPI.HasComponent<ExplosiveEnemyState>(enemy))
            {
                SystemAPI.SetComponent(enemy, new ExplosiveEnemyState());
                SystemAPI.SetComponentEnabled<ExplosiveDetonationRequest>(enemy, false);
            }

            if (SystemAPI.HasComponent<DasherState>(enemy))
            {
                SystemAPI.SetComponent(enemy, new DasherState
                {
                    Phase = DasherPhase.Positioning
                });
                SystemAPI.GetBuffer<DasherHitHistory>(enemy).Clear();
            }
            if (SystemAPI.HasComponent<ElitePunchState>(enemy))
            {
                ElitePunchSettings settings = SystemAPI.GetComponent<ElitePunchSettings>(enemy);
                SystemAPI.SetComponent(enemy, new ElitePunchState
                {
                    Phase = ElitePunchPhase.InitialDelay,
                    SecondsRemaining = math.max(0f, settings.InitialDelay),
                    RandomState = (uint)math.max(1, enemy.Index + 1)
                });
            }
            if (SystemAPI.HasComponent<ElitePunchReservation>(enemy))
                SystemAPI.SetComponent(enemy, new ElitePunchReservation());
        }

        private void ResetTransientState(Entity enemy)
        {
            if (SystemAPI.HasComponent<DamageRequest>(enemy))
            {
                SystemAPI.SetComponent(enemy, new DamageRequest());
                SystemAPI.SetComponentEnabled<DamageRequest>(enemy, false);
            }

            if (SystemAPI.HasComponent<DeathRequest>(enemy))
            {
                SystemAPI.SetComponentEnabled<DeathRequest>(enemy, false);
            }

            if (SystemAPI.HasComponent<ExternalImpulse>(enemy))
            {
                SystemAPI.SetComponent(enemy, new ExternalImpulse());
                SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, false);
            }

            if (SystemAPI.HasComponent<EnemyHealthBarVisibility>(enemy))
            {
                SystemAPI.SetComponent(enemy, new EnemyHealthBarVisibility());
                SystemAPI.SetComponentEnabled<EnemyHealthBarVisibility>(enemy, false);
            }

            if (SystemAPI.HasComponent<KnockbackRecovery>(enemy))
            {
                SystemAPI.SetComponent(enemy, new KnockbackRecovery());
                SystemAPI.SetComponentEnabled<KnockbackRecovery>(enemy, false);
            }

            if (SystemAPI.HasComponent<RespawnRequest>(enemy))
            {
                SystemAPI.SetComponent(enemy, new RespawnRequest());
                SystemAPI.SetComponentEnabled<RespawnRequest>(enemy, false);
            }
        }

        private static float GetRangedInitialDelayVariation(Entity enemy, float maximumVariation)
        {
            uint seed = (uint)math.max(1, enemy.Index + 1) * 747796405u;
            float normalized = (seed & 0x00ffffffu) / 16777216f;
            return normalized * math.max(0f, maximumVariation);
        }

        private static float3 GetRandomSpawnPosition(ref Random random, float3 center, float radius)
        {
            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = math.sqrt(random.NextFloat()) * math.max(0f, radius);
            float x = math.cos(angle) * distance;
            float z = math.sin(angle) * distance;
            return center + new float3(x, 0f, z);
        }
    }
}
