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
    public static class ChickenModelBuilder
    {
        public const string ModelPath = "Assets/CrowdPunch/Prefabs/Enemies/Goblin Giant Wizard Variant.prefab";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/GoblinChicken";
        public const string ControllerPath = "Assets/CrowdPunch/Data/ChickenBoss/Chicken.controller";
        public const string AnimationFolder = "Assets/Goblin Trooper Giant Evolution Modular Pack Cute Series/Goblin Giant Modular Pack Cute Series/Animations";

        [MenuItem("Crowd Punch/Bosses/Apply Player Standing Turns To Chicken")]
        public static void ApplyPlayerStandingTurns()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sample Chicken turns outside Play Mode.");
            var clips = GetClips();
            var controller = ConfigureController(clips);
            AssetDatabase.SaveAssetIfDirty(controller);
            // All three Chicken encounters share these sample assets. Resample the
            // existing rig in a preview scene without rebuilding or saving any scene.
            var scene = EditorSceneManager.OpenPreviewScene(
                "Assets/CrowdPunch/Scenes/Campaign/Campaign_20/Campaign_20 Sub Scene.unity");
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChickenBossAuthoring>()).Single();
                foreach (var animation in boss.GetComponentsInChildren<EnemyAnimationAuthoring>())
                    GauntletProgressionBuilder.WriteBossSamples(animation.GetComponentInParent<Animator>(),
                        animation.GetComponent<SkinnedMeshRenderer>(), clips, AssetDatabase.GetAssetPath(animation.Samples),
                        ControllerPath, humanoid: true, inPlaceTurns: true);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [MenuItem("Crowd Punch/Bosses/Apply Goblin Chicken Model")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before replacing the boss model.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before replacing the boss model.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] {
                    "Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_20/Gauntlet_20 Sub Scene.unity",
                    "Assets/CrowdPunch/Scenes/Campaign/Campaign_20/Campaign_20 Sub Scene.unity",
                    "Assets/CrowdPunch/Scenes/Campaign/Campaign_60/Campaign_60 Sub Scene.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var boss in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChickenBossAuthoring>(true)))
                    {
                        var existing = boss.transform.Find("Chicken Model");
                        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                        AddModel(boss.gameObject, boss.settings);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void AddModel(GameObject root, ChickenBossSettings settings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Missing supplied Goblin Giant Wizard Variant prefab.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "Chicken Model";
            // Preserve every authored scale. Only translate the model to the collider floor.
            var animator = visual.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("The supplied wizard needs its Humanoid avatar.");
            var clips = GetClips();
            var controller = ConfigureController(clips);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EnemyRangedPrefabBuilder.ConvertAccessories(visual, "GoblinGiantChickenAccessory");
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/CrowdPunch/Materials/Enemies", "GoblinChicken");
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
                string path = $"Assets/CrowdPunch/Data/ChickenBoss/GoblinMovement_{i:00}.bytes";
                GauntletProgressionBuilder.WriteBossSamples(animator, renderer, clips, path, ControllerPath, true, true);
                var animation = renderer.GetComponent<EnemyAnimationAuthoring>() ?? renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                animation.Profile = EnemyAnimationProfile.Goblin;
                animation.SpawnMotion = -1;
            }
            float lowest = float.PositiveInfinity;
            BossAppearanceBuilder.VisitSamples(visual, p => lowest = Mathf.Min(lowest, visual.transform.TransformPoint(p).y));
            visual.transform.position += Vector3.up * (root.transform.position.y - settings.bodyHeight * .5f + .08f - lowest);
            GoblinJogSampling.Configure(root, "ChickenBoss");
            int lastFrame = Mathf.Max(1, Mathf.RoundToInt(clips[2].length * clips[2].frameRate));
            settings.firingAnimationLastFrame = lastFrame;
            settings.castDuration = clips[5].length;
            EditorUtility.SetDirty(settings);
        }

        private static AnimationClip[] GetClips()
        {
            var clips = new[] { "Idle", "Run Forward In Place", "Sweep Attack", "Take Damage", "Die", "Cast Spell" }
                .Select(FindClip).ToList();
            foreach (string direction in new[] { "Left", "Right" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/CrowdPunch/Animation/Player{direction}Turn.anim");
                if (clip == null || !clip.humanMotion)
                    throw new InvalidOperationException("Apply the player's humanoid standing-turn clips first.");
                clips.Add(clip);
            }
            return clips.ToArray();
        }

        private static AnimatorController ConfigureController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < clips.Length; i++)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Motion" + i)
                    ?? machine.AddState("Motion" + i);
                if (state.motion != clips[i])
                {
                    state.motion = clips[i];
                    EditorUtility.SetDirty(state);
                    EditorUtility.SetDirty(controller);
                }
                if (i == 0) machine.defaultState = state;
            }
            return controller;
        }

        private static AnimationClip FindClip(string name) => AssetDatabase.LoadAllAssetsAtPath($"{AnimationFolder}/Char@{name}.FBX")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));
    }
}
