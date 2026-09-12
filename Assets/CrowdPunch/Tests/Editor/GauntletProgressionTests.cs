using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaveSequence = CrowdPunch.Components.EnemyWaveSequence;
using EnemyArchetype = CrowdPunch.Configuration.EnemyArchetype;

namespace CrowdPunch.Tests
{
    public sealed class GauntletProgressionTests
    {
        private const string Root = "Assets/CrowdPunch/Scenes/";
        private SceneSetup[] sceneSetup;

        [SetUp]
        public void IsolateSceneInspection()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Save open scenes before running scene validation");
            sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void RestoreScenes() { if (sceneSetup != null) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup); }

        [Test]
        public void Loop002_ActiveSequenceAndBuildSettingsContainExactlyTenOrderedLevels()
        {
            Scene scene = EditorSceneManager.OpenScene(Root + "Bootstrap.unity", OpenSceneMode.Additive);
            try
            {
                var sequence = Find<GauntletSequence>(scene);
                var names = new SerializedObject(sequence).FindProperty("levelSceneNames");
                Assert.That(names.arraySize, Is.EqualTo(10));
                string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                Assert.That(enabled.Length, Is.EqualTo(11));
                Assert.That(enabled[0], Is.EqualTo(Root + "Bootstrap.unity"));
                for (int i = 0; i < 10; i++)
                {
                    string id = $"Gauntlet_{i + 1:00}";
                    Assert.That(names.GetArrayElementAtIndex(i).stringValue, Is.EqualTo(id));
                    Assert.That(enabled[i + 1], Is.EqualTo(Root + "Gauntlets/" + id + ".unity"));
                    Assert.That(sequence.GetLevelName(i), Does.StartWith($"{i + 1:00} "));
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void Combat017_LevelReferencesBoundsAndSpawnRegionsAreValid(int number)
        {
            string id = $"Gauntlet_{number:00}";
            Scene main = EditorSceneManager.OpenScene(Root + "Gauntlets/" + id + ".unity", OpenSceneMode.Additive);
            Scene sub = default;
            try
            {
                var marker = Find<GauntletLevel>(main);
                var reference = Find<SubScene>(main);
                Assert.That(reference.AutoLoadScene, Is.True);
                Assert.That(reference.SceneAsset, Is.Not.Null);
                Assert.That(reference.SceneGUID.ToString(), Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference.SceneAsset))));
                sub = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(reference.SceneAsset), OpenSceneMode.Additive);
                var arena = Find<ArenaAuthoring>(sub);
                var authoring = Find<EnemyWaveSequenceAuthoring>(sub);
                var floor = Find<MeshCollider>(sub);
                Assert.That(floor.sharedMesh, Is.Not.Null);
                Assert.That(floor.convex, Is.True,
                    "VISION-004 / COMBAT-018: floor collision must not expose internal triangle edges");
                Vector3[] vertices = floor.sharedMesh.vertices;
                Vector2[] outline = vertices.Take(vertices.Length / 2).Select(v => new Vector2(v.x, v.z)).ToArray();
                AssertInside(outline, marker.PlayerEntryPoint.position, 0.6f);
                Assert.That(marker.PlayerEntryPoint.position.y, Is.EqualTo(0.5f));
                foreach (BoxCollider rail in sub.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>()))
                    Assert.That(rail.bounds.max.y, Is.GreaterThan(marker.PlayerEntryPoint.position.y + 1.02f), "Perimeter must intercept the elevated player sphere cast");
                Assert.That(arena.DefeatSize.x, Is.GreaterThan(arena.SpacingSize.x));
                Assert.That(arena.DefeatSize.z, Is.GreaterThan(arena.SpacingSize.z));
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    AssertInside(outline, arena.transform.position + new Vector3(x * arena.SpacingSize.x / 2, 0, z * arena.SpacingSize.z / 2), 0.75f);
                Assert.That(Find<GameSettingsAuthoring>(sub).Settings, Is.SameAs(
                    AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>("Assets/CrowdPunch/Data/Settings/GameRuntimeSettings.asset")));
                Assert.That(authoring.Waves.Count, Is.GreaterThan(0));
                Assert.That(authoring.Waves.Last().ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
                int outstandingBudget = 0;
                foreach (EnemyWaveSettings wave in authoring.Waves)
                {
                    Assert.That(wave, Is.Not.Null);
                    Assert.That(wave.Enemies.Sum(e => e.MinimumCount), Is.EqualTo(wave.TotalEnemyCount), wave.name);
                    Assert.That(wave.Enemies.All(e => e.Weight == 0), Is.True, "No random excess special threats");
                    foreach (var entry in wave.Enemies)
                    {
                        Assert.That(entry.Settings, Is.Not.Null);
                        Assert.That(entry.Settings.EnemyPrefab, Is.Not.Null);
                        Assert.That(entry.Settings.Archetype, Is.Not.EqualTo(EnemyArchetype.Elite));
                        Assert.That(AssetDatabase.GetAssetPath(entry.Settings), Does.StartWith("Assets/CrowdPunch/Data/Settings/Enemies/"));
                    }
                    foreach (var elite in wave.EliteEnemies)
                    {
                        Assert.That(elite.Settings.Archetype, Is.EqualTo(EnemyArchetype.Elite));
                        Assert.That(elite.Settings.EnemyPrefab, Is.Not.Null);
                        Assert.That(elite.Count, Is.EqualTo(1));
                        Assert.That(wave.ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated), "Replenishment must drain before advancing");
                    }
                    Assert.That(wave.SpawnRectangles.Count, Is.GreaterThan(0));
                    foreach (var range in wave.SpawnRectangles)
                    {
                        Assert.That(range.Width, Is.GreaterThan(0));
                        Assert.That(range.Depth, Is.GreaterThan(0));
                        Assert.That(range.Center.y, Is.EqualTo(2f));
                        float avoidance = authoring.MinimumPlayerDistance + 0.5f + (wave.EliteEnemies.Count > 0 ? 1.061f : 0.708f);
                        Assert.That(new Vector2(range.Width, range.Depth).magnitude / 2, Is.GreaterThan(avoidance), "One camping player must not exclude a whole rectangle");
                        for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                            AssertInside(outline, range.Center + new Vector3(x * range.Width / 2, 0, z * range.Depth / 2), 1.1f);
                    }
                    outstandingBudget += wave.TotalEnemyCount + wave.EliteEnemies.Sum(e => e.Count);
                    Assert.That(outstandingBudget, Is.LessThanOrEqualTo(30), "Bounded authored peak, including timed overlap");
                    if (wave.ActivationMode == EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated) outstandingBudget = 0;
                }
                foreach (GameObject root in sub.GetRootGameObjects())
                    foreach (Component component in root.GetComponentsInChildren<Component>(true)) Assert.That(component, Is.Not.Null, "Missing script");
            }
            finally
            {
                if (sub.IsValid()) EditorSceneManager.CloseScene(sub, true);
                EditorSceneManager.CloseScene(main, true);
            }
        }

        [Test]
        public void Enemy011_PooledDefeatedEliteCannotRestartReplenishment()
        {
            using var world = new World("Elite wave bookkeeping test");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<EliteWaveReplenishmentSystem>();
            Entity sequence = em.CreateEntity();
            Entity elite = em.CreateEntity(typeof(EnemyWaveOwnership), typeof(EnemyTier), typeof(EnemyLaunchState));
            em.SetComponentData(elite, new EnemyTier { Value = EnemyCombatTier.Elite });
            em.SetComponentData(elite, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            em.SetComponentData(elite, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Entity normal = em.CreateEntity(typeof(EnemyWaveOwnership), typeof(EnemyRespawnSettings), typeof(EliteWaveReplenishment));
            em.SetComponentData(normal, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(normal).Enabled, Is.EqualTo(1));
            // Real pooling restores Active for future reuse, but elite defeat is terminal.
            em.SetComponentData(elite, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1, DefeatCounted = 1 });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(normal).Enabled, Is.Zero);
        }

        [Test]
        public void Loop006_RestartDestroysShotsAndLinkedBodiesAndResetsEncounter()
        {
            using var world = new World("Gauntlet restart test");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<GameRestartSystem>();
            em.CreateEntity(typeof(MatchState));
            Entity sequence = em.CreateEntity(typeof(WaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentData(sequence, new WaveSequence { InitialSeed = 123, RunGeneration = 7,
                Initialized = 1, SpawnedCount = 28, UndefeatedCount = 12, NextActionAt = 1000,
                Phase = EnemyWaveRuntimePhase.Complete });
            Entity shot = em.CreateEntity(typeof(RangedProjectile));
            Entity child = em.CreateEntity();
            var linked = em.AddBuffer<LinkedEntityGroup>(shot);
            linked.Add(new LinkedEntityGroup { Value = shot });
            linked.Add(new LinkedEntityGroup { Value = child });
            Entity enemy = em.CreateEntity(typeof(EnemyWaveOwnership));
            GameRestartRegistry.RequestRestart();
            system.Update();
            Assert.That(em.Exists(shot) || em.Exists(child) || em.Exists(enemy), Is.False);
            WaveSequence state = em.GetComponentData<WaveSequence>(sequence);
            Assert.That(state.RunGeneration, Is.EqualTo(8));
            Assert.That(state.RandomState, Is.EqualTo(123));
            Assert.That(state.Initialized + state.SpawnedCount + state.UndefeatedCount, Is.Zero);
            Assert.That(state.NextActionAt, Is.Zero);
            Assert.That(em.IsComponentEnabled<EnemyWaveEncounterComplete>(sequence), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Enemy011_OutOfBoundsPooledNormalCannotStrandTheEncounter(bool alreadyPooled)
        {
            using var world = new World("Elite wave out-of-bounds accounting test");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<EnemyWaveDefeatCountSystem>();
            Entity sequence = em.CreateEntity(typeof(WaveSequence));
            em.SetComponentData(sequence, new WaveSequence { RunGeneration = 1, UndefeatedCount = 2 });
            Entity normal = em.CreateEntity(typeof(EnemyWaveOwnership), typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentData(normal, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            em.SetComponentData(normal, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(normal, new RespawnRequest { IsPooled = alreadyPooled ? (byte)1 : (byte)0 });
            Entity elite = em.CreateEntity(typeof(EnemyWaveOwnership), typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentData(elite, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            em.SetComponentData(elite, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentEnabled<RespawnRequest>(elite, false);
            system.Update(world.Unmanaged);
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<WaveSequence>(sequence).UndefeatedCount, Is.EqualTo(1), "Living elite still blocks completion; pooled normal counts once");
            Assert.That(em.GetComponentData<EnemyWaveOwnership>(normal).DefeatCounted, Is.EqualTo(1));
            em.SetComponentData(elite, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<WaveSequence>(sequence).UndefeatedCount, Is.Zero, "Defeating elite can now finish even if its normal never returns");
            Assert.That(em.GetComponentData<WaveSequence>(sequence).DefeatedCount, Is.EqualTo(2));
        }

        [Test]
        public void Loop006_CompletionRequiresEverySequenceAndReportsOnce()
        {
            using var world = new World("Gauntlet completion test");
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            Entity first = em.CreateEntity(typeof(WaveSequence), typeof(EnemyWaveEncounterComplete));
            Entity second = em.CreateEntity(typeof(WaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(second, false);
            system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(second, true);
            system.Update();
            system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
        }

        private static T Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static void AssertInside(Vector2[] polygon, Vector3 point, float clearance)
        {
            Vector2 p = new Vector2(point.x, point.z);
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], edge = polygon[(i + 1) % polygon.Length] - a;
                float distance = (edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / edge.magnitude;
                Assert.That(distance, Is.GreaterThanOrEqualTo(clearance), $"Point {p} too close to perimeter {i}");
            }
        }
    }
}
