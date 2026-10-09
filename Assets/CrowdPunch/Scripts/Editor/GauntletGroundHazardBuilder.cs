using System;
using System.Collections.Generic;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Levels/Build Ground Hazards Gauntlet 23")]
        public static void BuildGroundHazards()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                const string policyPath = Root + "Data/Settings/GroundHazardSettings.asset";
                var policy = AssetDatabase.LoadAssetAtPath<GroundHazardSettings>(policyPath);
                if (policy == null) { policy = ScriptableObject.CreateInstance<GroundHazardSettings>(); AssetDatabase.CreateAsset(policy, policyPath); }
                const string materialPath = Root + "Resources/GroundHazard.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                {
                    var shader = Shader.Find("CrowdPunch/GroundHazard");
                    if (shader == null) throw new InvalidOperationException("Import GroundHazard.shader before building.");
                    AssetDatabase.CreateAsset(new Material(shader), materialPath);
                }
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++) profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var ranges = new[] { Range(-9,1,6,23), Range(9,1,6,23), Range(0,11,12,3) };
                var design = new Level { Name = "Hot Footing", Outline = Clipped(32,34,3), Spacing = new Vector2(27.5f,29.5f),
                    Entry = new Vector2(0,-12), Lanes = Array.Empty<Vector4>(), Waves = new[] {
                        new Wave { Name = "Permanent Patches", B = 12, Delay = 2, Ranges = ranges },
                        new Wave { Name = "Pulsing Patches", B = 20, Delay = 3, Ranges = ranges } } };
                BuildLevel(22, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)), MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                foreach (string waveName in new[] { "CP23_01_Permanent_Patches", "CP23_02_Pulsing_Patches" })
                {
                    var waveData = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + waveName + ".asset"));
                    waveData.FindProperty("replenishWhileBossLives").boolValue = false;
                    waveData.ApplyModifiedPropertiesWithoutUndo();
                }
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_23/Gauntlet_23 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                encounter.gameObject.AddComponent<GroundHazardPolicyAuthoring>().settings = policy;
                var data = new SerializedObject(encounter); data.FindProperty("minimumPlayerDistance").floatValue = 4; data.ApplyModifiedPropertiesWithoutUndo();
                AddGroundPatch(encounter, "Permanent Rectangle", new Vector2(-3,-1), GroundHazardShape.Rectangle, false, 0, new Vector2(4,8), 0);
                AddGroundPatch(encounter, "Permanent Circle", new Vector2(4,5), GroundHazardShape.Circle, false, 0, new Vector2(5,5), 0);
                AddGroundPatch(encounter, "Periodic Rectangle", new Vector2(3,-5), GroundHazardShape.Rectangle, true, 1, new Vector2(4,5), 0);
                AddGroundPatch(encounter, "Periodic Circle", new Vector2(-4,8), GroundHazardShape.Circle, true, 1, new Vector2(5,5), 2);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_23.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Launch enemies into red floor patches. Amber patches warn before activating. Hazards hurt you even while dashing.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(23,names.arraySize);
                names.GetArrayElementAtIndex(22).stringValue = "Gauntlet_23"; titles.GetArrayElementAtIndex(22).stringValue = "23 Hot Footing";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s => s.path == Scenes + "Gauntlet_23.unity");
                int last = build.FindIndex(s => s.path == Scenes + "Gauntlet_22.unity");
                build.Insert(last + 1,new EditorBuildSettingsScene(Scenes + "Gauntlet_23.unity",true)); EditorBuildSettings.scenes = build.ToArray();
                GauntletNatureEnvironment.ApplyLevel(23); AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        private static void AddGroundPatch(EnemyWaveSequenceAuthoring sequence, string name, Vector2 point,
            GroundHazardShape shape, bool periodic, int firstWave, Vector2 size, float offset)
        {
            var a = new GameObject(name).AddComponent<GroundHazardAuthoring>();
            a.transform.position = new Vector3(point.x,-.965f,point.y); a.sequence = sequence;
            a.shape = shape; a.operation = periodic ? GroundHazardOperation.Periodic : GroundHazardOperation.AlwaysActive;
            a.firstWave = firstWave; a.width = size.x; a.depth = size.y; a.radius = size.x * .5f;
            a.damage = 12; a.damageInterval = .75f; a.inactiveDuration = 3; a.warningDuration = 1.5f;
            a.activeDuration = 2.5f; a.cycleOffset = offset;
        }
    }
}
