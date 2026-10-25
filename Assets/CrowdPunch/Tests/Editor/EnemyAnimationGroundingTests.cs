using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyAnimationGroundingTests
    {
        private World world;
        private Entity root, owner, visual, renderer;
        private BlobAssetReference<EnemyGroundingMesh> mesh;

        [SetUp]
        public void SetUp()
        {
            world = new World("Trail mesh clearance");
            var em = world.EntityManager;
            root = em.CreateEntity(typeof(LocalToWorld));
            owner = em.CreateEntity(typeof(LocalTransform), typeof(Unity.Physics.PhysicsVelocity), typeof(EnemyLaunchState));
            em.SetComponentData(owner, LocalTransform.Identity);
            visual = em.CreateEntity(typeof(EnemyAnimationGrounding));
            em.AddBuffer<SkinMatrix>(visual);
            renderer = em.CreateEntity(typeof(EnemyGroundingVisual), typeof(RenderBounds));
            em.SetComponentData(renderer, new EnemyGroundingVisual
            {
                Animation = visual, AuthoredBounds = new AABB { Center = new float3(1, 2, 3), Extents = new float3(4) }
            });
            em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = .7f });
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            if (mesh.IsCreated) mesh.Dispose();
        }

        private void SetMesh(UnityEngine.Vector3[] vertices, UnityEngine.BoneWeight[] weights, float clearance)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyGroundingMesh>();
            var output = builder.Allocate(ref data.Vertices, vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                output[i] = new EnemyGroundingVertex { Position = vertices[i],
                    Bones = new int4(w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3),
                    Weights = new float4(w.weight0, w.weight1, w.weight2, w.weight3) };
            }
            mesh = builder.CreateBlobAssetReference<EnemyGroundingMesh>(Allocator.Persistent);
            world.EntityManager.SetComponentData(visual, new EnemyAnimationGrounding
                { Root = root, Owner = owner, Mesh = mesh, Clearance = clearance });
        }

        private void Tick()
        {
            world.GetOrCreateSystem<EnemyAnimationGroundingSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }

        private float Bottom()
        {
            var em = world.EntityManager;
            float4x4 matrix = em.GetComponentData<LocalToWorld>(root).Value;
            var skin = em.GetBuffer<SkinMatrix>(visual);
            float bottom = float.PositiveInfinity;
            for (int i = 0; i < mesh.Value.Vertices.Length; i++)
            {
                var v = mesh.Value.Vertices[i];
                float4 p = new float4(v.Position, 1);
                float3 point = math.mul(skin[v.Bones.x].Value, p) * v.Weights.x
                    + math.mul(skin[v.Bones.y].Value, p) * v.Weights.y
                    + math.mul(skin[v.Bones.z].Value, p) * v.Weights.z
                    + math.mul(skin[v.Bones.w].Value, p) * v.Weights.w;
                bottom = math.min(bottom, math.transform(matrix, point).y);
            }
            return bottom;
        }

        [TestCase(1f, 0f)]
        [TestCase(3f, .1f)]
        [TestCase(3f, .35f)]
        public void Trail006ClearanceUsesWorldMetresPreservesScalePhysicsAndCulling(float scale, float clearance)
        {
            var em = world.EntityManager;
            var transform = float4x4.TRS(new float3(2, -2, 4), quaternion.RotateZ(.3f), new float3(scale, scale * 2, scale));
            em.SetComponentData(root, new LocalToWorld { Value = transform });
            SetMesh(new[] { new UnityEngine.Vector3(0, -.4f, 0), new UnityEngine.Vector3(1, 2, 0) },
                new[] { new UnityEngine.BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = .25f, weight1 = .75f },
                    new UnityEngine.BoneWeight { boneIndex0 = 1, weight0 = 1 } }, clearance);
            var a = new float3x4(new float3(2, 0, 0), new float3(0, 3, 0), new float3(0, 0, 4), new float3(0, -2, 0));
            var b = a; b.c3.y = -3;
            var skin = em.GetBuffer<SkinMatrix>(visual);
            skin.Add(new SkinMatrix { Value = a }); skin.Add(new SkinMatrix { Value = b });
            Tick();
            Assert.That(Bottom(), Is.EqualTo(.7f + clearance).Within(.00001f));
            skin = em.GetBuffer<SkinMatrix>(visual);
            Assert.That(skin[0].Value.c0, Is.EqualTo(a.c0));
            Assert.That(skin[0].Value.c1, Is.EqualTo(a.c1));
            Assert.That(skin[0].Value.c2, Is.EqualTo(a.c2));
            Assert.That(skin[1].Value.c3 - skin[0].Value.c3, Is.EqualTo(b.c3 - a.c3));
            Assert.That(em.GetComponentData<LocalToWorld>(root).Value, Is.EqualTo(transform));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(em.GetComponentData<Unity.Physics.PhysicsVelocity>(owner), Is.EqualTo(default(Unity.Physics.PhysicsVelocity)));
            var offset = em.GetComponentData<EnemyAnimationGrounding>(visual).Offset;
            Assert.That(em.GetComponentData<RenderBounds>(renderer).Value.Center, Is.EqualTo(new float3(1, 2, 3) + offset));
            Assert.That(em.GetComponentData<RenderBounds>(renderer).Value.Extents, Is.EqualTo(new float3(4)));
            // Re-applying the same raw pose has no accumulating lift or culling-bound drift.
            skin[0] = new SkinMatrix { Value = a }; skin[1] = new SkinMatrix { Value = b };
            Tick();
            Assert.That(Bottom(), Is.EqualTo(.7f + clearance).Within(.00001f));
            Assert.That(em.GetComponentData<EnemyAnimationGrounding>(visual).Offset, Is.EqualTo(offset));
            transform.c3.y += 100;
            em.SetComponentData(root, new LocalToWorld { Value = transform });
            skin = em.GetBuffer<SkinMatrix>(visual);
            skin[0] = new SkinMatrix { Value = a }; skin[1] = new SkinMatrix { Value = b };
            Tick();
            Assert.That(Bottom(), Is.EqualTo(.7f + clearance).Within(.0001f), "Grounded poses must not hover above the configured gap.");
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            skin = em.GetBuffer<SkinMatrix>(visual);
            skin[0] = new SkinMatrix { Value = a }; skin[1] = new SkinMatrix { Value = b };
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationGrounding>(visual).Offset, Is.EqualTo(float3.zero), "Airborne bodies keep their physical height.");
        }

        [Test]
        public void Trail006EverySquidPoseIncludingSpawnAndInterpolationClearsFloorAtCurrentPrefabScale()
        {
            var prefab = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyTrail.prefab");
            try
            {
                var animator = prefab.GetComponentInChildren<UnityEngine.Animator>();
                var r = prefab.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
                var authoring = r.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
                SetMesh(r.sharedMesh.vertices, r.sharedMesh.boneWeights, authoring.GroundClearance);
                var em = world.EntityManager;
                em.SetComponentData(root, new LocalToWorld { Value = prefab.transform.worldToLocalMatrix * animator.transform.localToWorldMatrix });
                var method = typeof(EnemyAnimationSystem).GetNestedType("EnemyPosePlaybackJob", System.Reflection.BindingFlags.NonPublic)
                    .GetMethod("BlendPose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                reader.ReadInt32(); reader.ReadString();
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int i = 0; i < bones; i++) reader.ReadString();
                for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) reader.ReadSingle();
                var samples = new float3x4[EnemyAnimationSamples.MotionCount * frames * bones];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = new float3x4(new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                em.GetBuffer<SkinMatrix>(visual).ResizeUninitialized(bones);
                float lowest = float.PositiveInfinity;
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
                for (int frame = 0; frame < frames; frame++)
                for (int step = 0; step < 3; step++)
                {
                    int next = motion <= 2 ? (frame + 1) % frames : math.min(frame + 1, frames - 1);
                    var skin = em.GetBuffer<SkinMatrix>(visual);
                    for (int bone = 0; bone < bones; bone++)
                        skin[bone] = new SkinMatrix { Value = (float3x4)method.Invoke(null, new object[] {
                            samples[(motion * frames + frame) * bones + bone], samples[(motion * frames + next) * bones + bone], step / 3f, true }) };
                    Tick();
                    float bottom = Bottom();
                    Assert.That(bottom, Is.GreaterThanOrEqualTo(.7f + authoring.GroundClearance - .00001f), $"Motion {motion}, frame {frame}, step {step}");
                    lowest = math.min(lowest, bottom);
                }
                Assert.That(lowest, Is.EqualTo(.7f + authoring.GroundClearance).Within(.00001f));
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
