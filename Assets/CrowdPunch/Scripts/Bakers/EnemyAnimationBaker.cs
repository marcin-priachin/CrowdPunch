using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class EnemyAnimationBaker : Baker<EnemyAnimationAuthoring>
    {
        public override void Bake(EnemyAnimationAuthoring authoring)
        {
            var owner = GetComponentInParent<EnemyAuthoring>();
            var chicken = GetComponentInParent<ChickenBossAuthoring>();
            var rolling = GetComponentInParent<RollingBossAuthoring>();
            var dino = GetComponentInParent<DinoBossAuthoring>();
            var gatekeeper = GetComponentInParent<BossPartAuthoring>();
            var renderer = GetComponent<SkinnedMeshRenderer>();
            var animator = GetComponentInParent<Animator>();
            if (owner == null && chicken == null && rolling == null && dino == null && gatekeeper == null || animator == null || authoring.Samples == null)
                throw new System.InvalidOperationException("Enemy animation needs an enemy owner, Animator and generated samples.");

            DependsOn(authoring.Samples);
            DependsOn(renderer.sharedMesh);
            DependsOn(animator.runtimeAnimatorController);
            DependsOn(animator.avatar);
            DependsOn(animator.transform);
            using var reader = new BinaryReader(new MemoryStream(authoring.Samples.bytes));
            if (reader.ReadInt32() != 0x43504133)
                throw new InvalidDataException("Unsupported enemy animation samples. Run Crowd Punch/Animation/Rebuild Enemy Samples.");
            string sourceHash = reader.ReadString();
#if UNITY_EDITOR
            string currentHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
            if (sourceHash != currentHash)
                throw new InvalidDataException("Enemy animation sources changed. Run Crowd Punch/Animation/Rebuild Enemy Samples.");
#endif
            int bones = reader.ReadInt32();
            int frames = reader.ReadInt32();
            if (bones != renderer.bones.Length || frames < 2 || frames > 120 || renderer.rootBone != animator.transform)
                throw new InvalidDataException("Enemy animation skeleton/root does not match the sampled model.");
            for (int bone = 0; bone < bones; bone++)
                if (reader.ReadString() != renderer.bones[bone].name)
                    throw new InvalidDataException("Enemy animation bone order changed. Rebuild samples.");

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = bones;
            data.FrameCount = frames;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) durations[i] = reader.ReadSingle();
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * frames * bones);
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = new float3x4(ReadColumn(reader), ReadColumn(reader), ReadColumn(reader), ReadColumn(reader));
            var blob = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            var jog = ReadJogSamples(authoring, renderer, animator, bones);
            if (jog.IsCreated) AddBlobAsset(ref jog, out _);
            AddComponent(entity, new EnemyAnimation
            {
                Owner = gatekeeper != null ? GetEntity(gatekeeper, TransformUsageFlags.Dynamic) : dino != null ? GetEntity(dino, TransformUsageFlags.Dynamic) : rolling != null ? GetEntity(rolling, TransformUsageFlags.Dynamic) : chicken != null ? GetEntity(chicken, TransformUsageFlags.Dynamic) : GetEntity(owner, TransformUsageFlags.Dynamic),
                Samples = blob,
                JogSamples = jog,
                Profile = (byte)authoring.Profile,
                SpawnMotion = authoring.SpawnMotion,
                WorldToAnimationHeight = math.abs(animator.transform.InverseTransformVector(Vector3.up).y),
                BlendResponse = math.max(0f, authoring.BlendResponse),
                MaxLegAnimationSpeed = math.max(0f, authoring.MaxLegAnimationSpeed),
                LaunchPivot = authoring.RotateLaunchAroundOwner && owner != null
                    ? (float3)animator.transform.InverseTransformPoint(owner.transform.position
                        + owner.transform.up * authoring.LaunchPivotHeight) : float3.zero
            });
            AddComponent<EnemyAnimationPlayback>(entity);
            if (authoring.ProjectileOrigin != null && owner != null)
            {
                if (bones != 1)
                    throw new System.InvalidOperationException("Projectile origin requires a single-bone accessory renderer.");
                DependsOn(authoring.ProjectileOrigin);
                var volume = GetComponent<SphereCollider>(authoring.ProjectileOrigin);
                if (volume != null) DependsOn(volume);
                Vector3 center = volume != null ? volume.center : Vector3.zero;
                AddComponent(entity, new RangedProjectileOrigin
                {
                    Animation = entity,
                    Root = GetEntity(animator, TransformUsageFlags.Dynamic),
                    BindPosition = renderer.transform.InverseTransformPoint(authoring.ProjectileOrigin.TransformPoint(center)),
                    ModelToOwner = (float4x4)(owner.transform.worldToLocalMatrix * animator.transform.localToWorldMatrix),
                    ChargeRadius = volume != null
                        ? volume.radius * math.cmax(math.abs((float3)authoring.ProjectileOrigin.lossyScale)) : .13f
                });
            }
            if (owner != null)
            {
                using var trailBuilder = new BlobBuilder(Allocator.Temp);
                ref var trail = ref trailBuilder.ConstructRoot<EnemyGroundingMesh>();
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                int count = math.min(96, vertices.Length);
                var probes = trailBuilder.Allocate(ref trail.Vertices, count);
                for (int i = 0; i < count; i++)
                {
                    int index = i * (vertices.Length - 1) / math.max(1, count - 1);
                    var weight = weights[index];
                    probes[i] = new EnemyGroundingVertex { Position = vertices[index],
                        Bones = new int4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                        Weights = new float4(weight.weight0, weight.weight1, weight.weight2, weight.weight3) };
                }
                var probesBlob = trailBuilder.CreateBlobAssetReference<EnemyGroundingMesh>(Allocator.Persistent);
                AddBlobAsset(ref probesBlob, out _);
                AddComponent(entity, new EnemyTrailVisual { Owner = GetEntity(owner, TransformUsageFlags.Dynamic),
                    Root = GetEntity(animator, TransformUsageFlags.Dynamic), Probes = probesBlob });
            }
            if (owner != null && GetComponentInParent<LinkedPairAuthoring>() != null)
            {
                var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                int leftIndex = System.Array.IndexOf(renderer.bones, left);
                int rightIndex = System.Array.IndexOf(renderer.bones, right);
                // Equipment may contain only one hand; the body supplies both endpoints.
                if (leftIndex >= 0 && rightIndex >= 0)
                {
                    var leftMarker = left.Find("+ L Hand");
                    var rightMarker = right.Find("+ R Hand");
                    if (leftMarker == null || rightMarker == null)
                        throw new System.InvalidOperationException("LinkedPair requires + L Hand and + R Hand markers.");
                    DependsOn(leftMarker);
                    DependsOn(rightMarker);
                    AddComponent(entity, new LinkedPairHandAnchors
                    {
                        Owner = GetEntity(owner, TransformUsageFlags.Dynamic),
                        Root = GetEntity(animator, TransformUsageFlags.Dynamic),
                        LeftBone = leftIndex,
                        RightBone = rightIndex,
                        LeftBindPosition = renderer.sharedMesh.bindposes[leftIndex].inverse.MultiplyPoint3x4(left.InverseTransformPoint(leftMarker.position)),
                        RightBindPosition = renderer.sharedMesh.bindposes[rightIndex].inverse.MultiplyPoint3x4(right.InverseTransformPoint(rightMarker.position))
                    });
                }
            }
            if (authoring.Profile == EnemyAnimationProfile.Trail && owner != null)
            {
                using var groundingBuilder = new BlobBuilder(Allocator.Temp);
                ref var grounding = ref groundingBuilder.ConstructRoot<EnemyGroundingMesh>();
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                var destination = groundingBuilder.Allocate(ref grounding.Vertices, vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    var weight = weights[i];
                    destination[i] = new EnemyGroundingVertex
                    {
                        Position = vertices[i],
                        Bones = new int4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                        Weights = new float4(weight.weight0, weight.weight1, weight.weight2, weight.weight3)
                    };
                }
                var groundingMesh = groundingBuilder.CreateBlobAssetReference<EnemyGroundingMesh>(Allocator.Persistent);
                AddBlobAsset(ref groundingMesh, out _);
                AddComponent(entity, new EnemyAnimationGrounding
                {
                    Root = GetEntity(animator, TransformUsageFlags.Dynamic),
                    Owner = GetEntity(owner, TransformUsageFlags.Dynamic),
                    Clearance = math.max(0, authoring.GroundClearance),
                    Mesh = groundingMesh
                });
            }
            if (authoring.ArmorStagesRequired > 0 && owner != null)
            {
                AddComponent(entity, new EnemyArmorVisual
                {
                    Owner = GetEntity(owner, TransformUsageFlags.Dynamic),
                    Animation = entity,
                    StagesRequired = authoring.ArmorStagesRequired
                });
                if (bones != 1)
                    throw new System.InvalidOperationException("Removable armor requires a single-bone equipment renderer.");
                AddComponent(entity, new EnemyArmorDebris
                {
                    Root = GetEntity(animator, TransformUsageFlags.Dynamic),
                    MeshBounds = new AABB { Center = renderer.sharedMesh.bounds.center, Extents = renderer.sharedMesh.bounds.extents }
                });
            }
            if (chicken != null || dino != null || gatekeeper != null)
            {
                if (chicken != null) DependsOn(chicken.settings);
                if (dino != null) DependsOn(dino.settings);
                Transform root = gatekeeper != null ? gatekeeper.transform : dino != null ? dino.transform : chicken.transform;
                float halfHeight = gatekeeper != null ? gatekeeper.height * .5f : dino != null ? dino.settings.bodyHeight * .5f : chicken.settings.bodyHeight * .5f;
                AddComponent(entity, new BossAnimationAnchor { Floor = animator.transform.InverseTransformPoint(root.position - root.up * halfHeight) });
            }
            if (rolling != null) AddComponent<RollingAnimationState>(entity);
        }

        private BlobAssetReference<EnemyJogSamples> ReadJogSamples(EnemyAnimationAuthoring authoring,
            SkinnedMeshRenderer renderer, Animator animator, int boneCount)
        {
            if (authoring.JogSamples == null) return default;
            DependsOn(authoring.JogSamples);
            using var reader = new BinaryReader(new MemoryStream(authoring.JogSamples.bytes));
            int version = reader.ReadInt32();
            if (version != 0x43504A31 && version != 0x43504A32) throw new InvalidDataException("Unsupported Goblin jog samples.");
            string hash = reader.ReadString();
#if UNITY_EDITOR
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/CrowdPunch/Animation/EnemyMovement.controller");
            DependsOn(controller);
            string currentHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(controller)).ToString()
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
            if (hash != currentHash) throw new InvalidDataException("Goblin jog sources changed. Run Crowd Punch/Animation/Apply Goblin Jog Legs.");
