using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // Material recipes are chapter-owned. Source textures keep their authoring settings.
    internal static class ChapterOneVillageSurfaces
    {
        private static string[] textures;

        internal static void Prepare()
        {
            textures=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/CrowdPunch/Textures/StylizedTextures"})
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        private static Texture2D Texture(string name)
        {
            var path=textures.FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)==name+"_basecolor"
                || Path.GetFileNameWithoutExtension(p)==name+"_color");
            if(path==null) throw new InvalidOperationException("Missing village texture: "+name);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static void Configure(Material material,string name,Color tint)
        {
            string texture=null; float scale=.3f,strength=.5f;
            switch(name)
            {
                case "Meadow": texture="Stylized_Grass_002"; scale=.18f; strength=.7f; tint=new Color(.35f,.47f,.18f); break;
                case "Warm courtyard paving": texture="Stylized_Stone_Floor_003"; scale=.14f; strength=.65f; tint=new Color(.60f,.55f,.40f); break;
                case "Limestone": texture="Stylized_Cliff_Rock_005"; scale=.18f; strength=.4f; break;
                case "Foundation": texture="Stylized_Stone_Wall_001"; scale=.23f; break;
                case "Dark oak": case "Gate timber": texture="Stylized_Wood_Planks_002"; scale=.3f; strength=.65f; break;
                case "Cream plaster": strength=0; break;
                case "Terracotta roof": texture="Stylized_Roof_Tiles_003"; scale=.3f; strength=.6f; break;
            }
            if(texture==null && name!="Cream plaster") return;
            material.shader=Shader.Find("CrowdPunch/Cel/Village Surface");
            material.SetColor("_BaseColor",tint);
            material.SetTexture("_BaseMap",texture==null?null:Texture(texture));
            material.SetFloat("_TextureScale",scale);
            material.SetFloat("_TextureStrength",strength);
            material.SetFloat("_Variation",name=="Meadow"?.22f:.10f);
            material.SetFloat("_PlasterGrain",name=="Cream plaster"?.07f:0);
            material.SetFloat("_Meadow",0);
            // Roof tiles retain their cel-lit form; a clean proxy casts their outline onto walls.
            material.SetFloat("_ReceiveShadows",name=="Terracotta roof"?0:1);
            material.shaderKeywords=Array.Empty<string>();
            EditorUtility.SetDirty(material);
        }

        internal static Material Meadow(Bounds floor,int stage)
        {
            var source=ChapterOneVillageAssets.Surface("Meadow",Color.white);
            string path=ChapterOneVillageAssets.Output+$"/Materials/Meadow_{stage:00}.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(source); AssetDatabase.CreateAsset(material,path); }
            else material.CopyPropertiesFromMaterial(source);
            material.SetFloat("_Meadow",1);
            material.SetVector("_Bounds",new Vector4(floor.center.x,floor.center.z,floor.extents.x,floor.extents.z));
            material.SetTexture("_DirtMap",Texture("Stylized_Ground_002"));
            material.SetColor("_PathColor",new Color(.58f,.47f,.30f));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
