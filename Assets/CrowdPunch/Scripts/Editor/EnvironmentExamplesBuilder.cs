using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Mono.Levels;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // OQ-015 art experiments. Reuse BARRICADE/COVER/SHELL/TRACK rules and VISION-004 geometry.
    public static partial class EnvironmentExamplesBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string Scenes = Root + "Scenes/Gauntlets/";
        private const string Output = Root + "Data/EnvironmentExamples/";
        private static readonly int[] Sources = { 13, 16, 14, 15 };
        private static readonly string[] Titles = { "Sunlit Village Gate", "Emberworks Foundry", "Overgrown Observatory", "Frostbound Vault" };
        private static readonly Color[] Floors = { new(.40f,.34f,.23f), new(.18f,.23f,.27f), new(.26f,.34f,.29f), new(.48f,.59f,.65f) };
        private static readonly Color[] Stones = { new(.66f,.56f,.39f), new(.27f,.32f,.36f), new(.43f,.51f,.43f), new(.55f,.68f,.76f) };
        private static readonly Color[] Accents = { new(.83f,.48f,.17f), new(1,.42f,.09f), new(.37f,.74f,.65f), new(.42f,.82f,1) };
        private static string[] modelPaths;

        [MenuItem("Crowd Punch/Levels/Refresh Example Scenery 28-31")]
        public static void RefreshScenery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            modelPaths=AssetDatabase.FindAssets("t:Model",new[]{Root+"Models"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int theme=0;theme<4;theme++)
                {
                    int n=28+theme;
                    var sub=EditorSceneManager.OpenScene($"{Scenes}Gauntlet_{n}/Gauntlet_{n} Sub Scene.unity");
                    var floor=sub.GetRootGameObjects().Single(g=>g.name.StartsWith("Layout - ")).transform.Find("Continuous Convex Floor").GetComponent<MeshRenderer>().bounds;
                    NormalizeLayout(sub);
                    if(theme==0)
                    {
                        var wall=UnityEngine.Object.FindFirstObjectByType<BarricadeAuthoring>();
                        foreach(var plank in wall.GetComponentsInChildren<Transform>().Where(t=>t.name=="Timber facing").ToArray()) UnityEngine.Object.DestroyImmediate(plank.gameObject);
                        TimberFacing(wall,theme);
                    }
                    if(theme==1)
                    {
                        var track=UnityEngine.Object.FindFirstObjectByType<TrackObjectAuthoring>();
                        var old=track.transform.Find("machine-fortified");
                        if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                        Model(track.transform,"kenney_factory","machine-fortified",new Vector3(0,-.9f,-5),new Vector3(3.3f,3.1f,2.9f),0,theme,Material(theme,"Machine",new Color(.61f,.34f,.14f)));
                    }
                    EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                    var main=EditorSceneManager.OpenScene($"{Scenes}Gauntlet_{n}.unity");
                    var previous=main.GetRootGameObjects().Single(g=>g.name.StartsWith("Environment Example - "));
                    UnityEngine.Object.DestroyImmediate(previous);
                    var root=new GameObject("Environment Example - "+Titles[theme]).transform;
                    DressArena(root,floor,theme);
                    EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("Crowd Punch/Levels/Create Environment Examples 28-31")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            // This is an additive recipe, never a destructive rebuild of hand-edited examples.
            for (int i = 28; i <= 31; i++)
                if (File.Exists(Scenes + $"Gauntlet_{i}.unity")) throw new InvalidOperationException($"Gauntlet_{i} already exists; edit the saved example directly.");
            modelPaths = AssetDatabase.FindAssets("t:Model", new[] { Root + "Models" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var pair in new[] { ("KayKit", "wall"), ("Stylized Nature", "CommonTree_1"), ("kenney_factory", "machine"), ("Medieval Village", "Roof_RoundTiles_8x8"), ("Fantasy Props", "Anvil") })
                ModelPath(pair.Item1, pair.Item2);
            Directory.CreateDirectory(Output + "Materials");
            Directory.CreateDirectory(Output + "Waves");
            AssetDatabase.Refresh();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < 4; i++) Create(i);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames");
                var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(31, names.arraySize);
                for (int i = 0; i < 4; i++)
                {
                    names.GetArrayElementAtIndex(27 + i).stringValue = $"Gauntlet_{28 + i}";
                    titles.GetArrayElementAtIndex(27 + i).stringValue = $"{28 + i} {Titles[i]}";
                }
                sequence.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Created legacy environment examples 28-31. Campaign, original encounters and source models preserved.");
        }

        private static void Create(int theme)
        {
            int number = 28 + theme;
            string id = $"Gauntlet_{number}", source = $"Gauntlet_{Sources[theme]:00}";
            Directory.CreateDirectory(Scenes + id);
            AssetDatabase.Refresh();
            string subPath = $"{Scenes}{id}/{id} Sub Scene.unity";
            if (!AssetDatabase.CopyAsset($"{Scenes}{source}/{source} Sub Scene.unity", subPath) ||
                !AssetDatabase.CopyAsset($"{Scenes}{source}.unity", $"{Scenes}{id}.unity"))
                throw new InvalidOperationException("Could not copy source encounter " + source);
            var sub = EditorSceneManager.OpenScene(subPath, OpenSceneMode.Single);
            var layout = sub.GetRootGameObjects().Single(g => g.name.StartsWith("Layout - "));
            layout.name = "Layout - " + Titles[theme];
            var floor = layout.transform.Find("Continuous Convex Floor").GetComponent<MeshRenderer>();
            Bounds bounds = floor.bounds;
            foreach (var renderer in layout.GetComponentsInChildren<MeshRenderer>())
            {
                Color color = renderer.name == "Continuous Convex Floor" ? Floors[theme] :
                    renderer.name == "Presentation Backdrop" ? Floors[theme] * .65f : Stones[theme];
                renderer.sharedMaterials = Enumerable.Repeat(Material(theme, "Layout_" + renderer.name.Replace(' ', '_'), color), renderer.sharedMaterials.Length).ToArray();
            }
            // Independent wave assets allow tuning these demonstrations without touching legacy sources.
            int waveIndex = 0;
            foreach (var encounter in sub.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>()))
            {
                var data = new SerializedObject(encounter);
                var waves = data.FindProperty("waves");
                for (int w = 0; w < waves.arraySize; w++)
                {
                    var field = waves.GetArrayElementAtIndex(w);
                    string wavePath = Output + $"Waves/CP{number}_{++waveIndex:00}.asset";
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(field.objectReferenceValue), wavePath)) throw new InvalidOperationException("Could not copy wave.");
                    field.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(wavePath);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            DressObjectives(sub, theme);
            NormalizeLayout(sub);
            EditorSceneManager.MarkSceneDirty(sub);
            EditorSceneManager.SaveScene(sub);

            var main = EditorSceneManager.OpenScene($"{Scenes}{id}.unity", OpenSceneMode.Single);
            var reference = UnityEngine.Object.FindFirstObjectByType<SubScene>();
            reference.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subPath);
            var marker = UnityEngine.Object.FindFirstObjectByType<GauntletLevel>();
            marker.name = id + " - " + Titles[theme];
            var dataMarker = new SerializedObject(marker);
            dataMarker.FindProperty("openingHint").stringValue = Titles[theme] + " - " + marker.OpeningHint;
            dataMarker.ApplyModifiedPropertiesWithoutUndo();
            var light = UnityEngine.Object.FindFirstObjectByType<Light>();
            light.color = theme == 0 ? new Color(1,.91f,.75f) : theme == 1 ? new Color(1,.84f,.69f) : theme == 2 ? new Color(.87f,1,.91f) : new Color(.77f,.88f,1);
            light.intensity = theme == 3 ? 1.15f : 1.35f;
            light.transform.rotation = Quaternion.Euler(48, -35 + theme * 22, 0);
            var scenery = new GameObject("Environment Example - " + Titles[theme]).transform;
            DressArena(scenery, bounds, theme);
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }

        private static void DressObjectives(Scene scene, int theme)
        {
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>()))
            {
                if (renderer.transform.root.name.StartsWith("Layout - ")) continue;
                string n = renderer.name.ToLowerInvariant();
                // Preserve authored damage bands, cracks, socket state and direction arrows.
                if (n.Contains("crack") || n.Contains("damage") || n.Contains("arrow") || n.Contains("socket") || n.Contains("exit")) continue;
                Color color = n.Contains("core") || n.Contains("target") ? Accents[theme] : Stones[theme];
                if (n.Contains("rib") || n.Contains("base") || n.Contains("rail")) color = theme == 0 ? new Color(.24f,.15f,.09f) : new Color(.17f,.23f,.25f);
                if (theme == 0 && n.Contains("plate")) color = new Color(.46f,.27f,.12f);
                renderer.sharedMaterials = Enumerable.Repeat(Material(theme, "Objective_" + ColorUtility.ToHtmlStringRGB(color), color), renderer.sharedMaterials.Length).ToArray();
            }
            if (theme == 0)
            {
                var wall = UnityEngine.Object.FindFirstObjectByType<BarricadeAuthoring>();
                TimberFacing(wall,theme);
            }
            if (theme == 1)
            {
                var track = UnityEngine.Object.FindFirstObjectByType<TrackObjectAuthoring>();
                var block = track.transform.Find("Chunky launch target");
                // The decorative machine stays within the original solid body and follows its ECS transform.
                block.GetComponent<MeshRenderer>().enabled = false;
                Model(track.transform, "kenney_factory", "machine-fortified", new Vector3(0,-.9f,-5), new Vector3(3.3f,3.1f,2.9f), 0, theme, Material(theme,"Machine",new Color(.61f,.34f,.14f)));
            }
        }

        private static void TimberFacing(BarricadeAuthoring wall,int theme)
        {
            int count=Mathf.Max(3,Mathf.CeilToInt(wall.size.x/.65f));
            for(int i=0;i<count;i++)
            {
                float x=-wall.size.x*.5f+(i+.5f)*wall.size.x/count;
                var bottom=wall.transform.position+new Vector3(x,-wall.size.y*.5f,-wall.size.z*.5f-.06f);
                var plank=Block(wall.transform,"Timber facing",bottom,new Vector3(wall.size.x/count-.045f,wall.size.y,.1f),Material(theme,"Timber",new Color(.55f,.34f,.17f)));
                var visual=plank.AddComponent<BarricadeVisualAuthoring>(); visual.barricade=wall;visual.debrisDirection=new Vector3(x*.3f,.5f,-1);
            }
        }

        private static void NormalizeLayout(Scene scene)
        {
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh=primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            var layout=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Layout - "));
            foreach(var filter in layout.GetComponentsInChildren<MeshFilter>())
                if(filter.name.StartsWith("Perimeter ") || filter.name=="Entry Apron" || filter.name=="Launch Lane") filter.sharedMesh=mesh;
        }

        private static string ModelPath(string kit, string name) => modelPaths
            .Where(p => p.Contains(kit) && Path.GetFileNameWithoutExtension(p) == name && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Contains("fbx(unity)") || p.Contains("FBX (Unity)")).ThenBy(p => p.Length).FirstOrDefault()
            ?? throw new InvalidOperationException($"Missing imported model {kit}/{name}");

        private static Material Material(int theme, string name, Color color, float emission = 0)
        {
            string path = Output + $"Materials/{theme}_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", .12f);
            if (emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * emission); }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Decorations have no colliders or gameplay scripts; collision belongs to the copied SubScene.
        private static GameObject Model(Transform parent, string kit, string name, Vector3 bottom, Vector3 size, float yaw, int theme, Material material = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(kit, name));
            var instance = new GameObject(name);
            var imported = UnityEngine.Object.Instantiate(source, instance.transform, false);
            // Preserve the FBX root's import rotation/scale (some supplied kits use Z-up).
            if (name == "pipe-large-long") imported.transform.localRotation *= Quaternion.Euler(0,0,90);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no static mesh: " + name);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);
            var rotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.rotation = rotation;
            instance.transform.position = bottom - rotation * Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
            foreach (var c in instance.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var a in instance.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(a);
            foreach (var r in renderers)
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => material != null ? material : ImportedMaterial(m, theme)).ToArray();
                r.gameObject.isStatic = parent.GetComponentInParent<TrackObjectAuthoring>() == null;
            }
            return instance;
        }

        private static Material ImportedMaterial(Material source, int theme)
        {
            string key = AssetDatabase.GetAssetPath(source) + "/" + source.name;
            string path = Output + $"Materials/{theme}_Imported_{Hash128.Compute(key)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { ConfigureFoliage(material,source); return material; }
            material = new Material(source) { name = source.name, enableInstancing = true };
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            if (source.mainTexture != null) material.SetTexture("_BaseMap", source.mainTexture);
            material.SetFloat("_Smoothness", .1f);
            material.SetFloat("_Metallic", 0);
            ConfigureFoliage(material,source);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureFoliage(Material material,Material source)
        {
            if(!AssetDatabase.GetAssetPath(source).Contains("Stylized Nature") || !source.name.StartsWith("Leaves")) return;
            material.SetFloat("_Cull",0);
            if(source.name=="Leaves")
            {
                material.SetTexture("_BaseMap",null); material.color=new Color(.34f,.51f,.2f);
                material.SetFloat("_AlphaClip",0); material.DisableKeyword("_ALPHATEST_ON"); material.renderQueue=2000;
            }
            else
            {
                // Tree leaves are sculpted geometry. A solid leaf color avoids the export palette's black border.
                material.SetTexture("_BaseMap",null); material.color=source.name.Contains("Twisted")?new Color(.36f,.48f,.19f):new Color(.32f,.47f,.16f);
            }
            EditorUtility.SetDirty(material);
        }

        private static GameObject Block(Transform parent, string name, Vector3 bottom, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name; block.transform.SetParent(parent, false);
            block.transform.position = bottom + Vector3.up * size.y * .5f;
            Vector3 parentScale=parent.lossyScale;
            block.transform.localScale = new Vector3(size.x/parentScale.x,size.y/parentScale.y,size.z/parentScale.z);
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }
    }
}
