using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // LOOP-002, VISION-004/006: use the level 13 wall dimensions and each court's authored outline.
    public static class CampaignBorderBuilder
    {
        private const string CampaignScenes = "Assets/CrowdPunch/Scenes/Campaign/";
        private static readonly string[] Chapters = { "ChapterOneVillage", "ChapterTwoFoundry",
            "ChapterThreeQuarry", "ChapterFourObservatory", "ChapterFiveNecropolis", "ChapterSixCanyon",
            "ChapterSevenVault", "ChapterEightCitadel", "ChapterNineCrown" };

        internal static string ShadowFolder(int level) => $"Assets/CrowdPunch/Data/{Chapters[(level - 1) / 10]}/BakedShadows";
        internal static string MainScene(int level) => $"{CampaignScenes}Campaign_{level:00}.unity";
        internal static string SubScene(int level) => $"{CampaignScenes}Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity";

        [MenuItem("Crowd Punch/Campaign/Bake Straight Border Lighting")]
        public static void BakeCampaign() => BakeRange(1, 81);

        public static void BakeRange(int first, int last)
        {
            if (first < 1 || last > 81 || first > last) throw new ArgumentOutOfRangeException(nameof(first));
            var levels = Enumerable.Range(first, last - first + 1).Where(level => level != 13);
            CampaignShadowBake.Begin(levels.Select(level => (MainScene(level), ShadowFolder(level), level <= 10)).ToArray());
        }

        [MenuItem("Crowd Punch/Campaign/Apply Straight Borders")]
        public static void ApplyCampaign()
        {
            CheckReady();
            for (int level = 1; level <= 81; level++)
            {
                EditorUtility.DisplayProgressBar("Campaign borders", $"Level {level} of 81", level / 81f);
                try { ApplyLevel(level); }
                catch (Exception error) { throw new InvalidOperationException($"Level {level} border authoring failed.", error); }
                finally { EditorUtility.ClearProgressBar(); }
            }
        }

        public static string ApplyLevel(int level)
        {
            CheckReady();
            if (level < 1 || level > 81) throw new ArgumentOutOfRangeException(nameof(level));
            if (level == 13) return "Level 13 is the authored reference.";
            string meshPath = $"{ShadowFolder(level)}/Meshes/Campaign_{level:00}_StraightWalls.asset";
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(SubScene(level));
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(SubScene(level), OpenSceneMode.Additive);
            try
            {
                var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshCollider>(true))
                    .Single(c => c.name == "Continuous Convex Floor" || c.name == "Continuous Round Floor");
                var rim = floor.transform.parent.Cast<Transform>()
                    .Where(t => t.name.StartsWith("Perimeter ", StringComparison.Ordinal)).ToArray();
                var navigation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NavigationArenaAuthoring>(true)).Single();
                var registered = new HashSet<MeshCollider>(navigation.perimeterColliders ?? Array.Empty<MeshCollider>());
                bool fitWideOpening = level == 4 || level == 6;
                bool updatePerimeter = registered.Count > 0 || rim.Any(t => AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>().sharedMesh) != meshPath);
                if (!updatePerimeter && !fitWideOpening)
                    return $"Level {level:00} already has {rim.Length} straight wall sections.";

                var outline = floor.name == "Continuous Round Floor"
                    ? CampaignBorderGeometry.RoundCourtOutline(rim, new Vector2(floor.bounds.center.x, floor.bounds.center.z))
                    : CampaignBorderGeometry.FloorOutline(floor);
                if (rim.Length > outline.Length) outline = CampaignBorderGeometry.SplitLongEdges(outline, rim);
                if (rim.Length != outline.Length)
                    throw new InvalidOperationException($"Floor has {outline.Length} edges and the scene has {rim.Length} perimeter sections.");
                var savedMeshes = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().ToDictionary(m => m.name);
                bool hasAsset = savedMeshes.Count > 0;
                Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
                Mesh SaveMesh(Mesh mesh)
                {
                    if (savedMeshes.TryGetValue(mesh.name, out var existing))
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        Object.DestroyImmediate(mesh);
                        EditorUtility.SetDirty(existing);
                        return existing;
                    }
                    if (hasAsset) AssetDatabase.AddObjectToAsset(mesh, meshPath);
                    else AssetDatabase.CreateAsset(mesh, meshPath);
                    hasAsset = true;
                    savedMeshes.Add(mesh.name, mesh);
                    return mesh;
                }
                float ground = floor.bounds.max.y;
                var wallMaterial = MaterialSource(rim[0].GetComponent<MeshRenderer>().sharedMaterial);
                if (updatePerimeter)
                {
                    var outside = CampaignBorderGeometry.Outside(outline);
                    var remaining = new List<Transform>(rim);
                    for (int i = 0; i < outline.Length; i++)
                    {
                        var start = outline[i]; var end = outline[(i + 1) % outline.Length];
                        var origin = (start + end) * .5f + CampaignBorderGeometry.Normal(start, end) * (CampaignBorderGeometry.Depth * .5f);
                        var wall = remaining.OrderBy(t => (new Vector2(t.position.x, t.position.z) - origin).sqrMagnitude).First();
                        remaining.Remove(wall);
                        var filter = wall.GetComponent<MeshFilter>();
                        var renderer = wall.GetComponent<MeshRenderer>();
                        var collider = wall.GetComponent<MeshCollider>();
                        var direction = (end - start).normalized;
                        var rotation = registered.Contains(collider)
                            ? Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y)) : Quaternion.identity;
                        renderer.sharedMaterial = MaterialSource(renderer.sharedMaterial);
                        renderer.sharedMaterials = new[] { renderer.sharedMaterial };
                        wall.SetPositionAndRotation(new Vector3(origin.x, ground, origin.y), rotation);
                        wall.localScale = Vector3.one;
                        filter.sharedMesh = SaveMesh(CampaignBorderGeometry.Wall(wall.name + " straight wall", outline, outside, origin, rotation, i, false));
                        collider.sharedMesh = SaveMesh(CampaignBorderGeometry.Wall(wall.name + " overlapping collision", outline, outside, origin, rotation, i, true));
                    }
                }
                int gates = CampaignGateBorderFit.Apply(scene, outline, ground, wallMaterial, SaveMesh, fitWideOpening);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the campaign SubScene.");
                return $"Level {level:00}: {rim.Length} straight perimeter sections and {gates} fitted gates.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static Material MaterialSource(Material material)
        {
            var path = material.GetTag("CrowdPunchSourceMaterial", false);
            if (!string.IsNullOrEmpty(path)) material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.HasProperty("_CameraCutaway") && material.GetFloat("_CameraCutaway") > .5f)
                throw new InvalidOperationException("A perimeter wall needs an opaque chapter surface.");
            return material;
        }

        private static void CheckReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                EditorSceneManager.GetSceneManagerSetup().Any(s => SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save open scenes and stop Play or lighting bakes before authoring borders.");
        }
    }
}
