using System;
using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Tests
{
    public sealed class LevelScoreTests
    {
        [Test]
        public void Score001_ChainBridgeReportsEveryPunchOncePerFrameWithItsCumulativeCount()
        {
            using var world = new World("Level score bridge");
            var em = world.EntityManager;
            var score = em.CreateEntity(typeof(LevelClearScore), typeof(ScoredDefeat));
            var system = world.GetOrCreateSystemManaged<LevelScoreBridgeSystem>();
            var bridge = new GameObject("Level score bridge test").AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            PlayerBridgeRegistry.Register(bridge);

            try
            {
                var awards = new List<(uint Punch, int Count)>();
                var positions = new List<Vector3>();
                bridge.PunchChainScored += (punch, count) => awards.Add((punch, count));
                bridge.ScoredDefeatReceived += positions.Add;
                var buffer = em.GetBuffer<ScoredDefeat>(score);
                uint[] punches = { 1, 2, 1, 2, 1, 3 };
                for (int i = 0; i < punches.Length; i++)
                    buffer.Add(new ScoredDefeat { PunchSequence = punches[i], WorldPosition = new float3(i, 0, 0) });

                system.Update();

                Assert.That(awards, Is.EqualTo(new[] { (1u, 3), (2u, 2) }));
                Assert.That(positions.Count, Is.EqualTo(punches.Length));
                Assert.That(positions[5], Is.EqualTo(Vector3.right * 5));
                Assert.That(buffer.Length, Is.Zero);

                buffer.Add(new ScoredDefeat { PunchSequence = 2 });
                system.Update();
                Assert.That(awards, Is.EqualTo(new[] { (1u, 3), (2u, 2), (2u, 3) }));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                UnityEngine.Object.DestroyImmediate(bridge.gameObject);
            }
        }

        [Test]
        public void Score001_ConsecutiveCalloutsProtectReadTimeAndGrowInPlace()
        {
            var canvas = new GameObject("Score callout test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var callouts = new LevelChainCallouts(canvas.transform);
                callouts.Queue(1, 3, 0);
                callouts.Tick(0);
                var first = ReadCount("3");
                Assert.That(first, Is.Not.Null);

                callouts.Queue(2, 5, .15);
                callouts.Tick(.15);
                var second = ReadCount("5");
                Assert.That(second, Is.Not.Null);
                callouts.Queue(2, 6, .20);
                callouts.Queue(3, 8, .20);
                callouts.Queue(3, 9, .20);
                callouts.Tick(.20);
                Assert.That(ReadCount("6"), Is.SameAs(second));
                Assert.That(second.rectTransform.localScale.y, Is.GreaterThan(1f));

                callouts.Tick(.39);
                var firstBadge = (RectTransform)first.transform.parent;
                Assert.That(firstBadge.anchoredPosition.x, Is.LessThan(0f));
                Assert.That(firstBadge.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(ReadCount("9"), Is.Null);

                callouts.Tick(.50);
                Assert.That(firstBadge.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.5f).Within(.01f));
                callouts.Tick(.61);
                Assert.That(ReadCount("3"), Is.Null);
                Assert.That(ReadCount("6"), Is.SameAs(second));
                Assert.That(ReadCount("9"), Is.Not.Null);

                callouts.Queue(4, 10, .62);
                callouts.Reset();
                callouts.Tick(1);
                Assert.That(canvas.GetComponentsInChildren<Text>(), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }

            Text ReadCount(string count) => Array.Find(canvas.GetComponentsInChildren<Text>(), label => label.text == count);
        }

        [Test]
        public void Score001_DeferredDefeatRetainsLethalPunchAndScoresOnce()
        {
            using var world = new World("Level score");
            var entities = world.EntityManager;
            Entity sequence = entities.CreateEntity(typeof(EnemyWaveSequence));
            entities.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 1, UndefeatedCount = 1 });
            Entity enemy = entities.CreateEntity(typeof(Enemy), typeof(EnemyWaveOwnership), typeof(Health),
                typeof(EnemyLaunchState), typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest),
                typeof(EnemyHealthBarVisibility), typeof(RespawnRequest));
            entities.SetComponentData(enemy, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            entities.SetComponentData(enemy, new Health { Current = 10, Max = 10 });
            entities.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, OriginatingPunchSequence = 15 });
            entities.SetComponentEnabled<RespawnRequest>(enemy, false);
            entities.SetComponentEnabled<DeathRequest>(enemy, false);

            var damage = new DamageRequest { Amount = 4 };
            damage.Add(6, 10, 12);
            damage.Add(5, 10, 15);
            entities.SetComponentData(enemy, damage);
            world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);

            var counter = world.GetOrCreateSystem<EnemyWaveDefeatCountSystem>();
            using var scoreQuery = entities.CreateEntityQuery(typeof(LevelClearScore));
            Entity score = scoreQuery.GetSingletonEntity();
            counter.Update(world.Unmanaged);
            Assert.That(entities.GetComponentData<LevelClearScore>(score).Defeats, Is.Zero);

            entities.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated, OriginatingPunchSequence = 15 });
            counter.Update(world.Unmanaged);
            counter.Update(world.Unmanaged);

            var defeats = entities.GetBuffer<ScoredDefeat>(score);
            Assert.That(entities.GetComponentData<LevelClearScore>(score).Defeats, Is.EqualTo(1));
            Assert.That(defeats.Length, Is.EqualTo(1));
            Assert.That(defeats[0].PunchSequence, Is.EqualTo(12));
        }

        [TestCase(null)]
        [TestCase(typeof(Barricade))]
        [TestCase(typeof(ProtectedPoint))]
        [TestCase(typeof(BossEncounter))]
        public void Score001_Loop006_FinalLethalHitsClearOnlyWaveEncounters(Type objective)
        {
            using var world = new World("Final wave lethal hits");
            var em = world.EntityManager;
            var damage = world.GetOrCreateSystem<DamageApplicationSystem>();
            var counter = world.GetOrCreateSystem<EnemyWaveDefeatCountSystem>();
            var spawner = world.GetOrCreateSystem<EnemyWaveSpawnSystem>();
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            em.CreateEntity(typeof(PhysicsWorldSingleton));
            if (objective != null) em.CreateEntity(ComponentType.ReadWrite(objective));

            var sequence = em.CreateEntity(typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete),
                typeof(EnemyWaveDefinition), typeof(EnemyWaveProfile), typeof(EnemyWaveEliteProfile), typeof(EnemyWaveSpawnRange));
            var waves = em.GetBuffer<EnemyWaveDefinition>(sequence);
            waves.Add(default);
            waves.Add(new EnemyWaveDefinition
            {
                ActivationMode = (byte)EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated,
                TotalEnemyCount = 1
            });
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence, false);
            em.SetComponentData(sequence, new EnemyWaveSequence
            {
                Initialized = 1, RunGeneration = 1, CurrentWaveIndex = 1, SpawnedCount = 1,
                UndefeatedCount = 2, Phase = EnemyWaveRuntimePhase.AwaitingActivation
            });
            Entity finalWaveEnemy = CreateEnemy(1);
            Entity previousWaveEnemy = CreateEnemy(0);

            uint before = GauntletCompletionRegistry.Sequence;
            Hit(previousWaveEnemy, 1);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(sequence).UndefeatedCount, Is.EqualTo(2));
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));

            Hit(finalWaveEnemy, 2);
            using var scoreQuery = em.CreateEntityQuery(typeof(LevelClearScore));
            var score = scoreQuery.GetSingletonEntity();
            Assert.That(em.GetComponentData<LevelClearScore>(score).Points, Is.EqualTo(objective == null ? 200 : 0));
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(objective == null ? before + 1 : before));
            if (objective == null)
            {
                Assert.That(GauntletCompletionRegistry.Score.Defeats, Is.EqualTo(2));
                Assert.That(GauntletCompletionRegistry.Focus?.x, Is.EqualTo(10));
            }
            foreach (Entity enemy in new[] { previousWaveEnemy, finalWaveEnemy })
            {
                Assert.That(em.GetComponentData<Health>(enemy).Current, Is.Zero);
                Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
                Assert.That(em.GetComponentData<EnemyDamageState>(enemy).IsDefeatDeferred, Is.EqualTo(1));
                Assert.That(em.IsComponentEnabled<DeathRequest>(enemy), Is.False);
                Assert.That(em.IsComponentEnabled<RespawnRequest>(enemy), Is.False);
            }

            em.SetComponentData(finalWaveEnemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            counter.Update(world.Unmanaged);
            completion.Update();
            Assert.That(em.GetBuffer<ScoredDefeat>(score).Length, Is.EqualTo(objective == null ? 2 : 0));
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(objective == null ? before + 1 : before));

            Entity CreateEnemy(int wave)
            {
                var enemy = em.CreateEntity(typeof(Enemy), typeof(EnemyWaveOwnership), typeof(Health),
                    typeof(EnemyLaunchState), typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest),
                    typeof(EnemyHealthBarVisibility), typeof(RespawnRequest), typeof(LocalTransform));
                em.SetComponentData(enemy, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1, WaveIndex = wave });
                em.SetComponentData(enemy, new Health { Current = 10, Max = 10 });
                em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
                em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(wave * 10, 0, 0)));
                em.SetComponentEnabled<DamageRequest>(enemy, false);
                em.SetComponentEnabled<DeathRequest>(enemy, false);
                em.SetComponentEnabled<RespawnRequest>(enemy, false);
                return enemy;
            }

            void Hit(Entity enemy, double now)
            {
                world.SetTime(new Unity.Core.TimeData(now, .02f));
                var request = new DamageRequest();
                request.Add(10, 10, 12);
                em.SetComponentData(enemy, request);
                em.SetComponentEnabled<DamageRequest>(enemy, true);
                damage.Update(world.Unmanaged);
                counter.Update(world.Unmanaged);
                spawner.Update(world.Unmanaged);
                completion.Update();
            }
        }

        [Test]
        public void Loop006_BossClearTracksBossPositionAfterCompletion()
        {
            using var world = new World("Boss clear subject");
            var em = world.EntityManager;
            var boss = em.CreateEntity(typeof(ChickenBoss), typeof(LocalTransform));
            em.SetComponentData(boss, new ChickenBoss { Phase = ChickenPhase.Defeated });
            em.SetComponentData(boss, LocalTransform.FromPosition(new float3(7f, 2f, 9f)));
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Focus?.x, Is.EqualTo(7f));
            em.SetComponentData(boss, LocalTransform.FromPosition(new float3(8f, 2f, 9f)));
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Focus?.x, Is.EqualTo(8f));
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
        }

        [Test]
        public void Loop006_ExitClearFocusesExitInsteadOfDestroyedBarrier()
        {
            using var world = new World("Exit clear subject");
            var em = world.EntityManager;
            var wall = em.CreateEntity(typeof(Barricade), typeof(LocalTransform));
            em.SetComponentData(wall, new Barricade { ExitPosition = new float3(9f, 0f, 4f), ExitRadius = 2f });
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));
            var player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(9f, 0f, 4f) });
            world.GetOrCreateSystemManaged<GauntletCompletionSystem>().Update();
            Assert.That(GauntletCompletionRegistry.Focus?.x, Is.EqualTo(9f));
        }
    }
}
