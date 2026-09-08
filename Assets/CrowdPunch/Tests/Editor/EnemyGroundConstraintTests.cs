using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyGroundConstraintTests
    {
        [Test]
        public void Combat018SpawnAboveFloorKeepsFalling()
        {
            var ground = new EnemyGroundConstraint { HasGround = 1, Height = 1f };
            var transform = LocalTransform.FromPosition(new float3(3f, 5f, 7f));
            var velocity = new PhysicsVelocity { Linear = new float3(2f, -4f, 6f) };
            EnemyGroundProjection.Apply(ref ground, ref transform, ref velocity);
            Assert.That(transform.Position.y, Is.EqualTo(5f));
            Assert.That(velocity.Linear.y, Is.EqualTo(-4f));
            Assert.That(ground.IsLocked, Is.Zero);
        }

        [TestCase(1f, 0, 0)]
        [TestCase(5f, 0, 1)]
        [TestCase(5f, 1, 0)]
        public void Combat018LandingPunchAndSolverLiftPreserveHorizontalMomentum(float height, int locked, int snap)
        {
            var ground = new EnemyGroundConstraint
                { HasGround = 1, Height = 1f, IsLocked = (byte)locked, SnapRequested = (byte)snap };
            var transform = LocalTransform.FromPosition(new float3(3f, height, 7f));
            var velocity = new PhysicsVelocity { Linear = new float3(12f, 8f, -9f) };
            EnemyGroundProjection.Apply(ref ground, ref transform, ref velocity);
            Assert.That(transform.Position, Is.EqualTo(new float3(3f, 1f, 7f)));
            Assert.That(velocity.Linear, Is.EqualTo(new float3(12f, 0f, -9f)));
            Assert.That(ground.IsLocked, Is.EqualTo(1));
            Assert.That(ground.SnapRequested, Is.Zero);
        }
    }
}
