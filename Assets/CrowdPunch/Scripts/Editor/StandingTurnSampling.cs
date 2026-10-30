using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class StandingTurnSampling
    {
        private const int Frames = 32;
        private const string ElitePath = "Assets/CrowdPunch/Prefabs/EliteEnemy.prefab";

        [MenuItem("Crowd Punch/Animation/Apply Elite And Dino Standing Turns")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply standing turns outside Play Mode.");
            var root = PrefabUtility.LoadPrefabContents(ElitePath);
            try
            {
                Configure(root, "EnemyElite");
                PrefabUtility.SaveAsPrefabAsset(root, ElitePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var active = SceneManager.GetActiveScene();
            try
            {
                foreach (int level in new[] { 40, 80 })
                {
                    string path = $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity";
                    var scene = SceneManager.GetSceneByPath(path);
                    bool open = scene.IsValid() && scene.isLoaded;
                    if (open && scene.isDirty) throw new InvalidOperationException("Save the targeted Dino scene before applying turns: " + path);
                    if (!open) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DinoBossAuthoring>()).Single();
                        Configure(boss.gameObject, "DinoBoss");
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (!open) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }

        public static void Configure(GameObject root, string prefix)
        {
            var clips = new[] { "Left", "Right" }.Select(d => AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/CrowdPunch/Animation/Player{d}Turn.anim")).ToArray();
            if (clips.Any(c => c == null || !c.humanMotion)) throw new InvalidOperationException("Missing shared player Humanoid turn clips.");
            var preview = EditorSceneManager.NewPreviewScene();
            var clone = UnityEngine.Object.Instantiate(root);
            SceneManager.MoveGameObjectToScene(clone, preview);
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("Turns");
                for (int i = 0; i < 2; i++)
                {
                    var state = controller.layers[0].stateMachine.AddState("Turn" + i);
                    state.motion = clips[i]; state.writeDefaultValues = false;
                    if (i == 0) controller.layers[0].stateMachine.defaultState = state;
                }
                var animator = clone.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) throw new InvalidOperationException("Standing turns require a Humanoid avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                foreach (var authoring in root.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
                {
                    if (authoring.JogSamples == null) continue;
                    string hierarchy = AnimationUtility.CalculateTransformPath(authoring.transform, root.transform);
                    var renderer = clone.transform.Find(hierarchy).GetComponent<SkinnedMeshRenderer>();
                    var hip = animator.GetBoneTransform(HumanBodyBones.Hips);
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    var right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    int hipIndex = Array.IndexOf(renderer.bones, hip);
                    int[] legs = Enumerable.Range(0, renderer.bones.Length).Where(i =>
                        renderer.bones[i].IsChildOf(left) || renderer.bones[i].IsChildOf(right)).ToArray();
                    if (hipIndex < 0 || legs.Length == 0) throw new InvalidOperationException("Standing turns require the body pelvis and leg bones.");
                    string path = $"Assets/CrowdPunch/Animation/{prefix}StandingTurns.bytes";
                    var bindposes = renderer.sharedMesh.bindposes;
                    var matrices = new Matrix4x4[2 * Frames * legs.Length];
                    for (int clip = 0; clip < 2; clip++)
                    for (int frame = 0; frame < Frames; frame++)
                    {
                        animator.Play("Turn" + clip, 0, frame / (float)(Frames - 1));
                        animator.Update(0f);
                        // Store the complete source leg chain in its own pelvis frame.
                        // Runtime composes it rigidly below the independently animated torso.
                        for (int leg = 0; leg < legs.Length; leg++)
                            matrices[(clip * Frames + frame) * legs.Length + leg] =
                                hip.worldToLocalMatrix * renderer.bones[legs[leg]].localToWorldMatrix * bindposes[legs[leg]];
                    }
                    string hash = "StandingTurn1"
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(clips[0]))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(clips[1]))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                    using (var writer = new BinaryWriter(File.Create(path)))
                    {
                        writer.Write(0x43505431); writer.Write(hash);
                        writer.Write(renderer.bones.Length); writer.Write(Frames);
                        foreach (var bone in renderer.bones) writer.Write(bone.name);
                        writer.Write(hipIndex);
                        var inverseBind = bindposes[hipIndex].inverse;
                        for (int col = 0; col < 4; col++) for (int row = 0; row < 4; row++) writer.Write(inverseBind[row, col]);
                        writer.Write(legs.Length); foreach (int leg in legs) writer.Write(leg);
                        foreach (var clip in clips) writer.Write(clip.length);
                        foreach (var matrix in matrices)
                            for (int col = 0; col < 4; col++) for (int row = 0; row < 3; row++) writer.Write(matrix[row, col]);
                    }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    authoring.StandingTurnSamples = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    EditorUtility.SetDirty(authoring);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(controller);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
