using System;
using System.Collections.Generic;
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
    // LOOP-002/008: expand existing approach banks, keeping objective routes and safe ground clear.
    public static class CampaignSpawnLayoutBuilder
    {
        [MenuItem("Crowd Punch/Campaign/Apply Spawn Spacing")]
        public static void Apply() => ApplyLevels(1, 80);

        public static void ApplyLevels(int first, int last)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var changed = new HashSet<EnemyWaveSettings>();
            try
            {
                for (int level = first; level <= last; level++)
                {
                    string path = $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity";
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var arena = Find<ArenaAuthoring>(scene).Single();
                    var hazards = Find<GroundHazardAuthoring>(scene);
                    var track = Find<TrackObjectAuthoring>(scene).SingleOrDefault();
                    var rail = track == null ? Array.Empty<Vector3>() : new[] { track.transform.position }
                        .Concat(track.corners.Select(c => c.position)).Concat(new[] { track.destination.position }).ToArray();
                    Vector2 center = Xz(arena.transform.position) + new Vector2(arena.SpacingCenterOffset.x, arena.SpacingCenterOffset.z);
                    Vector2 half = new Vector2(arena.SpacingSize.x, arena.SpacingSize.z) * .5f;
                    foreach (var crowd in Find<EnemyWaveSequenceAuthoring>(scene))
                    {
                        var room = crowd.GetComponent<BarricadeRoomAuthoring>();
                        Vector2 minimum = center - half + Vector2.one * .75f;
                        Vector2 maximum = center + half - Vector2.one * .75f;
                        if (room != null)
                        {
                            minimum = Vector2.Max(minimum, room.minimum + Vector2.one * .75f);
                            maximum = Vector2.Min(maximum, room.maximum - Vector2.one * .75f);
                        }
                        foreach (var wave in crowd.Waves)
                        {
                            if (!AssetDatabase.GetAssetPath(wave).StartsWith("Assets/CrowdPunch/Data/Campaign/Waves/"))
                                throw new InvalidOperationException("Campaign crowd must use campaign-owned waves.");
                            if (!changed.Add(wave)) continue;
                            var data = new SerializedObject(wave);
                            bool large = level % 10 == 8;
                            // PROTECT-001: retain the existing finite defense cadence.
                            if (wave.SpawnMode != EnemyWaveSpawnMode.Batched)
                            {
                                data.FindProperty("spawnMode").intValue = (int)EnemyWaveSpawnMode.Batched;
                                data.FindProperty("batchSize").intValue = large ? 8 : level == 1 ? 3 : 4;
                                data.FindProperty("batchInterval").floatValue = large ? 2f : level == 1 ? 1f : 1.5f;
                            }
                            var rectangles = data.FindProperty("spawnRectangles");
                            for (int i = 0; i < rectangles.arraySize; i++)
                            {
                                var item = rectangles.GetArrayElementAtIndex(i);
                                Vector3 position = item.FindPropertyRelative("Center").vector3Value;
                                Vector2 bankCenter = Xz(position);
                                Vector2 bankHalf = new Vector2(item.FindPropertyRelative("Width").floatValue,
                                    item.FindPropertyRelative("Depth").floatValue) * .5f;
                                bool lateral = Mathf.Abs(bankCenter.x - center.x) / Mathf.Max(1, half.x)
                                    > Mathf.Abs(bankCenter.y - center.y) / Mathf.Max(1, half.y);
                                Vector2 target = large ? (lateral ? new Vector2(24, 64) : new Vector2(64, 24))
                                    : lateral ? new Vector2(12, 24) : new Vector2(28, 14);
                                if (room != null) target = new Vector2(10, 22);
                                if (crowd.GetComponent<ProtectedPointAuthoring>() != null) target = new Vector2(28, 8);
                                if (level % 10 == 0 && Mathf.Abs(bankCenter.x) < 1 && Mathf.Abs(bankCenter.y) < 1)
                                    target = new Vector2(28, 28);
                                Vector2 low = bankCenter - bankHalf, high = bankCenter + bankHalf;
                                // Keep each bank on its authored side; expansion is permitted inward and outward.
                                Vector2 bankMin = minimum, bankMax = maximum;
                                if (lateral)
                                {
                                    if (bankCenter.x < center.x) bankMax.x = Mathf.Min(bankMax.x, center.x - 2);
                                    else bankMin.x = Mathf.Max(bankMin.x, center.x + 2);
                                }
                                else if (bankCenter.y > center.y + 2) bankMin.y = Mathf.Max(bankMin.y, center.y + 2);
                                else if (bankCenter.y < center.y - 2) bankMax.y = Mathf.Min(bankMax.y, center.y - 2);
                                if (crowd.GetComponent<ProtectedPointAuthoring>() != null) bankMin.y = Mathf.Max(bankMin.y, 37);
                                for (int step = 0; step < 256; step++)
                                {
                                    bool grew = false;
                                    for (int side = 0; side < 4; side++)
                                    {
                                        int axis = side / 2;
                                        if (high[axis] - low[axis] >= target[axis] - .001f) continue;
                                        Vector2 nextLow = low, nextHigh = high;
                                        if (side % 2 == 0) nextLow[axis] -= .25f; else nextHigh[axis] += .25f;
                                        if (nextLow.x < bankMin.x || nextLow.y < bankMin.y || nextHigh.x > bankMax.x || nextHigh.y > bankMax.y) continue;
                                        if (!Clear(nextLow, nextHigh, hazards, rail)) continue;
                                        low = nextLow; high = nextHigh; grew = true;
                                    }
                                    if (!grew) break;
                                }
                                Vector2 resolvedCenter = (low + high) * .5f;
                                item.FindPropertyRelative("Center").vector3Value = new Vector3(resolvedCenter.x, position.y, resolvedCenter.y);
                                item.FindPropertyRelative("Width").floatValue = high.x - low.x;
                                item.FindPropertyRelative("Depth").floatValue = high.y - low.y;
                            }
                            data.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(wave);
                        }
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log($"Campaign spawn spacing: tuned {changed.Count} waves in levels {first}-{last}.");
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        private static Vector2 Xz(Vector3 p) => new Vector2(p.x, p.z);

        private static bool Clear(Vector2 low, Vector2 high, GroundHazardAuthoring[] hazards, Vector3[] rail)
        {
            foreach (var hazard in hazards)
            {
                Vector2 extent = hazard.shape == GroundHazardShape.Circle ? Vector2.one * hazard.radius
                    : new Vector2(hazard.width, hazard.depth) * .5f;
                float angle = hazard.transform.eulerAngles.y * Mathf.Deg2Rad;
                extent = new Vector2(Mathf.Abs(Mathf.Cos(angle)) * extent.x + Mathf.Abs(Mathf.Sin(angle)) * extent.y,
                    Mathf.Abs(Mathf.Sin(angle)) * extent.x + Mathf.Abs(Mathf.Cos(angle)) * extent.y);
                Vector2 p = Xz(hazard.transform.position);
                if (low.x <= p.x + extent.x + .75f && high.x >= p.x - extent.x - .75f
                    && low.y <= p.y + extent.y + .75f && high.y >= p.y - extent.y - .75f) return false;
            }
            for (int segment = 1; segment < rail.Length; segment++)
            {
                int samples = Mathf.CeilToInt(Vector3.Distance(rail[segment - 1], rail[segment]) * 4);
                for (int i = 0; i <= samples; i++)
                {
                    Vector2 p = Xz(Vector3.Lerp(rail[segment - 1], rail[segment], i / (float)Mathf.Max(1, samples)));
                    Vector2 nearest = Vector2.Max(low, Vector2.Min(high, p));
                    if (Vector2.Distance(p, nearest) < 3.65f) return false;
                }
            }
            return true;
        }
    }
}
