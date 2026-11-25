using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Campaign/Apply Chapter One Encounter Revision")]
        public static void ApplyChapterOneEncounterRevision()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before authoring.");
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if (catalog == null || catalog.Count < 10)
                throw new InvalidOperationException("An installed Chapter One is required.");
            var designs = ChapterOneDesigns();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Validate existing wiring before any write. Never recreate scenery,
                // lighting, objectives, spawn regions or asset identities for this pass.
                for (int level = 1; level <= 10; level++)
                {
                    if (!catalog.Get(level - 1).Available)
                        throw new InvalidOperationException($"Campaign level {level} is not installed.");
                    EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    int count = level == 10 ? 1 : designs[level - 1].Waves.Length;
                    if (encounter == null || encounter.Waves.Count != count)
                        throw new InvalidOperationException($"Unexpected wave wiring in level {level}.");
                    for (int w = 0; w < count; w++)
                    {
                        var existing = encounter.Waves[w];
                        string name = level == 10 ? "CP10_01_Gatekeeper"
                            : $"CP{level:00}_{w + 1:00}_" + designs[level - 1].Waves[w].Name.Replace(' ', '_');
                        if (existing == null || AssetDatabase.GetAssetPath(existing) != CampaignData + "Waves/" + name + ".asset")
                            throw new InvalidOperationException($"Unexpected wave asset in level {level}.");
                    }
                }
                var profiles = LoadCampaignProfiles();
                foreach (int p in new[] { 0, 1, 12 })
                    if (profiles[p].EnemyPrefab == null)
                        throw new InvalidOperationException("Missing prefab for " + profiles[p].name);

                for (int level = 1; level <= 10; level++)
                {
                    var recipes = level == 10
                        ? new[] { W("Gatekeeper", 10, Array.Empty<EnemyWaveSettings.SpawnRectangle>(), batch: 4, interval: 6) }
                        : designs[level - 1].Waves;
                    for (int w = 0; w < recipes.Length; w++)
                    {
                        string name = $"CP{level:00}_{w + 1:00}_" + recipes[w].Name.Replace(' ', '_');
                        var existing = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(CampaignData + "Waves/" + name + ".asset");
                        recipes[w].Ranges = existing.SpawnRectangles.ToArray();
                        WaveAsset(name, recipes[w], profiles, CampaignData + "Waves/");
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("LOOP-011: Chapter One revised in place; Mushroom Shooter at 5/8/9, timed practice at 2/8/9, 270 enemies at 8.");
        }
    }
}
