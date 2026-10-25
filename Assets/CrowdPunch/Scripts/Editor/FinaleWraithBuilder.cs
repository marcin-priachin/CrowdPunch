using System;
using System.Linq;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class FinaleWraithBuilder
    {
        public const string Source = "Assets/Wraith Cute Series/Prefabs/Wraith.prefab";
        public const string ScenePath = "Assets/CrowdPunch/Scenes/Campaign/Campaign_81.unity";
        public const string Art = "Assets/CrowdPunch/Data/Campaign/Finale/";
        public static readonly string[] Motions = { "Fly Forward In Place", "Idle", "Take Damage", "Die",
            "Projectile Attack", "Spin Wing Slash Attack", "Wing Stab Attack", "Cast Spell" };

        [MenuItem("Crowd Punch/Bosses/Apply Wraith Finale")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var finale = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FinalePresentation>(true)).Single();
                Configure(finale);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void Configure(FinalePresentation finale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (prefab == null) throw new InvalidOperationException("Missing Wraith prefab.");
            var clips = Motions.Select(Clip).ToArray();
            if (finale.model != null) UnityEngine.Object.DestroyImmediate(finale.model.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, finale.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            finale.model = model.transform;
            finale.groundHeight = finale.settings.colliderSize.y * .5f;
            finale.animator = model.GetComponent<Animator>();
            if (finale.animator == null) finale.animator = model.AddComponent<Animator>();
            var animator = finale.animator;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string path = Art + "Wraith.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name);
                state.motion = clip;
                if (clip.name == "Fly Forward In Place") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController = controller;

            var body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in body)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(CelMaterial).ToArray();
                renderer.quality = SkinQuality.Bone4;
                renderer.forceMeshLod = 0;
                renderer.updateWhenOffscreen = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            // One fixed translation covers the complete grounded clip envelope, including death.
            // No per-frame fitting or scale changes disturb the authored floating motion.
            float minimum = float.PositiveInfinity;
            var mesh = new Mesh();
            try
            {
                animator.Rebind();
                foreach (var clip in clips)
                    for (int frame = 0; frame <= 40; frame++)
                    {
                        clip.SampleAnimation(model, clip.length * frame / 40);
                        foreach (var renderer in body)
                        {
                            renderer.BakeMesh(mesh);
                            foreach (var vertex in mesh.vertices)
                                minimum = Mathf.Min(minimum, model.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)).y);
                        }
                    }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            finale.groundedModelOffset = minimum - .04f;
            animator.Rebind();
            clips[0].SampleAnimation(model, 0);
            model.transform.position = new Vector3(0, finale.settings.flightHeight - finale.groundHeight, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorUtility.SetDirty(finale);
        }

        public static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Wraith Cute Series/FBX/Wraith@" + name + ".FBX").OfType<AnimationClip>().Single(c => c.name == name);

        private static Material CelMaterial(Material source)
        {
            string path = Art + "Wraith Cel.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("CrowdPunch/Cel/Environment")) { name = "Wraith Cel" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("CrowdPunch/Cel/Environment");
            material.SetTexture("_BaseMap", source.mainTexture);
            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
