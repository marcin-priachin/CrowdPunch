#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace CrowdPunch.Systems.Presentation
{
    // Editor-only presentation bridge. No Unity object references enter ECS components.
    public static class ArtLabPreview
    {
        public static bool Enabled;
        public static int EnvironmentStyle, CharacterStyle, Revision;
        private static readonly Dictionary<Material,Material> variants = new();
        private static readonly Dictionary<Material,Material> originals = new();
        public static int MaterialCount => variants.Count;

        public static Material Original(Material material) => material != null && originals.TryGetValue(material,out var original) ? original : material;

        public static Material Variant(Material source)
        {
            source=Original(source);
            if(source==null || !Enabled) return source;
            string name=source.shader.name;
            bool enemy=name=="CrowdPunch/EnemySkinning"||name=="CrowdPunch/Cel/Enemy", player=name=="Shader Graphs/Sidekick_ShaderGraph"||name=="CrowdPunch/Cel/Player";
            bool character=enemy||player;
            if((character?CharacterStyle:EnvironmentStyle)==0) return source;
            if(!character && name!="Universal Render Pipeline/Lit" && name!="CrowdPunch/Cel/Environment") return source;
            if(variants.TryGetValue(source,out var existing)) return existing;
            string kind=enemy?"Enemy":player?"Player":"Environment";
            var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>($"Assets/CrowdPunch/Data/ArtLab/Shaders/{kind}.shader");
            if(shader==null) return source;
            var variant=new Material(source) {name=source.name+" [Art Lab]",hideFlags=HideFlags.HideAndDontSave};
            variant.shader=shader;
            variants.Add(source,variant); originals.Add(variant,source);
            return variant;
        }

        public static void DisposeMaterials()
        {
            foreach(var material in variants.Values) if(material!=null) Object.DestroyImmediate(material);
            variants.Clear(); originals.Clear();
        }
    }
}
#endif
