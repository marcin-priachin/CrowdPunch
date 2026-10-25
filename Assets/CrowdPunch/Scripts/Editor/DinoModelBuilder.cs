using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class DinoModelBuilder
    {
        public const string ModelPath = "Assets/CrowdPunch/Prefabs/Enemies/Goblin Giant Armored Variant.prefab";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/GoblinDino";
        public const string ControllerPath = "Assets/CrowdPunch/Data/DinoBoss/Dino.controller";
        public const string AnimationFolder = ChickenModelBuilder.AnimationFolder;
        public static readonly string[] ScenePaths = {
            "Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_22/Gauntlet_22 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_40/Campaign_40 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_80/Campaign_80 Sub Scene.unity" };

        [MenuItem("Crowd Punch/Bosses/Apply Goblin Dino Model")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before replacing the boss model.");
            var active = SceneManager.GetActiveScene();
            try
            {
                foreach (string path in ScenePaths)
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                    if (alreadyOpen && scene.isDirty)
                        throw new InvalidOperationException("Save the Dino encounter before replacing its model: " + path);
                    if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        foreach (var boss in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DinoBossAuthoring>(true)))
                        {
                            var existing = boss.transform.Find("Dino Model");
                            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                            AddModel(boss.gameObject, boss.settings);
                        }
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }

        internal static void AddModel(GameObject root, DinoBossSettings settings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Missing supplied Goblin Giant Armored Variant prefab.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "Dino Model";
            var animator = visual.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("The armored Giant needs its supplied Humanoid avatar.");
            string[] names = { "Block", "Block", "Cast Spell", "Take Damage", "Die" };
            var clips = names.Select(FindClip).ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Length != clips.Length || machine.states.Where((s, i) =>
                s.state.name != "Motion" + i || s.state.motion != clips[i]).Any())
            {
                foreach (var state in machine.states) machine.RemoveState(state.state);
                for (int i = 0; i < clips.Length; i++)
                {
                    var state = machine.AddState("Motion" + i); state.motion = clips[i];
                    if (i == 0) machine.defaultState = state;
                }
                EditorUtility.SetDirty(controller);
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EnemyRangedPrefabBuilder.ConvertAccessories(visual, "GoblinGiantDinoAccessory");
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/CrowdPunch/Materials/Enemies", "GoblinDino");
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                renderer.rootBone = animator.transform;
                renderer.quality = SkinQuality.Bone4;
                renderer.forceMeshLod = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterials = EnemyRangedPrefabBuilder.BuildMaterials(renderer.sharedMaterials, MaterialFolder);
                AssetDatabase.SaveAssets();
                string path = $"Assets/CrowdPunch/Data/DinoBoss/GoblinMovement_{i:00}.bytes";
                GauntletProgressionBuilder.WriteBossSamples(animator, renderer, clips, path, ControllerPath, humanoid: true);
                var animation = renderer.GetComponent<EnemyAnimationAuthoring>() ?? renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                animation.Profile = EnemyAnimationProfile.Goblin;
                animation.SpawnMotion = -1;
            }
            // Translation alone grounds the supplied scale, including the final death pose.
            float lowest = float.PositiveInfinity;
            BossAppearanceBuilder.VisitSamples(visual, p => lowest = Mathf.Min(lowest, visual.transform.TransformPoint(p).y), EnemyAnimationSamples.MotionCount);
            visual.transform.position += Vector3.up * (root.transform.position.y - settings.bodyHeight * .5f + .08f - lowest);
            GoblinJogSampling.Configure(root, "DinoBoss");
        }

        private static AnimationClip FindClip(string name) => AssetDatabase.LoadAllAssetsAtPath($"{AnimationFolder}/Char@{name}.FBX")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));
    }
}
