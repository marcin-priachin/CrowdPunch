using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Campaign/Apply Barricade Sector Difficulty")]
        public static void ApplyBarricadeSectorDifficulty()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int index = 1; index < CampaignBarricadeLevels.Length; index++)
                {
                    int level = CampaignBarricadeLevels[index];
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level));
                    AuthorBarricadeSectorDifficulty(scene, level, index);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("BARRICADE-009: distinct ascending sectors with preserved final layouts and mixed crowds.");
        }

        private static void AuthorBarricadeSectorDifficulty(Scene scene, int level, int index)
        {
            var arena = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArenaAuthoring>()).Single();
            var crowds = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>())
                .Where(c => c.barricade != null).OrderBy(c => c.barricade.transform.position.z).ToArray();
            if (crowds.Length != index + 1) throw new InvalidOperationException("Author the complete barricade room chain first.");
            var final = crowds.Last();
            var finalHazards = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroundHazardAuthoring>())
                .Where(h => h.sequence == final).OrderBy(h => h.transform.position.x).ToArray();
            var profiles = LoadCampaignProfiles();
            int finalCount = new[] {12, 14, 14, 16, 14, 16, 18}[index];
            for (int room = 0; room < crowds.Length; room++)
            {
                var crowd = crowds[room]; var bounds = crowd.GetComponent<BarricadeRoomAuthoring>();
                bool last = room == index;
                float progress = room / (float)index;
                int total = last ? finalCount : Mathf.FloorToInt(Mathf.Lerp(8, finalCount, progress));
                // Keep ample replenishing Baselines as usable gate/armor ammunition.
                int explosives = last ? 2 : 1;
                int ranged = room > 0 ? 1 : 0;
                int dashers = progress >= .5f ? 1 : 0;
                int armored = progress >= .75f ? 1 : 0;
                int[] counts = {total - explosives - ranged - dashers - armored, ranged, explosives, dashers, 0, armored};
                var wave = crowd.Waves.Single();
                var data = new SerializedObject(wave);
                data.FindProperty("totalEnemyCount").intValue = total;
                var entries = data.FindProperty("enemies"); entries.ClearArray();
                for (int kind = 0; kind < counts.Length; kind++)
                {
                    if (counts[kind] <= 0) continue;
                    int at = entries.arraySize++; var item = entries.GetArrayElementAtIndex(at);
                    item.FindPropertyRelative("Settings").objectReferenceValue = profiles[kind];
                    item.FindPropertyRelative("MinimumCount").intValue = counts[kind];
                    item.FindPropertyRelative("Weight").floatValue = 0;
                }
                // Room replenishment already supplies Baselines without creating extra roots.
                data.FindProperty("armoredAmmunitionProfile").objectReferenceValue = null;
                if (!last)
                {
                    foreach (var rock in arena.GetComponentsInChildren<SolidObstacleAuthoring>()
                        .Where(s => !s.name.StartsWith("Gate terrain") && s.transform.position.z >= bounds.minimum.y
                            && s.transform.position.z <= bounds.maximum.y).ToArray())
                        UnityEngine.Object.DestroyImmediate(rock.gameObject);
                    foreach (var hazard in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroundHazardAuthoring>())
                        .Where(h => h.sequence == crowd).ToArray()) UnityEngine.Object.DestroyImmediate(hazard.gameObject);
                    AuthorEasierBarricadeLayout(arena, bounds, room);
                    int hazardCount = Mathf.FloorToInt(finalHazards.Length * progress);
                    for (int h = 0; h < hazardCount; h++)
                    {
                        var copy = UnityEngine.Object.Instantiate(finalHazards[h].gameObject);
                        copy.name = $"Room hazard {room + 1:00} - Gentle warning";
                        var patch = copy.GetComponent<GroundHazardAuthoring>(); patch.sequence = crowd;
                        float size = Mathf.Lerp(.4f, .8f, progress);
                        patch.width *= size; patch.depth *= size; patch.radius *= size;
                        patch.operation = GroundHazardOperation.Periodic;
                        patch.inactiveDuration = 5; patch.warningDuration = 1.5f; patch.activeDuration = 2;
                        float sign = (room + h) % 2 == 0 ? -1 : 1;
                        copy.transform.position = new Vector3(sign * (bounds.maximum.x - bounds.minimum.x) * .2f,
                            finalHazards[h].transform.position.y, Mathf.Lerp(bounds.minimum.y, bounds.maximum.y, .72f));
                    }
                    var banks = data.FindProperty("spawnRectangles"); banks.arraySize = 2;
                    for (int bank = 0; bank < 2; bank++)
                    {
                        var item = banks.GetArrayElementAtIndex(bank);
                        item.FindPropertyRelative("Center").vector3Value = new Vector3((bank == 0 ? -1 : 1)
                            * (BarricadeRoomWidths[index] * .5f - 4), 2,
                            Mathf.Lerp(bounds.minimum.y, bounds.maximum.y, .48f));
                        item.FindPropertyRelative("Width").floatValue = 2;
                        item.FindPropertyRelative("Depth").floatValue = 8;
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(wave);
            }
        }

        private static void AuthorEasierBarricadeLayout(ArenaAuthoring arena, BarricadeRoomAuthoring bounds, int rank)
        {
            float span = bounds.maximum.y - bounds.minimum.y;
            float side = (rank % 2 == 0 ? -1 : 1) * (bounds.maximum.x - bounds.minimum.x) * .23f;
            int depth = rank < 2 ? rank + 2 : rank;
            int islands = rank < 2 ? 1 : 2;
            for (int island = 0; island < islands; island++)
            {
                float z = bounds.minimum.y + span * (island == 0 ? .35f + rank * .025f : .58f);
                var rock = CampaignRock(arena, new Vector2(island == 0 ? side : -side, z), new Vector2Int(2, depth));
                rock.name = $"Sector {rank + 1:00} {(islands == 1 ? "side pocket" : "offset islands")} {island + 1}";
            }
        }
    }
}
