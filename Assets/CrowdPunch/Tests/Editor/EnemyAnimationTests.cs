using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyAnimationTests
    {
        private World world;
        private Entity owner;
        private Entity visual;
        private BlobAssetReference<EnemyAnimationSamples> samples;

        [SetUp]
        public void SetUp()
        {
            world = new World("Enemy animation test");
            var em = world.EntityManager;
            owner = em.CreateEntity(typeof(DesiredMovement), typeof(EnemyMovementSettings), typeof(LocalTransform),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(PhysicsVelocity));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new EnemyMovementSettings { MoveSpeed = 4f });
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<EnemyAnimationSamples>();
            root.BoneCount = 1;
            root.FrameCount = 2;
            var durations = builder.Allocate(ref root.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref root.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
            {
                durations[motion] = 1f;
                // x identifies the motion; y identifies the sampled time.
                for (int frame = 0; frame < 2; frame++)
                    matrices[motion * 2 + frame] = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0),
                        new float3(0, 0, 1), new float3(motion, frame, 0));
            }
            samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            visual = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples });
            em.SetComponentData(visual, new EnemyAnimationPlayback { Initialized = 1 });
            em.AddBuffer<SkinMatrix>(visual).Add(default);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            samples.Dispose();
        }

        private void Tick(float delta = 0.25f)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + delta, delta));
            world.GetOrCreateSystem<EnemyAnimationSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }

        private float3 Pose => world.EntityManager.GetBuffer<SkinMatrix>(visual)[0].Value.c3;

        [Test]
        public void Info004IdleLoopsAndInterpolates()
        {
            Tick();
            Assert.That(Pose.x, Is.Zero);
            Assert.That(Pose.y, Is.EqualTo(0.5f).Within(0.001f));
            Tick(1f);
            Assert.That(Pose.y, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Info004MovementIsRelativeToEnemyFacing()
        {
            world.EntityManager.SetComponentData(owner, LocalTransform.FromRotation(quaternion.RotateY(math.PI / 2f)));
            world.EntityManager.SetComponentData(owner, new DesiredMovement { Direction = new float3(1, 0, 0), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Info004StrafingAndPartialSpeedBlendWithIdle()
        {
            world.EntityManager.SetComponentData(owner, new DesiredMovement { Direction = new float3(1, 0, 0), Speed = 2f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void Combat011RecoveryAndDefeatFreezePoseWithoutPhysicsWrites()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            var velocity = new PhysicsVelocity { Linear = new float3(10, 7, 3), Angular = new float3(0, 2, 0) };
            em.SetComponentData(owner, velocity);
            Tick();
            float3 pose = Pose;
            foreach (var phase in new[] { EnemyLaunchPhase.Recovering, EnemyLaunchPhase.Defeated })
            {
                em.SetComponentData(owner, new EnemyLaunchState { Phase = phase });
                Tick();
                Assert.That(Pose, Is.EqualTo(pose));
                Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(velocity.Linear));
                Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Angular, Is.EqualTo(velocity.Angular));
                Assert.That(em.GetComponentData<LocalTransform>(owner).Position, Is.EqualTo(float3.zero));
            }
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Tick();
            Assert.That(Pose.y, Is.Not.EqualTo(pose.y));
        }

        [Test]
        public void Combat014FlyingRestartsOnRepunchAndHoldsLastFrame()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(9f, 0f, 0f)));
            Tick();
            Assert.That(Pose.y, Is.EqualTo(0.25f).Within(0.001f));
            Tick(2f);
            Assert.That(Pose.y, Is.EqualTo(1f).Within(0.001f));
            Tick();
            Assert.That(Pose.y, Is.EqualTo(1f).Within(0.001f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 2 });
            Tick();
            Assert.That(Pose.y, Is.Zero);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Tick();
            Assert.That(Pose.x, Is.Zero);
        }

        [Test]
        public void Info004FlyingPitchFollowsVelocityWithoutPhysicsWrites()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            var velocity = new PhysicsVelocity { Linear = new float3(0f, 4f, 4f) };
            em.SetComponentData(owner, velocity);
            Tick();
            var pose = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Assert.That(math.distance(pose.c2, math.normalize(velocity.Linear)), Is.LessThan(0.0001f));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<LocalTransform>(owner).Rotation, Is.EqualTo(quaternion.identity));
            em.SetComponentData(owner, new PhysicsVelocity());
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c2, Is.EqualTo(pose.c2));
        }

        [Test]
        public void Info004PoolingResetsPlaybackAndDisabledRequestAllowsReuse()
        {
            var em = world.EntityManager;
            Tick();
            em.SetComponentData(owner, new RespawnRequest { IsPooled = 1 });
            em.SetComponentEnabled<RespawnRequest>(owner, true);
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.Zero);
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.EqualTo(1));
        }

        [TestCase(EnemyLaunchPhase.Recovering)]
        [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat011LaunchEndsWithImpactAndRepunchInterruptsIt(EnemyLaunchPhase ending)
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            em.SetComponentData(owner, new EnemyLaunchState { Phase = ending, LaunchSequence = 1, RecoverySecondsRemaining = 0.5f });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)));
            Tick();
            Assert.That(Pose.y, Is.EqualTo(ending == EnemyLaunchPhase.Recovering ? 0.5f : 0.25f).Within(0.001f));
            Tick(2f);
            Assert.That(Pose, Is.EqualTo(new float3(10f, 1f, 0f)));
            Tick();
            Assert.That(Pose.y, Is.EqualTo(1f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 2 });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(9f, 0f, 0f)));
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Landing, Is.Zero);
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(float3.zero));
        }

        [Test]
        public void Info004DestroyedOwnerDoesNotLeaveAStaleLookup()
        {
            world.EntityManager.DestroyEntity(owner);
            Assert.DoesNotThrow(() => Tick());
        }
    }
}
