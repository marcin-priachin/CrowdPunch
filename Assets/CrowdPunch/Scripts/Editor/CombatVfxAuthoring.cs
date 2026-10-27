using System;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // VISION-005/006, ENEMY-003, CHICKEN-007, WIZARD-006. Explicit, repeatable owned-asset authoring.
    public static class CombatVfxAuthoring
    {
        internal const string Materials = "Assets/CrowdPunch/Materials/Feedback/";
        internal const string Prefabs = "Assets/CrowdPunch/Prefabs/Feedback/";
        internal const string Kenney = "Assets/CrowdPunch/Models/kenney_particle-pack/PNG (Transparent)/";
        internal const string Trails = "Assets/Vefects/Trails VFX URP/VFX/Textures/";

        [MenuItem("Crowd Punch/Art Direction/Apply Combat VFX")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before authoring VFX.");
            var library = AssetDatabase.LoadAssetAtPath<CombatVfxLibrary>("Assets/CrowdPunch/Resources/CombatVfxLibrary.asset");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CombatVfxLibrary>();
                AssetDatabase.CreateAsset(library,"Assets/CrowdPunch/Resources/CombatVfxLibrary.asset");
            }
            // Saving referenced prefabs can reimport their dependent assets. Work on detached
            // copies, then reacquire the persistent assets after all prefab writes finish.
            library = UnityEngine.Object.Instantiate(library);
            library.hideFlags=HideFlags.HideAndDontSave;
            var settings = UnityEngine.Object.Instantiate(Load<CombatFeedbackSettings>("Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset"));
            settings.hideFlags=HideFlags.HideAndDontSave;
            try
            {
            Material streak = Sprite("LaunchStreak",Trails+"T_VFX_Trail_02.tga",true);
            streak.SetFloat("_Erosion",0);
            streak.SetFloat("_Ribbon",1);
            library.LaunchTrail=streak;
            library.ArcaneTrail=Sprite("ArcaneStreak",Trails+"T_VFX_Trail_01.tga",true);
            library.BossTrail=Sprite("ArchmageStreak",Trails+"T_VFX_Trail_03.tga",true);
            library.SporeCloud=Sprite("SporeCloud",Kenney+"smoke_07.png");
            library.SporeCloud.SetFloat("_Erosion",0);
            library.Pollen=Sprite("Pollen",Kenney+"light_01.png");
            library.DragonFlame=Sprite("DragonTongues",Kenney+"flame_02.png");
            library.DragonFlame.SetTexture("_NoiseMap",Load<Texture2D>("Assets/Vefects/Stylized Fire VFX URP/VFX/Shared/Textures/T_VFX_Perlin_01.tga"));
            library.DragonFlame.SetFloat("_Erosion",.02f);
            library.DragonFlame.SetFloat("_Glow",1.25f);
            library.Shockwave=Sprite("BlastShockwave",null);
            var smoke=NovaSmoke();
            var spark=Sprite("ImpactSplinters",Kenney+"spark_05.png");
            var flash=Sprite("ImpactFlash","Assets/Travis Game Assets/Hit Impact Effects/Textures/HIE_Hit_01_Flash_01.tga");
            var ring=Sprite("ArcaneOrbit",Kenney+"circle_02.png");
            var droplet=Sprite("SnakeDroplet",Kenney+"circle_05.png");
            library.RangedFlight=CombatVfxParticles.Flight("ArcaneComet",library.Pollen,ring,false,false);
            library.RangedCharge=CombatVfxParticles.Charge(library.Pollen,ring);
            library.BossFlight=CombatVfxParticles.Flight("ArchmageOrb",library.Pollen,ring,true,false);
            library.SnakeFlight=CombatVfxParticles.Flight("SnakeDroplets",droplet,ring,false,true);
            library.RangedImpact=CombatVfxParticles.Impact("ArcaneImpact",flash,spark,smoke,new Color(.25f,.85f,1),.7f);
            library.BossImpact=CombatVfxParticles.Impact("ArchmageImpact",flash,spark,smoke,new Color(.85f,.42f,1),1);
            library.SnakeSever=CombatVfxParticles.Impact("SnakeSever",ring,droplet,null,new Color(1,.72f,.18f),.5f);
            library.LaunchDust=CombatVfxParticles.Smoke("LaunchDust",smoke,new Color(.72f,.65f,.48f,.4f),5,.3f,.4f);
            library.NinjaSmoke=CombatVfxParticles.Smoke("NinjaPoof",smoke,new Color(.48f,.42f,.63f,.7f),10,.48f,.85f);
            settings.PunchParticles=CombatVfxParticles.Impact("ArtPunch",flash,spark,smoke,new Color(1,.7f,.16f),.85f);
            settings.EnemyParticles=CombatVfxParticles.Impact("ArtCollision",flash,spark,null,new Color(.62f,.9f,1),.48f);
            settings.PlayerDamageParticles=CombatVfxParticles.Impact("ArtPlayerDamage",flash,spark,null,new Color(1,.2f,.12f),.9f);
            settings.EnvironmentParticles=CombatVfxParticles.Environment(smoke,spark);
            settings.ComicImpactScale=.65f;
            settings.TrailWidthInModels=1; settings.TrailLengthInModels=2.5f;
            settings.TrailColor=new Color(.97f,.99f,1,1);
            RefineExplosion(settings.ExplosionParticles);
            SetProjectileMaterial("Assets/CrowdPunch/Prefabs/RangedProjectile.prefab","ArcaneCore",new Color(.16f,.8f,1));
            SetProjectileMaterial("Assets/CrowdPunch/Data/ChickenBoss/ChickenProjectile.prefab","ArchmageCore",new Color(.7f,.22f,1));
            var savedSettings=Load<CombatFeedbackSettings>("Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset");
            var savedLibrary=Load<CombatVfxLibrary>("Assets/CrowdPunch/Resources/CombatVfxLibrary.asset");
            EditorUtility.CopySerialized(settings,savedSettings);EditorUtility.CopySerialized(library,savedLibrary);
            savedSettings.hideFlags=HideFlags.None;savedLibrary.hideFlags=HideFlags.None;
            savedSettings.name="CombatFeedbackSettings";savedLibrary.name="CombatVfxLibrary";
            EditorUtility.SetDirty(savedSettings); EditorUtility.SetDirty(savedLibrary);
            foreach (var guid in AssetDatabase.FindAssets("t:Material",new[]{Materials.TrimEnd('/')}))
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(library);UnityEngine.Object.DestroyImmediate(settings); }
        }
        internal static T Load<T>(string path) where T: UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing VFX source: "+path);
        private static Material Sprite(string name,string texture,bool red=false)
        {
            string path=Materials+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("CrowdPunch/VFX/Combat Sprite")); AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",texture==null?Texture2D.whiteTexture:Load<Texture2D>(texture));
            material.SetTexture("_NoiseMap",Load<Texture2D>(Trails+"T_VFX_Noise_01.tga"));
            material.SetVector("_MaskChannel",red?new Vector4(1,0,0,0):new Vector4(0,0,0,1));
            material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Glow",1);material.SetFloat("_Erosion",0);
            return material;
        }
        private static Material NovaSmoke()
        {
            const string path=Materials+"SoftSmokeNova.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Nova/Particles/UberUnlit"));AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",Load<Texture2D>("Assets/CrowdPunch/Models/kenney_smoke-particles/White puff/whitePuff00.png"));
            material.SetFloat("_RenderType",2);material.SetFloat("_TransparentBlendMode",0);material.SetFloat("_Cull",0);
            material.SetFloat("_ZWriteOverride",0);
            // Use the package's own material setup so its blend state and shader keywords agree.
            Nova.Editor.Core.Scripts.ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(material);
            Nova.Editor.Core.Scripts.ParticlesUberUnlitMaterialPostProcessor.SetupMaterialBlendMode(material);
            return material;
        }
        private static void SetProjectileMaterial(string path,string name,Color tint)
        {
            string materialPath=Materials+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null) { material=new Material(Shader.Find("CrowdPunch/Cel/Environment"));AssetDatabase.CreateAsset(material,materialPath); }
            material.enableInstancing=true;material.SetColor("_BaseColor",tint);
            material.SetColor("_EmissionColor",tint*.35f);material.EnableKeyword("_EMISSION");
            var root=PrefabUtility.LoadPrefabContents(path);
            try { root.GetComponent<MeshRenderer>().sharedMaterial=material; PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(material);
        }
        private static void RefineExplosion(ParticleSystem source)
        {
            if(source==null) return;
            string path=AssetDatabase.GetAssetPath(source);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main=ps.main;main.useUnscaledTime=true;main.stopAction=ParticleSystemStopAction.None;
                    main.maxParticles=Mathf.Min(main.maxParticles,32);
                    if(ps.name.Contains("Smoke")) { main.startLifetime=new ParticleSystem.MinMaxCurve(.22f,.42f);main.startColor=new Color(.44f,.39f,.35f,.45f); }
                    if(ps.name=="Ring") ps.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
