using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using EnemyArchetype = CrowdPunch.Components.EnemyArchetype;

namespace CrowdPunch.Tests
{
    public sealed class ProtectedPointTests
    {
        private World world;
        private EntityManager em;
        private Entity objective, player;
        [SetUp] public void Setup()
        {
            world = new World("PROTECT requirements"); em = world.EntityManager;
            world.SetTime(new TimeData(1, .02f));
            objective = em.CreateEntity(typeof(ProtectedPoint), typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentData(objective, new ProtectedPoint { Center = new float2(0, -46), HalfSize = new float2(4, 2),
                BreachThreshold = 2, MaximumPlayerAttackers = 3 });
            em.SetComponentData(objective, new EnemyWaveSequence { RunGeneration = 1, UndefeatedCount = 6, SpawnedCount = 6 });
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(objective, false);
            em.AddBuffer<ProtectedPointAttacker>(objective);
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = 1 });
        }
        [TearDown] public void Cleanup() => world.Dispose();
        private Entity Enemy(float x, float z = 0)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyLaunchState), typeof(EnemyWaveOwnership),
                typeof(EnemyContactDamageSettings), typeof(RespawnRequest), typeof(EnemyArchetype), typeof(EnemyContactAttemptState),
                typeof(DesiredMovement), typeof(NavigationIntent), typeof(WanderDestination), typeof(EnemyMovementSettings),
                typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances), typeof(NavigationAgent));
            em.SetComponentData(e, LocalTransform.FromPosition(new float3(x, 0, z)));
            em.SetComponentData(e, new EnemyWaveOwnership { Sequence = objective, RunGeneration = 1 });
            em.SetComponentData(e, new EnemyContactDamageSettings { AttemptDistance = 5, ContactRadius = .5f });
            em.SetComponentData(e, new EnemyMovementSettings { MoveSpeed = 4, StoppingDistance = .5f });
            em.SetComponentEnabled<RespawnRequest>(e, false);
            return e;
        }
        private void Select() => world.GetOrCreateSystem<ProtectedPointPrioritySystem>().Update(world.Unmanaged);
        private void Breach() => world.GetOrCreateSystem<ProtectedPointBreachSystem>().Update(world.Unmanaged);

        [Test] public void Protect001_EliteDefenseStaysFiniteWhileOrdinaryEliteWavesReplenish()
        {
            var elite=Enemy(10);
            em.AddComponentData(elite,new EnemyTier { Value=EnemyCombatTier.Elite });
            var normal=Enemy(11);
            em.AddComponent<EliteWaveReplenishment>(normal);
            em.AddComponentData(normal,new EnemyRespawnSettings { Enabled=1 });
            var system=world.GetOrCreateSystem<EliteWaveReplenishmentSystem>();
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(normal).Enabled,Is.Zero);
            em.RemoveComponent<ProtectedPoint>(objective);
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(normal).Enabled,Is.EqualTo(1));
            em.SetComponentData(elite,new EnemyLaunchState { Phase=EnemyLaunchPhase.Defeated });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(normal).Enabled,Is.Zero);
        }

        [Test] public void Protect002_SelectsClosestAcrossArchetypesAndReassignsSlots()
        {
            var far = Enemy(4); var baseline = Enemy(1); var wizard = Enemy(2); var ranged = Enemy(3); Enemy(6);
            em.AddComponentData(wizard, new WizardSettings { EngagementRange = 10 });
            em.AddComponentData(ranged, new RangedEnemySettings { EngagementRange = 10 });
            Select();
            using (var selected = em.GetBuffer<ProtectedPointAttacker>(objective).ToNativeArray(Unity.Collections.Allocator.Temp))
                CollectionAssert.AreEqual(new[] { baseline, wizard, ranged }, selected.Select(a => a.Enemy).ToArray());
            em.SetComponentData(far, LocalTransform.FromPosition(new float3(.5f, 0, 0))); Select();
            Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective)[0].Enemy, Is.EqualTo(far));
            Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective).Length, Is.EqualTo(3));
        }

        [Test] public void Protect002_RangesAvailabilityAndZeroCapAreRespected()
        {
            var wizard = Enemy(4); em.AddComponentData(wizard, new WizardSettings { EngagementRange = 2 });
            var dash = Enemy(1); em.AddComponentData(dash, new DasherSettings { PreparationMinimumDistance = 3, PreparationMaximumDistance = 8 });
            var pooled = Enemy(2); em.SetComponentEnabled<RespawnRequest>(pooled, true);
            var launched = Enemy(3); em.SetComponentData(launched, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Select(); Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective).Length, Is.Zero);
            Enemy(1); Select(); Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective).Length, Is.EqualTo(1));
            var point = em.GetComponentData<ProtectedPoint>(objective); point.MaximumPlayerAttackers = 0; em.SetComponentData(objective, point);
            Select(); Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective).Length, Is.Zero);
        }

        [Test] public void Protect002_UnselectedExplosiveUsesZoneDespiteContactRangeException()
        {
            var e = Enemy(1); em.SetComponentData(e, new EnemyArchetype { Value = EnemyArchetypeKind.Explosive });
            em.CreateEntity(typeof(ArenaBounds)); em.CreateEntity(typeof(EnemyCrowdPressureSettings));
            world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            var intent = em.GetComponentData<NavigationIntent>(e);
            Assert.That(em.GetComponentData<DesiredMovement>(e).Direction.z, Is.LessThan(-.9f));
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.EqualTo(4));
        }

        [Test] public void Protect002_DasherAdvancesUntilSelectedAndFinishesCommittedPreparation()
        {
            var e = Enemy(4);
            em.AddComponentData(e, new DasherSettings { PreparationMinimumDistance = 2, PreparationMaximumDistance = 8, TelegraphDuration = 1 });
            em.AddComponent<DasherState>(e);
            em.SetComponentData(e, new DesiredMovement { Direction = new float3(0,0,-1), Speed = 4 });
            var system = world.GetOrCreateSystem<DasherDecisionSystem>();
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<DasherState>(e).Phase, Is.EqualTo(DasherPhase.Positioning));
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.EqualTo(4));
            em.SetComponentData(e, new DasherState { Phase = DasherPhase.Preparing, SecondsRemaining = .01f });
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<DasherState>(e).Phase, Is.EqualTo(DasherPhase.Dashing));
            Assert.That(em.GetComponentData<DasherState>(e).LockedDirection.x, Is.LessThan(-.9f));
        }

        [Test] public void Protect002_WizardCannotStartWithoutSlotButCommittedCastFinishes()
        {
            var e = Enemy(1); var settings = WizardSettings.Default; settings.CastWheneverInRange = true;
            em.AddComponentData(e, settings);
            em.AddComponentData(e, new WizardCastState { Phase = WizardCastPhase.Checking, RandomState = 1 });
            var system = world.GetOrCreateSystem<WizardCastSystem>(); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<WizardCastState>(e).Phase, Is.EqualTo(WizardCastPhase.Checking));
            em.SetComponentData(e, new WizardCastState { Phase = WizardCastPhase.Telegraph, Remaining = .01f });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<WizardCastState>(e).Phase, Is.EqualTo(WizardCastPhase.Active));
        }

        [Test] public void Protect002_RangedAdvancesWithoutSlotAndFinishesExistingWindUp()
        {
            var e = Enemy(2);
            em.AddComponentData(e, new RangedEnemySettings { EngagementRange = 10 });
            em.AddComponentData(e, new RangedAttackState { Phase = RangedAttackPhase.Ready });
            em.AddComponent<RangedPositioningState>(e);
            em.SetComponentData(e, new DesiredMovement { Direction = new float3(0,0,-1), Speed = 4 });
            var positioning = world.GetOrCreateSystem<RangedEnemyPositioningSystem>();
            var attack = world.GetOrCreateSystem<RangedEnemyAttackSystem>();
            positioning.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); attack.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.EqualTo(4));
            Assert.That(em.GetComponentData<RangedAttackState>(e).IsAttackEligible, Is.Zero);
            em.SetComponentData(e, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = 1 });
            positioning.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); attack.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.Zero);
            Assert.That(em.GetComponentData<RangedAttackState>(e).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
            Assert.That(em.GetComponentData<RangedAttackState>(e).SecondsRemaining, Is.LessThan(1));
        }

        [Test] public void Protect004_RestartClearsBreachesSelectionAndOwnedEnemies()
        {
            em.CreateEntity(typeof(MatchState));
            var point = em.GetComponentData<ProtectedPoint>(objective); point.Breaches = 1; em.SetComponentData(objective, point);
            var enemy = Enemy(0);
            em.GetBuffer<ProtectedPointAttacker>(objective).Add(new ProtectedPointAttacker { Enemy = enemy });
            var reset = world.GetOrCreateSystemManaged<GameRestartSystem>();
            GameRestartRegistry.RequestRestart(); reset.Update();
            Assert.That(em.GetComponentData<ProtectedPoint>(objective).Breaches, Is.Zero);
            Assert.That(em.GetBuffer<ProtectedPointAttacker>(objective).Length, Is.Zero);
            Assert.That(em.Exists(enemy), Is.False);
            Assert.That(em.IsComponentEnabled<EnemyWaveEncounterComplete>(objective), Is.False);
        }

        [TestCase(EnemyLaunchPhase.Active, true)]
        [TestCase(EnemyLaunchPhase.Launched, false)]
        [TestCase(EnemyLaunchPhase.Recovering, false)]
        [TestCase(EnemyLaunchPhase.Defeated, false)]
        public void Protect003_OnlyActiveEnemiesBreach(EnemyLaunchPhase phase, bool expected)
        {
            var e = Enemy(0, -45); em.SetComponentData(e, new EnemyLaunchState { Phase = phase });
            Breach();
            Assert.That(em.Exists(e), Is.EqualTo(!expected));
            Assert.That(em.GetComponentData<ProtectedPoint>(objective).Breaches, Is.EqualTo(expected ? 1 : 0));
            Assert.That(em.GetComponentData<EnemyWaveSequence>(objective).UndefeatedCount, Is.EqualTo(expected ? 5 : 6));
        }

        [Test] public void Protect003_BreachDestroysLinkedVisualsCountsOnceAndHonorsThreshold()
        {
            var e = Enemy(0, -46); var child = em.CreateEntity();
            var linked = em.AddBuffer<LinkedEntityGroup>(e); linked.Add(new LinkedEntityGroup { Value = e }); linked.Add(new LinkedEntityGroup { Value = child });
            Breach(); Breach();
            Assert.That(em.Exists(child), Is.False);
            Assert.That(em.GetComponentData<ProtectedPoint>(objective).Failed, Is.False);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(objective).DefeatedCount, Is.EqualTo(1));
            Enemy(4, -44); Breach();
            Assert.That(em.GetComponentData<ProtectedPoint>(objective).Failed, Is.True);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(objective).DefeatedCount, Is.EqualTo(2));
        }

        [Test] public void Protect003_OutsideZoneAndStaleOwnershipCannotBreach()
        {
            Enemy(4.01f, -46); Enemy(0, -43.99f);
            var stale = Enemy(0, -46); em.SetComponentData(stale, new EnemyWaveOwnership { Sequence = objective, RunGeneration = 0 });
            Breach(); Assert.That(em.GetComponentData<ProtectedPoint>(objective).Breaches, Is.Zero);
        }

        [Test] public void Protect004_FailureBeatsLastWaveCompletionAndReportsOnlyOnce()
        {
            var point = em.GetComponentData<ProtectedPoint>(objective); point.Breaches = point.BreachThreshold; em.SetComponentData(objective, point);
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(objective, true);
            uint completion = GauntletCompletionRegistry.Sequence, failure = GauntletFailureRegistry.Sequence;
            var system = world.GetOrCreateSystemManaged<GauntletCompletionSystem>(); system.Update(); system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(completion));
            Assert.That(GauntletFailureRegistry.Sequence, Is.EqualTo(failure + 1));
            point.Breaches = 0; em.SetComponentData(objective, point); system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(completion + 1));
        }

        [Test] public void Protect004_AuthoredWavesAreFiniteExactAndBatched()
        {
            string[] names = { "First_Advance", "Mixed_Advance", "Final_Advance" };
            int[,] counts = { {14,0,2,0,0,0}, {16,2,4,2,0,0}, {20,3,4,3,1,1} };
            for (int i = 0; i < 3; i++)
            {
                var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>($"Assets/CrowdPunch/Data/Settings/Waves/Progression/CP18_{i+1:00}_{names[i]}.asset");
                Assert.That(wave, Is.Not.Null);
                Assert.That(wave.TotalEnemyCount, Is.EqualTo(16 + i * 8));
                Assert.That(wave.Enemies.Sum(x => x.MinimumCount), Is.EqualTo(wave.TotalEnemyCount));
                Assert.That(wave.Enemies.All(x => x.Weight == 0), Is.True);
                var kinds = new[] { Configuration.EnemyArchetype.Baseline, Configuration.EnemyArchetype.Ranged,
                    Configuration.EnemyArchetype.Explosive, Configuration.EnemyArchetype.Dasher,
                    Configuration.EnemyArchetype.Armored, Configuration.EnemyArchetype.Wizard };
                for (int k = 0; k < kinds.Length; k++)
                    Assert.That(wave.Enemies.Where(x => x.Settings.Archetype == kinds[k]).Sum(x => x.MinimumCount), Is.EqualTo(counts[i,k]));
                Assert.That(wave.BatchSize, Is.EqualTo(4)); Assert.That(wave.BatchInterval, Is.EqualTo(3));
                Assert.That(wave.SpawnMode, Is.EqualTo(EnemyWaveSpawnMode.Batched));
                Assert.That(wave.ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
                if (i > 0) Assert.That(wave.DelayBeforeWave, Is.EqualTo(5));
                Assert.That(wave.ArmoredAmmunitionProfile, Is.Null); Assert.That(wave.WizardAmmunitionProfile, Is.Null);
                Assert.That(wave.WaitForPersistentHazards, Is.False);
                Assert.That(wave.SpawnRectangles.All(r => r.Center.z - r.Depth * .5f >= 37), Is.True);
            }
        }
    }
}
