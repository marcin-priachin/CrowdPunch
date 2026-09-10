using System;
using System.IO;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class EnemyAnimationSampling
    {
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/Enemy.prefab";
        private const string SamplesPath = "Assets/CrowdPunch/Animation/EnemyMovement.bytes";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Animation/Rebuild Enemy Samples")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild animation samples outside Play Mode.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null || !animator.avatar.isHuman || controller.layers.Length != 1)
                    throw new InvalidOperationException("Expected the enemy humanoid locomotion controller with one layer.");
                var tree = controller.layers[0].stateMachine.defaultState.motion as BlendTree;
                if (tree == null || tree.children.Length != 9)
                    throw new InvalidOperationException("Expected idle and eight locomotion directions.");

                renderer.rootBone = animator.transform;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                var matrices = new Matrix4x4[9 * FrameCount * renderer.bones.Length];
                var durations = new float[9];
                var bindposes = renderer.sharedMesh.bindposes;
                var bounds = new Bounds();
                var sampledMesh = new Mesh();
                try
                {
                    for (int motion = 0; motion < 9; motion++)
                    {
                        float angle = (motion - 1) * Mathf.PI / 4f;
                        animator.SetFloat("MoveX", motion == 0 ? 0f : Mathf.Sin(angle));
                        animator.SetFloat("MoveZ", motion == 0 ? 0f : Mathf.Cos(angle));
                        for (int frame = 0; frame < FrameCount; frame++)
                        {
                            animator.Play("Base Layer.Locomotion", 0, frame / (float)FrameCount);
                            animator.Update(0f);
                            durations[motion] = animator.GetCurrentAnimatorStateInfo(0).length;
                            if (durations[motion] <= 0f) throw new InvalidOperationException("Animator did not evaluate locomotion.");
                            Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                            for (int bone = 0; bone < renderer.bones.Length; bone++)
                                matrices[(motion * FrameCount + frame) * renderer.bones.Length + bone] =
                                    inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                            renderer.BakeMesh(sampledMesh);
                            var toRoot = inverse * renderer.transform.localToWorldMatrix;
                            foreach (Vector3 vertex in sampledMesh.vertices)
                                bounds.Encapsulate(toRoot.MultiplyPoint3x4(vertex));
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(sampledMesh); }

                string hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(controller)).ToString()
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                using (var writer = new BinaryWriter(File.Create(SamplesPath)))
                {
                    writer.Write(0x43504131);
                    writer.Write(hash);
                    writer.Write(renderer.bones.Length);
                    writer.Write(FrameCount);
                    foreach (Transform bone in renderer.bones) writer.Write(bone.name);
                    foreach (float duration in durations) writer.Write(duration);
                    foreach (Matrix4x4 matrix in matrices)
                        for (int column = 0; column < 4; column++)
                            for (int row = 0; row < 3; row++) writer.Write(matrix[row, column]);
                }
                AssetDatabase.ImportAsset(SamplesPath, ImportAssetOptions.ForceSynchronousImport);

                // Sampling must not persist an evaluated pose into the prefab's skeleton.
                PrefabUtility.UnloadPrefabContents(root);
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                animator = root.GetComponentInChildren<Animator>();
                renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.rootBone = animator.transform;
                bounds.Expand(0.1f);
                renderer.localBounds = bounds;
                var authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplesPath);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Enemy animation: sampled 9 motions x {FrameCount} frames x {renderer.bones.Length} bones. Bounds: {bounds}");
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
