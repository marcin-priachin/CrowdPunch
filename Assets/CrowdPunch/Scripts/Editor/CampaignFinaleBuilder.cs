using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        private const string FinaleData=CampaignData+"Finale/";
        private const string FinaleModel=FinaleWraithBuilder.Source;

        [MenuItem("Crowd Punch/Campaign/Create Final Encounter")]
        public static void BuildFinale()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(79).Available) throw new InvalidOperationException("The existing 80-level campaign is required.");
            if(File.Exists(CampaignScenes+"Campaign_81.unity")) throw new InvalidOperationException("Finale already exists; edit its saved assets.");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(FinaleModel)==null) throw new InvalidOperationException("Wraith model is unavailable.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(FinaleData); AssetDatabase.Refresh();
            try {
                var design=new Level {Name="Bring Down the Sky",Outline=Rectangle(100,100),Spacing=new Vector2(96,96),
                    Entry=new Vector2(0,-40),Lanes=Array.Empty<Vector4>(),
                    Waves=new[]{W("Finale South West Supply",4,new[]{Range(-35,-25,8,18)},x:1,delay:2)} };
                BuildLevel(80,design,LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                var scene=EditorSceneManager.OpenScene(CampaignSub(81),OpenSceneMode.Single);
                var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var nav=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
                nav.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
                nav.overrideParticipationAnchor=true; nav.participationAnchor=new Vector2(0,-38);
                var settings=FinaleAsset<FinaleSettings>("FinaleSettings");
                var bossObject=new GameObject("Goleling - finale ECS body");
                var body=bossObject.AddComponent<BarricadeAuthoring>();
                body.settings=FinaleAsset<BarricadeSettings>("BossSolid"); body.settings.requiredHits=1;
                body.settings.eligibleLaunchSources=BarricadeLaunchSources.Player; body.settings.reboundMultiplier=1;
                body.size=settings.colliderSize; body.completeOnDestruction=true; body.transform.position=new Vector3(0,settings.flightHeight,0);
                var boss=bossObject.AddComponent<FinaleBossAuthoring>(); boss.settings=settings;
                var initial=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(initial.Waves[0]),FinaleData+initial.Waves[0].name+".asset");
                ConfigureFinaleSupply(initial,boss,1,true,new Vector3(-35,2,-25));
                var positions=new[]{new Vector3(-27,1,27),new Vector3(27,1,27),new Vector3(-27,.7f,-31),new Vector3(27,1,-27)};
                for(int i=0;i<4;i++) {
                    BarricadeAuthoring target;
                    if(i==1) {
                        CopyCampaignObjective(scene,arena,initial,81,false,new Vector3(27,0,27));
                        target=initial.barricade; initial.barricade=null;
                        target.settings.eligibleLaunchSources=BarricadeLaunchSources.Player;
                        var shell=target.GetComponent<ShellTargetAuthoring>().settings;
                        shell.requiredExplosions=3; shell.coreMaxHealth=100; EditorUtility.SetDirty(shell);
                    }
                    else {
                        target=new GameObject(new[]{"Rotating cover anchor","Shell anchor","Rail anchor","Narrow target anchor"}[i]).AddComponent<BarricadeAuthoring>();
                        target.transform.position=positions[i]; target.completeOnDestruction=true;
                        target.settings=FinaleAsset<BarricadeSettings>("Anchor"+i);
                        target.settings.requiredHits=i==2?1:4; target.settings.eligibleLaunchSources=BarricadeLaunchSources.Player;
                        target.size=i==3?new Vector3(1.5f,4,1.5f):i==2?new Vector3(3.6f,3.4f,3.2f):new Vector3(2.4f,4,2.4f);
                        var material=MaterialAsset("FinaleAnchor"+i,i==2?new Color(1,.55f,.12f):new Color(.2f,.8f,.75f));
                        if(i==2) Box("Sliding launch block",target.transform,target.transform.position,target.size,material,false);
                        else {
                            BarricadePiece(target,"Anchor target",Vector3.zero,target.size,material,0,Vector3.down);
                            for(int h=1;h<4;h++) {
                                var scar=BarricadePiece(target,"Damage band "+h,new Vector3(0,h-2,-target.size.z*.5f-.03f),
                                    new Vector3(target.size.x,.18f,.06f),MaterialAsset("FinaleScars",Color.black),h,Vector3.down);
                            }
                        }
                    }
                    var anchor=target.gameObject.AddComponent<FinaleAnchorAuthoring>(); anchor.boss=boss;
                    anchor.landing=new Vector3(i==0||i==2?-15:15,3,i<2?18:-18);
                    if(i==0) {
                        var cover=new GameObject("Finale rotating shield").AddComponent<RotatingCoverAuthoring>();
                        cover.transform.position=positions[i]; cover.settings=FinaleAsset<RotatingCoverSettings>("Cover");
                        cover.target=target; target.cover=cover;
                        for(int panel=0;panel<CoverGeometry.PanelCount;panel++) {
                            var p=Box("Cover panel "+panel,cover.transform,cover.transform.position,Vector3.one,
                                MaterialAsset("FinaleCover",new Color(.18f,.32f,.45f)),false).AddComponent<CoverPanelAuthoring>();
                            p.cover=cover; p.index=panel;
                        }
                    }
                    if(i==2) {
                        var rail=target.gameObject.AddComponent<TrackObjectAuthoring>(); rail.settings=FinaleAsset<TrackObjectSettings>("Rail");
                        rail.settings.requiredNetHits=4; rail.settings.playerLaunchedBodiesOnly=true; rail.settings.filterLaunchedExplosions=true;
                        rail.destination=new GameObject("Finale rail socket").transform; rail.destination.position=positions[i]+Vector3.forward*8;
                        for(int side=-1;side<=1;side+=2) {
                            Box("Short rail",null,positions[i]+new Vector3(side*1.6f,-1.6f,4),new Vector3(.2f,.15f,12),MaterialAsset("FinaleRail",Color.gray),false);
                            var rim=Box("Socket rim",rail.destination,rail.destination.position+new Vector3(side*2,-1.4f,0),new Vector3(.25f,.3f,4),
                                MaterialAsset("FinaleSocket",Color.cyan),false).AddComponent<TrackSocketVisualAuthoring>(); rim.trackObject=rail;
                        }
                    }
                    EditorUtility.SetDirty(target.settings);
                }
                CreateFinaleSupply(boss,"North West",new Wave {Name="North West",B=8,X=2,Delay=2,Ranges=new[]{Range(-35,25,8,18)}},1,true,new Vector3(-35,2,25));
                CreateFinaleSupply(boss,"North East",new Wave {Name="North East",B=8,X=2,Delay=2,Ranges=new[]{Range(35,25,8,18)}},1,true,new Vector3(35,2,25));
                CreateFinaleSupply(boss,"South East",new Wave {Name="South East",B=8,X=2,Delay=2,Ranges=new[]{Range(35,-25,8,18)}},1,true,new Vector3(35,2,-25));
                for(int stage=1;stage<=3;stage++) {
                    CreateFinaleSupply(boss,"Specialists Stage "+stage,new Wave {Name="Specialists Stage "+stage,R=1,D=1,E=1,A=1,Wizard=stage==1?1:0,
                        Trail=stage==1?1:0,Dragon=stage==1?1:0,Snake=stage==1?1:0,Ninja=stage<3?1:0,Delay=5,
                        Ranges=new[]{Range(0,36,28,8),Range(0,-30,24,8)}},stage,false,Vector3.zero);
                }
                var pair=CreateFinaleSupply(boss,"Linked Pair",new Wave{Name="Linked Pair",LinkedPairs=1,Delay=6,Ranges=new[]{Range(0,28,28,8)}},1,false,Vector3.zero);
                pair.GetComponent<FinaleSupplyAuthoring>().pairOnly=true;
                AuthorFinalePhaseSupply(boss);
                foreach(var p in new[]{new Vector2(-42,0),new Vector2(42,0),new Vector2(0,43),new Vector2(12,-43)})
                    CampaignRock(arena,p,new Vector2Int(5,5));
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                FinishFinaleScene();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Authored level 81, four anchors, Wraith and bounded finale supply.");
        }

        public static void FinishFinaleScene()
        {
                var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_81.unity",OpenSceneMode.Single);
                var settings=AssetDatabase.LoadAssetAtPath<FinaleSettings>(FinaleData+"FinaleSettings.asset");
                var presentation=new GameObject("Finale presentation").AddComponent<FinalePresentation>();
                presentation.gameObject.AddComponent<CrowdPunch.Mono.UI.FinaleBossHealthBar>();
                presentation.settings=settings;
                BossAppearanceBuilder.ConfigureFinale(presentation);
                var hint=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<CrowdPunch.Mono.Levels.GauntletLevel>());
                hint.FindProperty("openingHint").stringValue="Overload any cyan anchor: shoot through the cover, blast the shell then break its core, dock the rail, or hit the narrow target. Dodge the landing, then launch the crowd into the grounded giant. Dark anchors reset after all four are used.";
                hint.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                Array.Resize(ref catalog.levels,81); catalog.levels[80]=new CampaignCatalog.Level {id="cp-081",title="Bring Down the Sky",scenePath=CampaignScenes+"Campaign_81.unity"};
                Array.Resize(ref catalog.chapterNames,9); catalog.chapterNames[8]="Bring Down the Sky";
                catalog.chapterStarts=new[]{0,10,20,30,40,50,60,70,80};
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
        }

        private static T FinaleAsset<T>(string name) where T:ScriptableObject
        {
            string path=FinaleData+name+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset!=null) return asset;
            asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); return asset;
        }

        [MenuItem("Crowd Punch/Campaign/Apply Finale Phase Supply")]
        public static void ApplyFinalePhaseSupply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try {
                var scene=EditorSceneManager.OpenScene(CampaignSub(81),OpenSceneMode.Single);
                var boss=UnityEngine.Object.FindFirstObjectByType<FinaleBossAuthoring>();
                if(boss==null) throw new InvalidOperationException("Finale boss is missing.");
                AuthorFinalePhaseSupply(boss);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void AuthorFinalePhaseSupply(FinaleBossAuthoring boss)
        {
            // FINALE-006: incremental fixed slots preserve the former 66-body maximum.
            var banks=UnityEngine.Object.FindObjectsByType<FinaleSupplyAuthoring>(FindObjectsSortMode.None)
                .Where(s=>s.localAmmunition && s.minimumStage==1).ToArray();
            if(banks.Length!=4) throw new InvalidOperationException("Four initial ammunition banks are required.");
            foreach(var bank in banks) {
                var wave=bank.GetComponent<EnemyWaveSequenceAuthoring>().Waves[0];
                var data=new SerializedObject(wave);
                data.FindProperty("totalEnemyCount").intValue=5;
                var entries=data.FindProperty("enemies");
                for(int i=0;i<wave.Enemies.Count;i++) {
                    int count=wave.Enemies[i].Settings.Archetype==CrowdPunch.Configuration.EnemyArchetype.Baseline?4:1;
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("MinimumCount").intValue=count;
                }
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(wave);
                string name=(bank.bank.x<0?"West":"East")+(bank.bank.z<0?" South":" North");
                for(int stage=2;stage<=3;stage++) {
                    if(UnityEngine.Object.FindObjectsByType<FinaleSupplyAuthoring>(FindObjectsSortMode.None)
                        .Any(s=>s.localAmmunition && s.minimumStage==stage && s.bank==bank.bank)) continue;
                    CreateFinaleSupply(boss,name+" Stage "+stage,new Wave {Name=name+" Stage "+stage,
                        B=2,X=stage==3?1:0,Delay=2,Ranges=wave.SpawnRectangles.ToArray()},stage,true,bank.bank);
                }
            }
        }
        private static EnemyWaveSequenceAuthoring CreateFinaleSupply(FinaleBossAuthoring boss,string name,Wave design,int stage,bool ammunition,Vector3 bank)
        {
            var a=new GameObject("Finale supply - "+name).AddComponent<EnemyWaveSequenceAuthoring>();
            var wave=WaveAsset("CP81_"+name.Replace(' ','_'),design,LoadCampaignProfiles(),FinaleData);
            var data=new SerializedObject(a); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            ConfigureFinaleSupply(a,boss,stage,ammunition,bank); return a;
        }
        private static void ConfigureFinaleSupply(EnemyWaveSequenceAuthoring a,FinaleBossAuthoring boss,int stage,bool ammunition,Vector3 bank)
        {
            a.finaleBoss=boss; a.barricade=null;
            var supply=a.gameObject.AddComponent<FinaleSupplyAuthoring>(); supply.minimumStage=stage; supply.localAmmunition=ammunition; supply.bank=bank;
            var d=new SerializedObject(a); d.FindProperty("minimumPlayerDistance").floatValue=4; d.FindProperty("placementAttemptsPerEnemy").intValue=48; d.ApplyModifiedPropertiesWithoutUndo();
            var wave=new SerializedObject(a.Waves[0]); wave.FindProperty("replenishWhileBossLives").boolValue=true;
            wave.FindProperty("bossReplenishDelay").floatValue=ammunition?3:7;
            wave.FindProperty("armoredAmmunitionProfile").objectReferenceValue=null; wave.FindProperty("wizardAmmunitionProfile").objectReferenceValue=null;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
