using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class LaunchedEnemyPlayerImpactTests
    {
        private static readonly EnemyLaunchSettings Settings = new EnemyLaunchSettings
        {
            MinimumDamageImpulse = 2f,
            BaseCollisionDamageMultiplier = 0.25f,
            DamageMultiplierPerExcessImpulse = 0.05f,
            MaximumCollisionDamageMultiplier = 0.75f
        };

        [TestCase(EnemyLaunchOwner.Player, EnemyLaunchCause.PlayerPunch, 7.5f)]
        [TestCase(EnemyLaunchOwner.Player, EnemyLaunchCause.EnemyCollision, 7.5f)]
        [TestCase(EnemyLaunchOwner.Enemy, EnemyLaunchCause.ElitePunch, 15f)]
        [TestCase(EnemyLaunchOwner.Enemy, EnemyLaunchCause.EnemyCollision, 15f)]
        [TestCase(EnemyLaunchOwner.Boss, EnemyLaunchCause.BossAttack, 15f)]
        [TestCase(EnemyLaunchOwner.Boss, EnemyLaunchCause.EnemyCollision, 15f)]
        [TestCase(EnemyLaunchOwner.Environment, EnemyLaunchCause.EnvironmentImpact, 15f)]
        [TestCase(EnemyLaunchOwner.None, EnemyLaunchCause.None, 15f)]
        public void Combat015_ReductionFollowsCurrentOwnerIncludingChains(
            EnemyLaunchOwner owner, EnemyLaunchCause cause, float expected)
        {
            var settings = Settings;
            settings.PlayerOwnedLaunchDamageReduction = 0.5f;
            var launch = new EnemyLaunchState { Owner = owner, LastCause = cause, LaunchDamage = 20f };
            Assert.That(LaunchedEnemyPlayerImpactSystem.CalculatePlayerDamage(launch, 12f, settings),
                Is.EqualTo(expected).Within(0.0001f));
            Assert.That(EnemyCollisionDamage.Calculate(20f, 12f, settings), Is.EqualTo(15f));
            Assert.That(LaunchedEnemyPlayerImpactSystem.CalculatePlayerDamage(launch, 1.99f, settings), Is.Zero);
        }

        [TestCase(-1f, 15f)]
        [TestCase(0f, 15f)]
        [TestCase(0.5f, 7.5f)]
        [TestCase(1f, 0f)]
        [TestCase(2f, 0f)]
        public void Combat015_ReductionIsClampedToZeroThroughFullProtection(float reduction, float expected)
        {
            var settings = Settings;
            settings.PlayerOwnedLaunchDamageReduction = reduction;
            var launch = new EnemyLaunchState { Owner = EnemyLaunchOwner.Player, LaunchDamage = 20f };
            Assert.That(LaunchedEnemyPlayerImpactSystem.CalculatePlayerDamage(launch, 12f, settings),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Combat015_SimultaneousImpactsSelectStrongestAfterReductionAndRepunchResetsEligibility()
        {
            using var world = new World("COMBAT-015 player-owned damage reduction");
            var em = world.EntityManager;
            var player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = 1f });
            var settings = Settings;
            settings.PlayerOwnedLaunchDamageReduction = 0.5f;
            em.SetComponentData(em.CreateEntity(typeof(EnemyLaunchSettings)), settings);
            var playerBody = Body(EnemyLaunchOwner.Player, 20f);
            var bossBody = Body(EnemyLaunchOwner.Boss, 12f);
            var go = new GameObject("COMBAT-015 bridge");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            bridge.PublishPlayerHealth(100f, 100f);
            PlayerBridgeRegistry.Register(bridge);
            int hits = 0;
            float received = 0f;
            bridge.EnemyContactHitReceived += (damage, protection, impulse) => { hits++; received = damage * 100f; };
            try
            {
                var system = world.GetOrCreateSystemManaged<LaunchedEnemyPlayerImpactSystem>();
                system.Update();
                Assert.That(received, Is.EqualTo(9f).Within(0.0001f));
                Assert.That(hits, Is.EqualTo(1));
                Assert.That(em.GetComponentData<EnemyLaunchState>(playerBody).PlayerImpactLaunchSequence, Is.EqualTo(1));
                Assert.That(em.GetComponentData<EnemyLaunchState>(bossBody).PlayerImpactLaunchSequence, Is.EqualTo(1));
                system.Update();
                Assert.That(hits, Is.EqualTo(1));

                var state = em.GetComponentData<EnemyLaunchState>(bossBody);
                EnemyLaunchTransition.Begin(ref state, EnemyLaunchCause.PlayerPunch, 12f);
                em.SetComponentData(bossBody, state);
                system.Update();
                Assert.That(hits, Is.EqualTo(2));
                Assert.That(received, Is.EqualTo(4.5f).Within(0.0001f));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                Object.DestroyImmediate(go);
            }

            Entity Body(EnemyLaunchOwner owner, float damage)
            {
                var entity = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                    typeof(PhysicsMass), typeof(EnemyContactDamageSettings), typeof(EnemyLaunchState));
                em.SetComponentData(entity, LocalTransform.Identity);
                em.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(12f, 0f, 0f) });
                em.SetComponentData(entity, new PhysicsMass { InverseMass = 1f });
                em.SetComponentData(entity, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched,
                    Owner = owner, LaunchDamage = damage, LaunchSequence = 1 });
                return entity;
            }
        }

        [Test]
        public void EnemyAndPlayerTargetsUseSharedCollisionDamageCurve()
        {
            Assert.That(EnemyCollisionDamage.Calculate(20f, 2f, Settings), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(EnemyCollisionDamage.Calculate(20f, 12f, Settings), Is.EqualTo(15f).Within(0.0001f));
            Assert.AreEqual(0f, EnemyCollisionDamage.Calculate(20f, 1.99f, Settings));
        }

        [Test]
        public void LaunchOwnershipStillTracksItsCause()
        {
            Assert.AreEqual(
                EnemyLaunchOwner.Player,
                EnemyLaunchOwnership.FromCause(EnemyLaunchCause.PlayerPunch));
            Assert.AreEqual(
                EnemyLaunchOwner.Enemy,
                EnemyLaunchOwnership.FromCause(EnemyLaunchCause.ElitePunch));
        }

        [TestCase(EnemyLaunchOwner.Player)]
        [TestCase(EnemyLaunchOwner.Enemy)]
        public void LaunchedBodyCanDamagePlayerRegardlessOfOwner(EnemyLaunchOwner owner)
        {
            EnemyLaunchState launchState = new EnemyLaunchState
            {
                Phase = EnemyLaunchPhase.Launched,
                Owner = owner,
                LaunchSequence = 2,
                PlayerImpactLaunchSequence = 1
            };

            Assert.IsTrue(LaunchedEnemyPlayerImpactSystem.CanDamagePlayer(launchState));
        }

        [Test]
        public void LaunchedBodyDamagesPlayerOnlyOncePerLaunchSequence()
        {
            EnemyLaunchState launchState = new EnemyLaunchState
            {
                Phase = EnemyLaunchPhase.Launched,
                Owner = EnemyLaunchOwner.Player,
                LaunchSequence = 2,
                PlayerImpactLaunchSequence = 2
            };

            Assert.IsFalse(LaunchedEnemyPlayerImpactSystem.CanDamagePlayer(launchState));
        }

        [Test]
        public void SweptBodyDetectsPlayerBetweenFrames()
        {
            Assert.IsTrue(LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(
                new float3(-2f, 0f, 0f),
                new float3(2f, 0f, 0f),
                float3.zero,
                0.5f));
        }

        [Test]
        public void DynamicMassConvertsVelocityToEstimatedImpulse()
        {
            Assert.That(
                LaunchedEnemyPlayerImpactSystem.EstimateImpactImpulse(new float3(4f, 0f, 0f), 0.5f),
                Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void RepunchReplacesExistingLinearAndAngularVelocity()
        {
            Unity.Physics.PhysicsVelocity velocity = new Unity.Physics.PhysicsVelocity
            {
                Linear = new float3(-12f, 3f, 1f),
                Angular = new float3(1f, 2f, 3f)
            };

            EnemyLaunchVelocity.ResetForPlayerPunchReplacement(
                ref velocity,
                EnemyLaunchPhase.Launched,
                EnemyLaunchCause.PlayerPunch);

            Assert.AreEqual(float3.zero, velocity.Linear);
            Assert.AreEqual(float3.zero, velocity.Angular);
        }

        [TestCase(EnemyLaunchPhase.Active)]
        [TestCase(EnemyLaunchPhase.Recovering)]
        public void Player004_FirstAndRecoveringPunchesReplaceVelocityToMatchPreview(EnemyLaunchPhase phase)
        {
            Unity.Physics.PhysicsVelocity velocity = new Unity.Physics.PhysicsVelocity
            {
                Linear = new float3(1f, 0f, 0f),
                Angular = new float3(0f, 1f, 0f)
            };

            EnemyLaunchVelocity.ResetForPlayerPunchReplacement(
                ref velocity,
                phase,
                EnemyLaunchCause.PlayerPunch);

            Assert.AreEqual(float3.zero, velocity.Linear);
            Assert.AreEqual(float3.zero, velocity.Angular);
        }

        [Test]
        public void EnemyRepunchDoesNotUsePlayerReplacementRule()
        {
            Unity.Physics.PhysicsVelocity velocity = new Unity.Physics.PhysicsVelocity
            {
                Linear = new float3(1f, 0f, 0f)
            };

            EnemyLaunchVelocity.ResetForPlayerPunchReplacement(
                ref velocity,
                EnemyLaunchPhase.Launched,
                EnemyLaunchCause.ElitePunch);

            Assert.AreEqual(new float3(1f, 0f, 0f), velocity.Linear);
        }
    }
}
