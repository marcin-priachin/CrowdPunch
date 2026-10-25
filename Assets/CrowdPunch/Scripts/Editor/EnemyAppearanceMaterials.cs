using System;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // Shared by prefab rebuilds so the palette survives re-sampling and model changes.
    public static class EnemyAppearanceMaterials
    {
        public const string AtlasPath = "Assets/CrowdPunch/Models/UltimateMonsters/Atlas_Monsters.png";

        public static void Configure(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material);
            if (path.StartsWith(EnemySnakePrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyBaselinePrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyRangedPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyElitePrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyArmoredPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyLinkedPairPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(ChickenModelBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(RollingSpikeBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyDasherPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyTrailPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyWizardPrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)
                || path.StartsWith(EnemyExplosivePrefabBuilder.MaterialFolder + "/", StringComparison.Ordinal)) return;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null) throw new InvalidOperationException("Ultimate Monsters color atlas is missing.");
            if (!material.HasProperty("_ColorMap")) return;
            material.SetTexture("_ColorMap", atlas);
            material.SetColor("_BaseColor", new Color(1.15f, 1.15f, 1.15f, 1));
            foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount" })
                if (material.HasProperty(property)) material.SetFloat(property, 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        public static void ApplyExploderPalette(SkinnedMeshRenderer renderer, int index)
        {
            // Remap only the green body swatch to the atlas's existing amber Dragon swatch.
            // Eyes and pale spikes keep their original UVs; the supplied atlas stays untouched.
            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
            mesh.name = "ExploderPalette" + index;
            var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++)
                if (Mathf.Abs(uv[i].x - .078125f) < .008f && Mathf.Abs(uv[i].y - .515625f) < .008f)
                    uv[i] = new Vector2(.078125f, .234375f);
            mesh.uv = uv;
            string path = $"Assets/CrowdPunch/Animation/EnemyExplosivePalette_{index:00}.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
            EditorUtility.SetDirty(saved);
            renderer.sharedMesh = saved;
        }

        [MenuItem("Crowd Punch/Enemies/Apply Monster Atlas")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before changing enemy materials.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/CrowdPunch/Materials/Enemies" }))
                Configure(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
        }
    }
}