#endif
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyJogSamples>();
            data.BoneCount = reader.ReadInt32();
            data.FrameCount = reader.ReadInt32();
            data.HipBoneIndex = reader.ReadInt32();
            int legCount = reader.ReadInt32();
            if (data.BoneCount != boneCount || data.FrameCount < 2 || data.FrameCount > 120
                || data.HipBoneIndex < 0 || data.HipBoneIndex >= boneCount || legCount < 1 || legCount > boneCount)
                throw new InvalidDataException("Goblin jog skeleton does not match the body.");
            data.HipBindInverse = new float4x4(new float4(ReadColumn(reader), reader.ReadSingle()),
                new float4(ReadColumn(reader), reader.ReadSingle()), new float4(ReadColumn(reader), reader.ReadSingle()),
                new float4(ReadColumn(reader), reader.ReadSingle()));
            data.HipBind = renderer.sharedMesh.bindposes[data.HipBoneIndex];
            var legs = builder.Allocate(ref data.LegBones, legCount);
            for (int i = 0; i < legCount; i++)
            {
                legs[i] = reader.ReadInt32();
                if (legs[i] < 0 || legs[i] >= boneCount || legs[i] == data.HipBoneIndex)
                    throw new InvalidDataException("Invalid Goblin jog leg mask.");
            }
            var durations = builder.Allocate(ref data.Durations, EnemyJogSamples.DirectionCount);
            for (int i = 0; i < durations.Length; i++) durations[i] = reader.ReadSingle();
            var distances = builder.Allocate(ref data.CycleDistances, EnemyJogSamples.DirectionCount);
            for (int i = 0; i < distances.Length; i++)
                distances[i] = authoring.JogCycleDistances != null && authoring.JogCycleDistances.Length == distances.Length
                    ? math.max(.01f, authoring.JogCycleDistances[i]) : 2f;
            var matrices = builder.Allocate(ref data.Matrices, EnemyJogSamples.DirectionCount * data.FrameCount * legCount);
            for (int i = 0; i < matrices.Length; i++) matrices[i] = new float3x4(ReadColumn(reader), ReadColumn(reader), ReadColumn(reader), ReadColumn(reader));
            if (version == 0x43504A32)
            {
                var hips = builder.Allocate(ref data.HipMatrices, EnemyJogSamples.DirectionCount * data.FrameCount);
                for (int i = 0; i < hips.Length; i++) hips[i] = new float3x4(ReadColumn(reader), ReadColumn(reader), ReadColumn(reader), ReadColumn(reader));
                // CPJ2 embeds its measured stride so shared preview-scene resampling
                // updates gait cadence without rewriting every saved encounter.
                if (reader.BaseStream.Length - reader.BaseStream.Position >= 8 * sizeof(float))
                    for (int i = 0; i < distances.Length; i++) distances[i] = math.max(.01f, reader.ReadSingle());
            }
            return builder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
        }

        private static float3 ReadColumn(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
