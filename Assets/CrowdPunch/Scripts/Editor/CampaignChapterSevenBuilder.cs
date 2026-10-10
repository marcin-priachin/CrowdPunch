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
        private static readonly string[] ChapterSevenHints =
        {
            "Break armor with launched bodies, then clear the distant shooters.",
            "Find the gate on the right. Land four hits, then reach the exit. The warning strips have safe bypasses.",
            "Interrupt the Wizard and launch Trails across useful lanes. Keep a retreat route clear.",
            "Crack the shell with four explosions, then hit the exposed core. Keep Exploders out of the hot pockets.",
            "Read each new approach. Disrupt the Elite, interrupt Wizards, and leave room to dodge charges.",
            "A successful hit pauses the cover for one second. Use that opening to line up the next shot.",
            "Move around the islands as the ground changes. Break armor and keep clear of committed charges.",
            "Clear six large waves from changing directions. Keep the central cross clear and use the outer bypasses.",
            "Drive the block straight down the rail with five forward hits.",
            "The Blob reflects from walls while rolling. Attack during its pause or wind-up, and watch the outer ground warnings."
        };

        private static Level[] ChapterSevenDesigns() => new[]
        {
            new Level { Name="Stable Footing", Outline=new[]{new Vector2(-18,-17),new Vector2(18,-17),new Vector2(18,3),new Vector2(15,3),new Vector2(15,17),new Vector2(-15,17),new Vector2(-15,7),new Vector2(-18,7)},
                Spacing=new Vector2(30,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave{Name="Steady Armor",B=12,A=1,Ranges=new[]{Range(-5,9,14,5)}},W("Far Shooters",16,new[]{Range(4,9,16,5)},r:2)} },
            new Level { Name="Side Door", Outline=new[]{new Vector2(-18,-22),new Vector2(18,-22),new Vector2(18,22),new Vector2(3,22),new Vector2(-8,16),new Vector2(-18,3)},
                Spacing=new Vector2(30,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Side Door Ammunition",14,new[]{Range(-12,-5,4,18),Range(13,-4,4,20)},x:2)} },
            new Level { Name="Draw a Line", Outline=Clipped(42,38,4), Spacing=new Vector2(36,32), Entry=new Vector2(0,-14), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave{Name="West Lines",B=16,Trail=2,Wizard=1,Ranges=new[]{Range(-15,1,4,22)}},
                new Wave{Name="East Charges",B=20,D=2,Trail=2,Ranges=new[]{Range(15,1,4,22)}}} },
            new Level { Name="Shell Island", Outline=new[]{new Vector2(-19,-19),new Vector2(-4,-19),new Vector2(-4,-17),new Vector2(4,-17),new Vector2(4,-19),new Vector2(19,-19),new Vector2(19,-4),new Vector2(17,-4),new Vector2(17,4),new Vector2(19,4),new Vector2(19,19),new Vector2(4,19),new Vector2(4,17),new Vector2(-4,17),new Vector2(-4,19),new Vector2(-19,19),new Vector2(-19,4),new Vector2(-17,4),new Vector2(-17,-4),new Vector2(-19,-4)},
                Spacing=new Vector2(32,32), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Island Fuses",14,new[]{Range(-11,0,4,16),Range(11,0,4,16)},x:2)} },
            new Level { Name="Three Approaches", Outline=new[]{new Vector2(-21,-14),new Vector2(-14,-21),new Vector2(14,-21),new Vector2(21,-14),new Vector2(21,12),new Vector2(10,21),new Vector2(-10,21),new Vector2(-21,12)},
                Spacing=new Vector2(36,36), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave{Name="North Armor",B=18,A=2,E=1,Ranges=new[]{Range(0,14,18,4)}},
                new Wave{Name="West Casters",B=20,R=2,Wizard=2,Ranges=new[]{Range(-15,0,4,20)}},
                new Wave{Name="East Trails",B=22,D=2,Trail=2,Ranges=new[]{Range(15,0,4,20)}}} },
            new Level { Name="Set the Pace", Outline=new[]{new Vector2(-19,-20),new Vector2(19,-20),new Vector2(19,10),new Vector2(12,20),new Vector2(-11,20),new Vector2(-19,12)},
                Spacing=new Vector2(32,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Pause Ammunition",12,new[]{Range(-13,-1,4,20),Range(13,0,4,22)},x:2)} },
            new Level { Name="Keep It Moving", Outline=Clipped(42,40,6), Spacing=new Vector2(36,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("North Pressure",20,new[]{Range(0,14,24,4)},r:2,e:1),
                new Wave{Name="East Armor",B=22,A=2,D=2,Ranges=new[]{Range(15,0,4,24)}}} },
            new Level { Name="Shifting Front", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("North Front",64,new[]{Range(0,33,52,14)},r:4),
                W("West Fuses",80,new[]{Range(-33,0,14,44)},x:6),
                new Wave{Name="East Armor",B=88,A=4,E=2,Ranges=new[]{Range(33,0,14,44)}},
                new Wave{Name="South Trails",B=96,D=4,Trail=2,Ranges=new[]{Range(0,-34,52,14)}},
                new Wave{Name="Split Casters",B=112,R=4,Wizard=2,Ranges=new[]{Range(-33,0,14,40),Range(33,0,14,40)}},
                new Wave{Name="Final Front",B=128,A=4,X=6,Ranges=new[]{Range(0,34,64,16)}}} },
            new Level { Name="Straighten Up", Outline=new[]{new Vector2(-18,-20),new Vector2(18,-20),new Vector2(18,-3),new Vector2(15,-3),new Vector2(15,3),new Vector2(18,3),new Vector2(18,20),new Vector2(-18,20),new Vector2(-18,3),new Vector2(-15,3),new Vector2(-15,-3),new Vector2(-18,-3)},
                Spacing=new Vector2(30,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Straight Ammunition",10,new[]{Range(-11,0,4,22),Range(11,0,4,22)},x:2)} }
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter Seven")]
        public static void BuildChapterSeven()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(59).Available) throw new InvalidOperationException("Chapters One through Six are required.");
            for(int n=61;n<=70;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Seven content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterSevenDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+60,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterSevenInterior(i+61);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+61);
                    SetCampaignHint(i+61,ChapterSevenHints[i]);
                }
                CopyCampaignRollingRematch();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=61;n<=70;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Seven authored: levels 61-70, offset gate, four-explosion shell, hit-pause cover, 606-enemy arena and reflecting Rolling Blob.");
        }

        private static void AuthorChapterSevenInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true; navigation.participationAnchor=new Vector2(0,number==68?-16:-10);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue=number==68?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 62:
                    CampaignBarricade(number,encounter);
                    OffsetChapterSevenGate(arena,encounter.barricade);
                    CampaignRock(arena,new Vector2(-6,-5),new Vector2Int(3,7));
                    CampaignGround(encounter,number,"West central crossing",new Vector2(-1,1),true);
                    CampaignGround(encounter,number,"East central crossing",new Vector2(5,-6),true,4); break;
                case 63:
                    CampaignRock(arena,new Vector2(-6,0),new Vector2Int(3,10));
                    CampaignRock(arena,new Vector2(6,0),new Vector2Int(3,10)); break;
                case 64:
                    CopyCampaignObjective(sub,arena,encounter,number,false,Vector3.zero);
                    var shell=encounter.barricade.GetComponent<ShellTargetAuthoring>().settings;
                    shell.requiredExplosions=4; EditorUtility.SetDirty(shell);
                    CampaignGroundRectangle(encounter,number,"West hot pocket",new Vector2(-13,-12));
                    CampaignGroundRectangle(encounter,number,"East hot pocket",new Vector2(13,12)); break;
                case 65:
                    CampaignRock(arena,new Vector2(-6,5),new Vector2Int(4,5));
                    CampaignRock(arena,new Vector2(6,5),new Vector2Int(4,5));
                    CampaignGround(encounter,number,"Far side crossing",new Vector2(10,-13),true); break;
                case 66:
                    CampaignCover(arena,encounter,number,90,25);
                    encounter.barricade.settings.requiredHits=4; EditorUtility.SetDirty(encounter.barricade.settings);
                    var cover=encounter.barricade.cover;
                    cover.settings.hitResponse=CoverHitResponse.Pause; cover.settings.hitPauseSeconds=1; EditorUtility.SetDirty(cover.settings);
                    var offset=new Vector3(-2,0,3);
                    encounter.barricade.transform.position+=offset; cover.transform.position+=offset;
                    GameObject.Find("Central enclosure plinth").transform.position+=offset; break;
                case 67:
                    CampaignRock(arena,new Vector2(-6,6),new Vector2Int(4,5));
                    CampaignRock(arena,new Vector2(6,-6),new Vector2Int(4,5));
                    CampaignGround(encounter,number,"West island crossing",new Vector2(-4,-5),true);
                    CampaignGround(encounter,number,"East island crossing",new Vector2(4,5),true,4); break;
                case 68:
                    CampaignRock(arena,new Vector2(-17,17),new Vector2Int(7,9));
                    CampaignRock(arena,new Vector2(17,17),new Vector2Int(9,7));
                    CampaignRock(arena,new Vector2(-17,-17),new Vector2Int(9,7));
                    CampaignRock(arena,new Vector2(17,-17),new Vector2Int(7,9));
                    CampaignGround(encounter,number,"Northwest outer warning",new Vector2(-43,30),true);
                    CampaignGround(encounter,number,"Northeast outer warning",new Vector2(43,30),true,8f/3);
                    CampaignGround(encounter,number,"Southwest outer warning",new Vector2(-43,-30),true,16f/3); break;
                case 69: CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero); break;
            }
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void OffsetChapterSevenGate(ArenaAuthoring arena,BarricadeAuthoring gate)
        {
            gate.settings.requiredHits=4; EditorUtility.SetDirty(gate.settings);
            var offset=new Vector3(8,0,2); gate.transform.position+=offset; gate.exit.position+=offset;
            // BARRICADE-006: the offset opening needs unequal spans, both extending past the perimeter.
            foreach(var side in arena.GetComponentsInChildren<SolidObstacleAuthoring>().Where(o=>o.name.StartsWith("Gate terrain")))
            {
                bool left=side.name.EndsWith("left"); int span=left?26:10;
                side.footprint=new Vector2Int(span,3);
                side.transform.position=new Vector3(8+(left?-1:1)*(1+span*.5f),side.transform.position.y,gate.transform.position.z+1.2f);
                side.transform.GetChild(0).localScale=new Vector3(span,gate.size.y,3);
            }
        }

        private static void CopyCampaignRollingRematch()
        {
            // LOOP-002/ROLL-006: preserve the original boss arena and obstacle geometry exactly.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(70))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Scenes+"Gauntlet_21/Gauntlet_21 Sub Scene.unity",CampaignSub(70))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_21.unity",CampaignScenes+"Campaign_70.unity"))
                throw new InvalidOperationException("Could not copy the authored Rolling Blob arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(70),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.GetComponent<NavigationArenaAuthoring>();
            if(navigation==null) navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            var boss=UnityEngine.Object.FindFirstObjectByType<RollingBossAuthoring>();
            boss.settings=CampaignSettings<RollingBossSettings>("RollingBossSettings","L70_Rolling");
            boss.settings.health=600; boss.settings.rollingAim=RollingAim.Reflect;
            boss.settings.pauseDurations=new Vector3(2.5f,1.8f,1.2f); EditorUtility.SetDirty(boss.settings);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var wave=WaveAsset("CP70_01_Rolling_Pressure",W("Rolling Pressure",8,new[]{Range(-12,0,5,20),Range(12,0,5,20)},x:2),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            encounter.rollingBoss=boss;
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            CampaignGround(encounter,70,"West outer warning",new Vector2(-17,-14),true);
            CampaignGround(encounter,70,"East outer warning",new Vector2(17,14),true,4);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_70.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(70));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(70,ChapterSevenHints[9]);
        }
    }
}
