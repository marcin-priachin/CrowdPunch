using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class DasherObstacleRedirectTests
    {
        [Test]
        public void Enemy005ObstacleRedirectUsesSolvedLaunchLikeDirection()
        {
            float3 direction = DasherObstacleRedirectSystem.ResolveRedirectedDirection(
                new float3(1f, 0f, 0f),
                new float3(0f, 2f, 5f),
                new float3(-1f, 0f, 0f));

            Assert.That(math.distance(direction, new float3(0f, 0f, 1f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void Enemy005HeadOnObstacleRedirectReflectsWhenSolverStopsHorizontalMotion()
        {
            float3 direction = DasherObstacleRedirectSystem.ResolveRedirectedDirection(
                new float3(1f, 0f, 0f),
                new float3(0f, 3f, 0f),
                new float3(-1f, 0f, 0f));

            Assert.That(math.distance(direction, new float3(-1f, 0f, 0f)), Is.LessThan(0.0001f));
        }
    }
}
