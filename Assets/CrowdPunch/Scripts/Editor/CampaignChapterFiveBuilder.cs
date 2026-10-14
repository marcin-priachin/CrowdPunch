using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
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
        private static readonly string[] ChapterFiveHints =
        {
            "Punch either linked enemy to launch both. Keep clear of the tether, even while they recover.",
            "Circle the island and break the narrow gate with four hits. Keep clear of the hot inner pockets.",
            "Reuse paired sweeps to disrupt Elite shot setups; walking and recovering tethers remain dangerous.",
            "The cover pauses between turns. Line up four hits through its opening.",
            "Combine paired body chains with armor and changing routes; the tether itself cannot strip intact armor.",
            "Strike the block from behind along the diagonal rail. Five forward hits reach the blue socket.",
            "Intercept every wave before it reaches the turquoise zone. No enemy can be allowed through.",
            "Large crowd encounter: swing paired projectiles through groups and reposition around dangerous recovery links.",
            "Make room around the Wizard and move out of purple warnings. Clear the shooters in the next wave.",
            "The Gatekeeper warns faster now. Launch bodies through his open guard and watch the side-sector warnings."
        };

        private static Level[] ChapterFiveDesigns() => new[]
        {
            new Level { Name="Back to Bodies", Outline=new[]{new Vector2(-17,-16),new Vector2(17,-16),new Vector2(17,9),new Vector2(0,16),new Vector2(-17,9)}, Spacing=new Vector2(28,26), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Distant Shooters",B=6,LinkedPairs=1,Ranges=new[]{Range(0,7,20,7)}}, new Wave {Name="Explosive Return",B=12,LinkedPairs=2,Ranges=new[]{Range(0,7,20,7)}} } },
            new Level { Name="Outer Route", Outline=new[]{new Vector2(-17,-22),new Vector2(17,-22),new Vector2(17,-5),new Vector2(13,10),new Vector2(11,22),new Vector2(-11,22),new Vector2(-13,10),new Vector2(-17,-5)}, Spacing=new Vector2(28,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Outer Ammunition",14,new[]{Range(-12,-4,3,20),Range(12,-4,3,20)},x:2) } },
            new Level { Name="Turn the Shot", Outline=Clipped(38,36,4), Spacing=new Vector2(32,30), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="West Command",B=14,R=2,E=1,LinkedPairs=1,Ranges=new[]{Range(-14,0,4,24)}}, new Wave {Name="East Command",B=16,D=2,E=1,LinkedPairs=1,Ranges=new[]{Range(14,0,4,24)}} } },
            new Level { Name="Stop and Go", Outline=Clipped(40,36,4), Spacing=new Vector2(34,30), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Measured Opening",12,new[]{Range(-13,0,4,20),Range(13,0,4,20)},r:2,x:1) } },
            new Level { Name="Armor in Motion", Outline=Clipped(38,40,5), Spacing=new Vector2(32,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Armor",B=12,A=2,LinkedPairs=1,Ranges=new[]{Range(0,14,24,4)}},
                new Wave {Name="East Armor",B=14,D=2,A=2,LinkedPairs=1,Ranges=new[]{Range(14,0,4,20)}},
                new Wave {Name="West Command",B=16,R=2,E=1,LinkedPairs=1,Ranges=new[]{Range(-14,0,4,20)}} } },
            new Level { Name="Measured Push", Outline=Clipped(36,40,4), Spacing=new Vector2(30,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Measured Push",10,new[]{Range(-12,0,4,24),Range(12,0,4,24)},x:2) } },
            new Level { Name="Divided Approach", Outline=Rectangle(32,96), Spacing=new Vector2(30,94), Entry=new Vector2(0,-36), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Split Shooters",20,new[]{Range(0,41,24,8)},r:2,batch:4),
                W("Split Dashers",24,new[]{Range(0,41,24,8)},d:2,delay:5,batch:4),
                W("Divided Command",24,new[]{Range(0,41,24,8)},x:2,delay:5,batch:4) } },
            new Level { Name="Firebreak", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Explosives",B=60,X=6,LinkedPairs=2,Ranges=new[]{Range(0,30,48,16)}},
                new Wave {Name="West Trails",B=76,R=4,Trail=2,LinkedPairs=2,Ranges=new[]{Range(-30,0,16,40)}},
                new Wave {Name="East Armor",B=84,E=2,A=4,LinkedPairs=2,Ranges=new[]{Range(30,0,16,40)}},
                new Wave {Name="Split Casters",B=98,D=4,Wizard=2,LinkedPairs=3,Ranges=new[]{Range(-25,30,22,16),Range(25,30,22,16)}},
                new Wave {Name="Firebreak",B=114,X=6,Trail=2,LinkedPairs=3,Ranges=new[]{Range(-30,0,16,40),Range(30,0,16,40)}} } },
            new Level { Name="Make Room", Outline=new[]{new Vector2(-10,-17),new Vector2(10,-17),new Vector2(17,8),new Vector2(14,17),new Vector2(-14,17),new Vector2(-17,8)}, Spacing=new Vector2(28,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Make Room",B=14,Wizard=1,Ranges=new[]{Range(0,9,22,8)}},
                W("Far Shooters",18,new[]{Range(0,9,22,8)},r:2) } }
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter Five")]
        public static void BuildChapterFive()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(39).Available) throw new InvalidOperationException("Chapters One through Four are required.");
            for(int n=41;n<=50;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Five content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterFiveDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+40,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterFiveInterior(i+41);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+41);
                    SetCampaignHint(i+41,ChapterFiveHints[i]);
                }
                CopyCampaignGatekeeperRematch();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=41;n<=50;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Five authored: levels 41-50, finite defense without Elites, 488-enemy Firebreak and Gatekeeper rematch.");
        }

        private static void AuthorChapterFiveInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true;
            navigation.participationAnchor=number==47?new Vector2(0,-32):number==48?new Vector2(0,-16):new Vector2(0,-10);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue=number==48?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 41:
                    CampaignRock(arena,new Vector2(-12,-2),new Vector2Int(3,7));
                    CampaignRock(arena,new Vector2(-10,-7),new Vector2Int(5,3)); break;
                case 42:
                    CampaignRock(arena,new Vector2(0,-2),new Vector2Int(7,12));
                    CampaignBarricade(number,encounter);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    CampaignGroundRectangle(encounter,number,"West hot inner pocket",new Vector2(-6,-1));
                    CampaignGroundRectangle(encounter,number,"East hot inner pocket",new Vector2(6,3)); break;
                case 43:
                    CampaignRock(arena,new Vector2(-6,5),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(6,7),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(3,-6),new Vector2Int(3,4)); break;
                case 44:
                    CampaignRock(arena,new Vector2(-15,14),new Vector2Int(5,3));
                    CampaignRock(arena,new Vector2(14,-14),new Vector2Int(6,3));
                    CampaignRock(arena,new Vector2(0,15),new Vector2Int(4,2));
                    CampaignCover(arena,encounter,number,85,30);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    var cover=encounter.barricade.cover.settings;
                    cover.rotationMode=CoverRotationMode.RotateAndPause; cover.rotateSeconds=3; cover.pauseSeconds=1; EditorUtility.SetDirty(cover); break;
                case 45:
                    CampaignRock(arena,new Vector2(-6,4),new Vector2Int(3,6));
                    CampaignRock(arena,new Vector2(6,-5),new Vector2Int(3,6));
                    CampaignGround(encounter,number,"Side warning crossing",new Vector2(-8,-8),true); break;
                case 46:
                    CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero);
                    RotateCampaignRail(sub,encounter.barricade.GetComponent<TrackObjectAuthoring>(),30);
                    CampaignRock(arena,new Vector2(-9,12),new Vector2Int(5,3));
                    CampaignRock(arena,new Vector2(9,-12),new Vector2Int(5,3)); break;
                case 47:
                    CampaignRock(arena,new Vector2(0,15),new Vector2Int(5,32));
                    var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
                    defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings","L47_Defense");
                    defense.settings.breachThreshold=1; defense.settings.maximumPlayerAttackers=3; EditorUtility.SetDirty(defense.settings);
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
                case 48:
                    // LOOP-008: side basins connect across a broad, unobstructed centre.
                    CampaignRock(arena,new Vector2(-15,-26),new Vector2Int(5,18));
                    CampaignRock(arena,new Vector2(15,-26),new Vector2Int(5,18));
                    CampaignGroundRectangle(encounter,number,"West hot outer pocket",new Vector2(-43,-32));
                    CampaignGroundRectangle(encounter,number,"East hot outer pocket",new Vector2(43,32)); break;
                case 49:
                    CampaignRock(arena,new Vector2(-13,-2),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(13,-2),new Vector2Int(3,4)); break;
            }
            int trackIndex = Array.IndexOf(CampaignTrackLevels, number);
            if (trackIndex > 0) AuthorProgressiveTrackRoute(sub, number, trackIndex + 1);
            int gateIndex = Array.IndexOf(CampaignBarricadeLevels, number);
            if (gateIndex > 0) AuthorBarricadeRooms(sub, number, gateIndex);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void CampaignGroundRectangle(EnemyWaveSequenceAuthoring encounter,int number,string name,Vector2 position)
        {
            CampaignGround(encounter,number,name,position);
            var patch=GameObject.Find(name).GetComponent<GroundHazardAuthoring>();
            patch.shape=GroundHazardShape.Rectangle; patch.width=3; patch.depth=6;
        }

        private static void CopyCampaignGatekeeperRematch()
        {
            // LOOP-002: copy the original boss geometry; only crowd, timing and side hazards change.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(50))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(BossGauntletBuilder.SubPath,CampaignSub(50))
                || !AssetDatabase.CopyAsset(BossGauntletBuilder.ScenePath,CampaignScenes+"Campaign_50.unity"))
                throw new InvalidOperationException("Could not copy the authored Gatekeeper arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(50),OpenSceneMode.Single);
            var navigation=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>().gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            var boss=UnityEngine.Object.FindFirstObjectByType<BossEncounterAuthoring>();
            boss.settings=CampaignSettings<BossEncounterSettings>("BossEncounterSettings","L50_Gatekeeper");
            var tuning=boss.settings;
            tuning.health=360; tuning.openingDuration=1.5f; tuning.minimumAnticipation=.65f;
            tuning.slam.Anticipation=.65f; tuning.lunge.Anticipation=.65f; tuning.sweep.Anticipation=.85f;
            EditorUtility.SetDirty(tuning);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var wave=WaveAsset("CP50_01_Gatekeeper_Returns",W("Gatekeeper Returns",12,new[]{Range(0,0,20,18)},r:1),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            CampaignGround(encounter,50,"West side warning",new Vector2(-15,0),true);
            CampaignGround(encounter,50,"East side warning",new Vector2(15,0),true,4);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_50.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(50));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(50,ChapterFiveHints[9]);
        }
    }
}
