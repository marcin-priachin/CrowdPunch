using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Tests
{
    public sealed class EnemySpawnSpacingTests
    {
        [Test]
        public void Info006_WarningStaysReservedUntilMovingEnemyClearsMinimumDistance()
        {
            using var world = new World("Spawn warning clearance");
            var em = world.EntityManager;
            Entity pending = Enemy(em, float3.zero, 3);
            em.AddComponentData(pending, new EnemySpawnWarning { Position = float3.zero });
            em.SetEnabled(pending, false);
            Entity active = Enemy(em, new float3(2, 9, 0), 7);
            Entity pooled = Enemy(em, new float3(0, 0, 1), 3);
            em.AddComponent<RespawnRequest>(pooled);
            var system = world.GetOrCreateSystem<EnemySpawnWarningSystem>();
            Tick(world, system, 1);
            Assert.IsTrue(em.HasComponent<Disabled>(pending));
            Assert.IsTrue(em.HasComponent<EnemySpawnWarning>(pending));
            em.SetComponentData(active, LocalTransform.FromPosition(new float3(7, 0, 0)));
            Tick(world, system, 2);
            Assert.IsFalse(em.HasComponent<Disabled>(pending));
            Assert.IsFalse(em.HasComponent<EnemySpawnWarning>(pending));
        }

        [Test]
        public void Loop002_BatchesHonorSpacingAcrossSequencesAndResumeAfterSpaceClears()
        {
            using var world = new World("Wave spacing across independent sequences");
            var em = world.EntityManager;
            var physics = new PhysicsWorld(0, 0, 0);
            try
            {
                em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
                Entity prefab = em.CreateEntity(typeof(Prefab), typeof(Enemy), typeof(LocalTransform),
                    typeof(EnemyMovementSettings), typeof(NavigationAgent), typeof(Health),
                    typeof(EnemyContactDamageSettings), typeof(EnemyContactAttemptState),
                    typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances), typeof(KnockbackResponse));
                var profile = new EnemySpawnProfile { EnemyPrefab = prefab, SpawnClearance = .4f,
                    NavigationRadius = .4f, MovementSettings = new EnemyMovementSettings { SeparationDistanceMin = 3, SeparationDistanceMax = 5 } };
                Entity first = Sequence(em, profile);
                Entity second = Sequence(em, profile);
                var system = world.GetOrCreateSystem<EnemyWaveSpawnSystem>();
                Tick(world, system, 0); // Initialize both sequences.
                Tick(world, system, 1);
                Assert.AreEqual(1, Spawned(em, first) + Spawned(em, second), "Only one fits this shared bank.");
                using (var spacing = EnemySpawnSpacing.Capture(em))
                {
                    Assert.AreEqual(1, spacing.Length);
                    Assert.IsFalse(EnemySpawnSpacing.IsClear(new float3(1, 100, 0), 7, spacing));
                    em.SetComponentData(spacing[0].Entity, LocalTransform.FromPosition(new float3(20, 0, 0)));
                    var warning = em.GetComponentData<EnemySpawnWarning>(spacing[0].Entity);
                    warning.Position = new float3(20, 0, 0);
                    em.SetComponentData(spacing[0].Entity, warning);
                }
                Tick(world, system, 3);
                Assert.AreEqual(2, Spawned(em, first) + Spawned(em, second), "Pending counts retry when space becomes available.");
                Tick(world, system, 3.1);
                Assert.AreEqual(2, Spawned(em, first) + Spawned(em, second), "A successful partial batch retains its interval.");
            }
            finally { physics.Dispose(); }
        }

        [Test]
        public void Info006_MotorBrakesForWarningsButLaunchedBodiesRetainMomentum()
        {
            using var world = new World("Movement around spawn reservations");
            var em = world.EntityManager;
            em.AddComponentData(em.CreateEntity(), new ArenaBounds { Extents = new float3(50) });
            Entity pending = Enemy(em, new float3(3, 0, 0), 3);
            em.AddComponentData(pending, new EnemySpawnWarning { Position = new float3(3, 0, 0) });
            em.SetEnabled(pending, false);
            Entity active = Enemy(em, float3.zero, 3);
            em.AddComponent<RespawnRequest>(active); em.SetComponentEnabled<RespawnRequest>(active, false);
            em.AddComponentData(active, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.AddComponentData(active, new PhysicsVelocity()); em.AddComponent<PhysicsMass>(active);
            em.AddComponentData(active, new DesiredMovement { Direction = new float3(1, 0, 0), Speed = 8 });
            var movement = em.GetComponentData<EnemyMovementSettings>(active);
            movement.Acceleration = 100; movement.BrakingAcceleration = 100;
            em.SetComponentData(active, movement);
            var system = world.GetOrCreateSystem<EnemyMovementSystem>();
            Tick(world, system, 1);
            Assert.AreEqual(float3.zero, em.GetComponentData<PhysicsVelocity>(active).Linear);
            em.SetComponentData(active, new PhysicsVelocity { Linear = new float3(8, 0, 0) });
            em.SetComponentData(active, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick(world, system, 2);
            Assert.AreEqual(new float3(8, 0, 0), em.GetComponentData<PhysicsVelocity>(active).Linear);
            float2 lateral = EnemySpawnSpacing.LimitApproachVelocity(float2.zero, new float2(-8, 2), new float2(3, 0), 3, 10);
            Assert.AreEqual(new float2(-8, 2), lateral, "Retreat and tangential movement remain available.");
        }

        [Test]
        public void Loop002_AllCampaignWavesUseFiniteBatches()
        {
            var waves = AssetDatabase.FindAssets("t:EnemyWaveSettings", new[] { "Assets/CrowdPunch/Data/Campaign/Waves" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.Greater(waves.Length, 160);
            foreach (var wave in waves)
            {
                Assert.AreEqual(EnemyWaveSpawnMode.Batched, wave.SpawnMode, wave.name);
                Assert.Greater(wave.BatchInterval, 0, wave.name);
                Assert.That(wave.BatchSize, Is.InRange(1, 8), wave.name);
            }
        }

        [Test]
        public void Loop002_AllCampaignSpawnBanksRemainConnectedAndInsideRooms()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int level = 1; level <= 80; level++)
                {
                    var scene = EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
                    var arena = Object.FindFirstObjectByType<ArenaAuthoring>();
                    var navigation = Object.FindFirstObjectByType<NavigationArenaAuthoring>();
                    if (navigation == null)
                    {
                        Assert.AreEqual(0, level % 10, "Only preserved boss arenas may use physical clearance without navigation.");
                        continue;
                    }
                    var obstacles = arena.GetComponentsInChildren<SolidObstacleAuthoring>();
                    var rectangles = new NativeArray<NavigationRectangle>(obstacles.Length, Allocator.Temp);
                    for (int i = 0; i < obstacles.Length; i++)
                    {
                        float2 center = ((float3)obstacles[i].transform.position).xz;
                        float2 half = (float2)obstacles[i].Size * .5f;
                        rectangles[i] = new NavigationRectangle { Minimum = center - half, Maximum = center + half };
                    }
                    float2 origin = ((float3)arena.transform.position).xz + arena.SpacingCenterOffset.xz;
                    using var blob = NavigationGridConstruction.Build(origin - arena.SpacingSize.xz * .5f,
                        origin + arena.SpacingSize.xz * .5f, navigation.settings.cellSize,
                        (float3)navigation.settings.clearanceRadii + navigation.settings.clearanceMargin, rectangles, Allocator.Temp);
                    rectangles.Dispose();
                    blob.Value.Margin = navigation.settings.clearanceMargin;
                    var grid = new NavigationGrid { Data = blob, ParticipationAnchor = navigation.participationAnchor };
                    foreach (var crowd in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>(true)))
                    foreach (var bank in crowd.Waves.SelectMany(w => w.SpawnRectangles))
                    {
                        string context = $"Level {level}, bank {bank.Center}";
                        Assert.IsTrue(NavigationGeometry.SpawnAllowed(grid, ((float3)bank.Center).xz, .5f), context);
                        var room = crowd.GetComponent<BarricadeRoomAuthoring>();
                        if (room == null) continue;
                        Assert.GreaterOrEqual(bank.Center.x - bank.Width * .5f, room.minimum.x, context);
                        Assert.LessOrEqual(bank.Center.x + bank.Width * .5f, room.maximum.x, context);
                        Assert.GreaterOrEqual(bank.Center.z - bank.Depth * .5f, room.minimum.y, context);
                        Assert.LessOrEqual(bank.Center.z + bank.Depth * .5f, room.maximum.y, context);
                    }
                }
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Entity Enemy(EntityManager em, float3 position, float separation)
        {
            Entity enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyMovementSettings));
            em.SetComponentData(enemy, LocalTransform.FromPosition(position));
            em.SetComponentData(enemy, new EnemyMovementSettings { SeparationDistanceMin = separation });
            return enemy;
        }

        private static Entity Sequence(EntityManager em, EnemySpawnProfile profile)
        {
            Entity entity = em.CreateEntity(typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentData(entity, new EnemyWaveSequence { InitialSeed = (uint)entity.Index + 1, PlacementAttemptsPerEnemy = 48 });
            em.AddBuffer<EnemyWaveDefinition>(entity).Add(new EnemyWaveDefinition { IsValid = 1, TotalEnemyCount = 2,
                ProfileCount = 1, TotalProfileWeight = 1, RangeCount = 1, TotalRangeArea = 1,
                SpawnMode = (byte)EnemyWaveSpawnMode.Batched, BatchSize = 2, BatchInterval = 2 });
            em.AddBuffer<EnemyWaveProfile>(entity).Add(new EnemyWaveProfile { Profile = profile, Weight = 1 });
            em.AddBuffer<EnemyWaveEliteProfile>(entity);
            em.AddBuffer<EnemyWaveSpawnRange>(entity).Add(new EnemyWaveSpawnRange { Width = 1, Depth = 1, Area = 1 });
            return entity;
        }

        private static int Spawned(EntityManager em, Entity sequence) => em.GetComponentData<EnemyWaveSequence>(sequence).SpawnedCount;
        private static void Tick(World world, SystemHandle system, double now)
        {
            world.SetTime(new TimeData(now, .1f));
            system.Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}
