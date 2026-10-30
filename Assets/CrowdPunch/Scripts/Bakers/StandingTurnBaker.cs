using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class StandingTurnBaker : Baker<EnemyAnimationAuthoring>
    {
        public override void Bake(EnemyAnimationAuthoring authoring)
        {
            if (authoring.StandingTurnSamples == null) return;
            var renderer = GetComponent<SkinnedMeshRenderer>();
            var animator = GetComponentInParent<Animator>();
            DependsOn(authoring.StandingTurnSamples); DependsOn(renderer.sharedMesh); DependsOn(animator.avatar);
            using var reader = new BinaryReader(new MemoryStream(authoring.StandingTurnSamples.bytes));
            if (reader.ReadInt32() != 0x43505431) throw new InvalidDataException("Rebuild standing-turn samples.");
            string hash = reader.ReadString();
#if UNITY_EDITOR
            foreach (string direction in new[] { "Left", "Right" })
                DependsOn(UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/CrowdPunch/Animation/Player{direction}Turn.anim"));
            string currentHash = "StandingTurn1"
                + UnityEditor.AssetDatabase.GetAssetDependencyHash("Assets/CrowdPunch/Animation/PlayerLeftTurn.anim")
                + UnityEditor.AssetDatabase.GetAssetDependencyHash("Assets/CrowdPunch/Animation/PlayerRightTurn.anim")
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
            if (hash != currentHash) throw new InvalidDataException("Standing-turn sources changed. Apply Elite And Dino Standing Turns again.");
#endif
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<StandingTurnSamples>();
            data.BoneCount = reader.ReadInt32(); data.FrameCount = reader.ReadInt32();
            if (data.BoneCount != renderer.bones.Length || data.FrameCount < 2 || data.FrameCount > 120)
                throw new InvalidDataException("Standing-turn skeleton/frame count mismatch.");
            for (int i = 0; i < data.BoneCount; i++)
                if (reader.ReadString() != renderer.bones[i].name) throw new InvalidDataException("Standing-turn bone order changed.");
            data.HipBoneIndex = reader.ReadInt32();
            data.HipBindInverse = new float4x4(Read4(reader), Read4(reader), Read4(reader), Read4(reader));
            int legCount = reader.ReadInt32();
            if (data.HipBoneIndex < 0 || data.HipBoneIndex >= data.BoneCount || legCount < 1 || legCount > data.BoneCount)
                throw new InvalidDataException("Invalid standing-turn leg mask.");
            var legs = builder.Allocate(ref data.LegBones, legCount);
            for (int i = 0; i < legCount; i++)
            {
                legs[i] = reader.ReadInt32();
                if (legs[i] < 0 || legs[i] >= data.BoneCount) throw new InvalidDataException("Invalid standing-turn bone.");
            }
            var durations = builder.Allocate(ref data.Durations, 2);
            for (int i = 0; i < 2; i++) durations[i] = reader.ReadSingle();
            var matrices = builder.Allocate(ref data.Matrices, 2 * data.FrameCount * legCount);
            for (int i = 0; i < matrices.Length; i++) matrices[i] = new float3x4(Read3(reader), Read3(reader), Read3(reader), Read3(reader));
            var blob = builder.CreateBlobAssetReference<StandingTurnSamples>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new StandingTurnAnimation
            {
                Samples = blob,
                MinimumAngle = math.radians(math.max(0, authoring.StandingTurnMinimumAngle)),
                MinimumSpeed = math.radians(math.max(0, authoring.StandingTurnMinimumSpeed)),
                BlendInDuration = math.max(0, authoring.StandingTurnBlendInDuration),
                BlendOutDuration = math.max(0, authoring.StandingTurnBlendOutDuration),
                StopDelay = math.max(0, authoring.StandingTurnStopDelay)
            });
            AddComponent<StandingTurnPlayback>(entity);
        }

        private static float3 Read3(BinaryReader r) => new float3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static float4 Read4(BinaryReader r) => new float4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
