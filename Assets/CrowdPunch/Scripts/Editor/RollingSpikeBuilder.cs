using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class RollingSpikeBuilder
    {
        public const string ModelPath = "Assets/Spike Cute Series/Prefabs/Spike.prefab";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/RollingSpike";
        public const string ControllerPath = "Assets/CrowdPunch/Data/RollingBoss/Spike.controller";
        public static readonly string[] ScenePaths = {
            "Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_21/Gauntlet_21 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_30/Campaign_30 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_70/Campaign_70 Sub Scene.unity" };

        [MenuItem("Crowd Punch/Bosses/Apply Rolling Spike Model")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before replacing the boss model.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before replacing the boss model.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in ScenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var boss in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RollingBossAuthoring>(true)))
                    {
                        foreach (string name in new[] { "Green Spiky Blob Model", "Spike Model" })
                        {
                            var old = boss.transform.Find(name);
                            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                        }
                        AddModel(boss.gameObject, boss.settings);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void AddModel(GameObject root, RollingBossSettings settings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Missing supplied Spike prefab.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "Spike Model";
            // Preserve proportions; a constant scene scale fits the existing collision footprint below.
            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            // Reverse the authored emergence for a continuous idle-to-hide wind-up.
            string[] names = { "Idle", "Hide and Spin Attack In Place", "Hide to Idle", "Take Damage", "Die", "Hide to Idle" };
            var clips = names.Select(name => AssetDatabase.LoadAllAssetsAtPath($"Assets/Spike Cute Series/FBX/Spike@{name}.FBX")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"))).ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Length != clips.Length || machine.states.Where((s, i) => s.state.name != "Motion" + i || s.state.motion != clips[i]).Any())
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
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/CrowdPunch/Materials/Enemies", "RollingSpike");
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Spike needs skinned renderers.");
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                renderer.rootBone = animator.transform;
                renderer.quality = SkinQuality.Bone4;
                renderer.forceMeshLod = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterials = EnemyRangedPrefabBuilder.BuildMaterials(renderer.sharedMaterials, MaterialFolder);
                string path = $"Assets/CrowdPunch/Data/RollingBoss/SpikeMovement_{i:00}.bytes";
                GauntletProgressionBuilder.WriteBossSamples(animator, renderer, clips, path, ControllerPath, reverseMotion: 2, groundEachFrame: true);
                var animation = renderer.GetComponent<EnemyAnimationAuthoring>() ?? renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                animation.SpawnMotion = -1;
            }
            FitCollisionFootprint(visual, settings);
            float lowest = float.PositiveInfinity;
            // Include defeat and return-to-idle as well as idle, spin, hide and damage.
            BossAppearanceBuilder.VisitSamples(visual, p => lowest = Mathf.Min(lowest, visual.transform.TransformPoint(p).y), 6);
            visual.transform.position += Vector3.up * (root.transform.position.y - settings.bodyHeight * .5f + .08f - lowest);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
        }

        private static void FitCollisionFootprint(GameObject visual, RollingBossSettings settings)
        {
            var transform = visual.transform;
            var authored = Matrix4x4.TRS(Vector3.zero, transform.localRotation, transform.localScale);
            Bounds bounds = default;
            bool first = true;
            // Idle and spin define the solid body; emergence/death may intentionally extend beyond it.
            BossAppearanceBuilder.VisitSamples(visual, point =>
            {
                var position = authored.MultiplyPoint3x4(point);
                if (first) { bounds = new Bounds(position, Vector3.zero); first = false; }
                else bounds.Encapsulate(position);
            }, 2);
            var center = new Vector3(bounds.center.x, 0, bounds.center.z);
            float radius = 0;
            BossAppearanceBuilder.VisitSamples(visual, point =>
            {
                var offset = authored.MultiplyPoint3x4(point) - center;
                radius = Mathf.Max(radius, new Vector2(offset.x, offset.z).magnitude);
            }, 2);
            float fit = Mathf.Min(settings.bodyRadius / Mathf.Max(.001f, radius),
                Mathf.Max(.01f, settings.bodyHeight - .08f) / Mathf.Max(.001f, bounds.size.y));
            transform.localScale *= fit;
            transform.localPosition = -center * fit;
        }
    }
}
