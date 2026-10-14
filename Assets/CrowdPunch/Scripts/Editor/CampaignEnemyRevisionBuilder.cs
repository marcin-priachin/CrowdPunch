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
        [MenuItem("Crowd Punch/Campaign/Apply Four Enemy Revision")]
        public static void ApplyCampaignEnemyRevision()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before authoring.");

            var chapters = new[] {
                (Offset: 40, Designs: ChapterFiveDesigns(), Hints: ChapterFiveHints),
                (Offset: 50, Designs: ChapterSixDesigns(), Hints: ChapterSixHints),
                (Offset: 60, Designs: ChapterSevenDesigns(), Hints: ChapterSevenHints),
                (Offset: 70, Designs: ChapterEightDesigns(), Hints: ChapterEightHints)
            };
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if (catalog == null || catalog.Count != 80)
                throw new InvalidOperationException("The existing 80-level campaign is required.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int levels = 0, waves = 0;
            try
            {
                // Preflight every encounter before changing saved assets. Scene geometry,
                // authored safe spawn banks and stable wave GUIDs are retained.
                foreach (var chapter in chapters)
                    for (int i = 0; i < chapter.Designs.Length; i++)
                    {
                        var design = chapter.Designs[i];
                        if (!design.Waves.Any(HasCampaignRevisionEnemy)) continue;
                        int number = chapter.Offset + i + 1;
                        if (!catalog.Get(number - 1).Available)
                            throw new InvalidOperationException($"Campaign level {number} is not installed.");
                        EditorSceneManager.OpenScene(CampaignSub(number), OpenSceneMode.Single);
                        var profiles = LoadCampaignProfiles();
                        var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                        if (encounter == null || encounter.Waves.Count != design.Waves.Length
                            || encounter.Waves.Any(w => w == null || !AssetDatabase.GetAssetPath(w).StartsWith(CampaignData)))
                            throw new InvalidOperationException($"Campaign level {number} has unexpected wave wiring.");
                        if (encounter.barricade != null || encounter.bossEncounter != null
                            || encounter.chickenBoss != null || encounter.rollingBoss != null || encounter.dinoBoss != null
                            || encounter.GetComponent<ProtectedPointAuthoring>() != null)
                            throw new InvalidOperationException($"Campaign level {number} must remain ordinary wave clearing.");
                        foreach (var recipe in design.Waves)
                        {
                            int[] counts = { recipe.B, recipe.R, recipe.X, recipe.D, recipe.E, recipe.A,
                                recipe.Wizard, recipe.Trail, recipe.Dragon, recipe.Snake, recipe.Ninja, recipe.LinkedPairs };
                            for (int p = 0; p < counts.Length; p++)
                                if (counts[p] > 0 && profiles[p].EnemyPrefab == null)
                                    throw new InvalidOperationException($"Missing enemy prefab: {profiles[p].name}");
                        }
                    }

                foreach (var chapter in chapters)
                    for (int i = 0; i < chapter.Designs.Length; i++)
                    {
                        var design = chapter.Designs[i];
                        if (!design.Waves.Any(HasCampaignRevisionEnemy)) continue;
                        int number = chapter.Offset + i + 1;
                        EditorSceneManager.OpenScene(CampaignSub(number), OpenSceneMode.Single);
                        // Opening a scene can unload assets held only by a previous
                        // local array. Resolve profiles after the scene switch.
                        var profiles = LoadCampaignProfiles();
                        var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                        for (int w = 0; w < design.Waves.Length; w++)
                        {
                            var existing = encounter.Waves[w];
                            var recipe = design.Waves[w];
                            recipe.Ranges = existing.SpawnRectangles.ToArray();
                            string path = AssetDatabase.GetAssetPath(existing);
                            WaveAsset(existing.name, recipe, profiles, path.Substring(0, path.LastIndexOf('/') + 1));
                            var data = new SerializedObject(existing);
                            data.FindProperty("replenishWhileBossLives").boolValue = false;
                            data.ApplyModifiedPropertiesWithoutUndo();
                            waves++;
                        }
                        SetCampaignHint(number, chapter.Hints[i]);
                        levels++;
                    }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log($"LINK/DRAGON/SNAKE/NINJA campaign revision applied to {levels} levels and {waves} waves. Geometry, legacy content and campaign identifiers preserved.");
        }

        private static bool HasCampaignRevisionEnemy(Wave wave) =>
            wave.LinkedPairs > 0 || wave.Dragon > 0 || wave.Snake > 0 || wave.Ninja > 0;
    }
}
