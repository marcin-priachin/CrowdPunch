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
        private static readonly string[] ChapterSixHints =
        {
            "Break the shell with three explosions, then hit the exposed core. More Exploders arrive when both are gone.",
            "Dodge committed charges and turn Trails across the crowd. Keep clear of the ground they leave behind.",
            "The cover reverses every five seconds. Follow the opening and line up four hits.",
            "Push the block along the diagonal rail with six forward hits. Watch the alternating side crossings.",
            "Interrupt the Wizards with launched bodies. Break armor to keep your next shot available.",
            "Line up four hits on the narrow gate, then move through it to the exit.",
            "Stop every wave before it reaches the turquoise zone. Move between the approaches to prevent a breach.",
            "Clear all five large waves. Disrupt Elites and Wizards, and use explosive groups to clear shooting lanes.",
            "Choose where the explosives land, then clear the distant shooters.",
            "The Chicken leads its shots toward your movement. Change direction, return shots, and watch the edge warnings."
        };

        private static Level[] ChapterSixDesigns() => new[]
        {
            new Level { Name="Fresh Fuse", Outline=OvalCourt(34,36), Spacing=new Vector2(28,30), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Fresh Fuse",10,new[]{Range(-11,0,4,18),Range(11,0,4,18)},x:2) } },
            new Level { Name="Crossing Paths", Outline=new[]{new Vector2(-15,-20),new Vector2(15,-20),new Vector2(19,-12),new Vector2(15,0),new Vector2(19,12),new Vector2(15,20),new Vector2(-15,20),new Vector2(-19,12),new Vector2(-15,0),new Vector2(-19,-12)},
                Spacing=new Vector2(32,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="West Crossing",B=16,D=2,Trail=1,Ranges=new[]{Range(-13,0,3,20)}},
                new Wave {Name="East Crossing",B=20,R=2,Trail=2,Ranges=new[]{Range(13,0,3,20)}} } },
            new Level { Name="Reverse the Window", Outline=new[]{new Vector2(-14,-20),new Vector2(15,-20),new Vector2(20,-13),new Vector2(20,14),new Vector2(12,20),new Vector2(-16,20),new Vector2(-20,11),new Vector2(-20,-14)},
                Spacing=new Vector2(34,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Reversing Window",14,new[]{Range(-13,0,4,22),Range(13,0,4,22)},x:2) } },
            new Level { Name="Push Under Pressure", Outline=Clipped(38,44,5), Spacing=new Vector2(32,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Pressure Ammunition",14,new[]{Range(-14,0,3,28),Range(14,0,3,28)},x:2) } },
            new Level { Name="Disrupt the Casters", Outline=Clipped(40,40,5), Spacing=new Vector2(34,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Casters",B=14,Wizard=2,Ranges=new[]{Range(0,15,24,4)}},
                new Wave {Name="East Casters",B=18,R=2,Wizard=2,Ranges=new[]{Range(14,0,4,20)}},
                new Wave {Name="Armored Casters",B=18,A=2,Wizard=1,Ranges=new[]{Range(-14,0,4,20)}} } },
            new Level { Name="Clean Break", Outline=new[]{new Vector2(-16,-21),new Vector2(16,-21),new Vector2(16,-6),new Vector2(12,21),new Vector2(-12,21),new Vector2(-16,-6)},
                Spacing=new Vector2(26,36), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Clean Ammunition",12,new[]{Range(-10,-2,3,20),Range(10,-2,3,20)},x:2) } },
            new Level { Name="Long Watch", Outline=Rectangle(32,96), Spacing=new Vector2(30,94), Entry=new Vector2(0,-36), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Distant Watch",20,new[]{Range(0,41,24,8)},r:2,batch:4),
                W("Charging Watch",24,new[]{Range(0,41,24,8)},d:2,delay:5,batch:4),
                W("Explosive Watch",24,new[]{Range(0,41,24,8)},x:2,delay:5,batch:4) } },
            new Level { Name="Moving Batteries", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("North Battery",72,new[]{Range(0,32,52,16)},r:4,e:1),
                W("West Reaction",88,new[]{Range(-31,0,16,44)},x:6,d:2),
                new Wave {Name="East Battery",B=96,A=4,E=2,Ranges=new[]{Range(31,0,16,44)}},
                new Wave {Name="Split Casters",B=112,R=4,Wizard=2,Ranges=new[]{Range(-30,0,16,40),Range(30,0,16,40)}},
                W("Moving Batteries",128,new[]{Range(0,30,64,24)},x:6,d:4) } },
            new Level { Name="Open Air", Outline=new[]{new Vector2(-15,-16),new Vector2(15,-16),new Vector2(18,-12),new Vector2(16,-3),new Vector2(18,6),new Vector2(18,12),new Vector2(15,16),new Vector2(-15,16),new Vector2(-18,12),new Vector2(-18,6),new Vector2(-16,-3),new Vector2(-18,-12)},
                Spacing=new Vector2(30,26), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Open Fuses",12,new[]{Range(0,7,24,8)},x:2), W("Open Shooters",16,new[]{Range(0,7,24,8)},r:2) } }
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter Six")]
        public static void BuildChapterSix()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(49).Available) throw new InvalidOperationException("Chapters One through Five are required.");
            for(int n=51;n<=60;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Six content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterSixDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+50,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterSixInterior(i+51);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+51);
                    SetCampaignHint(i+51,ChapterSixHints[i]);
                }
                CopyCampaignChickenRematch();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=51;n<=60;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Six authored: levels 51-60, reversing cover, six-hit rail, 531-enemy arena and movement-leading Chicken rematch.");
        }

        private static void AuthorChapterSixInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true;
            navigation.participationAnchor=number==57?new Vector2(0,-32):number==58?new Vector2(0,-16):new Vector2(0,-10);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue=number==58?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 51: CopyCampaignObjective(sub,arena,encounter,number,false,Vector3.zero); break;
                case 52:
                    CampaignRock(arena,new Vector2(-5,6),new Vector2Int(3,7));
                    CampaignRock(arena,new Vector2(5,-6),new Vector2Int(3,7)); break;
                case 53:
                    CampaignRock(arena,new Vector2(0,16),new Vector2Int(10,3));
                    CampaignCover(arena,encounter,number,90,30);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    var cover=encounter.barricade.cover.settings;
                    cover.rotationMode=CoverRotationMode.ReversePeriodically; cover.reverseSeconds=5; EditorUtility.SetDirty(cover); break;
                case 54:
                    CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero);
                    var track=encounter.barricade.GetComponent<TrackObjectAuthoring>();
                    RotateCampaignRail(sub,track,-30);
                    track.settings.requiredNetHits=6; EditorUtility.SetDirty(track.settings);
                    CampaignRock(arena,new Vector2(-7,13),new Vector2Int(5,3));
                    CampaignRock(arena,new Vector2(7,-13),new Vector2Int(5,3));
                    CampaignGround(encounter,number,"West pressure crossing",new Vector2(-9,0),true);
                    CampaignGround(encounter,number,"East pressure crossing",new Vector2(9,0),true,4); break;
                case 55:
                    CampaignRock(arena,new Vector2(-6,3),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(6,3),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(0,9),new Vector2Int(4,4)); break;
                case 56:
                    CampaignRock(arena,new Vector2(-5,-4),new Vector2Int(5,3));
                    CampaignBarricade(number,encounter);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings); break;
                case 57:
                    CampaignRock(arena,new Vector2(-7,24),new Vector2Int(4,12));
                    CampaignRock(arena,new Vector2(7,8),new Vector2Int(4,12));
                    CampaignRock(arena,new Vector2(-6,-9),new Vector2Int(4,12));
                    var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
                    defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings","L57_Defense");
                    defense.settings.breachThreshold=1; defense.settings.maximumPlayerAttackers=3; EditorUtility.SetDirty(defense.settings);
                    // PROTECT-001/004: finite ordinary enemies; no Elite or ammunition replenishment.
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
                case 58:
                    CampaignRock(arena,new Vector2(-13,7),new Vector2Int(6,10));
                    CampaignRock(arena,new Vector2(13,-7),new Vector2Int(6,10));
                    CampaignRock(arena,new Vector2(0,-30),new Vector2Int(10,6));
                    CampaignGround(encounter,number,"West outer crossing",new Vector2(-40,-30),true);
                    CampaignGround(encounter,number,"East outer crossing",new Vector2(40,30),true,4); break;
                case 59:
                    CampaignRock(arena,new Vector2(-15,-8),new Vector2Int(2,3));
                    CampaignRock(arena,new Vector2(15,-8),new Vector2Int(2,3));
                    CampaignRock(arena,new Vector2(0,14),new Vector2Int(6,2)); break;
            }
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void CopyCampaignChickenRematch()
        {
            // LOOP-002/CHICKEN-006: retain every original arena transform and collision mesh.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(60))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Scenes+"Gauntlet_20/Gauntlet_20 Sub Scene.unity",CampaignSub(60))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_20.unity",CampaignScenes+"Campaign_60.unity"))
                throw new InvalidOperationException("Could not copy the authored Chicken arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(60),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.GetComponent<NavigationArenaAuthoring>();
            if(navigation==null) navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            var boss=UnityEngine.Object.FindFirstObjectByType<ChickenBossAuthoring>();
            boss.settings=CampaignSettings<ChickenBossSettings>("ChickenBossSettings","L60_Chicken");
            boss.settings.health=2100; boss.settings.shotAim=ChickenShotAim.MovementLead;
            boss.settings.windUp=.8f; boss.settings.shotSpacing=1.5f; EditorUtility.SetDirty(boss.settings);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var wave=WaveAsset("CP60_01_Chicken_Crossfire",W("Chicken Crossfire",8,new[]{Range(0,0,24,24)},x:2),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            encounter.chickenBoss=boss;
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            CampaignGround(encounter,60,"West edge warning",new Vector2(-16,0),true);
            CampaignGround(encounter,60,"East edge warning",new Vector2(16,0),true,4);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_60.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(60));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(60,ChapterSixHints[9]);
        }
    }
}
