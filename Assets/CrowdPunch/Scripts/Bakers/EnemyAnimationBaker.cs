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
            var renderer = GetComponent<SkinnedMeshRenderer>();
            var animator = GetComponentInParent<Animator>();
            if (owner == null || animator == null || authoring.Samples == null)
                throw new System.InvalidOperationException("Enemy animation needs an enemy owner, Animator and generated samples.");

            DependsOn(authoring.Samples);
            DependsOn(renderer.sharedMesh);
            DependsOn(animator.runtimeAnimatorController);
            DependsOn(animator.avatar);
            using var reader = new BinaryReader(new MemoryStream(authoring.Samples.bytes));
            if (reader.ReadInt32() != 0x43504132)
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
            AddComponent(entity, new EnemyAnimation
            {
                Owner = GetEntity(owner, TransformUsageFlags.Dynamic),
                Samples = blob,
                BlendResponse = math.max(0f, authoring.BlendResponse)
            });
            AddComponent<EnemyAnimationPlayback>(entity);
        }

        private static float3 ReadColumn(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
