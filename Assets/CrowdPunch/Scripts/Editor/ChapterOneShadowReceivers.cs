using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // Bake-time bindings keep UV2 visibility identical for GameObject and ECS receivers.
    public static class ChapterOneShadowReceivers
    {
        public static void Refresh(int stage)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity");
                Bind(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void ApplyCourtBounds(MeshRenderer renderer, Material surface)
        {
            if (surface == null || !surface.HasProperty("_CourtPaintBounds") ||
                (renderer.name != "Continuous Convex Floor" && renderer.name != "Continuous Round Floor")) return;
            Bounds bounds = renderer.bounds;
            surface.SetVector("_CourtPaintBounds", new Vector4(bounds.center.x, bounds.center.z, bounds.extents.x, bounds.extents.z));
            EditorUtility.SetDirty(surface);
        }

        internal static void RefreshSurface(Material receiver, Material source)
        {
            var mask = receiver.GetTexture("_BakedShadowMask");
            var chart = receiver.GetVector("_BakedShadowST");
            var court = receiver.GetVector("_CourtPaintBounds");
            receiver.CopyPropertiesFromMaterial(source);
            receiver.SetTexture("_BakedShadowMask", mask);
            receiver.SetVector("_BakedShadowST", chart);
            receiver.SetFloat("_UseBakedShadowMask", 1);
            receiver.SetVector("_CourtPaintBounds", court);
            receiver.SetOverrideTag("CrowdPunchSourceMaterial", AssetDatabase.GetAssetPath(source));
            EditorUtility.SetDirty(receiver);
        }

        internal static void Bind(Scene scene, string outputFolder=null)
        {
            string folder=outputFolder ?? ChapterOneVillageAssets.Output+"/BakedShadows/Receivers";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.lightmapIndex<0 || r.lightmapIndex>=LightmapSettings.lightmaps.Length ||
                    !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;
                var mask=LightmapSettings.lightmaps[r.lightmapIndex].shadowMask;
                if(mask==null) continue;
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];
                    if(source==null || !source.HasProperty("_BakedShadowMask")) continue;
                    string sourcePath=source.GetTag("CrowdPunchSourceMaterial",false,AssetDatabase.GetAssetPath(source));
                    source=AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                    string key=GlobalObjectId.GetGlobalObjectIdSlow(r)+"/"+i;
                    string path=folder+"/"+Hash128.Compute(key)+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null) {material=new Material(source);AssetDatabase.CreateAsset(material,path);}
                    else
                    {
                        // Copy through a fresh native material so a rebake does not retain
                        // a stale property layout from the previous receiver bindings.
                        var snapshot=new Material(source);
                        EditorUtility.CopySerialized(snapshot,material);
                        Object.DestroyImmediate(snapshot);
                    }
                    // Unity may reimport an asset to correct a main-object/file-name
                    // mismatch while saving. Keep the name stable before binding data.
                    material.name=Path.GetFileNameWithoutExtension(path);
                    material.SetOverrideTag("CrowdPunchSourceMaterial",sourcePath);
                    material.SetTexture("_BakedShadowMask",mask);
                    material.SetVector("_BakedShadowST",r.lightmapScaleOffset);
                    material.SetFloat("_UseBakedShadowMask",1);
                    if (i == 0) ApplyCourtBounds(r, material);
                    EditorUtility.SetDirty(material); materials[i]=material;
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                r.sharedMaterials=materials;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
