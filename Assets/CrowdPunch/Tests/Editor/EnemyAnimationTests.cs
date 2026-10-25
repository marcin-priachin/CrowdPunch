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

        private void UseTrail()
        {
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Trail;
            animation.SpawnMotion = 7;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new Health { Current = 5, Max = 5 });
        }

        [Test]
        public void Trail006AuthoredLocomotionHitLaunchRecoveryAndDefeatKeepPhysicsOwnership()
        {
            UseTrail();
            var em = world.EntityManager;
            var velocity = new PhysicsVelocity { Linear = new float3(3, 4, 5) };
            em.SetComponentData(owner, velocity);
            Tick();
            Assert.That(Pose.x, Is.Zero);
            em.SetComponentData(owner, new DesiredMovement { Speed = 2, Direction = math.forward() });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1));
            em.SetComponentData(owner, new DesiredMovement { Speed = 4, Direction = math.forward() });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(2));
            em.SetComponentData(owner, new Health { Current = 4, Max = 5 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(5));
            Tick(.25f);
            Assert.That(Pose.y, Is.EqualTo(.25f).Within(.001f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(9));
            Tick(2);
            Assert.That(Pose.y, Is.EqualTo(1).Within(.001f), "Launch holds the final authored damage pose.");
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 2 });
            Tick();
            Assert.That(Pose.y, Is.Zero, "A fresh launch restarts the reaction.");
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(6));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            Tick();
            Assert.That(Pose.y, Is.EqualTo(.5f).Within(.001f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(10));
            Assert.That(Pose.y, Is.Zero);
            Tick(2);
            Assert.That(Pose.y, Is.EqualTo(1).Within(.001f));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner), Is.EqualTo(velocity));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [Test]
        public void Trail006SpawnUsesSharedClockAndLaunchInterruptsImmediately()
        {
            UseTrail();
            var em = world.EntityManager;
            em.AddComponentData(owner, new EnemySpawnAnimation { Duration = 2, UseAnimation = 1 });
            em.SetComponentEnabled<EnemySpawnAnimation>(owner, true);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(7));
            Assert.That(Pose.y, Is.EqualTo(.5f).Within(.001f));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(9));
            em.SetComponentEnabled<RespawnRequest>(owner, true);
            em.SetComponentData(owner, new RespawnRequest { IsPooled = 1 });
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.Zero);
        }

        [Test]
        public void Trail006BodyFeedbackDoesNotTintShakeOrScaleAuthoredVisuals()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new EnemyArchetype { Value = EnemyArchetypeKind.Trail });
            em.AddComponentData(owner, new EnemyImpactFeedback { Squash = 1, Intensity = 1, VisualRemaining = 1, VisualDuration = 1 });
            em.AddComponentData(visual, new EnemyVisualOwner { Value = owner, UsesAtlas = 1, AnimationDriven = 1 });
            em.AddComponentData(visual, new EnemyVisualDeformation());
            var matrix = float4x4.TRS(new float3(1, 2, 3), quaternion.RotateY(.5f), new float3(2));
            em.AddComponentData(visual, new LocalToWorld { Value = matrix });
            em.AddComponentData(visual, new Unity.Rendering.URPMaterialPropertyBaseColor { Value = new float4(1) });
            world.GetOrCreateSystem<EnemyReadabilitySystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            world.GetOrCreateSystem<EnemyImpactVisualSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(matrix));
            Assert.That(em.GetComponentData<Unity.Rendering.URPMaterialPropertyBaseColor>(visual).Value, Is.EqualTo(new float4(1)));
        }

        [Test]
        public void Trail006SquidPrefabPreservesScaleTextureAndGroundedSamples()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyTrail.prefab");
            try
            {
                var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(CrowdPunch.Editor.EnemyTrailPrefabBuilder.ModelPath);
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                foreach (var transform in animator.GetComponentsInChildren<UnityEngine.Transform>(true))
                {
                    string path = UnityEditor.AnimationUtility.CalculateTransformPath(transform, animator.transform);
                    // The model root is intentionally user-scalable; descendants retain source proportions.
                    if (string.IsNullOrEmpty(path)) continue;
                    var original = string.IsNullOrEmpty(path) ? source.transform : source.transform.Find(path);
                    Assert.That(original, Is.Not.Null, path);
                    Assert.That(UnityEngine.Vector3.Distance(transform.localScale, original.localScale), Is.LessThan(.0001f), path);
                }
                Assert.That(root.transform.localScale, Is.EqualTo(UnityEngine.Vector3.one));
                Assert.That(animator.applyRootMotion, Is.False);
                var renderer = root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                Assert.That(renderer.sharedMaterial.GetTexture("_ColorMap"), Is.EqualTo(source.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMaterial.mainTexture));
                var authoring = renderer.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
                Assert.That(authoring.Profile, Is.EqualTo(EnemyAnimationProfile.Trail));
                Assert.That(authoring.SpawnMotion, Is.EqualTo(7));
                var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
                Assert.That(controller.layers[0].stateMachine.states[7].state.motion.name, Does.Contain("Spawn"));
                var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                    "Assets/CrowdPunch/Data/Settings/Enemies/TrailEnemySpawnSettings.asset");
                Assert.That(settings.SpawnFromGround, Is.True);
                Assert.That(settings.EnemyPrefab, Is.EqualTo(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/EnemyTrail.prefab")));
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                string hash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(controller)).ToString()
                    + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
                Assert.That(reader.ReadString(), Is.EqualTo(hash));
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int bone = 0; bone < bones; bone++) reader.ReadString();
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                var matrices = new UnityEngine.Matrix4x4[bones];
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                bool spawnBelowFloor = false;
                for (int pose = 0; pose < frames * EnemyAnimationSamples.MotionCount; pose++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        matrices[bone] = UnityEngine.Matrix4x4.identity;
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                    }
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        var w = weights[index]; var point = vertices[index];
                        var local = matrices[w.boneIndex0].MultiplyPoint3x4(point) * w.weight0
                            + matrices[w.boneIndex1].MultiplyPoint3x4(point) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(point) * w.weight2
                            + matrices[w.boneIndex3].MultiplyPoint3x4(point) * w.weight3;
                        Assert.That(renderer.localBounds.Contains(local), Is.True, "Animated vertex escaped culling bounds.");
                        var position = root.transform.InverseTransformPoint(animator.transform.TransformPoint(local));
                        if (pose / frames == 7) spawnBelowFloor |= position.y < -1.01f;
                        // Runtime mesh grounding handles the current user-authored model scale.
                    }
                }
                Assert.That(spawnBelowFloor, Is.True, "Spawn should emerge from underground.");
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        private void UseGoblin()
        {
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Goblin;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new EnemyContactAttemptState());
            em.AddComponentData(owner, new EnemyContactDamageSettings { AttemptWindUpDuration = .4f, AttemptDuration = .6f });
            em.AddComponentData(owner, new Health { Current = 10, Max = 10 });
        }

        [Test]
        public void Enemy002GoblinRangedCutAttackTracksWindUpAndActualShotOnce()
        {
            UseGoblin();
            var em = world.EntityManager;
            em.AddComponentData(owner, new RangedEnemySettings { WindUpDuration = .8f, ProjectileLaunchProgress = .4f });
            em.AddComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(3));
            Assert.That(Pose.y, Is.EqualTo(.2f).Within(.001f));
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, ProjectilesSpawned = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(4));
            Assert.That(Pose.y, Is.EqualTo(.4f).Within(.001f), "Projectile emission starts at the configured authored frame.");
            Tick(.3f);
            Assert.That(Pose.y, Is.EqualTo(.7f).Within(.001f));
            Tick(.4f);
            Assert.That(Pose.x, Is.Zero, "Follow-through finishes once during cooldown.");
            Tick(2f);
            Assert.That(Pose.x, Is.Zero);
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, ProjectilesSpawned = 2 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(4));
            Assert.That(Pose.y, Is.EqualTo(.4f).Within(.001f), "Each actual shot restarts at the configured frame.");
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(25)]
        public void Enemy002RangedLaunchFrameSelectsFullClipPoseWithoutRebuilding(int frame)
        {
            UseGoblin();
            var settings = UnityEngine.ScriptableObject.CreateInstance<CrowdPunch.Configuration.EnemySpawnSettings>();
            try
            {
                var serialized = new UnityEditor.SerializedObject(settings);
                serialized.FindProperty("projectileLaunchFrame").intValue = frame;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var em = world.EntityManager;
                float release = settings.ProjectileLaunchProgress;
                Assert.That(release, Is.EqualTo(frame / 25f).Within(.001f));
                em.AddComponentData(owner, new RangedEnemySettings { WindUpDuration = 1, ProjectileLaunchProgress = release });
                em.AddComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .5f });
                Tick();
                Assert.That(Pose.y, Is.EqualTo(release * .5f).Within(.001f));
                em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, ProjectilesSpawned = 1 });
                Tick();
                Assert.That(Pose.x, Is.EqualTo(4));
                Assert.That(Pose.y, Is.EqualTo(release).Within(.001f));
                if (frame == 25)
                {
                    Tick();
                    Assert.That(Pose.x, Is.Zero, "A release on the final frame has no looping follow-through.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Enemy002GoblinRangedCancellationAndLaunchDiscardCutAttack()
        {
            UseGoblin();
            var em = world.EntityManager;
            em.AddComponentData(owner, new RangedEnemySettings { WindUpDuration = 1 });
            em.AddComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .5f });
            Tick();
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, CancelledWindUps = 1 });
            Tick();
            Assert.That(Pose.x, Is.Zero, "Cancelled wind-up never plays a firing strike.");
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, ProjectilesSpawned = 1 });
            Tick();
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(EnemyAnimationSamples.FlyingMotion));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Tick();
            Assert.That(Pose.x, Is.Zero, "Returning to Active cannot resume an interrupted strike.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [Test]
        public void Info004GoblinRangedPrefabUsesSuppliedClipsCelMaterialsAndSpawn()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyRanged.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
                var states = controller.layers[0].stateMachine.states;
                Assert.That(states[3].state.motion.name, Does.Contain("Cut Attack"));
                Assert.That(states[4].state.motion.name, Does.Contain("Cut Attack"));
                Assert.That(states[7].state.motion.name, Does.Contain("Spawn"));
                Assert.That(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(animator.gameObject), Is.EqualTo(
                    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                        "Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Wizard Variant.prefab")));
                Assert.That(root.GetComponentsInChildren<UnityEngine.MeshRenderer>(true), Is.Empty,
                    "Rigid equipment must be skinned to follow sampled hand and head bones.");
                foreach (var authoring in root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true))
                {
                    Assert.That(authoring.Profile, Is.EqualTo(EnemyAnimationProfile.Goblin));
                    Assert.That(authoring.SpawnMotion, Is.EqualTo(7));
                    var renderer = authoring.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                    foreach (var material in renderer.sharedMaterials)
                        Assert.That(material.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                    using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                    string hash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(controller)).ToString()
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.That(reader.ReadString(), Is.EqualTo(hash), "Every skinned part must have current samples.");
                    Assert.That(reader.ReadInt32(), Is.EqualTo(renderer.bones.Length));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(32));
                }
                var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                    "Assets/CrowdPunch/Data/Settings/Enemies/RangedEnemySpawnSettings.asset");
                Assert.That(settings.SpawnFromGround, Is.True);
                Assert.That(settings.EnemyPrefab, Is.EqualTo(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                    "Assets/CrowdPunch/Prefabs/EnemyRanged.prefab")));
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Info006SpawnMotionUsesSharedClockAndPunchImmediatelyInterrupts()
        {
            UseGoblin();
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.SpawnMotion = 7;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new EnemySpawnAnimation { Duration = 1, UseAnimation = 1 });
            em.SetComponentEnabled<EnemySpawnAnimation>(owner, true);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = 1 });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(7, 0, 0)));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(7, .5f, 0)));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(EnemyAnimationSamples.FlyingMotion));
            em.SetComponentEnabled<EnemySpawnAnimation>(owner, false);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Tick();
            Assert.That(Pose.x, Is.Zero);
        }

        [Test]
        public void Info004GoblinChargeUsesLoopingRunWithoutWindUpOrStrikePoses()
        {
            UseGoblin();
            var em = world.EntityManager;
            em.SetComponentData(owner, new EnemyContactAttemptState { IsWindingUp = 1, SecondsRemaining = .2f });
            Tick();
            Assert.That(Pose.x, Is.Zero, "The preparation pause uses idle rather than an attack wind-up.");
            em.SetComponentData(owner, new EnemyContactAttemptState { IsAttempting = 1, SecondsRemaining = .3f });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(2, 0, 0)));
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(2, .5f, 0)));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(owner).SecondsRemaining, Is.EqualTo(.3f));
            Assert.That(em.GetComponentData<LocalTransform>(owner).Position, Is.EqualTo(float3.zero));
        }

        [Test]
        public void Info004GoblinWalkRunAndDamageUseAuthoredMotions()
        {
            UseGoblin();
            var em = world.EntityManager;
            Tick();
            Assert.That(Pose.x, Is.Zero);
            em.SetComponentData(owner, new DesiredMovement { Speed = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1));
            em.SetComponentData(owner, new DesiredMovement { Speed = 4 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(2));
            em.SetComponentData(owner, new Health { Current = 8, Max = 10 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(5));
            Tick(1.1f);
            Assert.That(Pose.x, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Info004GoblinLaunchHoldsPoseTopForwardThroughTimeAndRepunch(bool ranged)
        {
            UseGoblin();
            var em = world.EntityManager;
            if (ranged) em.AddComponentData(owner, new RangedAttackState());
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.LaunchPivot = new float3(0, 1, 0);
            em.SetComponentData(visual, animation);
            var velocity = new PhysicsVelocity { Linear = new float3(6, 0, 3) };
            em.SetComponentData(owner, velocity);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            var held = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Assert.That(math.distance(held.c1, math.forward()), Is.LessThan(.0001f), "Model top must point along owner forward.");
            Assert.That(math.distance(held.c3, new float3(9, 1, -1)), Is.LessThan(.0001f), "Rotate around the physics owner, not the model feet.");
            Tick(2);
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value, Is.EqualTo(held));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 2 });
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value, Is.EqualTo(held));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [TestCase("Baseline")]
        [TestCase("Ranged")]
        public void Info004GoblinLaunchSamplesAreFrozenForBodyAndEquipment(string kind)
        {
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/Enemy" + kind + ".prefab");
            foreach (var authoring in root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true))
            {
                Assert.That(authoring.RotateLaunchAroundOwner, Is.True);
                Assert.That(authoring.LaunchPivotHeight, Is.Zero);
                using var stream = new System.IO.MemoryStream(authoring.Samples.bytes);
                using var reader = new System.IO.BinaryReader(stream);
                Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                reader.ReadString(); int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int bone = 0; bone < bones; bone++) reader.ReadString();
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                stream.Position += (long)EnemyAnimationSamples.FlyingMotion * frames * bones * 48;
                var held = reader.ReadBytes(bones * 48);
                for (int frame = 1; frame < frames; frame++)
                    Assert.That(reader.ReadBytes(bones * 48), Is.EqualTo(held), authoring.name + " frame " + frame);
            }
        }

        [Test]
        public void Combat011GoblinLaunchRecoveryDefeatAndReuseKeepPhysicsOwnership()
        {
            UseGoblin();
            var em = world.EntityManager;
            var velocity = new PhysicsVelocity { Linear = new float3(6, 2, 3) };
            em.SetComponentData(owner, velocity);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(EnemyAnimationSamples.FlyingMotion));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(EnemyAnimationSamples.ImpactMotion));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .3f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(6));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick(2);
            Tick(2);
            Assert.That(Pose, Is.EqualTo(new float3(10, 1, 0)));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(velocity.Linear));
            em.SetComponentData(owner, new RespawnRequest { IsPooled = 1 });
            em.SetComponentEnabled<RespawnRequest>(owner, true);
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.Zero);
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            Tick();
            Assert.That(Pose.x, Is.Zero);
        }

        [TestCase(EnemyAnimationProfile.Goblin)]
        [TestCase(EnemyAnimationProfile.GoblinArmored)]
        [TestCase(EnemyAnimationProfile.Explosive)]
        [TestCase(EnemyAnimationProfile.Dasher)]
        public void Info004AuthoredAnimationBypassesTintAndProceduralDeformation(EnemyAnimationProfile profile)
        {
            UseGoblin();
            var em = world.EntityManager;
            var animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)profile;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
            em.AddComponentData(owner, new EnemyImpactFeedback { Flash = 1, FlashRemaining = 1, FlashDuration = 1,
                Intensity = 1, Squash = 1, VisualRemaining = 1, VisualDuration = 1 });
            em.SetComponentData(owner, new EnemyContactAttemptState { IsWindingUp = 1 });
            em.AddComponentData(visual, new EnemyVisualOwner { Value = owner, UsesAtlas = 1, AnimationDriven = 1 });
            em.AddComponentData(visual, new Unity.Rendering.URPMaterialPropertyBaseColor { Value = new float4(2) });
            em.AddComponentData(visual, new EnemyVisualDeformation());
            var original = float4x4.TRS(new float3(2, 3, 4), quaternion.identity, new float3(2));
            em.AddComponentData(visual, new LocalToWorld { Value = original });
            world.GetOrCreateSystem<EnemyReadabilitySystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyImpactVisualSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<Unity.Rendering.URPMaterialPropertyBaseColor>(visual).Value, Is.EqualTo(new float4(1)));
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(original));
        }

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
        public void Enemy005DasherRollTracksChargeAndDashThenRecoversWithoutPhysicsWrites()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Dasher;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new DasherState { Phase = DasherPhase.Positioning });
            em.AddComponentData(owner, new DasherSettings { TelegraphDuration = 2, MaximumDistance = 20, RecoveryDuration = 1 });

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary Dasher.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(2f), "Run Forward In Place should drive fast movement.");

            em.SetComponentData(owner, new DasherState { Phase = DasherPhase.Preparing, SecondsRemaining = 1 });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(3, .5f, 0)), "Roll Forward In Place follows the telegraph clock.");
            var dash = new DasherState { Phase = DasherPhase.Dashing, DashDistanceTravelled = 10 };
            em.SetComponentData(owner, dash);
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(4, .5f, 0)), "Roll Forward In Place spans the committed dash distance.");
            Tick(.07f);
            Assert.That(Pose.y, Is.EqualTo(.5f), "Animation cannot run ahead of the gameplay dash.");
            dash.DashDistanceTravelled = 20;
            em.SetComponentData(owner, dash);
            Tick();
            Assert.That(Pose.y, Is.EqualTo(1f), "Roll Forward In Place holds its final sample instead of looping.");
            em.SetComponentData(owner, new DasherState { Phase = DasherPhase.Recovering, SecondsRemaining = .5f });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(8, .5f, 0)), "Look Around communicates dash recovery.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(9f, 0f, 0f)), "Launching restarts Roll Forward In Place.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Death should start at its first frame.");
            Tick();
            Assert.That(Pose.y, Is.EqualTo(0.25f).Within(0.001f), "Death should advance and then hold its final frame.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner), Is.EqualTo(default(PhysicsVelocity)));
            Assert.That(em.GetComponentData<DasherState>(owner).Phase, Is.EqualTo(DasherPhase.Recovering));
        }

        [Test]
        public void Enemy013BombUsesAuthoredMotionWithoutChangingDetonationState()
        {
            var em = world.EntityManager;
            EnemyAnimation animation = em.GetComponentData<EnemyAnimation>(visual);
            animation.Profile = (byte)EnemyAnimationProfile.Explosive;
            em.SetComponentData(visual, animation);
            em.AddComponentData(owner, new ExplosiveEnemyState());
            em.AddComponentData(owner, new EnemyContactAttemptState());

            Tick();
            Assert.That(Pose.x, Is.Zero, "Idle should drive a stationary Explosive.");

            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 4f });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(1f), "Walk should drive movement.");

            em.SetComponentData(owner, new EnemyContactAttemptState { IsAttempting = 1 });
            Tick();
            Assert.That(Pose.x, Is.EqualTo(2), "Contact pursuit uses Going To Explode without a timed fuse.");
            Assert.That(em.GetComponentData<ExplosiveEnemyState>(owner).HasExploded, Is.Zero);
            Assert.That(em.GetComponentData<EnemyLaunchState>(owner).Phase, Is.EqualTo(EnemyLaunchPhase.Active));

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(9f, 0f, 0f)), "Launch starts on the first Take Damage frame.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            em.SetComponentData(owner, new DesiredMovement());
            Tick();
            Assert.That(Pose.x, Is.EqualTo(6), "Recovery uses the authored Spawn clip.");
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .25f });
            Tick();
            Assert.That(Pose.y, Is.EqualTo(.5f).Within(.001f), "Recovery follows the gameplay clock.");

            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(8f, 0f, 0f)), "An ordinary defeat uses Die without displaying an explosion.");
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(owner, new EnemyContactAttemptState());
            Tick();
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            em.SetComponentData(owner, new ExplosiveEnemyState { HasExploded = 1 });
            Tick();
            Assert.That(Pose, Is.EqualTo(new float3(10f, 0f, 0f)), "Actual detonation uses Explode.");
            Tick(2);
            Assert.That(Pose.y, Is.EqualTo(1), "The one-shot explosion holds its final pose.");
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
            Tick();
            if (profile == EnemyAnimationProfile.Dasher)
                em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .25f });
            Tick(.25f);
            Assert.That(Pose.x, Is.EqualTo(profile == EnemyAnimationProfile.Explosive || profile == EnemyAnimationProfile.Dasher ? 6 : 0).Within(.001f),
                "Recovering uses an authored recovery or Idle, never the terminal death pose.");
            Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c1.z, Is.EqualTo(0).Within(.001f));
        }

        [Test]
        public void Info004ExploderPrefabAndSamplesPreserveUprightIdle()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyExplosive.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                    "Assets/Bomb Cute Series/Prefabs/Bomb.prefab");
                var configuredScale = animator.transform.localScale;
                var sourceScale = source.transform.localScale;
                float authoredSize = configuredScale.x / sourceScale.x;
                Assert.That(configuredScale.y / sourceScale.y, Is.EqualTo(authoredSize).Within(.001f));
                Assert.That(configuredScale.z / sourceScale.z, Is.EqualTo(authoredSize).Within(.001f),
                    "Inspector-authored uniform sizing may vary; source proportions must be preserved.");
                foreach (var authoring in root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>())
                {
                    var renderer = authoring.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                    Assert.That(renderer.name, Is.EqualTo("Bomb"));
                    Assert.That(renderer.bones.Length, Is.EqualTo(6));
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                    Assert.That(renderer.sharedMaterial.GetFloat("_ExploderPulse"), Is.Zero);
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMaterial.GetTexture("_ColorMap")),
                        Is.EqualTo("Assets/Bomb Cute Series/Textures/Bomb.psd"));
                    Assert.That(renderer.localBounds.center.magnitude, Is.LessThan(4f),
                        "Bounds must cover the authored Bomb poses without inheriting FBX scale twice.");
                    Assert.That(renderer.localBounds.extents.magnitude, Is.LessThan(5f));
                    Assert.That(authoring.Profile, Is.EqualTo(EnemyAnimationProfile.Explosive));
                    var vertices = renderer.sharedMesh.vertices;
                    var weights = renderer.sharedMesh.boneWeights;
                    var bindposes = renderer.sharedMesh.bindposes;
                    float floor = float.MaxValue;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        var w = weights[vertex];
                        var v = vertices[vertex];
                        UnityEngine.Vector3 p = renderer.bones[w.boneIndex0].localToWorldMatrix.MultiplyPoint3x4(bindposes[w.boneIndex0].MultiplyPoint3x4(v)) * w.weight0
                            + renderer.bones[w.boneIndex1].localToWorldMatrix.MultiplyPoint3x4(bindposes[w.boneIndex1].MultiplyPoint3x4(v)) * w.weight1
                            + renderer.bones[w.boneIndex2].localToWorldMatrix.MultiplyPoint3x4(bindposes[w.boneIndex2].MultiplyPoint3x4(v)) * w.weight2
                            + renderer.bones[w.boneIndex3].localToWorldMatrix.MultiplyPoint3x4(bindposes[w.boneIndex3].MultiplyPoint3x4(v)) * w.weight3;
                        floor = UnityEngine.Mathf.Min(floor, root.transform.InverseTransformPoint(p).y);
                    }
                    Assert.That(floor, Is.GreaterThanOrEqualTo(-1.005f),
                        "The upright Bomb must clear the capsule floor while retaining its Inspector-authored offset.");
                    using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                    string currentHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.That(reader.ReadString(), Is.EqualTo(currentHash), "Stale samples prevent the animation baker from attaching playback.");
                    int bones = reader.ReadInt32();
                    int frames = reader.ReadInt32();
                    for (int bone = 0; bone < bones; bone++) reader.ReadString();
                    for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                    long matrixStart = reader.BaseStream.Position;
                    for (int bone = 0; bone < bones; bone++)
                    {
                        var idle = animator.transform.worldToLocalMatrix * renderer.bones[bone].localToWorldMatrix * renderer.sharedMesh.bindposes[bone];
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++)
                                Assert.That(reader.ReadSingle(), Is.EqualTo(idle[row, column]).Within(.001f),
                                    "The saved skeleton must match upright Idle, not the final sampled Death pose.");
                    }
                    foreach (int motion in new[] { 0, 1, 2, 5, 6, 8, 9, 10 })
                    {
                        reader.BaseStream.Position = matrixStart + motion * frames * bones * 48;
                        byte[] first = reader.ReadBytes(bones * 48);
                        reader.BaseStream.Position = matrixStart + (motion * frames + frames / 2) * bones * 48;
                        Assert.That(reader.ReadBytes(bones * 48), Is.Not.EqualTo(first),
                            $"Bomb motion {motion} must contain animated poses.");
                    }
                }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Info004BombDeathSamplesStayAboveFloorIncludingInterpolatedPoses()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyExplosive.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var renderer = root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
                var authoring = renderer.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
                var collider = root.GetComponent<UnityEngine.CapsuleCollider>();
                float floor = collider.center.y - collider.height * .5f;
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                var toOwner = root.transform.worldToLocalMatrix * animator.transform.localToWorldMatrix;
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                reader.ReadInt32(); reader.ReadString();
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int bone = 0; bone < bones; bone++) reader.ReadString();
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
                long start = reader.BaseStream.Position;
                foreach (int motion in new[] { 8, EnemyAnimationSamples.ImpactMotion })
                {
                    reader.BaseStream.Position = start + motion * frames * bones * 48;
                    var poses = new UnityEngine.Matrix4x4[frames * bones];
                    for (int i = 0; i < poses.Length; i++)
                    {
                        var matrix = UnityEngine.Matrix4x4.identity;
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++) matrix[row, column] = reader.ReadSingle();
                        poses[i] = matrix;
                    }
                    string clipName = motion == 8 ? "Die" : "Explode";
                    var clip = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                        $"Assets/Bomb Cute Series/FBX/Bomb@{clipName}.FBX");
                    UnityEngine.AnimationClip sourceClip = null;
                    foreach (var asset in clip)
                        if (asset is UnityEngine.AnimationClip candidate && !candidate.name.StartsWith("__"))
                            sourceClip = candidate;
                    Assert.That(sourceClip, Is.Not.Null);
                    var bindposes = renderer.sharedMesh.bindposes;
                    for (int frame = 0; frame < frames; frame++)
                    {
                        sourceClip.SampleAnimation(animator.gameObject, sourceClip.length * frame / (frames - 1));
                        for (int bone = 0; bone < bones; bone++)
                        {
                            var authored = animator.transform.worldToLocalMatrix
                                * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                            for (int column = 0; column < 3; column++)
                                for (int row = 0; row < 3; row++)
                                    Assert.That(poses[frame * bones + bone][row, column],
                                        Is.EqualTo(authored[row, column]).Within(.001f),
                                        "Floor correction must preserve authored rotation, scale and proportions.");
                        }
                    }
                    for (int frame = 0; frame < frames; frame++)
                    for (int step = 0; step < 4; step++)
                    {
                        float blend = step * .25f;
                        int next = UnityEngine.Mathf.Min(frame + 1, frames - 1);
                        float minimum = float.MaxValue;
                        for (int vertex = 0; vertex < vertices.Length; vertex++)
                        {
                            var w = weights[vertex];
                            var v = vertices[vertex];
                            var a = poses[frame * bones + w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                + poses[frame * bones + w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                + poses[frame * bones + w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                + poses[frame * bones + w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                            var b = poses[next * bones + w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                + poses[next * bones + w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                + poses[next * bones + w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                + poses[next * bones + w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                            minimum = UnityEngine.Mathf.Min(minimum, toOwner.MultiplyPoint3x4(UnityEngine.Vector3.Lerp(a, b, blend)).y);
                        }
                        Assert.That(minimum, Is.GreaterThanOrEqualTo(floor - .001f),
                            $"Bomb death motion {motion}, frame {frame}, blend {blend} penetrates the floor.");
                    }
                }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(DasherPhase.Preparing)]
        [TestCase(DasherPhase.Dashing)]
        public void Info004DasherRollInterpolationPreservesBoneProportions(DasherPhase phase)
        {
            samples.Dispose();
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<EnemyAnimationSamples>();
            root.BoneCount = 1;
            root.FrameCount = 2;
            var durations = builder.Allocate(ref root.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref root.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
            {
                durations[motion] = 1;
                var rotation = new float3x3(quaternion.RotateX(math.radians(170)));
                matrices[motion * 2] = new float3x4(float3x3.identity.c0, float3x3.identity.c1, float3x3.identity.c2, float3.zero);
                matrices[motion * 2 + 1] = new float3x4(rotation.c0, rotation.c1, rotation.c2, float3.zero);
            }
            samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            var em = world.EntityManager;
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples, Profile = (byte)EnemyAnimationProfile.Dasher });
            em.AddComponentData(owner, new DasherSettings { TelegraphDuration = 2, MaximumDistance = 20 });
            em.AddComponentData(owner, new DasherState { Phase = phase, SecondsRemaining = 1, DashDistanceTravelled = 10 });
            Tick();
            var pose = em.GetBuffer<SkinMatrix>(visual)[0].Value;
            Assert.That(math.length(pose.c0), Is.EqualTo(1).Within(.0001f));
            Assert.That(math.length(pose.c1), Is.EqualTo(1).Within(.0001f), "Roll interpolation must not squash the bone.");
            Assert.That(math.length(pose.c2), Is.EqualTo(1).Within(.0001f));
            Assert.That(math.dot(pose.c1, pose.c2), Is.EqualTo(0).Within(.0001f));
            Assert.That(math.determinant(new float3x3(pose.c0, pose.c1, pose.c2)), Is.EqualTo(1).Within(.0001f));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [Test]
        public void Info004DasherActiveAnimationPreservesAuthoredScaleAndGroundClearance()
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyDasher.prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(CrowdPunch.Editor.EnemyDasherPrefabBuilder.ModelPath);
                Assert.That(animator.transform.localScale, Is.EqualTo(source.transform.localScale));
                Assert.That(root.transform.localScale, Is.EqualTo(UnityEngine.Vector3.one));
                foreach (var transform in animator.GetComponentsInChildren<UnityEngine.Transform>(true))
                {
                    string path = UnityEditor.AnimationUtility.CalculateTransformPath(transform, animator.transform);
                    var original = string.IsNullOrEmpty(path) ? source.transform : source.transform.Find(path);
                    Assert.That(original, Is.Not.Null, path);
                    Assert.That(UnityEngine.Vector3.Distance(transform.localScale, original.localScale),
                        Is.LessThan(.0001f), "Prefab scale changed at " + path);
                }
                var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
                foreach (var state in controller.layers[0].stateMachine.states)
                    if (state.state.name == "Motion3" || state.state.name == "Motion4" || state.state.name == "Motion9")
                        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(state.state.motion),
                            Does.EndWith("Goblin Minion@Roll Forward In Place.FBX"));
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
                for (int pose = 0; pose < frames * EnemyAnimationSamples.MotionCount; pose++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        matrices[bone] = UnityEngine.Matrix4x4.identity;
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                        for (int column = 0; column < 3; column++)
                            Assert.That(((UnityEngine.Vector3)matrices[bone].GetColumn(column)).magnitude,
                                Is.EqualTo(1).Within(.0001f), "Exported bones must retain prefab proportions.");
                    }
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        var weight = weights[index]; var point = vertices[index];
                        var position = matrices[weight.boneIndex0].MultiplyPoint3x4(point) * weight.weight0
                            + matrices[weight.boneIndex1].MultiplyPoint3x4(point) * weight.weight1
                            + matrices[weight.boneIndex2].MultiplyPoint3x4(point) * weight.weight2
                            + matrices[weight.boneIndex3].MultiplyPoint3x4(point) * weight.weight3;
                        position = root.transform.InverseTransformPoint(animator.transform.TransformPoint(position));
                        if (pose / frames != 7)
                            Assert.That(position.y, Is.GreaterThanOrEqualTo(-1.01f), "Only Spawn may go below the capsule floor.");
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
                    bool authoredGoblin = authoring.Profile == EnemyAnimationProfile.Goblin;
                    foreach (int motion in authoredGoblin ? new[] { 0, 1, 2, 5, 6, 9, 10 } : new[] { 0, EnemyAnimationSamples.FlyingMotion })
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
                            for (int angle = 0; angle < (authoredGoblin ? 1 : 25); angle++)
                            {
                                // Goblins launch at +90 degrees; their hit/recovery/death clips remain upright.
                                var rotation = UnityEngine.Quaternion.Euler(authoredGoblin
                                    ? motion == EnemyAnimationSamples.FlyingMotion ? 90f : 0f
                                    : -118.65f * angle / 24f, 0, 0);
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
