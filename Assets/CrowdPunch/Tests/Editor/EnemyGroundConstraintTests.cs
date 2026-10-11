using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Core;
using CrowdPunch.Utilities;

namespace CrowdPunch.Tests
{
    public sealed class EnemyGroundConstraintTests
    {
        [Test]
        public void Combat018ArenaDefaultsToExistingFloorSurfaceAndAllowsExplicitOverride()
        {
            var go = new UnityEngine.GameObject("Arena floor default");
            try
            {
                var authoring = go.AddComponent<CrowdPunch.Authoring.ArenaAuthoring>();
                Assert.AreEqual(-1f, authoring.GroundSurfaceHeight);
                var serialized = new UnityEditor.SerializedObject(authoring);
                serialized.FindProperty("overrideGroundSurfaceHeight").boolValue = true;
                serialized.FindProperty("groundSurfaceHeight").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(0f, authoring.GroundSurfaceHeight);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0f, 0f, 0)]
        [TestCase(1.25f, 1.25f, 0)]
        [TestCase(0f, 5f, 0)]
        [TestCase(0f, 5f, 1)]
        public void Combat018AuthoredFloorSetsPlaneAndPreservesFallAndPunchSnap(float floor, float height, int snap)
        {
            using var world = new World("Authored enemy floor");
            using var physics = new PhysicsWorld(0,0,0);
            using var collider = Unity.Physics.SphereCollider.Create(new SphereGeometry { Center = new float3(0,.5f,0), Radius = .5f });
            var em = world.EntityManager;
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = floor });
            var enemy = em.CreateEntity(typeof(Enemy),typeof(EnemyGroundConstraint),typeof(LocalTransform),typeof(PhysicsVelocity),typeof(PhysicsCollider));
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(-16.88f,height,16.38849f)));
            em.SetComponentData(enemy, new PhysicsCollider { Value = collider });
            em.SetComponentData(enemy, new PhysicsVelocity { Linear = new float3(3,-4,7) });
            em.SetComponentData(enemy, new EnemyGroundConstraint { SnapRequested = (byte)snap });
            world.SetTime(new TimeData(0,.02f));
            world.GetOrCreateSystem<EnemyGroundConstraintSystem>().Update(world.Unmanaged);
            var ground = em.GetComponentData<EnemyGroundConstraint>(enemy);
            Assert.That(ground.Height, Is.EqualTo(floor).Within(.001f));
            Assert.AreEqual(1, ground.HasGround);
            bool falling = height > floor + .02f && snap == 0;
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.y, Is.EqualTo(falling ? height : floor).Within(.001f));
            Assert.AreEqual(new float3(3,falling ? -4 : 0,7), em.GetComponentData<PhysicsVelocity>(enemy).Linear);
        }

        [TestCase(16.4f, false)]
        [TestCase(14f, true)]
        public void Combat018GroundSpawnClearanceRejectsBorderColumnRegardlessOfSpawnHeight(float x, bool clear)
        {
            using var floor = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = new float3(0,-.5f,0), Size = new float3(50,1,50), Orientation = quaternion.identity });
            using var wall = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = new float3(17,1,0), Size = new float3(1,2,50), Orientation = quaternion.identity });
            using var physicsOwner = new PhysicsWorld(2,0,0);
            var physics = physicsOwner;
            var bodies = physics.StaticBodies;
            bodies[0] = new RigidBody { Collider = floor, WorldFromBody = RigidTransform.identity, Scale = 1 };
            bodies[1] = new RigidBody { Collider = wall, WorldFromBody = RigidTransform.identity, Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero);
            Assert.AreEqual(clear, EnemyGroundSpawnClearance.IsClear(physics.CollisionWorld, new float3(x,5,0), .5f, 0));
        }

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
