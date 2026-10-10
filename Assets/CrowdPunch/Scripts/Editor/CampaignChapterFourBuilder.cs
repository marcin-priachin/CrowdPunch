using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        private static readonly string[] ChapterFourHints =
        {
            "Move out of the purple warning zones. Launch nearby enemies into the Wizards.",
            "Send bodies into the block from behind. Reach the blue socket in five forward hits.",
            "Trails leave dangerous ground behind them. Launch them along a useful path and keep clear of their wake.",
            "Break the narrow gate with bodies or explosions. The inner crossing turns hot after its warning.",
            "Watch the opening and the Trail. Move to clear ground before lining up your next shot.",
            "Stop every wave before it reaches the turquoise zone. Watch the shooters and intercept the Wizard.",
            "Break the shell with three explosions, then hit the exposed core. Keep clear of the hot corner.",
            "Clear each large wave. Disrupt Wizards and Elites, break armor with bodies, and redirect Trails through groups.",
            "Work around the block to line up forward hits. The blue socket is the destination.",
            "Lure Dino near a pillar, then launch a body into the pillar. Only falling pillars can hurt him."
        };

        private static Level[] ChapterFourDesigns() => new[]
        {
            new Level { Name="Purple Warning", Outline=Clipped(34,34,4), Spacing=new Vector2(28,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Purple Warning",B=6,Wizard=1,Ranges=new[]{Range(0,8,22,8)}},
                new Wave {Name="Paired Casters",B=12,Wizard=2,Ranges=new[]{Range(0,8,22,8)}} } },
            new Level { Name="Quiet Delivery", Outline=Clipped(34,38,3), Spacing=new Vector2(28,32), Entry=new Vector2(0,-14), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Quiet Delivery",10,new[]{Range(-10,0,4,22),Range(10,0,4,22)}) } },
            new Level { Name="Leave a Trail", Outline=Clipped(36,36,12), Spacing=new Vector2(28,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="First Trail",B=6,Trail=1,Ranges=new[]{Range(0,8,18,6)}},
                new Wave {Name="Crossing Trails",B=12,Trail=2,Ranges=new[]{Range(0,8,18,6)}} } },
            new Level { Name="Break the Detour", Outline=Clipped(32,44,4), Spacing=new Vector2(26,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Detour Traffic",12,new[]{Range(-11,-4,3,22),Range(11,-4,3,22)},x:2) } },
            new Level { Name="Moving Perimeter", Outline=Clipped(40,38,5), Spacing=new Vector2(34,32), Entry=new Vector2(0,-14), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Moving Perimeter",B=12,Trail=1,Ranges=new[]{Range(-13,0,4,18),Range(13,0,4,18)}} } },
            new Level { Name="Distant Defense", Outline=Rectangle(32,96), Spacing=new Vector2(30,94), Entry=new Vector2(0,-36), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Distant Fire",16,new[]{Range(0,41,24,8)},r:2,batch:4,interval:3),
                W("Closing Charge",20,new[]{Range(0,41,24,8)},d:1,delay:5,batch:4,interval:3),
                new Wave {Name="Caster Advance",B=20,R=2,Wizard=1,Delay=5,Batch=4,Interval=3,Ranges=new[]{Range(0,41,24,8)}} } },
            new Level { Name="Blast Shelter", Outline=new[]{new Vector2(-14,-17),new Vector2(14,-17),new Vector2(18,-10),new Vector2(15,0),new Vector2(18,10),new Vector2(14,17),new Vector2(-14,17),new Vector2(-18,10),new Vector2(-15,0),new Vector2(-18,-10)},
                Spacing=new Vector2(28,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Shelter Ammunition",12,new[]{Range(-10,-2,4,18),Range(10,-2,4,18)},x:2) } },
            new Level { Name="The Crowd Moves", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="West Casters",B=64,R=4,Wizard=2,Ranges=new[]{Range(-28,0,16,38)}},
                new Wave {Name="East Trails",B=80,D=4,Trail=2,Ranges=new[]{Range(28,0,16,38)}},
                new Wave {Name="North Command",B=96,A=4,E=2,Ranges=new[]{Range(0,30,40,16)}},
                new Wave {Name="Moving Crowd",B=112,X=4,Wizard=2,Trail=2,Ranges=new[]{Range(-28,0,16,42),Range(28,0,16,42)}} } },
            new Level { Name="Last Alignment", Outline=Clipped(34,42,4), Spacing=new Vector2(28,36), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Last Alignment",10,new[]{Range(-10,0,4,24),Range(10,0,4,24)}) } }
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter Four")]
        public static void BuildChapterFour()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(29).Available) throw new InvalidOperationException("Chapters One through Three are required.");
            for(int n=31;n<=40;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Four content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterFourDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+30,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterFourInterior(i+31);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+31);
                    SetCampaignHint(i+31,ChapterFourHints[i]);
                }
                CopyCampaignDino();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=31;n<=40;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Four authored: levels 31-40, Wizard/Trail introductions, mixed defense, 378-enemy arena and preserved Dino pillars.");
        }

        private static void AuthorChapterFourInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true;
            navigation.participationAnchor=number==34?new Vector2(3,-12):number==36?new Vector2(0,-32):number==38?new Vector2(0,-16):new Vector2(0,-10);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue=number==38?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 32:
                    CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero);
                    RotateCampaignRail(sub,encounter.barricade.GetComponent<TrackObjectAuthoring>(),-25); break;
                case 34:
                    CampaignRock(arena,new Vector2(-5,-10),new Vector2Int(7,3));
                    CampaignRock(arena,new Vector2(5,2),new Vector2Int(7,3));
                    CampaignBarricade(number,encounter);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    CampaignGround(encounter,number,"Inner warning shortcut",new Vector2(0,-3),true); break;
                case 35:
                    CampaignRock(arena,new Vector2(-14,13),new Vector2Int(5,3));
                    CampaignRock(arena,new Vector2(14,13),new Vector2Int(5,3));
                    CampaignRock(arena,new Vector2(0,-17),new Vector2Int(5,2));
                    CampaignCover(arena,encounter,number,100,25);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings); break;
                case 36:
                    CampaignRock(arena,new Vector2(-9,16),new Vector2Int(4,10));
                    CampaignRock(arena,new Vector2(9,-4),new Vector2Int(4,10));
                    var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
                    defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings","L36_Defense");
                    defense.settings.breachThreshold=1; defense.settings.maximumPlayerAttackers=3; EditorUtility.SetDirty(defense.settings);
                    // PROTECT-001/003: finite waves; the caster cannot keep this objective supplied or gated.
                    foreach(var wave in encounter.Waves)
                    {
                        var finite=new SerializedObject(wave);
                        finite.FindProperty("replenishWhileBossLives").boolValue=false;
                        finite.FindProperty("armoredAmmunitionProfile").objectReferenceValue=null;
                        finite.FindProperty("wizardAmmunitionProfile").objectReferenceValue=null;
                        finite.FindProperty("waitForPersistentHazards").boolValue=false;
                        finite.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Box("Protected Zone 8 x 4",encounter.transform,new Vector3(0,-.97f,-46),new Vector3(8,.035f,4),MaterialAsset("ProtectedPointZone",new Color(.15f,.75f,.68f)),false); break;
                case 37:
                    CopyCampaignObjective(sub,arena,encounter,number,false,Vector3.zero);
                    CampaignRock(arena,new Vector2(-5,13),new Vector2Int(4,3));
                    CampaignRock(arena,new Vector2(5,13),new Vector2Int(4,3));
                    CampaignGround(encounter,number,"Hot outer corner",new Vector2(-11,11)); break;
                case 38:
                    // LOOP-008: four shallow bays leave broad diagonal routes through the full footprint.
                    CampaignRock(arena,new Vector2(-44,-24),new Vector2Int(4,10));
                    CampaignRock(arena,new Vector2(44,24),new Vector2Int(4,10));
                    CampaignRock(arena,new Vector2(-24,44),new Vector2Int(10,4));
                    CampaignRock(arena,new Vector2(24,-44),new Vector2Int(10,4)); break;
                case 39:
                    CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero);
                    CampaignRock(arena,new Vector2(-14,8),new Vector2Int(2,6));
                    CampaignRock(arena,new Vector2(14,-8),new Vector2Int(2,6)); break;
            }
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void RotateCampaignRail(Scene scene,TrackObjectAuthoring track,float degrees)
        {
            var rotation=Quaternion.Euler(0,degrees,0);
            // Rotate cross-root references and their visuals together about the court centre.
            var parts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Where(t=>t==track.transform || t==track.destination || t.name=="Track rail" || t.name=="Direction arrow").ToArray();
            foreach(var part in parts)
                part.SetPositionAndRotation(rotation*part.position,rotation*part.rotation);
        }

        private static void CopyCampaignDino()
        {
            // PILLAR-006/LOOP-002: keep the boss model, arena and all three pillar transforms.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(40))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Scenes+"Gauntlet_22/Gauntlet_22 Sub Scene.unity",CampaignSub(40))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_22.unity",CampaignScenes+"Campaign_40.unity"))
                throw new InvalidOperationException("Could not copy the authored Dino arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(40),OpenSceneMode.Single);
            var boss=UnityEngine.Object.FindFirstObjectByType<DinoBossAuthoring>();
            boss.settings=CampaignSettings<DinoBossSettings>("DinoBossSettings","L40_Dino");
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            // Preserve the legacy safe spawn banks around the pillars.
            var wave=WaveAsset("CP40_01_Pillar_Crowd",W("Pillar Crowd",8,encounter.Waves[0].SpawnRectangles.ToArray()),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            encounter.dinoBoss=boss;
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_40.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(40));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(40,ChapterFourHints[9]);
        }
    }
}
