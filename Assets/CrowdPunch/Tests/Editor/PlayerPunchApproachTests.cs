using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Tests
{
    public sealed class PlayerPunchApproachTests
    {
        [TestCase(4f, 2.4f)]
        [TestCase(1.6f, 0f)]
        [TestCase(.9f, 0f)]
        [TestCase(.3f, 0f)]
        public void Player012_OnlyClosesMissingDistanceToEnemySurface(float enemyDistance, float expected)
        {
            using var collider = SphereCollider.Create(new SphereGeometry { Radius = .5f });
            var body = new RigidBody
            {
                Collider = collider,
                WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, .5f, enemyDistance)),
                Scale = 1f
            };

            float3 move = PunchDetectionSystem.ContactApproach(body, float3.zero, new float3(0, 2f, 1f));
            Assert.That(move.x, Is.Zero);
            Assert.That(move.y, Is.Zero);
            Assert.That(move.z, Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void Player012_StopsBeforeWallAlongTheApproachLine()
        {
            using var collider = BoxCollider.Create(new BoxGeometry
            {
                Orientation = quaternion.identity,
                Size = new float3(4f, 2f, .5f)
            });
            using var worldOwner = new PhysicsWorld(1, 0, 0);
            var world = worldOwner;
            var bodies = world.StaticBodies;
            bodies[0] = new RigidBody
            {
                Collider = collider,
                WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, 1f, 2f)),
                Scale = 1f
            };
            world.CollisionWorld.BuildBroadphase(ref world, .02f, float3.zero);

            var player = new PlayerSnapshot { Radius = .5f, CollisionLayer = 1u };
            float3 move = PlayerObstacleCollisionSystem.LimitPunchApproach(
                world.CollisionWorld, player, 0f, new float3(0, 0, 4f));
            Assert.That(move.x, Is.Zero);
            Assert.That(move.y, Is.Zero);
            Assert.That(move.z, Is.EqualTo(1.23f).Within(.002f));
        }
    }
}
