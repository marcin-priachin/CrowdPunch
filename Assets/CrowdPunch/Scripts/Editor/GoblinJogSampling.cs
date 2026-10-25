using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class GoblinJogSampling
    {
        public const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyMovement.controller";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Animation/Apply Goblin Jog Legs")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sample Goblin jogs outside Play Mode.");
            foreach (string kind in new[] { "Baseline", "Ranged", "Elite", "Armored", "LinkedPair" })
            {
                string path = kind == "Elite" || kind == "Armored" ? $"Assets/CrowdPunch/Prefabs/{kind}Enemy.prefab"
                    : $"Assets/CrowdPunch/Prefabs/Enemy{kind}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root, "Enemy" + kind);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject root, string samplePrefix)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing directional jog controller.");
            var preview = EditorSceneManager.NewPreviewScene();
            var clone = UnityEngine.Object.Instantiate(root);
            SceneManager.MoveGameObjectToScene(clone, preview);
            try
            {
                var animator = clone.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Goblin jogs require the supplied Humanoid avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                foreach (var source in root.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
                {
                    if (source.Profile != EnemyAnimationProfile.Goblin && source.Profile != EnemyAnimationProfile.GoblinArmored) continue;
                    var renderer = source.GetComponent<SkinnedMeshRenderer>();
                    string hierarchy = AnimationUtility.CalculateTransformPath(source.transform, root.transform);
                    var sampled = clone.transform.Find(hierarchy).GetComponent<SkinnedMeshRenderer>();
                    Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    int[] legs = Enumerable.Range(0, sampled.bones.Length)
                        .Where(i => sampled.bones[i].IsChildOf(left) || sampled.bones[i].IsChildOf(right)).ToArray();
                    if (legs.Length == 0) continue; // Hand/head equipment keeps the original animation only.
                    Transform hip = animator.GetBoneTransform(HumanBodyBones.Hips);
                    int hipIndex = Array.IndexOf(sampled.bones, hip);
                    if (hipIndex < 0) throw new InvalidOperationException("Goblin body must include its pelvis bone.");
                    string path = $"Assets/CrowdPunch/Animation/{samplePrefix}JogLegs.bytes";
                    Sample(path, source, renderer, sampled, animator, hip, hipIndex, legs);
                    source.JogSamples = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    EditorUtility.SetDirty(source);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Sample(string path, EnemyAnimationAuthoring source, SkinnedMeshRenderer renderer,
            SkinnedMeshRenderer sampled, Animator animator, Transform hip, int hipIndex, int[] legs)
        {
            var matrices = new Matrix4x4[EnemyJogSamples.DirectionCount * FrameCount * legs.Length];
            var durations = new float[EnemyJogSamples.DirectionCount];
            var bindposes = sampled.sharedMesh.bindposes;
            var vertices = sampled.sharedMesh.vertices;
            var weights = sampled.sharedMesh.boneWeights;
            var legMask = new bool[sampled.bones.Length];
            foreach (int leg in legs) legMask[leg] = true;
            var pose = new Matrix4x4[sampled.bones.Length];
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            source.JogCycleDistances = new float[EnemyJogSamples.DirectionCount];
            float radius = 0;
            for (int direction = 0; direction < EnemyJogSamples.DirectionCount; direction++)
            {
                float angle = direction * Mathf.PI / 4;
                animator.SetFloat("MoveX", Mathf.Sin(angle));
                animator.SetFloat("MoveZ", Mathf.Cos(angle));
                Vector2 minimumFootTravel = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 maximumFootTravel = new Vector2(float.MinValue, float.MinValue);
                Vector3 travelDirection = animator.transform.TransformDirection(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)));
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    animator.Play("Base Layer.Locomotion", 0, frame / (float)FrameCount);
                    animator.Update(0);
                    // Each planted foot travels along the step for half a cycle.
                    // Measure every direction at the supplied rig's authored scale.
                    Vector2 travel = new Vector2(Vector3.Dot(leftFoot.position - hip.position, travelDirection),
                        Vector3.Dot(rightFoot.position - hip.position, travelDirection));
                    minimumFootTravel = Vector2.Min(minimumFootTravel, travel);
                    maximumFootTravel = Vector2.Max(maximumFootTravel, travel);
                    durations[direction] = animator.GetCurrentAnimatorStateInfo(0).length;
                    if (durations[direction] <= 0) throw new InvalidOperationException("Jog controller did not evaluate.");
                    Matrix4x4 inverseHip = hip.worldToLocalMatrix;
                    for (int bone = 0; bone < pose.Length; bone++)
                        pose[bone] = inverseHip * sampled.bones[bone].localToWorldMatrix * bindposes[bone];
                    for (int leg = 0; leg < legs.Length; leg++)
                        matrices[(direction * FrameCount + frame) * legs.Length + leg] = pose[legs[leg]];
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        var w = weights[vertex];
                        if (!(legMask[w.boneIndex0] && w.weight0 > 0 || legMask[w.boneIndex1] && w.weight1 > 0
                            || legMask[w.boneIndex2] && w.weight2 > 0 || legMask[w.boneIndex3] && w.weight3 > 0)) continue;
                        var v = vertices[vertex];
                        Vector3 p = pose[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + pose[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + pose[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + pose[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                        radius = Mathf.Max(radius, p.magnitude);
                    }
                }
                Vector2 stride = maximumFootTravel - minimumFootTravel;
                source.JogCycleDistances[direction] = Mathf.Max(.01f, stride.x + stride.y);
            }
            string hash = AssetDatabase.GetAssetDependencyHash(ControllerPath).ToString()
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x43504A31);
                writer.Write(hash);
                writer.Write(sampled.bones.Length);
                writer.Write(FrameCount);
                writer.Write(hipIndex);
                writer.Write(legs.Length);
                Matrix4x4 inverseBind = bindposes[hipIndex].inverse;
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 4; row++) writer.Write(inverseBind[row, column]);
                foreach (int leg in legs) writer.Write(leg);
                foreach (float duration in durations) writer.Write(duration);
                foreach (var matrix in matrices)
                    for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) writer.Write(matrix[row, column]);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ExpandBounds(source, renderer, bindposes[hipIndex].inverse, hipIndex, radius);
            Debug.Log($"{Path.GetFileNameWithoutExtension(path)}: sampled eight directional jogs, {legs.Length} leg bones; original upper body retained.");
        }

        private static void ExpandBounds(EnemyAnimationAuthoring source, SkinnedMeshRenderer renderer,
            Matrix4x4 hipBindInverse, int hipIndex, float radius)
        {
            using var reader = new BinaryReader(new MemoryStream(source.Samples.bytes));
            reader.ReadInt32(); reader.ReadString();
            int bones = reader.ReadInt32(), frames = reader.ReadInt32();
            for (int bone = 0; bone < bones; bone++) reader.ReadString();
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) reader.ReadSingle();
            long start = reader.BaseStream.Position;
            Bounds bounds = renderer.localBounds;
            for (int frame = 0; frame < frames * EnemyAnimationSamples.MotionCount; frame++)
            {
                reader.BaseStream.Position = start + (frame * bones + hipIndex) * 48L;
                var skin = Matrix4x4.identity;
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 3; row++) skin[row, column] = reader.ReadSingle();
                Vector3 position = (skin * hipBindInverse).GetColumn(3);
                bounds.Encapsulate(position + Vector3.one * radius);
                bounds.Encapsulate(position - Vector3.one * radius);
            }
            renderer.localBounds = bounds;
            EditorUtility.SetDirty(renderer);
        }
    }
}
