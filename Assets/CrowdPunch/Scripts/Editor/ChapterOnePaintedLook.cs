using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-005/006: appearance-only migration, retaining layout and baked receiver identity.
    public static class ChapterOnePaintedLook
    {
        public const string Output = "Assets/CrowdPunch/Data/ChapterOneVillage/PaintedLook";
        private const string ShaderName = "CrowdPunch/Cel/Painted Foundry";
        private static readonly Dictionary<string, Material> Prepared = new();

        [MenuItem("Crowd Punch/Art Direction/Apply Painted Look to Chapter One")]
        public static void Apply()
        {
            for (int stage = 1; stage <= 10; stage++) ApplyStage(stage);
        }

        public static void ApplyStage(int stage)
        {
            if (stage < 1 || stage > 10) throw new ArgumentOutOfRangeException(nameof(stage));
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and stop Play/baking before applying the painted look.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var sub = EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity");
                ApplyScene(sub);
                EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
                ApplyScene(main);
                EditorSceneManager.SaveScene(main);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void ApplyScene(Scene scene)
        {
            Prepared.Clear();
            Directory.CreateDirectory(Output + "/Materials");
            AssetDatabase.Refresh();
            if (Shader.Find(ShaderName) == null) throw new InvalidOperationException("Painted surface shader is missing.");
            bool sub = scene.path.EndsWith(" Sub Scene.unity", StringComparison.Ordinal);
            var roots = scene.GetRootGameObjects();
            foreach (var renderer in roots.Where(g => sub || g.name == ChapterOneVillageBuilder.RootName)
                .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) return source;
                    // Moving/destructible objectives keep their existing ECS tint/material contract.
                    if (sub && !GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) return source;
                    string original = source.GetTag("CrowdPunchPaintedVillageSource", false, AssetDatabase.GetAssetPath(source));
                    if (!original.StartsWith(ChapterOneVillageAssets.Output + "/", StringComparison.Ordinal)) return source;
                    var recipe = AssetDatabase.LoadAssetAtPath<Material>(original);
                    if (recipe == null) throw new InvalidOperationException("Missing village source: " + original);
                    // Grounds and every gameplay renderer have independent non-cutaway variants.
                    bool ground = recipe.HasProperty("_Meadow") && recipe.GetFloat("_Meadow") > .5f;
                    bool cutaway = !sub && !ground;
                    return MaterialFor(recipe, cutaway, source);
                }).ToArray();
                EditorUtility.SetDirty(renderer);
            }
            if (!sub)
            {
                var volume = roots.SelectMany(g => g.GetComponentsInChildren<Volume>(true)).Single(v => v.priority == 150);
                volume.sharedProfile = Profile();
                EditorUtility.SetDirty(volume);
                // Existing village sky, sun, haze distances, warmth and bake stay unchanged.
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Material MaterialFor(Material source, bool cutaway, Material receiver)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            bool explicitMask = receiver.HasProperty("_UseBakedShadowMask") && receiver.GetFloat("_UseBakedShadowMask") > .5f;
            bool changedChart = explicitMask && (!source.HasProperty("_BakedShadowMask") || receiver.GetTexture("_BakedShadowMask") != source.GetTexture("_BakedShadowMask") ||
                receiver.GetVector("_BakedShadowST") != source.GetVector("_BakedShadowST"));
            // A later rebake may replace a chart independently of the original colour recipe.
            string chart = changedChart ? "_" + Hash128.Compute(AssetDatabase.GetAssetPath(receiver.GetTexture("_BakedShadowMask")) + receiver.GetVector("_BakedShadowST").ToString("R")) : "";
            string path = Output + "/Materials/" + AssetDatabase.AssetPathToGUID(sourcePath) + (cutaway ? "_Cutaway" : "_Solid") + chart + ".mat";
            if (Prepared.TryGetValue(path, out var ready)) return ready;
            var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            // Copy only the shared rendering contract into a fresh native property layout.
            // Cross-shader CopyPropertiesFromMaterial can retain the source's property table.
            var material = new Material(Shader.Find(ShaderName));
            foreach (string property in new[] { "_BaseMap", "_DirtMap", "_BakedShadowMask" })
                if (source.HasProperty(property))
                {
                    material.SetTexture(property, source.GetTexture(property));
                    material.SetTextureScale(property, source.GetTextureScale(property));
                    material.SetTextureOffset(property, source.GetTextureOffset(property));
                }
            foreach (string property in new[] { "_TextureScale", "_Meadow", "_ReceiveShadows", "_UseBakedShadowMask" })
                if (source.HasProperty(property)) material.SetFloat(property, source.GetFloat(property));
            foreach (string property in new[] { "_Bounds", "_BakedShadowST", "_PathColor" })
                if (source.HasProperty(property)) material.SetVector(property, source.GetVector(property));
            if (explicitMask)
            {
                material.SetFloat("_UseBakedShadowMask", 1);
                material.SetTexture("_BakedShadowMask", receiver.GetTexture("_BakedShadowMask"));
                material.SetVector("_BakedShadowST", receiver.GetVector("_BakedShadowST"));
            }
            material.name = Path.GetFileNameWithoutExtension(path);
            material.shaderKeywords = Array.Empty<string>();
            material.enableInstancing = true;
            material.SetOverrideTag("CrowdPunchPaintedVillageSource", sourcePath);
            // Rebinding after a later light bake resolves to this painted, solid recipe.
            material.SetOverrideTag("CrowdPunchSourceMaterial", path);
            bool atlas = source.shader.name == "CrowdPunch/Cel/Environment" && source.mainTexture != null;
            bool meadow = source.HasProperty("_Meadow") && source.GetFloat("_Meadow") > .5f;
            bool paving = source.name == "Warm courtyard paving" || source.GetTag("CrowdPunchSourceMaterial", false, "").EndsWith("Warm courtyard paving.mat", StringComparison.Ordinal);
            bool roof = source.name == "Terracotta roof";
            bool plaster = source.name == "Cream plaster";
            var tint = source.color;
            material.SetColor("_BaseColor", tint);
            material.SetColor("_WashColor", atlas ? new Color(.86f, .88f, .79f) : Color.Lerp(tint, new Color(.63f, .58f, .42f), .24f));
            material.SetColor("_ShadowColor", new Color(.45f, .51f, .64f));
            material.SetColor("_LightColor", new Color(1, .96f, .83f));
            material.SetFloat("_Contrast", .78f);
            material.SetFloat("_WashStrength", paving ? .12f : atlas ? .12f : .28f);
            material.SetFloat("_Variation", paving ? .035f : meadow ? .13f : .12f);
            material.SetFloat("_GrainStrength", paving ? .035f : .075f);
            material.SetFloat("_AtlasColor", atlas ? 1 : 0);
            if (atlas) material.SetFloat("_TextureStrength", 0);
            else if (material.HasProperty("_TextureStrength"))
                material.SetFloat("_TextureStrength", paving ? .18f : meadow ? .28f : roof ? .32f : plaster ? 0 : .35f);
            material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
            material.SetFloat("_CameraCutaway", cutaway ? 1 : 0);
            material.SetFloat("_ObjectSpacePaint", 0);
            material.SetFloat("_UseEntityColor", 0);
            material.SetFloat("_Window", 0);
            material.SetFloat("_SeamStrength", roof ? .16f : 0);
            if (saved == null) { saved = material; AssetDatabase.CreateAsset(saved, path); }
            else { EditorUtility.CopySerialized(material, saved); UnityEngine.Object.DestroyImmediate(material); }
            EditorUtility.SetDirty(saved);
            Prepared.Add(path, saved);
            return saved;
        }

        private static VolumeProfile Profile()
        {
            string path = Output + "/PaintedVillagePost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/Presentation/FullCelPost.asset", path);
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            }
            profile.TryGet<ColorAdjustments>(out var grade);
            grade.postExposure.Override(.18f);
            grade.contrast.Override(3);
            grade.saturation.Override(-5);
            profile.TryGet<Bloom>(out var bloom);
            bloom.intensity.Override(.015f);
            if (!profile.TryGet<SplitToning>(out var split))
            {
                split = profile.Add<SplitToning>();
                AssetDatabase.AddObjectToAsset(split, profile);
            }
            split.shadows.Override(new Color(.485f, .495f, .525f));
            split.highlights.Override(new Color(.52f, .505f, .48f));
            split.balance.Override(0);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
