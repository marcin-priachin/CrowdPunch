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
        private static readonly string[] ChapterEightHints =
        {
            "Clear the distant shooters, then choose where the explosives land.",
            "The opening reverses every six seconds. Interrupt the Wizard and line up four hits through the cover.",
            "Dodge committed charges and use the crowd to break armor. Keep the side warnings in view.",
            "Work around the two bends, land four hits on the narrow gate, then reach the exit.",
            "Stop every wave before it reaches the turquoise zone. Use launched bodies to break armor.",
            "Break the shell with three explosions, then hit the exposed core. More Exploders arrive when both are gone.",
            "Hold the final approach. Interrupt Wizards and stop every enemy before it reaches the zone.",
            "Clear six large waves. Change shooting angles, disrupt Elites, and keep the central routes clear.",
            "Choose clean launches through the explosives and armor. Clear both waves.",
            "Topple each pillar with a launched enemy while the Dino is in its path. Keep the outer ground warnings clear."
        };

        private static Level[] ChapterEightDesigns() => new[]
        {
            new Level{Name="Find Your Feet",Outline=new[]{new Vector2(-13,-18),new Vector2(13,-18),new Vector2(18,-10),new Vector2(17,11),new Vector2(12,18),new Vector2(-12,18),new Vector2(-17,11),new Vector2(-18,-10)},
                Spacing=new Vector2(30,30),Entry=new Vector2(0,-13),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("North Shooters",14,new[]{Range(-6,11,14,4)},r:2),W("West Fuses",18,new[]{Range(-12,0,4,20)},x:2)}},
            new Level{Name="Thread the Crowd",Outline=new[]{new Vector2(-16,-20),new Vector2(17,-20),new Vector2(21,-12),new Vector2(21,10),new Vector2(12,20),new Vector2(-15,20),new Vector2(-21,12),new Vector2(-21,-13)},
                Spacing=new Vector2(36,34),Entry=new Vector2(0,-15),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave{Name="Living Ammunition",B=14,Wizard=1,Trail=1,Ranges=new[]{Range(-15,0,4,24),Range(15,0,4,24)}}}},
            new Level{Name="Committed Lines",Outline=new[]{new Vector2(-22,-14),new Vector2(-16,-20),new Vector2(14,-20),new Vector2(22,-10),new Vector2(20,16),new Vector2(14,20),new Vector2(-14,20),new Vector2(-20,10)},
                Spacing=new Vector2(36,34),Entry=new Vector2(0,-15),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("East Commitments",18,new[]{Range(16,3,4,20)},d:2,e:1),
                new Wave{Name="West Armor",B=20,A=2,R=2,Ranges=new[]{Range(-15,-1,4,20)}},
                new Wave{Name="North Trails",B=22,X=2,Trail=2,Ranges=new[]{Range(0,14,20,4)}}}},
            new Level{Name="Open the Last Gate",Outline=new[]{new Vector2(-19,-23),new Vector2(19,-23),new Vector2(19,-8),new Vector2(16,0),new Vector2(19,9),new Vector2(15,23),new Vector2(-15,23),new Vector2(-19,9),new Vector2(-16,0),new Vector2(-19,-8)},
                Spacing=new Vector2(32,40),Entry=new Vector2(0,-18),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Last Gate Ammunition",16,new[]{Range(-14,2,3,16),Range(14,2,3,16)},x:2)}},
            new Level{Name="Guard the Approach",Outline=Rectangle(32,96),Spacing=new Vector2(30,94),Entry=new Vector2(0,-36),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Three Approaches",20,new[]{Range(0,41,24,8)},r:2,batch:4),
                new Wave{Name="Armored Approach",B=24,A=2,Delay=5,Batch=4,Ranges=new[]{Range(0,41,24,8)}},
                new Wave{Name="Casting Approach",B=24,D=2,Wizard=1,Delay=5,Batch=4,Ranges=new[]{Range(0,41,24,8)}}}},
            new Level{Name="A Clear Purpose",Outline=Clipped(36,38,5),Spacing=new Vector2(30,32),Entry=new Vector2(0,-14),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Clear Fuses",10,new[]{Range(-10,0,4,16),Range(10,0,4,16)},x:2)}},
            new Level{Name="Final Stand",Outline=Rectangle(32,96),Spacing=new Vector2(30,94),Entry=new Vector2(0,-36),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave{Name="Final Armor",B=24,A=2,Batch=4,Ranges=new[]{Range(0,41,24,8)}},
                W("Final Fuses",26,new[]{Range(0,41,24,8)},x:2,delay:5,batch:4),
                new Wave{Name="Final Casters",B=26,R=2,Wizard=2,Delay=5,Batch=4,Ranges=new[]{Range(0,41,24,8)}}}},
            new Level{Name="Claim the Ground",Outline=Rectangle(100,100),Spacing=new Vector2(96,96),Entry=new Vector2(0,-16),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("North Claim",72,new[]{Range(0,33,58,14)},r:4),
                W("West Reaction",88,new[]{Range(-36,0,12,44)},x:6,d:2),
                new Wave{Name="East Battery",B=96,A=4,E=2,Ranges=new[]{Range(36,0,12,44)}},
                new Wave{Name="Split North Casters",B=104,Wizard=2,Trail=2,Ranges=new[]{Range(-20,33,20,16),Range(20,33,20,16)}},
                W("Split Side Charges",120,new[]{Range(-36,0,12,40),Range(36,0,12,40)},r:4,d:4),
                new Wave{Name="Last Claim",B=136,A=4,X=6,E=2,Ranges=new[]{Range(-20,34,26,16),Range(20,34,26,16)}}}},
            new Level{Name="Room to Breathe",Outline=new[]{new Vector2(-14,-17),new Vector2(14,-17),new Vector2(19,-10),new Vector2(17,11),new Vector2(13,17),new Vector2(-13,17),new Vector2(-17,11),new Vector2(-19,-10)},
                Spacing=new Vector2(32,28),Entry=new Vector2(0,-12),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Room for Fuses",14,new[]{Range(-9,7,6,6),Range(9,7,6,6)},x:2),
                new Wave{Name="Clean Last Launches",B=18,R=2,A=1,Ranges=new[]{Range(-9,7,6,6),Range(9,7,6,6)}}}}
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter Eight")]
        public static void BuildChapterEight()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(69).Available) throw new InvalidOperationException("Chapters One through Seven are required.");
            for(int n=71;n<=80;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Eight content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterEightDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+70,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterEightInterior(i+71);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+71);
                    SetCampaignHint(i+71,ChapterEightHints[i]);
                }
                CopyCampaignDinoRematch();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=71;n<=80;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Eight authored: levels 71-80, finite defenses, 658-enemy arena, Dino rematch and complete 80-level campaign.");
        }

        private static void AuthorChapterEightInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true;
            navigation.participationAnchor=new Vector2(0,number==75||number==77?-32:number==78?-16:-11);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter); data.FindProperty("minimumPlayerDistance").floatValue=number==78?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 71: CampaignRock(arena,new Vector2(6,8),new Vector2Int(4,4)); break;
                case 72:
                    CampaignRock(arena,new Vector2(-8,11),new Vector2Int(4,3));
                    CampaignRock(arena,new Vector2(9,-8),new Vector2Int(4,4));
                    CampaignCover(arena,encounter,number,100,30);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    var cover=encounter.barricade.cover.settings;
                    cover.rotationMode=CoverRotationMode.ReversePeriodically; cover.reverseSeconds=6; EditorUtility.SetDirty(cover); break;
                case 73:
                    CampaignRock(arena,new Vector2(-5,6),new Vector2Int(3,8));
                    CampaignRock(arena,new Vector2(5,-5),new Vector2Int(3,8));
                    CampaignGround(encounter,number,"West optional crossing",new Vector2(-10,0),true);
                    CampaignGround(encounter,number,"East optional crossing",new Vector2(10,0),true,4); break;
                case 74:
                    CampaignRock(arena,new Vector2(-5,-8),new Vector2Int(4,6));
                    CampaignRock(arena,new Vector2(5,2),new Vector2Int(4,6));
                    CampaignBarricade(number,encounter);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    CampaignGroundRectangle(encounter,number,"West bend pocket",new Vector2(-10,-1));
                    CampaignGroundRectangle(encounter,number,"East bend pocket",new Vector2(10,-9)); break;
                case 75:
                    CampaignRock(arena,new Vector2(-7,23),new Vector2Int(4,7));
                    CampaignRock(arena,new Vector2(7,12),new Vector2Int(4,7));
                    CampaignRock(arena,new Vector2(-7,1),new Vector2Int(4,7));
                    CampaignRock(arena,new Vector2(7,-10),new Vector2Int(4,7));
                    ChapterEightDefense(number,encounter); break;
                case 76:
                    CampaignRock(arena,new Vector2(-15,11),new Vector2Int(2,4));
                    CampaignRock(arena,new Vector2(15,11),new Vector2Int(2,4));
                    CampaignRock(arena,new Vector2(0,16),new Vector2Int(8,2));
                    CopyCampaignObjective(sub,arena,encounter,number,false,Vector3.zero); break;
                case 77:
                    CampaignRock(arena,new Vector2(-6,18),new Vector2Int(4,18));
                    CampaignRock(arena,new Vector2(6,-7),new Vector2Int(4,18));
                    ChapterEightDefense(number,encounter); break;
                case 78:
                    CampaignRock(arena,new Vector2(-18,15),new Vector2Int(8,6));
                    CampaignRock(arena,new Vector2(-23,7),new Vector2Int(5,8));
                    CampaignRock(arena,new Vector2(-18,-1),new Vector2Int(8,6));
                    CampaignRock(arena,new Vector2(18,-15),new Vector2Int(8,6));
                    CampaignRock(arena,new Vector2(23,-7),new Vector2Int(5,8));
                    CampaignRock(arena,new Vector2(18,1),new Vector2Int(8,6));
                    CampaignGround(encounter,number,"West outer lane",new Vector2(-43,-32),true);
                    CampaignGround(encounter,number,"East outer lane",new Vector2(43,32),true,4); break;
                case 79: CampaignRock(arena,new Vector2(0,12),new Vector2Int(4,3)); break;
            }
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void ChapterEightDefense(int number,EnemyWaveSequenceAuthoring encounter)
        {
            var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
            defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings",$"L{number}_Defense");
            defense.settings.breachThreshold=1; defense.settings.maximumPlayerAttackers=3; EditorUtility.SetDirty(defense.settings);
            // PROTECT-001/004: finite waves without Elites or Wizard supply. Preserve the
            // approved armor safeguard only in waves that contain protected armor.
            foreach(var wave in encounter.Waves)
            {
                var finite=new SerializedObject(wave);
                finite.FindProperty("replenishWhileBossLives").boolValue=false;
                finite.FindProperty("wizardAmmunitionProfile").objectReferenceValue=null;
                finite.FindProperty("waitForPersistentHazards").boolValue=false;
                finite.ApplyModifiedPropertiesWithoutUndo();
            }
            Box("Protected Zone 8 x 4",encounter.transform,new Vector3(0,-.97f,-46),new Vector3(8,.035f,4),MaterialAsset("ProtectedPointZone",new Color(.15f,.75f,.68f)),false);
        }

        private static void CopyCampaignDinoRematch()
        {
            // LOOP-002/PILLAR-006: retain the complete arena and all three pillar positions.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(80))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Scenes+"Gauntlet_22/Gauntlet_22 Sub Scene.unity",CampaignSub(80))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_22.unity",CampaignScenes+"Campaign_80.unity"))
                throw new InvalidOperationException("Could not copy the authored Dino arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(80),OpenSceneMode.Single);
            var boss=UnityEngine.Object.FindFirstObjectByType<DinoBossAuthoring>();
            boss.settings=CampaignSettings<DinoBossSettings>("DinoBossSettings","L80_Dino");
            boss.settings.requiredSuccessfulHits=3; boss.settings.enemiesPerPillar=2;
            boss.settings.chaseDurations=new Vector3(6,4.5f,3.5f); boss.settings.warningDuration=1;
            boss.settings.fallDirection=PillarFallDirection.TowardBoss; EditorUtility.SetDirty(boss.settings);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var wave=WaveAsset("CP80_01_Giants_Fall_Again",W("Giants Fall Again",10,encounter.Waves[0].SpawnRectangles.ToArray(),r:1,d:1),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            encounter.dinoBoss=boss;
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            CampaignGround(encounter,80,"West outer approach",new Vector2(-28,0),true);
            CampaignGround(encounter,80,"East outer approach",new Vector2(28,0),true,4);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_80.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(80));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(80,ChapterEightHints[9]);
        }
    }
}
