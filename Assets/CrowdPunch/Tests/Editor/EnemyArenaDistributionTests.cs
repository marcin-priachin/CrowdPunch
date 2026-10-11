using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyArenaDistributionTests
    {
        private static readonly ArenaBounds Arena = new ArenaBounds
        {
            Center = float3.zero,
            Extents = new float3(10f, 1f, 10f)
        };

        private static readonly EnemyMovementSettings Movement = new EnemyMovementSettings
        {
            SurroundDistance = 4f,
            SurroundRingSpacing = 1f,
            StoppingDistance = 0.5f
        };

        [Test]
        public void ArenaRelativeTargetKeepsInsetFromBounds()
        {
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                new float3(9f, 0f, 9f),
                2f,
                Movement,
                Arena);

            Assert.That(target.x, Is.InRange(-9.5f, 9.5f));
            Assert.That(target.z, Is.InRange(-9.5f, 9.5f));
            Assert.AreEqual(2f, target.y);
        }

        [Test]
        public void BlockedBaseSlotUsesAnOpenArenaDirection()
        {
            float3 player = new float3(9f, 0f, 0f);
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                player,
                0f,
                Movement,
                Arena);

            Assert.Less(target.x, player.x);
            Assert.That(math.distance(player.xz, target.xz), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void UnblockedBaseSlotIsPreserved()
        {
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                float3.zero,
                0f,
                Movement,
                Arena);

            Assert.That(target.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(target.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ReserveTargetsCoverArenaInteriorInsteadOfOnlyEdges()
        {
            bool negativeX = false;
            bool positiveX = false;
            bool negativeZ = false;
            bool positiveZ = false;
            bool hasCentralTarget = false;

            for (int entityIndex = 0; entityIndex < 64; entityIndex++)
            {
                float3 target = EnemyChaseSystem.GetArenaDistributionTargetForTests(
                    entityIndex,
                    0f,
                    Movement,
                    Arena);

                negativeX |= target.x < -2f;
                positiveX |= target.x > 2f;
                negativeZ |= target.z < -2f;
                positiveZ |= target.z > 2f;
                hasCentralTarget |= math.abs(target.x) < 2f && math.abs(target.z) < 2f;
                Assert.That(target.x, Is.InRange(-9.5f, 9.5f));
                Assert.That(target.z, Is.InRange(-9.5f, 9.5f));
            }

            Assert.IsTrue(negativeX && positiveX && negativeZ && positiveZ);
            Assert.IsTrue(hasCentralTarget);
        }

        [Test]
        public void ReserveTargetIsStableForAnEnemy()
        {
            float3 first = EnemyChaseSystem.GetArenaDistributionTargetForTests(23, 0f, Movement, Arena);
            float3 second = EnemyChaseSystem.GetArenaDistributionTargetForTests(23, 0f, Movement, Arena);

            Assert.AreEqual(first, second);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Enemy010_OnlyClosestBlockedExplosiveMayIgnoreConfiguredSeparation(bool archetypeOverride)
        {
            using var world = ExplosiveWorld();
            var em = world.EntityManager;
            Entity closest = SpacingEnemy(em, new float3(2, 0, 0));
            Entity farther = SpacingEnemy(em, new float3(4, 0, 0));
            SpacingEnemy(em, new float3(1.8f, 0, 0), EnemyArchetypeKind.Baseline);
            SpacingEnemy(em, new float3(3.8f, 0, 0), EnemyArchetypeKind.Baseline);
            if (archetypeOverride)
                foreach (Entity enemy in new[] { closest, farther })
                {
                    em.SetComponentData(enemy, new EnemySeparationDistance());
                    em.SetComponentData(enemy, new EnemyArchetypeSeparationDistances
                    {
                        OverrideMask = (byte)(1 << (int)EnemyArchetypeKind.Baseline),
                        Baseline = .5f, BaselineWeight = 2f
                    });
                }
            Chase(world);
            Assert.AreEqual(float3.zero, em.GetComponentData<NavigationIntent>(closest).Separation);
            Assert.Less(em.GetComponentData<DesiredMovement>(closest).Direction.x, 0);
            Assert.Greater(em.GetComponentData<NavigationIntent>(farther).Separation.x, 1.9f);
            Assert.Greater(em.GetComponentData<DesiredMovement>(farther).Direction.x, 0);
        }

        [Test]
        public void Enemy010_ClosestKeepsSeparationWhenItCanStillApproach()
        {
            using var world = ExplosiveWorld();
            var em = world.EntityManager;
            Entity closest = SpacingEnemy(em, new float3(2, 0, 0));
            SpacingEnemy(em, new float3(2, 0, -.2f), EnemyArchetypeKind.Baseline);
            em.SetComponentData(closest, new EnemyContactAttemptState { IsAttempting = 1, SecondsRemaining = 10 });
            Chase(world);
            Assert.Greater(em.GetComponentData<NavigationIntent>(closest).Separation.z, 1.9f);
            Assert.Less(em.GetComponentData<DesiredMovement>(closest).Direction.x, 0);
        }

        [Test]
        public void Enemy010_EqualDistancesAllowOnlyOneException()
        {
            using var world = ExplosiveWorld();
            var em = world.EntityManager;
            Entity first = SpacingEnemy(em, new float3(2, 0, 0));
            Entity second = SpacingEnemy(em, new float3(2, 0, 0));
            SpacingEnemy(em, new float3(1.8f, 0, 0), EnemyArchetypeKind.Baseline);
            Chase(world);
            Assert.AreEqual(float3.zero, em.GetComponentData<NavigationIntent>(first).Separation);
            Assert.Greater(em.GetComponentData<NavigationIntent>(second).Separation.x, 1.9f);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Enemy010_UnavailableExplosivesDoNotReserveException(int exclusion)
        {
            using var world = ExplosiveWorld();
            var em = world.EntityManager;
            Entity closest = SpacingEnemy(em, new float3(2, 0, 0));
            Entity farther = SpacingEnemy(em, new float3(4, 0, 0));
            SpacingEnemy(em, new float3(3.8f, 0, 0), EnemyArchetypeKind.Baseline);
            if (exclusion == 0) em.SetComponentEnabled<RespawnRequest>(closest, true);
            else if (exclusion == 1) em.SetComponentData(closest, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            else em.SetComponentData(closest, new ExplosiveEnemyState { HasExploded = 1 });
            Chase(world);
            Assert.AreEqual(float3.zero, em.GetComponentData<NavigationIntent>(farther).Separation);
            Assert.Less(em.GetComponentData<DesiredMovement>(farther).Direction.x, 0);
        }

        [Test]
        public void Enemy010_ProtectedPointUsesClosestSelectedAttacker()
        {
            using var world = ExplosiveWorld();
            var em = world.EntityManager;
            Entity unselected = SpacingEnemy(em, new float3(2, 0, 0));
            Entity selected = SpacingEnemy(em, new float3(4, 0, 0));
            SpacingEnemy(em, new float3(3.8f, 0, 0), EnemyArchetypeKind.Baseline);
            Entity objective = em.CreateEntity(typeof(ProtectedPoint));
            em.SetComponentData(objective, new ProtectedPoint { Center = new float2(8, 8), BreachThreshold = 1 });
            em.AddBuffer<ProtectedPointAttacker>(objective).Add(new ProtectedPointAttacker { Enemy = selected, DistanceSq = 16 });
            Chase(world);
            Assert.AreEqual(float3.zero, em.GetComponentData<NavigationIntent>(selected).Separation);
            Assert.Less(em.GetComponentData<DesiredMovement>(selected).Direction.x, 0);
            Assert.AreEqual(new float3(8, 0, 8), em.GetComponentData<NavigationIntent>(unselected).Destination);
        }

        private static World ExplosiveWorld()
        {
            var world = new World("ENEMY-010 explosive separation");
            var em = world.EntityManager;
            em.SetComponentData(em.CreateEntity(typeof(PlayerSnapshot)), new PlayerSnapshot { IsAvailable = true });
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), Arena);
            // Proximity pursuit must work even with no ordinary pressure slots.
            em.CreateEntity(typeof(EnemyCrowdPressureSettings));
            world.SetTime(new TimeData(1, .02f));
            return world;
        }

        private static Entity SpacingEnemy(EntityManager em, float3 position,
            EnemyArchetypeKind archetype = EnemyArchetypeKind.Explosive)
        {
            Entity enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(RespawnRequest),
                typeof(EnemyLaunchState), typeof(EnemyArchetype), typeof(EnemyContactAttemptState),
                typeof(DesiredMovement), typeof(NavigationIntent), typeof(WanderDestination),
                typeof(EnemyMovementSettings), typeof(EnemyContactDamageSettings), typeof(EnemySeparationDistance),
                typeof(EnemyArchetypeSeparationDistances));
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentData(enemy, LocalTransform.FromPosition(position));
            em.SetComponentData(enemy, new EnemyArchetype { Value = archetype });
            em.SetComponentData(enemy, new EnemyMovementSettings
            {
                MoveSpeed = 2, ChargeSpeedMultiplier = 1, SeparationWeight = 2, StoppingDistance = .1f
            });
            em.SetComponentData(enemy, new EnemySeparationDistance { Value = .5f });
            em.SetComponentData(enemy, new EnemyContactDamageSettings
            {
                AttemptDistance = 8, AttemptDuration = 1, AttemptSpeedMultiplier = 1, AttemptSeparationWeight = 0
            });
            if (archetype == EnemyArchetypeKind.Explosive) em.AddComponent<ExplosiveEnemyState>(enemy);
            return enemy;
        }

        private static void Chase(World world)
        {
            world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}
