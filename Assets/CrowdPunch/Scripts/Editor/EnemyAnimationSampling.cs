using System;
using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class EnemyAnimationSampling
    {
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/Enemy.prefab";
        private const string SamplesPath = "Assets/CrowdPunch/Animation/EnemyMovement.bytes";
        private const string ModelPath = "Assets/CrowdPunch/Models/BaseEnemy/BaseEnemy.prefab";
        private const string MeshPath = "Assets/CrowdPunch/Animation/EnemySkinningMesh.asset";
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

                AnimationClip flying = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/CrowdPunch/Animation/Flying.fbx"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) flying = clip;
                if (flying == null || !flying.humanMotion)
                    throw new InvalidOperationException("Flying.fbx must contain a humanoid animation.");
                AnimatorState flyingState = null;
                foreach (var child in controller.layers[0].stateMachine.states)
                    if (child.state.name == "Flying") flyingState = child.state;
                if (flyingState == null) flyingState = controller.layers[0].stateMachine.AddState("Flying");
                flyingState.motion = flying;
                AnimationClip impact = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/CrowdPunch/Animation/Falling Flat Impact.fbx"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) impact = clip;
                if (impact == null || !impact.humanMotion)
                    throw new InvalidOperationException("Falling Flat Impact.fbx must contain a humanoid animation.");
                AnimatorState impactState = null;
                foreach (var child in controller.layers[0].stateMachine.states)
                    if (child.state.name == "Falling Flat Impact") impactState = child.state;
                if (impactState == null) impactState = controller.layers[0].stateMachine.AddState("Falling Flat Impact");
                impactState.motion = impact;
                EditorUtility.SetDirty(controller);

                var sourceRenderer = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
                    .GetComponentInChildren<SkinnedMeshRenderer>();
                Mesh generatedMesh = EnemyStaticShapeMesh.Create(sourceRenderer);
                Mesh skinningMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (skinningMesh == null)
                {
                    AssetDatabase.CreateAsset(generatedMesh, MeshPath);
                    skinningMesh = generatedMesh;
                }
                else
                {
                    EditorUtility.CopySerialized(generatedMesh, skinningMesh);
                    UnityEngine.Object.DestroyImmediate(generatedMesh);
                    EditorUtility.SetDirty(skinningMesh);
                }
                AssetDatabase.SaveAssets();
                renderer.sharedMesh = skinningMesh;
                renderer.quality = SkinQuality.Bone4;
                renderer.rootBone = animator.transform;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
                var durations = new float[EnemyAnimationSamples.MotionCount];
                var bindposes = renderer.sharedMesh.bindposes;
                var sourceVertices = renderer.sharedMesh.vertices;
                var boneWeights = renderer.sharedMesh.boneWeights;
                var bounds = new Bounds();
                var sampledMesh = new Mesh();
                try
                {
                    for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
                    {
                        float angle = (motion - 1) * Mathf.PI / 4f;
                        animator.SetFloat("MoveX", motion == 0 ? 0f : Mathf.Sin(angle));
                        animator.SetFloat("MoveZ", motion == 0 ? 0f : Mathf.Cos(angle));
                        for (int frame = 0; frame < FrameCount; frame++)
                        {
                            bool isFlying = motion == EnemyAnimationSamples.FlyingMotion;
                            bool isImpact = motion == EnemyAnimationSamples.ImpactMotion;
                            animator.Play(isFlying ? "Base Layer.Flying" : isImpact ? "Base Layer.Falling Flat Impact" : "Base Layer.Locomotion", 0,
                                frame / (float)(isFlying || isImpact ? FrameCount - 1 : FrameCount));
                            animator.Update(0f);
                            durations[motion] = animator.GetCurrentAnimatorStateInfo(0).length;
                            if (durations[motion] <= 0f) throw new InvalidOperationException("Animator did not evaluate locomotion.");
                            Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                            int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                            for (int bone = 0; bone < renderer.bones.Length; bone++)
                                matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                            renderer.BakeMesh(sampledMesh);
                            var toRoot = inverse * renderer.transform.localToWorldMatrix;
                            var vertices = sampledMesh.vertices;
                            float groundOffset = 0f;
                            if (isImpact)
                            {
                                // Retargeted body proportions can put the fall below its authored floor.
                                // Use the exported skin matrices, matching ECS rather than BakeMesh's transform handling.
                                toRoot = Matrix4x4.identity;
                                for (int vertex = 0; vertex < vertices.Length; vertex++)
                                {
                                    BoneWeight w = boneWeights[vertex];
                                    Vector3 v = sourceVertices[vertex];
                                    vertices[vertex] = matrices[poseStart + w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                        + matrices[poseStart + w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                        + matrices[poseStart + w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                        + matrices[poseStart + w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                                    groundOffset = Mathf.Max(groundOffset, -vertices[vertex].y);
                                }
                            }
                            for (int bone = 0; bone < renderer.bones.Length; bone++)
                            {
                                matrices[poseStart + bone][1, 3] += groundOffset;
                            }
                            foreach (Vector3 vertex in vertices)
                            {
                                Vector3 position = toRoot.MultiplyPoint3x4(vertex);
                                position.y += groundOffset;
                                bounds.Encapsulate(position);
                                if (isFlying)
                                {
                                    // Flying can pitch with vertical velocity without tilting the collider.
                                    float radius = new Vector2(position.y, position.z).magnitude;
                                    bounds.Encapsulate(new Vector3(position.x, radius, radius));
                                    bounds.Encapsulate(new Vector3(position.x, -radius, -radius));
                                }
                            }
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(sampledMesh); }

                string hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(controller)).ToString()
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                using (var writer = new BinaryWriter(File.Create(SamplesPath)))
                {
                    writer.Write(0x43504133);
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
                renderer.sharedMesh = skinningMesh;
                renderer.quality = SkinQuality.Bone4;
                renderer.rootBone = animator.transform;
                bounds.Expand(0.1f);
                renderer.localBounds = bounds;
                var authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplesPath);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Enemy animation: sampled {EnemyAnimationSamples.MotionCount} motions x {FrameCount} frames x {renderer.bones.Length} bones. Bounds: {bounds}");
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
