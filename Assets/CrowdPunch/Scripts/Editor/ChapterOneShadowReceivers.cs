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
                    EditorUtility.SetDirty(material); materials[i]=material;
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                r.sharedMaterials=materials;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
