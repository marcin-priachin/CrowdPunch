using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // PILLAR-002/003/005/006, VISION-005/006: presentation fits the existing ECS box and base pivot.
    public static class DinoPillarVisualBuilder
    {
        public const string ModelPath = "Assets/CrowdPunch/Models/KayKit_Dungeon_Pack_1.1_FREE/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)/pillar.fbx";
        public const string Output = "Assets/CrowdPunch/Data/DinoBoss/Pillars";
        private const string VisualName = "KayKit Pillar";

        [MenuItem("Crowd Punch/Bosses/Apply KayKit Pillars To Campaign 40 And 80")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before replacing pillar visuals.");
            var active = SceneManager.GetActiveScene();
            try
            {
                foreach (int stage in new[] { 40, 80 })
                {
                    string path = $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}/Campaign_{stage} Sub Scene.unity";
                    var scene = SceneManager.GetSceneByPath(path);
                    bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                    if (alreadyOpen && scene.isDirty)
                        throw new InvalidOperationException("Save the encounter before replacing pillars: " + path);
                    if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var pillars = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FallingPillarAuthoring>(true)).ToArray();
                        if (pillars.Length != 3) throw new InvalidOperationException("Expected three pillars in " + path);
                        foreach (var pillar in pillars) Build(pillar, stage == 80);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }

        private static void Build(FallingPillarAuthoring pillar, bool citadel)
        {
            if (pillar.boss == null || pillar.boss.settings == null)
                throw new InvalidOperationException("The pillar needs its existing boss settings.");
            var tuning = pillar.boss.settings;
            var meshes = FittedMeshes(tuning.pillarWidth, tuning.pillarHeight);
            var materials = Materials(citadel);
            // Only the old presentation children are replaced; root IDs and authored gameplay remain intact.
            foreach (Transform child in pillar.GetComponentsInChildren<Transform>(true))
                if (child != null && (child.name == "Pillar Shaft" || child.name == "Stone Band" || child.name == VisualName))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in pillar.transform.Cast<Transform>().ToArray())
                if (child.name == "GameObject" && child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            var visual = new GameObject(VisualName);
            visual.transform.SetParent(pillar.transform, false);
            // A single material per renderer keeps each baked render entity on PillarVisualBaker's tint path.
            // Multiple submeshes create extra render entities that do not inherit that gameplay colour component.
            for (int i = 0; i < meshes.Length; i++)
            {
                var piece = new GameObject(new[] { "Stone", "Collar", "Brass" }[i]);
                piece.transform.SetParent(visual.transform, false);
                piece.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials[i];
                renderer.forceMeshLod = 0;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveGI = ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(piece, 0);
            }
        }

        internal static void RefreshMaterials(FallingPillarAuthoring pillar, bool citadel)
        {
            var visual = pillar.transform.Find(VisualName);
            if (visual == null) return;
            var materials = Materials(citadel);
            for (int i = 0; i < materials.Length; i++)
                visual.Find(new[] { "Stone", "Collar", "Brass" }[i]).GetComponent<MeshRenderer>().sharedMaterial = materials[i];
        }

        private static Mesh[] FittedMeshes(float width, float height)
        {
            Directory.CreateDirectory(Output + "/Meshes");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Missing selected KayKit pillar.fbx.");
            var filter = source.GetComponentInChildren<MeshFilter>();
            var original = filter.sharedMesh;
            var vertices = original.vertices;
            float scale = width / original.bounds.size.x;
            // Stretch only the two plain shaft spans. Capitals, bevels and the middle collar retain their proportions.
            float lower = .5f * scale;
            float upper = height - .8f * scale;
            float collar = .2f * scale;
            float middle = (lower + upper) * .5f;
            if (upper - lower <= collar) throw new InvalidOperationException("Pillar is too short for the KayKit caps.");
            float[] sourceY = { 0, .5f, 1.75f, 1.95f, 3.2f, 4 };
            float[] targetY = { 0, lower, middle - collar * .5f, middle + collar * .5f, upper, height };
            var originalVertices = (Vector3[])vertices.Clone();
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                int interval = 0;
                while (interval < sourceY.Length - 2 && p.y > sourceY[interval + 1]) interval++;
                p.y = Mathf.Lerp(targetY[interval], targetY[interval + 1], Mathf.InverseLerp(sourceY[interval], sourceY[interval + 1], p.y));
                p.x = (p.x - original.bounds.center.x) * scale;
                p.z = (p.z - original.bounds.center.z) * scale;
                vertices[i] = p;
            }
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            var indices = original.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                float min = Mathf.Min(originalVertices[indices[i]].y, originalVertices[indices[i + 1]].y, originalVertices[indices[i + 2]].y);
                float max = Mathf.Max(originalVertices[indices[i]].y, originalVertices[indices[i + 1]].y, originalVertices[indices[i + 2]].y);
                int slot = min >= 1.749f && max <= 1.951f ? 1 :
                    max <= .251f || (min >= 3.199f && max <= 3.451f) ? 2 : 0;
                triangles[slot].AddRange(new[] { indices[i], indices[i + 1], indices[i + 2] });
            }
            string prefix = Output + "/Meshes/KayKit pillar " + width.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "x" + height.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var meshes = new Mesh[3];
            for (int i = 0; i < meshes.Length; i++)
            {
                string name = new[] { "Stone", "Collar", "Brass" }[i];
                string path = prefix + " " + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
                var used = triangles[i].Distinct().ToArray();
                var remap = used.Select((index, slot) => (index, slot)).ToDictionary(p => p.index, p => p.slot);
                var uv = original.uv;
                mesh.Clear(); mesh.name = "KayKit pillar - fitted " + name;
                mesh.vertices = used.Select(index => vertices[index]).ToArray();
                mesh.uv = used.Select(index => uv[index]).ToArray();
                mesh.triangles = triangles[i].Select(index => remap[index]).ToArray();
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh); meshes[i] = mesh;
            }
            return meshes;
        }

        private static Material[] Materials(bool citadel)
        {
            string folder = Output + "/Materials";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string sourceFolder = citadel ? ChapterEightCitadelAssets.Output : ChapterFourObservatoryAssets.Output;
            string[] recipes = citadel ? new[] { "PillarStone", "Slate", "PillarBrass" } : new[] { "Limestone", "Sage", "Ochre" };
            var result = new Material[3];
            for (int i = 0; i < result.Length; i++)
            {
                string sourcePath = sourceFolder + "/Materials/" + recipes[i] + " Local.mat";
                var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (source == null) throw new InvalidOperationException("Missing chapter material " + sourcePath);
                string path = folder + "/" + (citadel ? "Citadel" : "Observatory") + " " + new[] { "Stone", "Collar", "Brass" }[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
                else material.CopyPropertiesFromMaterial(source);
                material.name = Path.GetFileNameWithoutExtension(path);
                material.enableInstancing = true;
                material.SetFloat("_ObjectSpacePaint", 1);
                material.SetFloat("_UseEntityColor", 1);
                material.SetFloat("_MultiplyEntityColor", 1);
                material.SetFloat("_CameraCutaway", 0);
                material.SetFloat("_SceneryHaze", 0);
                // Compensate the legacy resting pillar tint while retaining its existing event flashes.
                var restTint = new Color(.7f, .8f, .85f, 1);
                var pigment = source.GetColor("_BaseColor");
                material.SetColor("_BaseColor", new Color(pigment.r / restTint.r, pigment.g / restTint.g, pigment.b / restTint.b, pigment.a));
                material.SetFloat("_TextureStrength", i == 0 ? .10f : 0);
                EditorUtility.SetDirty(material); result[i] = material;
            }
            return result;
        }
    }
}
