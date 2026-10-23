using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Editor;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class GatekeeperHandFitTests
    {
        [TestCase(0, 1f, 0f, 1f)] [TestCase(1, 1f, 0f, 1f)] [TestCase(2, 1f, 0f, 1f)]
        [TestCase(0, .6f, 0f, 1f)] [TestCase(1, 1.8f, 0f, 1f)] [TestCase(0, 1f, 3.5f, 1f)]
        [TestCase(0, 1f, 0f, 1.5f)] [TestCase(1, 1f, 0f, 2f)]
        public void Boss008ConstantSizedAnimatedHandsStayInsideBarrierAboveGround(int sceneIndex, float resize, float raisedFloor, float fistScale)
        {
            var scene = EditorSceneManager.OpenPreviewScene(GatekeeperMagmaBuilder.Scenes[sceneIndex]);
            try
            {
                var encounter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEncounterAuthoring>()).Single();
                var authoring = encounter.GetComponentInChildren<GatekeeperRigAuthoring>();
                var renderer = authoring.GetComponent<SkinnedMeshRenderer>();
                var animator = authoring.GetComponentInParent<Animator>();
                var bones = renderer.bones;
                var leftRoot = bones.First(b => b.name == "RigLArm1");
                var rightRoot = bones.First(b => b.name == "RigRArm1");
                byte[] sides = bones.Select(b => (byte)(b == leftRoot || b.IsChildOf(leftRoot) ? 1 : b == rightRoot || b.IsChildOf(rightRoot) ? 2 : 0)).ToArray();
                int lp = Array.FindIndex(bones, b => b.name == "RigLArmPalm"), rp = Array.FindIndex(bones, b => b.name == "RigRArmPalm");
                var mesh = renderer.sharedMesh;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var asset = authoring.GetComponent<EnemyAnimationAuthoring>().Samples;
                var samples = ReadSamples(asset);
                var bounds = GatekeeperHandGeometry.CreateBounds(mesh, sides, lp, rp, asset);
                try
                {
                    using var world = new World("Gatekeeper actual mesh clearance");
                    var em = world.EntityManager;
                    var left = em.CreateEntity(typeof(BossHand), typeof(BossPart), typeof(LocalToWorld), typeof(PhysicsVelocity));
                    var right = em.CreateEntity(typeof(BossHand), typeof(BossPart), typeof(LocalToWorld), typeof(PhysicsVelocity));
                    em.SetComponentData(left, new BossPart { Radius = encounter.leftHand.radius });
                    em.SetComponentData(right, new BossPart { Radius = encounter.rightHand.radius });
                    var boss = em.CreateEntity(typeof(BossEncounter));
                    var tuning = encounter.settings.Bake();
                    float leftSize = fistScale, rightSize = fistScale > 1 ? fistScale + .25f : 1;
                    var model = em.CreateEntity(typeof(LocalToWorld));
                    var visual = em.CreateEntity(typeof(EnemyAnimationPlayback));
                    var arena = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaAuthoring>()).Single();
                    float floor = arena.GroundSurfaceHeight + raisedFloor + authoring.groundClearance;
                    em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = arena.GroundSurfaceHeight + raisedFloor });
                    em.AddComponentData(visual, new EnemyAnimation { Owner = boss, Samples = samples });
                    em.AddComponentData(visual, new GatekeeperRig { Model = model, LeftPalm = lp, RightPalm = rp,
                        LeftBindPalm = mesh.bindposes[lp].inverse.MultiplyPoint3x4(Vector3.zero),
                        RightBindPalm = mesh.bindposes[rp].inverse.MultiplyPoint3x4(Vector3.zero), Bounds = bounds, GroundClearance = authoring.groundClearance,
                        FistScale = new float2(leftSize,rightSize) });
                    var mask = em.AddBuffer<GatekeeperArmBone>(visual);
                    foreach (byte side in sides) mask.Add(new GatekeeperArmBone { Side = side });
                    var skin = em.AddBuffer<SkinMatrix>(visual);
                    for (int i = 0; i < bones.Length; i++) skin.Add(new SkinMatrix { Value = samples.Value.Matrices[i] });
                    var system = world.GetOrCreateSystem<GatekeeperArmAnimationSystem>();
                    float largestRadius = 0;
                    int steps = samples.Value.FrameCount * 2 - 1;
                    for (int segment = 0; segment < 15; segment++)
                    for (int step = 0; step < steps; step++)
                    {
                        float progress = step / (float)(steps - 1);
                        var lh = Pose(segment, progress); var rh = Pose(14 - segment, progress);
                        var cycle = segment == 1 ? BossCycle.Relocating : segment == 13 ? BossCycle.Transition : segment == 14 ? BossCycle.Defeated : BossCycle.Opening;
                        em.SetComponentData(boss, new BossEncounter { LeftHand = left, RightHand = right, Cycle = cycle });
                        em.SetComponentData(visual, new EnemyAnimationPlayback { Phase = progress });
                        em.SetComponentData(left, lh); em.SetComponentData(right, rh);
                        float height = segment >= 3 && segment <= 8 ? 6 * (1 - progress) : 0;
                        float3 lpos = new float3(-7 + progress * 8, encounter.settings.handHeight + height, segment - 8);
                        float3 rpos = new float3(7 - progress * 6, encounter.settings.handHeight, 8 - segment);
                        lpos.xz = tuning.Center + math.normalizesafe(lpos.xz) * (tuning.HandCourtRadius - encounter.leftHand.radius * math.max(1,leftSize) - .1f);
                        rpos.xz = tuning.Center + math.normalizesafe(rpos.xz) * (tuning.HandCourtRadius - encounter.rightHand.radius * math.max(1,rightSize) - .1f);
                        em.SetComponentData(left, new LocalToWorld { Value = float4x4.Translate(lpos) });
                        em.SetComponentData(right, new LocalToWorld { Value = float4x4.Translate(rpos) });
                        var modelWorld = float4x4.TRS(new float3(3, animator.transform.position.y, -6),
                            quaternion.RotateY(segment * .7f + progress), (float3)animator.transform.lossyScale * resize);
                        em.SetComponentData(model, new LocalToWorld { Value = modelWorld });
                        system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                        skin = em.GetBuffer<SkinMatrix>(visual);
                        float lowest = float.MaxValue, excessRadius = float.MinValue;
                        float farthest = 0;
                        bool finite = true;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var w = weights[i]; byte side = sides[w.boneIndex0];
                            if (side == 0) continue;
                            float3 v = vertices[i];
                            float3 p = Point(skin[w.boneIndex0].Value, v) * w.weight0 + Point(skin[w.boneIndex1].Value, v) * w.weight1
                                + Point(skin[w.boneIndex2].Value, v) * w.weight2 + Point(skin[w.boneIndex3].Value, v) * w.weight3;
                            p = math.transform(modelWorld, p);
                            finite &= math.all(math.isfinite(p));
                            lowest = math.min(lowest, p.y);
                            float radius = math.distance(p.xz, (side == 1 ? lpos : rpos).xz);
                            excessRadius = math.max(excessRadius, radius - (side == 1 ? encounter.leftHand.radius * leftSize : encounter.rightHand.radius * rightSize));
                            farthest = math.max(farthest,math.distance(p.xz,tuning.Center));
                            largestRadius = math.max(largestRadius, radius);
                        }
                        Assert.That(finite, Is.True);
                        Assert.That(lowest, Is.GreaterThanOrEqualTo(floor - .0001f), $"scene {sceneIndex}, segment {segment}, step {step}");
                        Assert.That(excessRadius, Is.LessThanOrEqualTo(.0001f));
                        Assert.That(farthest,Is.LessThanOrEqualTo(tuning.HandCourtRadius+.0001f));
                        Assert.That(em.GetComponentData<LocalToWorld>(left).Position, Is.EqualTo(lpos));
                        Assert.That(em.GetComponentData<LocalToWorld>(right).Position, Is.EqualTo(rpos));
                        Assert.That(em.GetComponentData<PhysicsVelocity>(left).Linear, Is.EqualTo(float3.zero));
                        Assert.That(em.GetComponentData<PhysicsVelocity>(right).Linear, Is.EqualTo(float3.zero));
                    }
                    Assert.That(largestRadius, Is.GreaterThan(2.2f), "The fists must fill the original damage footprint, not become tiny to clear the floor.");
                }
                finally { samples.Dispose(); bounds.Dispose(); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void Boss005BarrierEnvelopeUsesManualFistSizesWithoutChangingColliderRadii()
        {
            using var world = new World("Gatekeeper manual size baking");
            var em = world.EntityManager;
            var left = em.CreateEntity(typeof(BossPart)); var right = em.CreateEntity(typeof(BossPart));
            em.SetComponentData(left,new BossPart { Radius=2.6f }); em.SetComponentData(right,new BossPart { Radius=2.6f });
            var owner = em.CreateEntity(typeof(BossEncounter),typeof(BossTuning));
            em.SetComponentData(owner,new BossEncounter { LeftHand=left,RightHand=right });
            em.SetComponentData(owner,new BossTuning { HandCourtRadius=19 });
            var visual=em.CreateEntity(typeof(GatekeeperRig),typeof(EnemyAnimation));
            em.SetComponentData(visual,new GatekeeperRig { FistScale=new float2(1.5f,2f) });
            em.SetComponentData(visual,new EnemyAnimation { Owner=owner });
            world.GetOrCreateSystem<GatekeeperHandFootprintBakingSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<GatekeeperHandFootprint>(left).Radius,Is.EqualTo(3.95f).Within(.0001f));
            Assert.That(em.GetComponentData<GatekeeperHandFootprint>(right).Radius,Is.EqualTo(5.25f).Within(.0001f));
            Assert.That(em.GetComponentData<BossPart>(left).Radius,Is.EqualTo(2.6f));
            Assert.That(em.GetComponentData<BossPart>(right).Radius,Is.EqualTo(2.6f));
        }

        private static BossHand Pose(int segment, float progress)
        {
            return new BossHand { Duration = 1, Remaining = 1 - progress,
                Phase = segment < 2 || segment > 12 ? BossHandPhase.Ready : segment == 2 ? BossHandPhase.Shielding
                    : segment < 6 ? BossHandPhase.Anticipation : segment < 9 ? BossHandPhase.Active
                    : segment < 12 ? BossHandPhase.Recovery : BossHandPhase.Staggered,
                Attack = (BossAttack)((segment - 3 + 15) % 3) };
        }

        private static float3 Point(float3x4 matrix, float3 p) => matrix.c0 * p.x + matrix.c1 * p.y + matrix.c2 * p.z + matrix.c3;

        private static BlobAssetReference<EnemyAnimationSamples> ReadSamples(TextAsset asset)
        {
            using var reader = new BinaryReader(new MemoryStream(asset.bytes));
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133)); reader.ReadString();
            int bones = reader.ReadInt32(), frames = reader.ReadInt32();
            for (int i = 0; i < bones; i++) reader.ReadString();
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = bones; data.FrameCount = frames;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            for (int i = 0; i < durations.Length; i++) durations[i] = reader.ReadSingle();
            var matrices = builder.Allocate(ref data.Matrices, frames * bones * EnemyAnimationSamples.MotionCount);
            for (int i = 0; i < matrices.Length; i++) matrices[i] = new float3x4(Column(reader), Column(reader), Column(reader), Column(reader));
            return builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
        }

        private static float3 Column(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
