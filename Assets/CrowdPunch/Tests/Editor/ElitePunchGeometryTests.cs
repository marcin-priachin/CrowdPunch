using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class ElitePunchGeometryTests
    {
        [Test]
        public void TacticProbabilityEndpointsAreDeterministic()
        {
            uint zeroSeed = 123u, oneSeed = 123u;
            Assert.AreEqual(ElitePunchTactic.CrowdShot, ElitePunchSystem.ChooseTactic(ref zeroSeed, 0f));
            Assert.AreEqual(ElitePunchTactic.ClearPath, ElitePunchSystem.ChooseTactic(ref oneSeed, 1f));
        }

        [Test]
        public void IntermediateTacticSelectionRepeatsFromSameSeed()
        {
            uint first = 9876u, second = 9876u;
            Assert.AreEqual(ElitePunchSystem.ChooseTactic(ref first, 0.42f), ElitePunchSystem.ChooseTactic(ref second, 0.42f));
            Assert.AreEqual(first, second);
        }

        [Test]
        public void DesiredPositionIsBehindTargetRelativeToPlayer()
        {
            float3 position = ElitePunchSystem.DesiredPosition(new float3(2f, 3f, 0f), new float3(10f, 0f, 0f), 1.5f);
            Assert.That(position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TargetSearchEligibilityUsesDedicatedPhaseSwitches()
        {
            ElitePunchSettings settings = new ElitePunchSettings
            {
                AllowActiveTargets = 1,
                AllowRecoveringTargets = 0,
                AllowLaunchedTargets = 0
            };
            Health living = new Health { Current = 1f, Max = 1f };
            Assert.IsTrue(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Active }, living, settings));
            Assert.IsFalse(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering }, living, settings));
            Assert.IsFalse(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched }, living, settings));
        }

        [Test]
        public void SetupSpeedBrakesIntoPositionTolerance()
        {
            Assert.AreEqual(0f, ElitePunchSystem.CalculateSetupSpeed(0.4f, 0.4f, 75f, 40f, 12f));
            Assert.That(ElitePunchSystem.CalculateSetupSpeed(1f, 0.4f, 75f, 40f, 12f), Is.EqualTo(12f));
            Assert.AreEqual(75f, ElitePunchSystem.CalculateSetupSpeed(100f, 0.4f, 75f, 40f, 12f));
        }

        [Test]
        public void SetupSpeedStaysAboveMovingTargetCatchupSpeed()
        {
            float speed = ElitePunchSystem.CalculateSetupSpeed(0.5f, 0.4f, 37.5f, 20f, 11f);
            Assert.AreEqual(11f, speed);
        }

        [Test]
        public void EnemyInsideShotCorridorMovesTowardNearestSide()
        {
            bool shouldExit = EliteCrowdSupportSystem.TryGetCorridorExitDirection(
                new float3(4f, 0f, 0.5f),
                float3.zero,
                new float3(10f, 0f, 0f),
                1.5f,
                out float3 direction);

            Assert.IsTrue(shouldExit);
            Assert.Greater(direction.z, 0f);
            Assert.That(direction.x, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void EnemyOutsideShotSegmentKeepsItsNormalIntent()
        {
            Assert.IsFalse(EliteCrowdSupportSystem.TryGetCorridorExitDirection(
                new float3(12f, 0f, 0f),
                float3.zero,
                new float3(10f, 0f, 0f),
                1.5f,
                out _));
        }
    }
}
