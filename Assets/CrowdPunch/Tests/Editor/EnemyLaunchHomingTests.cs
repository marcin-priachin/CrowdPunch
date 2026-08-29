using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class EnemyLaunchHomingTests
    {
        [Test]
        public void HomingCapsTurnAndPreservesHorizontalSpeedAndVerticalVelocity()
        {
            float3 result = EnemyLaunchHoming.RotateHorizontalVelocity(
                new float3(10f, 4f, 0f),
                new float3(0f, 20f, 10f),
                math.radians(10f));

            Assert.That(math.length(result.xz), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(4f));
            Assert.That(math.degrees(math.atan2(result.z, result.x)), Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void HomingDoesNotOvershootTargetDirection()
        {
            float3 result = EnemyLaunchHoming.RotateHorizontalVelocity(
                new float3(10f, 2f, 0f),
                new float3(10f, 0f, 1f),
                math.radians(45f));

            float2 expected = math.normalize(new float2(10f, 1f));
            Assert.That(result.x / 10f, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(result.z / 10f, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2f));
        }
    }
}
