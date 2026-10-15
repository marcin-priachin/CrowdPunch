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

        [Test]
        public void Enemy005DasherFlapsDuringDashAndLaunchAndPlaysDeath()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Dasher;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new DasherState { Phase = DasherPhase.Positioning });

            Tick();
            Assert.That(Pose.x, Is.Zero, "Flying_Idle should drive a stationary Dasher.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Fast_Flying should drive movement.");

            em.SetComponentData(owner, new DasherState { Phase = DasherPhase.Dashing });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Dashing uses Fast_Flying.");
            float dashFrame = Pose.y;
            Tick(.07f);
            Assert.That(Pose.y, Is.Not.EqualTo(dashFrame), "Wings must continue moving during the committed dash.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(1f, 0f, 0f)), "Launching restarts Fast_Flying.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should start at its first frame.");
            Tick();
            Assert.That(Pose.y, Is.EqualTo(0.25f).Within(0.001f), "Death should advance and then hold its final frame.");
        }

        [Test]
        public void Enemy010ExplosiveUsesIdleWalkAndDeathWithoutDyingDuringRecovery()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Explosive;
            em.SetComponentData(visual, animation);

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary Explosive.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Walk should drive movement.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(1f, 0f, 0f)), "Launch starts on the first Walk frame.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering });
            em.SetComponentData(owner, new DesiredMovement());
            Tick();
            Assert.That(Pose.x, Is.Zero, "Recovery should return to Idle rather than play Death.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should begin only on defeat.");
        }

        [Test]
        public void Enemy002RangedUsesWaveForWindUpAndRotatedIdleWhileLaunched()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Ranged;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Ready });

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary ranged enemy.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Walk should drive ranged movement.");

            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(2f, 0f, 0f)), "Wave should restart on wind-up entry.");
            Tick();
            Assert.That(Pose.y, Is.EqualTo(0.25f).Within(0.001f));

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            float3x4 launched = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Assert.That(launched.c3.x, Is.EqualTo(9f).Within(0.001f), "The dedicated launch sample should use Idle.");
            Assert.That(launched.c1.z, Is.EqualTo(-1f).Within(0.001f), "The Idle pose should rotate onto its side.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should start on defeat.");
        }

        [Test]
        public void Enemy009EliteUsesPunchHitReactAndDeathMotions()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Elite;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.SelectingTarget });
            em.AddComponentData(owner, new Health { Current = 250f, Max = 250f });

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary elite.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Walk should drive elite movement.");

            em.SetComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.WindUp });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(2f, 0f, 0f)), "Punch should restart on wind-up entry.");

            em.SetComponentData(owner, new Health { Current = 240f, Max = 250f });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(3f, 0f, 0f)), "HitReact should override Punch after damage.");
            Tick();
            Assert.That(Pose.y, Is.EqualTo(0.25f).Within(0.001f));

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should override HitReact on defeat.");
        }

        [Test]
        public void Enemy012BaselineUsesIdleWalkRotatedLaunchAndDeathMotions()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Baseline;
            em.SetComponentData(visual, animation);

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary baseline enemy.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Walk should drive baseline movement.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            float3x4 launched = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Assert.That(launched.c3.x, Is.EqualTo(9f).Within(0.001f), "Launching should use the dedicated Idle sample.");
            Assert.That(launched.c1.z, Is.EqualTo(-1f).Within(0.001f), "The launched Idle pose should rotate onto its side.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should start on defeat.");
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

        [TestCase(EnemyAnimationProfile.Baseline)]
        [TestCase(EnemyAnimationProfile.Ranged)]
        [TestCase(EnemyAnimationProfile.Explosive)]
        [TestCase(EnemyAnimationProfile.Dasher)]
        public void Info004CreatureFlightAdvancesAndRepunchRestartsWithoutPhysicsWrites(EnemyAnimationProfile profile)
        {
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)profile;
            em.SetComponentData(visual, animation);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 4 });
            var velocity = new PhysicsVelocity { Linear = new float3(3, 2, 8) };
            em.SetComponentData(owner, velocity);
            Tick();
            var initial = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Tick(.13f);
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c3, Is.Not.EqualTo(initial.c3));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 5 });
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c3, Is.EqualTo(initial.c3));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<LocalTransform>(owner).Position, Is.EqualTo(float3.zero));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            Tick(); Tick(.25f);
            Assert.That(Pose.x, Is.Zero, "Recovering uses Idle, never the terminal Death pose.");
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c1.z, Is.EqualTo(0).Within(.001f));
        }

        [Test]
        public void Info004ExploderPrefabAndSamplesPreserveUprightIdle()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyExplosive.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                foreach (var authoring in root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>())
                {
                    var renderer = authoring.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                    Assert.That(renderer.name, Does.StartWith("Green_Blob"), "The Exploder uses the smooth blob silhouette.");
                    Assert.That(renderer.sharedMaterial.GetFloat("_ExploderPulse"), Is.GreaterThan(0));
                    Assert.That(renderer.localBounds.center.magnitude, Is.LessThan(4f),
                        "The eye's visibility bounds must stay on the blob, not inherit the FBX scale twice.");
                    Assert.That(renderer.localBounds.extents.magnitude, Is.LessThan(5f));
                    Assert.That(authoring.Profile, Is.EqualTo(EnemyAnimationProfile.Explosive));
                    using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                    string currentHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.That(reader.ReadString(), Is.EqualTo(currentHash), "Stale samples prevent the animation baker from attaching playback.");
                    int bones = reader.ReadInt32();
                    reader.ReadInt32();
                    for (int bone = 0; bone < bones; bone++) reader.ReadString();
                    for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                    for (int bone = 0; bone < bones; bone++)
                    {
                        var idle = animator.transform.worldToLocalMatrix * renderer.bones[bone].localToWorldMatrix * renderer.sharedMesh.bindposes[bone];
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++)
                                Assert.That(reader.ReadSingle(), Is.EqualTo(idle[row, column]).Within(.001f),
                                    "The saved skeleton must match upright Idle, not the final sampled Death pose.");
                    }
                }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Info004DasherActiveAnimationKeepsHoverClearance()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyDasher.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var renderer = root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
                var authoring = renderer.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                reader.ReadInt32(); reader.ReadString();
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int bone = 0; bone < bones; bone++) reader.ReadString();
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                var matrices = new UnityEngine.Matrix4x4[bones];
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                for (int pose = 0; pose < frames * 2; pose++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        matrices[bone] = UnityEngine.Matrix4x4.identity;
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                    }
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        var weight = weights[index]; var point = vertices[index];
                        var position = matrices[weight.boneIndex0].MultiplyPoint3x4(point) * weight.weight0
                            + matrices[weight.boneIndex1].MultiplyPoint3x4(point) * weight.weight1
                            + matrices[weight.boneIndex2].MultiplyPoint3x4(point) * weight.weight2
                            + matrices[weight.boneIndex3].MultiplyPoint3x4(point) * weight.weight3;
                        position = root.transform.InverseTransformPoint(animator.transform.TransformPoint(position));
                        Assert.That(position.y, Is.GreaterThanOrEqualTo(.199f), "Every active flight frame must clear the owner's origin.");
                    }
                }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        [TestCase(EnemyAnimationProfile.Baseline)]
        [TestCase(EnemyAnimationProfile.Wizard)]
        [TestCase(EnemyAnimationProfile.Ninja)]
        public void Info004CenteredLaunchAndRecoveryPreserveBodyHeight(EnemyAnimationProfile profile)
        {
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)profile;
            animation.LaunchPivot = new float3(0, 2, 0);
            em.SetComponentData(visual, animation);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.y, Is.EqualTo(2f).Within(.001f));
            Assert.That(Pose.z, Is.EqualTo(2f).Within(.001f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            Tick();
            Assert.That(Pose.y, Is.EqualTo(2f).Within(.001f), "Recovery starts around the same body pivot.");
            Tick(.25f);
            Assert.That(Pose.z, Is.EqualTo(0f).Within(.001f), "The offset disappears when upright.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [Test]
        [TestCase("EnemyLinkedPair")]
        [TestCase("EnemyNinja")]
        public void Info004CenteredPrefabFlightAndRecoveryClearTheFloor(string prefab)
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/" + prefab + ".prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var collider = root.GetComponent<UnityEngine.CapsuleCollider>();
                float floor = collider.center.y - collider.height * .5f;
                float minimum = float.MaxValue;
                foreach (var authoring in root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>())
                {
                    Assert.That(authoring.RotateLaunchAroundOwner, Is.True);
                    var pivot = animator.transform.InverseTransformPoint(root.transform.position
                        + root.transform.up * authoring.LaunchPivotHeight);
                    var renderer = authoring.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                    using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                    reader.ReadInt32(); reader.ReadString();
                    int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                    for (int bone = 0; bone < bones; bone++) reader.ReadString();
                    for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                    var renderBounds = renderer.localBounds;
                    var toOwner = root.transform.worldToLocalMatrix * animator.transform.localToWorldMatrix;
                    bool boundsValid = true;
                    long start = reader.BaseStream.Position;
                    var vertices = renderer.sharedMesh.vertices;
                    var weights = renderer.sharedMesh.boneWeights;
                    var matrices = new UnityEngine.Matrix4x4[bones];
                    foreach (int motion in new[] { 0, EnemyAnimationSamples.FlyingMotion })
                    {
                        reader.BaseStream.Position = start + motion * frames * bones * 48L;
                        for (int frame = 0; frame < frames; frame++)
                        {
                            for (int bone = 0; bone < bones; bone++)
                            {
                                matrices[bone] = UnityEngine.Matrix4x4.identity;
                                for (int column = 0; column < 4; column++)
                                    for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                            }
                            for (int angle = 0; angle < 25; angle++)
                            {
                                var rotation = UnityEngine.Quaternion.Euler(-118.65f * angle / 24f, 0, 0);
                                for (int index = 0; index < vertices.Length; index++)
                                {
                                    var w = weights[index]; var v = vertices[index];
                                    var point = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                        + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                        + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                        + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                                    point = pivot + rotation * (point - pivot);
                                    boundsValid &= renderBounds.Contains(point);
                                    minimum = UnityEngine.Mathf.Min(minimum,
                                        toOwner.MultiplyPoint3x4(point).y);
                                }
                            }
                        }
                    }
                    Assert.That(boundsValid, Is.True, "Culling bounds must cover the centered turn.");
                }
                Assert.That(minimum, Is.GreaterThanOrEqualTo(floor + .01f), "Every launch/recovery pose must clear the physics floor.");
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Info004DestroyedOwnerDoesNotLeaveAStaleLookup()
        {
            world.EntityManager.DestroyEntity(owner);
            Assert.DoesNotThrow(() => Tick());
        }
    }
}
