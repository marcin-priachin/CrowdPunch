using System;
using System.IO;
using System.Collections.Generic;
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
    // VISION-005/006: a scene-owned painted pilot, preserving the baked receiver contract.
    public static class LevelElevenPaintedLook
    {
        public const string Output = "Assets/CrowdPunch/Data/ChapterTwoFoundry/PaintedLevel11";
        private const string ShaderName = "CrowdPunch/Cel/Painted Foundry";
        private static readonly Dictionary<string, Material> Prepared = new();

        [MenuItem("Crowd Punch/Art Direction/Apply Painted Look to Level 11")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and stop Play/baking before applying the painted look.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ChapterTwoFoundryBuilder.SubPath(11), ChapterTwoFoundryBuilder.MainPath(11) })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    ApplyScene(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void ApplyScene(Scene scene)
        {
            Prepared.Clear();
            Directory.CreateDirectory(Output + "/Materials");
            AssetDatabase.Refresh();
            if (Shader.Find(ShaderName) == null) throw new InvalidOperationException("Painted Foundry shader is missing.");
            bool sub = scene.path == ChapterTwoFoundryBuilder.SubPath(11);
            var roots = scene.GetRootGameObjects();
            var renderers = roots.Where(g => sub || g.name == ChapterTwoFoundryBuilder.RootName)
                .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true));
            foreach (var renderer in renderers)
            {
                string ancestry = renderer.name;
                for (var t = renderer.transform.parent; t != null; t = t.parent) ancestry += "/" + t.name;
                bool distant = ancestry.Contains("skyline block");
                int variant = Mathf.Abs(Mathf.RoundToInt(renderer.bounds.center.x * .17f + renderer.bounds.center.z * .31f)) % 3;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) return source;
                    string kind = Kind(renderer, source);
                    return kind == null ? source : MaterialFor(source, kind, distant, variant);
                }).ToArray();
                EditorUtility.SetDirty(renderer);
            }
            if (sub) return;
            var lighting = roots.SelectMany(g => g.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            // Keep the baked sun direction/intensity and probes. Palette is authored in the surfaces.
            lighting.haze = new Color(.57f, .62f, .69f);
            lighting.hazeStart = 52;
            lighting.hazeEnd = 155;
            string skyPath = Output + "/PaintedSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(lighting.skybox);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetColor("_SkyTint", new Color(.48f, .54f, .68f));
            sky.SetColor("_GroundColor", new Color(.63f, .59f, .55f));
            sky.SetFloat("_Exposure", .85f);
            sky.SetFloat("_SunSize", .015f);
            EditorUtility.SetDirty(sky);
            lighting.skybox = sky;
            lighting.Apply();
            EditorUtility.SetDirty(lighting);
            var volume = roots.SelectMany(g => g.GetComponentsInChildren<Volume>(true)).Single(v => v.priority == 150);
            volume.sharedProfile = Profile();
            EditorUtility.SetDirty(volume);
        }

        private static string Kind(MeshRenderer r, Material source)
        {
            if (r.name.Contains("Floor") || r.name.Contains("paving") || r.name.Contains("apron") || r.name.Contains("concourse")) return "Paving";
            if (r.name.StartsWith("Perimeter ") || r.name == "Nature rock") return "Stone";
            if (r.name == "High clerestory window") return "Glazing";
            if (r.name.Contains("door-wide")) return "Door";
            string name = source.name;
            if (name.StartsWith("Painted ")) return name.Split('_')[0].Substring(8);
            if (name == "Kiln brick") return "Brick";
            if (name == "Muted teal enamel") return "Teal";
            if (name == "Oxidized copper") return "Copper";
            if (name == "Blue steel") return "Steel";
            if (name == "Worn brass") return "Brass";
            if (name == "Coal and dark iron") return "Coal";
            if (name == "Foundry earth" || r.name == "Presentation Backdrop") return "Earth";
            if (name == "Ivory industrial paint") return "Ivory";
            if (name == "Quiet blue grey paving") return "Paving";
            return null;
        }

        private static Material MaterialFor(Material source, string kind, bool distant, int variant)
        {
            bool explicitMask = source.HasProperty("_UseBakedShadowMask") && source.GetFloat("_UseBakedShadowMask") > .5f;
            // A separate material per explicit chart is required for ECS receivers.
            string maskKey = "";
            // Stable asset names use chart texture identity and ST.
            if (explicitMask) maskKey = "_" + Hash128.Compute(AssetDatabase.GetAssetPath(source.GetTexture("_BakedShadowMask")) + source.GetVector("_BakedShadowST").ToString("R"));
            if (kind != "Brick" && kind != "Teal" && kind != "Glazing") variant = 0;
            string path = Output + "/Materials/Painted " + kind + "_" + (distant ? "Skyline" : "Near") + "_" + variant + maskKey + ".mat";
            if (Prepared.TryGetValue(path, out var prepared)) return prepared;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(ShaderName);
            material.enableInstancing = true;
            Color color = new Color(.38f, .44f, .48f), wash = new Color(.43f, .39f, .49f);
            string texture = null;
            float detail = .35f, scale = .18f, variation = .18f, seams = 0;
            switch (kind)
            {
                case "Brick":
                    color = new[] { new Color(.64f,.43f,.34f), new Color(.60f,.46f,.38f), new Color(.68f,.49f,.37f) }[variant];
                    wash = new Color(.43f,.32f,.39f); texture = "Stylized_Bricks_005_basecolor"; scale = .20f; detail = .62f; break;
                case "Teal": color = new Color(.28f+variant*.025f,.48f+variant*.02f,.47f); wash = new Color(.46f,.57f,.51f); seams = .45f; break;
                case "Copper": color = new Color(.68f,.40f,.24f); wash = new Color(.43f,.30f,.36f); variation = .25f; break;
                case "Steel": color = new Color(.31f,.39f,.43f); wash = new Color(.48f,.50f,.53f); break;
                case "Brass": color = new Color(.73f,.57f,.30f); wash = new Color(.54f,.36f,.25f); break;
                case "Coal": color = new Color(.22f,.24f,.28f); wash = new Color(.31f,.30f,.35f); break;
                case "Earth": color = new Color(.57f,.45f,.32f); wash = new Color(.65f,.49f,.34f); texture = "Vol_16_2_Sand_Base_Color"; scale = .12f; detail = .25f; variation = .10f; break;
                case "Paving": color = new Color(.51f,.56f,.59f); wash = new Color(.47f,.48f,.54f); texture = "Stylized_Stone_Floor_002_basecolor"; scale = .15f; detail = .12f; variation = .035f; break;
                case "Stone": color = new Color(.40f,.46f,.51f); wash = new Color(.53f,.48f,.51f); texture = "Vol_23_1_Rocks_Base_Color"; scale = .18f; detail = .38f; break;
                case "Door": color = new Color(.30f,.40f,.43f); wash = new Color(.44f,.49f,.48f); texture = "Stylized_Wood_Wall_001_basecolor"; scale = .24f; detail = .48f; break;
                case "Ivory": color = new Color(.80f,.75f,.60f); wash = new Color(.67f,.57f,.47f); break;
                case "Glazing": color = new[] {new Color(.25f,.36f,.41f),new Color(.62f,.62f,.48f),new Color(.39f,.48f,.50f)}[variant]; wash = new[] {new Color(.38f,.49f,.53f),new Color(.76f,.72f,.52f),new Color(.56f,.60f,.58f)}[variant]; break;
            }
            if (distant) {color = Color.Lerp(color,new Color(.53f,.57f,.65f),.40f); wash = color; detail *= .25f; variation *= .4f; seams = 0;}
            material.SetColor("_BaseColor", color);
            material.SetColor("_WashColor", wash);
            material.SetColor("_ShadowColor", distant ? new Color(.59f,.64f,.76f) : new Color(.43f,.49f,.65f));
            material.SetColor("_LightColor", new Color(1,.94f,.83f));
            material.SetFloat("_Contrast", distant ? .5f : .78f);
            material.SetFloat("_WashStrength", kind == "Paving" ? .12f : .30f);
            material.SetFloat("_Variation", variation);
            material.SetFloat("_GrainStrength", distant ? .025f : .11f);
            material.SetFloat("_Window", kind == "Glazing" ? 1 : 0);
            material.SetFloat("_SeamStrength", seams);
            material.SetFloat("_TextureScale", scale);
            material.SetFloat("_TextureStrength", texture == null ? 0 : detail);
            material.SetFloat("_ReceiveShadows", 1);
            material.SetFloat("_Meadow", 0);
            if (texture != null)
            {
                var texPath = AssetDatabase.FindAssets(texture + " t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (texPath == null) throw new InvalidOperationException("Missing painted texture: " + texture);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            }
            if (explicitMask)
            {
                material.SetTexture("_BakedShadowMask", source.GetTexture("_BakedShadowMask"));
                material.SetVector("_BakedShadowST", source.GetVector("_BakedShadowST"));
            }
            material.SetFloat("_UseBakedShadowMask", explicitMask ? 1 : 0);
            EditorUtility.SetDirty(material);
            Prepared.Add(path, material);
            return material;
        }

        private static VolumeProfile Profile()
        {
            string path = Output + "/PaintedFoundryPost.asset";
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
            split.shadows.Override(new Color(.485f,.495f,.525f));
            split.highlights.Override(new Color(.52f,.505f,.48f));
            split.balance.Override(0);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
