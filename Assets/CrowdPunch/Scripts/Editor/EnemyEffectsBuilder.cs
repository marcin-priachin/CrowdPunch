using System;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class EnemyEffectsBuilder
    {
        private const string Source = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";
        private const string Destination = "Assets/CrowdPunch/Prefabs/Feedback/";

        [MenuItem("Crowd Punch/Enemies/Build Cartoon Impact Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before building effects.");
            var settings = AssetDatabase.LoadAssetAtPath<CombatFeedbackSettings>("Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset");
            settings.PunchParticles = Create("Impacts/CFXR Hit D 3D (Yellow).prefab", "MonsterPunch", .45f);
            settings.EnemyParticles = Create("Impacts/CFXR Hit A (Red).prefab", "MonsterCollision", .30f);
            settings.EnvironmentParticles = Create("Impacts/CFXR2 Ground Hit.prefab", "MonsterGroundImpact", .40f);
            settings.ExplosionParticles = Create("Explosions/CFXR Explosion 1.prefab", "MonsterExplosion", .65f);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static ParticleSystem Create(string source, string name, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + source);
            if (prefab == null) throw new InvalidOperationException("Missing Cartoon FX prefab: " + source);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = name;
                // The existing game pools own lifetime, camera response and lighting budgets.
                foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
                foreach (var light in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
                foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.shader));
                        if (importer == null) continue;
                        var serialized = new SerializedObject(importer);
                        var pipeline = serialized.FindProperty("renderPipelineDetection");
                        // Cartoon FX's importer can retain the package's Built-in variant after import.
                        if (pipeline != null && pipeline.intValue != 2)
                        {
                            pipeline.intValue = 2; // ForceUniversalRenderPipeline, as defined by CFXR_ShaderImporter.
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            importer.SaveAndReimport();
                        }
                    }
                }
                foreach (var effect in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = effect.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.maxParticles = Mathf.Min(main.maxParticles, 40);
                    var size = main.startSize; size.constantMin *= scale; size.constantMax *= scale; main.startSize = size;
                    var speed = main.startSpeed; speed.constantMin *= scale; speed.constantMax *= scale; main.startSpeed = speed;
                    var shape = effect.shape; shape.radius *= scale;
                    if (effect.transform != root.transform) effect.transform.localPosition *= scale;
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, Destination + name + ".prefab");
                return saved.GetComponent<ParticleSystem>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
