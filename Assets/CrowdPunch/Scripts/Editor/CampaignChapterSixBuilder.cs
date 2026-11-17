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
            "Use rocks to block the flame. Launch a Dragon to sweep its fire through the crowd.",
            "The cover reverses every five seconds. Launch bodies through the opening and watch the Mushroom Shooter's firing lane.",
            "Launch bodies or blast the block along the turning arrows into the blue socket. Change your shooting side at each corner; opposite hits push it back.",
            "Turn Dragon flame and Sporella's shots against the crowd. Watch for the next bank as Spike and Exploder groups arrive.",
            "Line up four hits on the narrow gate, then move through it to the exit.",
            "Intercept timed groups before they reach the turquoise zone. Cross between approaches when the warning strips are clear.",
            "Watch for new firing angles from each bank. Redirect flames and shots, then clear the Elite group to open the next approach.",
            "Choose where the explosives land as distant shooters arrive.",
            "The Chicken leads its shots toward your movement. Change direction, return shots, and watch the edge warnings."
        };

        [MenuItem("Crowd Punch/Campaign/Apply Chapter Six Encounter Revision")]
        public static void ApplyChapterSixEncounterRevision()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before revising encounters.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before revising encounters.");

            var designs = ChapterSixDesigns();
            int[] levels = { 53, 54, 55, 57, 58, 59 };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // LOOP-011: preserve the authored spawn banks and each objective's existing ownership.
                foreach (int level in levels)
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>()).Single();
                    var waves = designs[level - 51].Waves;
                    if (encounter.Waves.Count != waves.Length)
                        throw new InvalidOperationException($"Unexpected wave count in level {level}.");
                    for (int phase = 0; phase < waves.Length; phase++)
                    {
                        var existing = encounter.Waves[phase];
                        string name = ChapterTwoWaveName(level, phase, waves[phase]);
                        if (existing == null || AssetDatabase.GetAssetPath(existing) != CampaignData + "Waves/" + name + ".asset")
                            throw new InvalidOperationException($"Missing or unexpected wave asset {name}.");
                        waves[phase].Ranges = existing.SpawnRectangles.ToArray();
                    }
                }
                var profiles = LoadCampaignProfiles();
                foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 14 })
                    if (profiles[i].EnemyPrefab == null)
                        throw new InvalidOperationException($"Missing prefab for {profiles[i].name}.");

                foreach (int level in levels)
                {
                    var waves = designs[level - 51].Waves;
                    for (int phase = 0; phase < waves.Length; phase++)
                    {
                        var recipe = waves[phase];
                        var asset = WaveAsset(ChapterTwoWaveName(level, phase, recipe), recipe, profiles, CampaignData + "Waves/");
                        if (level == 57) ConfigureFiniteDefenseWave(asset);
                        else if (level >= 55 && asset.ReplenishWhileBossLives)
                        {
                            var data = new SerializedObject(asset);
                            data.FindProperty("replenishWhileBossLives").boolValue = false;
                            data.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                foreach (int level in new[] { 54, 55, 57, 58 })
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    AuthorChapterSixPressureHazards(scene, level, encounter);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                foreach (int level in new[] { 53, 55, 57, 58, 59 }) SetCampaignHint(level, ChapterSixHints[level - 51]);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("LOOP-011: Chapter Six revised in place. Timed defense at 57; 328 enemies at Moving Batteries.");
        }

        private static void AuthorChapterSixPressureHazards(Scene scene, int level, EnemyWaveSequenceAuthoring encounter)
        {
            void Strip(string name, float x, float z, float depth, float offset)
            {
                var patch = scene.GetRootGameObjects().SingleOrDefault(g => g.name == name)?.GetComponent<GroundHazardAuthoring>();
                if (patch == null) patch = CampaignGround(encounter, level, name, new Vector2(x, z), true, offset);
                patch.transform.position = new Vector3(x, -.965f, z);
                patch.depth = depth;
                patch.cycleOffset = offset;
            }

            switch (level)
            {
                case 54:
                    // Put the warnings in the outgoing shot lanes with room for the carrier to pass.
                    Strip("South warning pocket", -5, -16, 4, 0);
                    Strip("West pressure crossing", -10.5f, 0, 4, 2);
                    Strip("East pressure crossing", 15, 4, 4, 4);
                    break;
                case 55:
                    Strip("Southwest warning pocket", -11, -16, 6, 0);
                    Strip("Middle warning pocket", 0, -5, 6, 8f / 3f);
                    Strip("Southeast warning pocket", 11, -16, 6, 16f / 3f);
                    break;
                case 57:
                    Strip("West approach warning", -9, 6, 6, 0);
                    Strip("East approach warning", 8, -20, 6, 8f / 3f);
                    Strip("South approach warning", -8, -34, 6, 16f / 3f);
                    break;
                case 58:
                    Strip("West outer crossing", -40, -30, 8, 0);
                    Strip("North warning strip", 0, 12, 8, 1.6f);
                    Strip("West warning strip", -8, -12, 8, 3.2f);
                    Strip("East outer crossing", 40, 30, 8, 4.8f);
                    Strip("Southeast warning strip", 12, -26, 8, 6.4f);
                    break;
                default: return;
            }
            ChapterSixCanyonHazards.Apply(scene);
        }

        private static Level[] ChapterSixDesigns() => new[]
        {
            new Level { Name="Fresh Fuse", Outline=OvalCourt(34,36), Spacing=new Vector2(28,30), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Fresh Fuse",10,new[]{Range(-11,0,4,18),Range(11,0,4,18)},x:2) } },
            new Level { Name="Crossing Paths", Outline=new[]{new Vector2(-15,-20),new Vector2(15,-20),new Vector2(19,-12),new Vector2(15,0),new Vector2(19,12),new Vector2(15,20),new Vector2(-15,20),new Vector2(-19,12),new Vector2(-15,0),new Vector2(-19,-12)},
                Spacing=new Vector2(32,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="West Crossing",B=6,Dragon=1,Ranges=new[]{Range(-13,0,3,20)}},
                new Wave {Name="East Crossing",B=12,Dragon=2,Ranges=new[]{Range(13,0,3,20)}} } },
            new Level { Name="Reverse the Window", Outline=new[]{new Vector2(-14,-20),new Vector2(15,-20),new Vector2(20,-13),new Vector2(20,14),new Vector2(12,20),new Vector2(-16,20),new Vector2(-20,11),new Vector2(-20,-14)},
                Spacing=new Vector2(34,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Reversing Window",13,new[]{Range(-13,0,4,22),Range(13,0,4,22)},x:2,mushroom:1,batch:4,interval:1.5f) } },
            new Level { Name="Push Under Pressure", Outline=Clipped(38,44,5), Spacing=new Vector2(32,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Pressure Ammunition",B=13,X=2,Sporella=1,Batch=4,Interval=1.5f,Ranges=new[]{Range(-14,0,3,28),Range(14,0,3,28)}} } },
            new Level { Name="Disrupt the Casters", Outline=Clipped(40,40,5), Spacing=new Vector2(34,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Casters",B=9,Dragon=1,Sporella=2,R=3,D=3,LinkedPairs=1,Batch=6,Interval=5,NextAfter=6,Ranges=new[]{Range(0,15,24,4)}},
                new Wave {Name="East Casters",B=10,AlienSpike=3,X=6,D=3,Batch=6,Interval=5,Delay=0,NextAfter=6,Ranges=new[]{Range(14,0,4,20)}},
                new Wave {Name="Armored Casters",B=11,A=2,Dragon=1,LinkedPairs=1,Mushroom=2,Wizard=2,R=4,Batch=6,Interval=5,Delay=0,Ranges=new[]{Range(-14,0,4,20)}} } },
            new Level { Name="Clean Break", Outline=new[]{new Vector2(-16,-21),new Vector2(16,-21),new Vector2(16,-6),new Vector2(12,21),new Vector2(-12,21),new Vector2(-16,-6)},
                Spacing=new Vector2(26,36), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Clean Ammunition",12,new[]{Range(-10,-2,3,20),Range(10,-2,3,20)},x:2) } },
            new Level { Name="Long Watch", Outline=Rectangle(32,96), Spacing=new Vector2(30,94), Entry=new Vector2(0,-36), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Distant Watch",10,new[]{Range(0,41,24,8)},r:4,d:2,next:6,batch:4,interval:4),
                W("Charging Watch",12,new[]{Range(0,41,24,8)},d:5,x:3,delay:0,next:6,batch:4,interval:4),
                new Wave {Name="Explosive Watch",B=12,R=4,X=4,Trail=2,Batch=4,Interval=4,Delay=0,Ranges=new[]{Range(0,41,24,8)}} } },
            new Level { Name="Moving Batteries", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Battery",B=21,Dragon=2,Sporella=6,R=9,D=6,LinkedPairs=2,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(0,32,52,16)}},
                new Wave {Name="West Reaction",B=25,AlienSpike=8,X=14,D=3,LinkedPairs=3,Batch=8,Interval=6,Delay=0,NextAfter=6,Ranges=new[]{Range(-31,0,16,44)}},
                new Wave {Name="East Battery",B=29,A=8,Mushroom=6,X=8,R=5,E=2,LinkedPairs=3,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(31,0,16,44)}},
                new Wave {Name="Split Casters",B=32,Dragon=3,Sporella=5,D=11,X=7,R=8,LinkedPairs=3,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(-30,0,16,40),Range(30,0,16,40)}},
                new Wave {Name="Moving Batteries",B=39,Dragon=2,AlienSpike=4,Mushroom=8,X=12,R=10,D=5,Wizard=2,LinkedPairs=3,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(0,30,64,24)}} } },
            new Level { Name="Open Air", Outline=new[]{new Vector2(-15,-16),new Vector2(15,-16),new Vector2(18,-12),new Vector2(16,-3),new Vector2(18,6),new Vector2(18,12),new Vector2(15,16),new Vector2(-15,16),new Vector2(-18,12),new Vector2(-18,6),new Vector2(-16,-3),new Vector2(-18,-12)},
                Spacing=new Vector2(30,26), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Open Fuses",8,new[]{Range(0,7,24,8)},x:2,next:6,batch:4,interval:5),
                W("Open Shooters",8,new[]{Range(0,7,24,8)},r:2,delay:0,batch:4,interval:5) } }
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
            Debug.Log("Chapter Six authored: levels 51-60, reversing cover, seven-leg rail, 328-enemy arena and movement-leading Chicken rematch.");
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
                    CampaignRock(arena,new Vector2(7,-13),new Vector2Int(5,3)); break;
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
                    foreach(var wave in encounter.Waves)
                        ConfigureFiniteDefenseWave(wave);
                    Box("Protected Zone 8 x 4",encounter.transform,new Vector3(0,-.97f,-46),new Vector3(8,.035f,4),MaterialAsset("ProtectedPointZone",new Color(.15f,.75f,.68f)),false); break;
                case 58:
                    CampaignRock(arena,new Vector2(-13,7),new Vector2Int(6,10));
                    CampaignRock(arena,new Vector2(13,-7),new Vector2Int(6,10));
                    CampaignRock(arena,new Vector2(0,-30),new Vector2Int(10,6)); break;
                case 59:
                    CampaignRock(arena,new Vector2(-15,-8),new Vector2Int(2,3));
                    CampaignRock(arena,new Vector2(15,-8),new Vector2Int(2,3));
                    CampaignRock(arena,new Vector2(0,14),new Vector2Int(6,2)); break;
            }
            int trackIndex = Array.IndexOf(CampaignTrackLevels, number);
            if (trackIndex > 0) AuthorProgressiveTrackRoute(sub, number, trackIndex + 1);
            int gateIndex = Array.IndexOf(CampaignBarricadeLevels, number);
            if (gateIndex > 0) AuthorBarricadeRooms(sub, number, gateIndex);
            if (number != 54) AuthorChapterSixPressureHazards(sub, number, encounter);
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
