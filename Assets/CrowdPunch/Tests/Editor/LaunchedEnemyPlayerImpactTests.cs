using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using NUnit.Framework;
using Unity.Mathematics;

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

        [Test]
        public void EnemyAndPlayerTargetsUseSharedCollisionDamageCurve()
        {
            Assert.That(EnemyCollisionDamage.Calculate(20f, 2f, Settings), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(EnemyCollisionDamage.Calculate(20f, 12f, Settings), Is.EqualTo(15f).Within(0.0001f));
            Assert.AreEqual(0f, EnemyCollisionDamage.Calculate(20f, 1.99f, Settings));
        }

        [Test]
        public void PlayerPunchOwnsAndDisarmsItsNewLaunch()
        {
            Assert.AreEqual(
                EnemyLaunchOwner.Player,
                EnemyLaunchOwnership.FromCause(EnemyLaunchCause.PlayerPunch));
            Assert.AreEqual(
                EnemyLaunchOwner.Enemy,
                EnemyLaunchOwnership.FromCause(EnemyLaunchCause.ElitePunch));
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

        [Test]
        public void FirstPunchDoesNotReplaceActiveEnemyVelocity()
        {
            Unity.Physics.PhysicsVelocity velocity = new Unity.Physics.PhysicsVelocity
            {
                Linear = new float3(1f, 0f, 0f),
                Angular = new float3(0f, 1f, 0f)
            };

            EnemyLaunchVelocity.ResetForPlayerPunchReplacement(
                ref velocity,
                EnemyLaunchPhase.Active,
                EnemyLaunchCause.PlayerPunch);

            Assert.AreEqual(new float3(1f, 0f, 0f), velocity.Linear);
            Assert.AreEqual(new float3(0f, 1f, 0f), velocity.Angular);
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
