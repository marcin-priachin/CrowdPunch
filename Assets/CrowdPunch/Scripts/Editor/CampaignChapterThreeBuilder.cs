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
        private static readonly string[] ChapterThreeHints =
        {
            "Hit shields with launched bodies or explosions. Break both shields before punching the Armored enemy.",
            "Break the narrow gate with launched bodies or explosions, then pass through to the exit.",
            "Elites launch nearby enemies at you. Disrupt their setup and defeat them to stop their crowd returning.",
            "Protect the turquoise zone. Stop three waves; one active enemy entering the zone ends the attempt.",
            "Red ground hurts you and unprotected enemies. Launch bodies across it and take the safe route around.",
            "Hit the block from behind to reach the blue socket. Keep clear of the hot outer pocket.",
            "Amber ground warns before turning red. Use the other lane while the patch is active.",
            "Reinforcements arrive from changing sides. Break armor with bodies, stop the Elite to halt replacements, and use the safe lanes around the hot crossings.",
            "Watch the moving opening and send bodies through to the target.",
            "Launch bodies into the Blob during its pauses and wind-up. Rolling protects it; punch scattered bodies to reclaim them."
        };

        private static Level[] ChapterThreeDesigns() => new[]
        {
            new Level { Name="Peel the Armor", Outline=Clipped(32,34,3), Spacing=new Vector2(26,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave { Name="First Shields",B=8,A=1,Batch=4,Interval=5,Ranges=new[]{Range(-5,6,6,8),Range(5,6,6,8)} },
                new Wave { Name="Paired Shields",B=12,A=2,Batch=4,Interval=5,Ranges=new[]{Range(-7,6,6,10),Range(7,6,6,10)} } } },
            new Level { Name="Heavy Traffic", Outline=new[]{new Vector2(-13,-20),new Vector2(15,-20),new Vector2(15,15),new Vector2(10,20),new Vector2(-10,20),new Vector2(-15,12)},
                Spacing=new Vector2(24,34),Entry=new Vector2(0,-15),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Gate Traffic",12,new[]{Range(-11,-3,4,18),Range(11,-3,4,18)},x:2) } },
            new Level { Name="Borrowed Fist",Outline=Clipped(36,36,4),Spacing=new Vector2(30,30),Entry=new Vector2(0,-13),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Borrowed Fist",10,new[]{Range(0,8,24,8)},e:1,batch:4,interval:5),
                W("Supported Fist",14,new[]{Range(-7,7,9,10),Range(7,7,9,10)},r:2,e:1,batch:4,interval:5) } },
            new Level { Name="Hold the Line",Outline=Rectangle(32,96),Spacing=new Vector2(30,94),Entry=new Vector2(0,-36),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("First Advance",12,new[]{Range(0,41,24,8)},batch:4,interval:4),
                W("Second Advance",16,new[]{Range(0,41,24,8)},delay:5,batch:4,interval:4),
                W("Final Advance",20,new[]{Range(0,41,24,8)},delay:5,batch:4,interval:4) } },
            new Level { Name="Hot Ground",Outline=Clipped(34,34,5),Spacing=new Vector2(28,28),Entry=new Vector2(0,-12),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Hot Ground",10,new[]{Range(0,9,22,8)},next:8,batch:4,interval:5),
                W("Hot Crossfire",14,new[]{Range(10,-2,5,16)},r:2,delay:0,batch:4,interval:5) } },
            new Level { Name="Safe Side",Outline=Clipped(34,40,3),Spacing=new Vector2(28,34),Entry=new Vector2(-3,-14),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Safe Delivery",10,new[]{Range(-8,6,4,12),Range(8,-8,5,8)},x:2,batch:4,interval:5) } },
            new Level { Name="Wait for the Warning",Outline=Clipped(36,38,4),Spacing=new Vector2(30,32),Entry=new Vector2(4,-14),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("First Warning",10,new[]{Range(0,11,24,6)},d:1,next:8,batch:4,interval:5),
                W("Cross the Warning",14,new[]{Range(-12,10,4,4),Range(12,-3,4,12)},r:2,d:2,delay:0,batch:4,interval:5) } },
            new Level { Name="Crowd Channels",Outline=Rectangle(100,100),Spacing=new Vector2(96,96),Entry=new Vector2(0,-16),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave { Name="North Shields",B=26,R=10,A=6,Mushroom=6,Batch=8,Interval=6,NextAfter=6,
                    Ranges=new[]{Range(0,32,50,12)} },
                new Wave { Name="West Command",B=30,R=15,A=4,Mushroom=6,E=1,Batch=8,Interval=6,Delay=0,
                    Ranges=new[]{Range(-30,0,16,36)} },
                W("East Charge",40,new[]{Range(30,0,16,36)},d:12,x:14,alienSpike:6,next:6,batch:8,interval:6),
                new Wave { Name="Split Command",B=48,R=12,D=10,A=6,Mushroom=10,E=2,Batch=8,Interval=6,Delay=0,
                    Ranges=new[]{Range(-30,0,16,44),Range(30,0,16,44)} } } },
            new Level { Name="Window of Safety",Outline=Clipped(38,36,5),Spacing=new Vector2(32,30),Entry=new Vector2(0,-12),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Window Support",10,new[]{Range(0,-12,18,4),Range(13,5,4,12)},batch:4,interval:5) } }
        };

        private static Wave ChapterThreeRollingCrowd()
            => W("Rolling Crowd",6,new[]{Range(-13,0,6,28),Range(13,0,6,28),Range(0,-9,16,6)},batch:4,interval:5);

        private static Wave ChapterThreeGateCrowd(bool final, EnemyWaveSettings.SpawnRectangle[] ranges)
            => final
                ? new Wave { Name="Final Gate Traffic", B=7,R=1,X=2,D=1,A=1,Mushroom=1,AlienSpike=1,
                    Batch=4,Interval=5,Ranges=ranges }
                : W("Gate Traffic",7,ranges,x:1,batch:4,interval:5);

        private static void WriteChapterThreeGateCrowd(EnemyWaveSettings existing, bool final, EnemySpawnSettings[] profiles)
        {
            WaveAsset(existing.name, ChapterThreeGateCrowd(final, existing.SpawnRectangles.ToArray()), profiles, CampaignData+"Waves/");
            // The room's bounded crowd already replenishes launchable gate and armor ammunition.
            var data = new SerializedObject(existing);
            data.FindProperty("armoredAmmunitionProfile").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Crowd Punch/Campaign/Apply Chapter Three Encounter Revision")]
        public static void ApplyChapterThreeEncounterRevision()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if (catalog == null || catalog.Count < 30 || Enumerable.Range(20, 10).Any(i => !catalog.Get(i).Available))
                throw new InvalidOperationException("An installed Chapter Three is required.");

            var designs = ChapterThreeDesigns().Select(level => level.Waves).Append(new[] { ChapterThreeRollingCrowd() }).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Verify saved SubScene references and preserve their established spawn banks.
                for (int level=21; level<=30; level++)
                {
                    if (level == 24) continue; // PROTECT-004 introduction uses its existing recipe.
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var crowds = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>()).ToArray();
                    if (level == 22)
                    {
                        var rooms = crowds.OrderBy(c => c.barricade.transform.position.z).ToArray();
                        if (rooms.Length != 2 || rooms.Any(c => c.Waves.Count != 1)
                            || !IsChapterThreeWave(rooms[0].Waves[0], "CP22_01_Gate_Traffic")
                            || !IsChapterThreeWave(rooms[1].Waves[0], "CP22_Room02_Ammunition"))
                            throw new InvalidOperationException("Unexpected barricade room wiring in level 22.");
                        continue;
                    }
                    var recipes = designs[level-21];
                    if (crowds.Length != 1 || crowds[0].Waves.Count != recipes.Length)
                        throw new InvalidOperationException($"Unexpected wave wiring in level {level}.");
                    for (int phase=0; phase<recipes.Length; phase++)
                    {
                        var recipe = recipes[phase];
                        var existing = crowds[0].Waves[phase];
                        if (!IsChapterThreeWave(existing, ChapterTwoWaveName(level, phase, recipe)))
                            throw new InvalidOperationException($"Unexpected wave asset in level {level}.");
                        recipe.Ranges = existing.SpawnRectangles.ToArray();
                    }
                }
                var profiles = LoadCampaignProfiles();
                foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 12, 13 })
                    if (profiles[i].EnemyPrefab == null) throw new InvalidOperationException("Missing prefab for " + profiles[i].name);
                for (int level=21; level<=30; level++)
                {
                    if (level == 24) continue;
                    if (level == 22)
                    {
                        WriteChapterThreeGateCrowd(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(CampaignData+"Waves/CP22_01_Gate_Traffic.asset"), false, profiles);
                        WriteChapterThreeGateCrowd(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(CampaignData+"Waves/CP22_Room02_Ammunition.asset"), true, profiles);
                        continue;
                    }
                    var recipes = designs[level-21];
                    for (int phase=0; phase<recipes.Length; phase++)
                        WaveAsset(ChapterTwoWaveName(level, phase, recipes[phase]), recipes[phase], profiles, CampaignData+"Waves/");
                }
                AssetDatabase.SaveAssets();
                SetCampaignHint(28, ChapterThreeHints[7]);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("LOOP-011: Chapter Three revised in place. Mixed crowds at 22/28 and timed reinforcements at 25/27/28.");
        }

        private static bool IsChapterThreeWave(EnemyWaveSettings wave, string name)
            => wave != null && AssetDatabase.GetAssetPath(wave) == CampaignData + "Waves/" + name + ".asset";

        [MenuItem("Crowd Punch/Campaign/Create Chapter Three")]
        public static void BuildChapterThree()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if(catalog==null || catalog.Count!=80 || !catalog.Get(19).Available) throw new InvalidOperationException("Chapters One and Two are required.");
            for(int n=21;n<=30;n++)
                if(catalog.Get(n-1).Available || File.Exists($"{CampaignScenes}Campaign_{n:00}.unity"))
                    throw new InvalidOperationException("Chapter Three content already exists. Edit its saved assets; this recipe never overwrites encounters.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs=ChapterThreeDesigns();
                for(int i=0;i<designs.Length;i++)
                {
                    BuildLevel(i+20,designs[i],LoadCampaignProfiles(),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorChapterThreeInterior(i+21);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+21);
                    SetCampaignHint(i+21,ChapterThreeHints[i]);
                }
                CopyCampaignRolling();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=21;n<=30;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Three authored: levels 21-30, defense, armor/Elites, ground hazards, 264-enemy crowd arena and preserved Rolling Blob arena.");
        }

        private static void AuthorChapterThreeInterior(int number)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true;
            navigation.participationAnchor=number==24?new Vector2(0,-32):number==28?new Vector2(0,-16):new Vector2(0,-10);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data=new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue=number==28?10:3;
            data.FindProperty("placementAttemptsPerEnemy").intValue=48; data.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 21: CampaignRock(arena,new Vector2(0,13),new Vector2Int(8,2)); break;
                case 22:
                    CampaignRock(arena,new Vector2(-7,-3),new Vector2Int(3,5));
                    CampaignRock(arena,new Vector2(7,2),new Vector2Int(3,5)); CampaignBarricade(number,encounter); break;
                case 24:
                    var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
                    defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings","L24_Defense");
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
                case 25: CampaignGround(encounter,number,"Hot island",new Vector2(-5,0)); break;
                case 26:
                    CopyCampaignObjective(sub,arena,encounter,number,true,new Vector3(5,0,0));
                    CampaignGround(encounter,number,"Outer hot pocket",new Vector2(-11,-8)); break;
                case 27:
                    CampaignRock(arena,new Vector2(0,0),new Vector2Int(3,8));
                    CampaignGround(encounter,number,"West warning crossing",new Vector2(-9,0),true); break;
                case 28:
                    CampaignRock(arena,new Vector2(-12,4),new Vector2Int(6,30));
                    CampaignRock(arena,new Vector2(12,-6),new Vector2Int(6,30));
                    CampaignGround(encounter,number,"Outer hot pocket",new Vector2(-35,-30));
                    CampaignGround(encounter,number,"West warning crossing",new Vector2(-19,-22),true);
                    CampaignGround(encounter,number,"North warning crossing",new Vector2(1,14),true,4); break;
                case 29:
                    CampaignRock(arena,new Vector2(-14,3),new Vector2Int(3,6));
                    CampaignRock(arena,new Vector2(13,-5),new Vector2Int(3,5)); CampaignCover(arena,encounter,number,100,20); break;
            }
            int trackIndex = Array.IndexOf(CampaignTrackLevels, number);
            if (trackIndex > 0) AuthorProgressiveTrackRoute(sub, number, trackIndex + 1);
            int gateIndex = Array.IndexOf(CampaignBarricadeLevels, number);
            if (gateIndex > 0) AuthorBarricadeRooms(sub, number, gateIndex);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static GroundHazardAuthoring CampaignGround(EnemyWaveSequenceAuthoring encounter,int number,string name,Vector2 position,bool periodic=false,float offset=0)
        {
            // GROUND-001/006: visible from wave one, safe spawn banks, independent resettable phases.
            var policy=encounter.GetComponent<GroundHazardPolicyAuthoring>();
            if(policy==null) policy=encounter.gameObject.AddComponent<GroundHazardPolicyAuthoring>();
            policy.settings=CampaignSettings<GroundHazardSettings>("GroundHazardSettings",$"L{number:00}_Ground");
            var patch=new GameObject(name).AddComponent<GroundHazardAuthoring>();
            patch.sequence=encounter; patch.transform.position=new Vector3(position.x,-.965f,position.y);
            patch.shape=periodic?GroundHazardShape.Rectangle:GroundHazardShape.Circle;
            patch.operation=periodic?GroundHazardOperation.Periodic:GroundHazardOperation.AlwaysActive;
            patch.radius=2.5f; patch.width=3; patch.depth=8; patch.firstWave=0;
            patch.inactiveDuration=4; patch.warningDuration=1.5f; patch.activeDuration=2.5f; patch.cycleOffset=offset;
            patch.damage=12; patch.damageInterval=.75f;
            return patch;
        }

        private static void CopyCampaignRolling()
        {
            // ROLL-006/LOOP-002: preserve every boss arena transform and collider.
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(30))); AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(Scenes+"Gauntlet_21/Gauntlet_21 Sub Scene.unity",CampaignSub(30))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_21.unity",CampaignScenes+"Campaign_30.unity"))
                throw new InvalidOperationException("Could not copy the authored Rolling Blob arena.");
            var sub=EditorSceneManager.OpenScene(CampaignSub(30),OpenSceneMode.Single);
            var boss=UnityEngine.Object.FindFirstObjectByType<RollingBossAuthoring>();
            boss.settings=CampaignSettings<RollingBossSettings>("RollingBossSettings","L30_Rolling");
            var wave=WaveAsset("CP30_01_Rolling_Crowd",ChapterThreeRollingCrowd(),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
            wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>(); encounter.rollingBoss=boss;
            var data=new SerializedObject(encounter); data.FindProperty("waves").arraySize=1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_30.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(30));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); SetCampaignHint(30,ChapterThreeHints[9]);
        }
    }
}
