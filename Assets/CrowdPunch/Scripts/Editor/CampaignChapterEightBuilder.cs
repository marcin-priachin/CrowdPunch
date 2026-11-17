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
            "Watch the Ninja prepare and appear behind you. Move aside or interrupt, then strike during recovery.",
            "The opening reverses every six seconds. Step out of the Mushroom Shooter's lane and line up bodies through the cover.",
            "Interrupt Ninja ambushes and redirect Sporella's shots. Watch the next bank for armor and Alien Spikes.",
            "Work around the two bends, land four hits on the narrow gate, then reach the exit.",
            "Intercept timed groups before they reach the turquoise zone. Launch bodies to break armor and use the gaps between the warnings.",
            "Break the shell with three explosions, then hit the exposed core. More Exploders arrive when both are gone.",
            "Hold the final approach as new groups arrive. Interrupt Wizards and stop every enemy before it reaches the zone.",
            "Change angles as each bank opens. Clear the Elite group midway, then use the gaps in the warnings to reach Snake tips.",
            "Choose clean launches through the explosives as distant shooters arrive.",
            "Topple each pillar with a launched enemy while the Dino is in its path. Keep the outer ground warnings clear."
        };

        [MenuItem("Crowd Punch/Campaign/Apply Chapter Eight Encounter Revision")]
        public static void ApplyChapterEightEncounterRevision()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before revising encounters.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before revising encounters.");

            int[] levels = { 72, 73, 75, 77, 78, 79 };
            var designs = ChapterEightDesigns();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // LOOP-011: preserve wave GUIDs, spawn banks and objective ownership.
                foreach (int level in levels)
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>()).Single();
                    var waves = designs[level - 71].Waves;
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
                foreach (var profile in profiles)
                    if (profile.EnemyPrefab == null)
                        throw new InvalidOperationException($"Missing prefab for {profile.name}.");

                foreach (int level in levels)
                {
                    var waves = designs[level - 71].Waves;
                    var armorAmmunition = default(EnemySpawnSettings);
                    for (int phase = 0; phase < waves.Length; phase++)
                    {
                        var recipe = waves[phase];
                        var asset = WaveAsset(ChapterTwoWaveName(level, phase, recipe), recipe, profiles, CampaignData + "Waves/");
                        if (level is 75 or 77)
                        {
                            armorAmmunition = asset.ArmoredAmmunitionProfile ?? armorAmmunition;
                            ConfigureChapterEightDefenseWave(asset, armorAmmunition);
                        }
                        else if (level == 79 && asset.ReplenishWhileBossLives)
                        {
                            var finite = new SerializedObject(asset);
                            finite.FindProperty("replenishWhileBossLives").boolValue = false;
                            finite.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                foreach (int level in new[] { 73, 75, 77, 78 })
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    AuthorChapterEightPressureHazards(scene, level, encounter);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                foreach (int level in levels) SetCampaignHint(level, ChapterEightHints[level - 71]);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("LOOP-011: Chapter Eight revised in place. Timed defenses at 75/77 and 336 enemies at Claim the Ground.");
        }

        private static void AuthorChapterEightPressureHazards(Scene scene, int level, EnemyWaveSequenceAuthoring encounter)
        {
            void Strip(string name, float x, float z, float offset, float depth=6, string previousName=null)
            {
                var patch = scene.GetRootGameObjects().SingleOrDefault(g => g.name == name || g.name == previousName)
                    ?.GetComponent<GroundHazardAuthoring>();
                if (patch == null) patch = CampaignGround(encounter, level, name, new Vector2(x, z), true, offset);
                patch.gameObject.name = name;
                patch.transform.position = new Vector3(x, -.965f, z);
                patch.depth = depth;
                patch.cycleOffset = offset;
            }

            switch (level)
            {
                case 73:
                    Strip("West optional crossing", -9, -6, 0, 4);
                    Strip("Southwest approach warning", -10, -16, 2, 4);
                    Strip("East optional crossing", 10, -15, 4, 4);
                    Strip("East approach warning", 11.8f, 2.4f, 6, 4);
                    break;
                case 75:
                    Strip("West switchback warning", -8, 12, 0);
                    Strip("East switchback warning", 8, 1, 2);
                    Strip("Southeast approach warning", 8, -24, 4);
                    Strip("Southwest approach warning", -8, -33, 6);
                    break;
                case 77:
                    Strip("Northeast approach warning", 8, 23, 0);
                    Strip("West crossing warning", -8, -1, 2);
                    Strip("Southeast approach warning", 8, -29, 4);
                    Strip("Southwest approach warning", -8, -36, 6);
                    break;
                case 78:
                    Strip("Northwest approach warning", -8, 16, 0);
                    Strip("Northeast approach warning", 43, 32, 8f / 7, previousName:"East outer lane");
                    Strip("East crossing warning", 19, 12, 16f / 7);
                    Strip("Southeast approach warning", 20, -26, 24f / 7);
                    Strip("Southwest approach warning", -38, -34, 32f / 7, previousName:"West outer lane");
                    Strip("Middle crossing warning", -1, -7, 40f / 7);
                    Strip("West crossing warning", -20, -13, 48f / 7);
                    break;
                default: return;
            }
            ChapterEightCitadelHazards.Apply(scene);
        }

        private static Level[] ChapterEightDesigns() => new[]
        {
            new Level{Name="Find Your Feet",Outline=new[]{new Vector2(-13,-18),new Vector2(13,-18),new Vector2(18,-10),new Vector2(17,11),new Vector2(12,18),new Vector2(-12,18),new Vector2(-17,11),new Vector2(-18,-10)},
                Spacing=new Vector2(30,30),Entry=new Vector2(0,-13),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave {Name="North Shooters",B=6,Ninja=1,Ranges=new[]{Range(-6,11,14,4)}},new Wave {Name="West Fuses",B=12,Ninja=2,Ranges=new[]{Range(-12,0,4,20)}}}},
            new Level{Name="Thread the Crowd",Outline=new[]{new Vector2(-16,-20),new Vector2(17,-20),new Vector2(21,-12),new Vector2(21,10),new Vector2(12,20),new Vector2(-15,20),new Vector2(-21,12),new Vector2(-21,-13)},
                Spacing=new Vector2(36,34),Entry=new Vector2(0,-15),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave{Name="Living Ammunition",B=13,Wizard=1,Trail=1,Mushroom=1,Batch=4,Interval=1.5f,Ranges=new[]{Range(-15,0,4,24),Range(15,0,4,24)}}}},
            new Level{Name="Committed Lines",Outline=new[]{new Vector2(-22,-14),new Vector2(-16,-20),new Vector2(14,-20),new Vector2(22,-10),new Vector2(20,16),new Vector2(14,20),new Vector2(-14,20),new Vector2(-20,10)},
                Spacing=new Vector2(36,34),Entry=new Vector2(0,-15),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave {Name="East Commitments",B=8,Ninja=2,Sporella=2,LinkedPairs=1,R=3,D=3,Batch=6,Interval=5,NextAfter=6,Ranges=new[]{Range(16,3,4,20)}},
                new Wave {Name="West Armor",B=9,A=2,AlienSpike=3,X=5,D=3,Batch=6,Interval=5,Delay=0,NextAfter=6,Ranges=new[]{Range(-15,-1,4,20)}},
                new Wave {Name="North Trails",B=10,A=2,Snake=1,Dragon=1,Ninja=1,LinkedPairs=1,Trail=4,R=3,Batch=6,Interval=5,Delay=0,Ranges=new[]{Range(0,14,20,4)}}}},
            new Level{Name="Open the Last Gate",Outline=new[]{new Vector2(-19,-23),new Vector2(19,-23),new Vector2(19,-8),new Vector2(16,0),new Vector2(19,9),new Vector2(15,23),new Vector2(-15,23),new Vector2(-19,9),new Vector2(-16,0),new Vector2(-19,-8)},
                Spacing=new Vector2(32,40),Entry=new Vector2(0,-18),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Last Gate Ammunition",16,new[]{Range(-14,2,3,16),Range(14,2,3,16)},x:2)}},
            new Level{Name="Guard the Approach",Outline=Rectangle(32,96),Spacing=new Vector2(30,94),Entry=new Vector2(0,-36),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Three Approaches",10,new[]{Range(0,41,24,8)},r:4,x:4,next:6,batch:4,interval:4),
                new Wave{Name="Armored Approach",B=12,A=2,D=6,Delay=0,Batch=4,Interval=4,NextAfter=6,Ranges=new[]{Range(0,41,24,8)}},
                new Wave{Name="Casting Approach",B=13,R=4,X=4,Trail=3,Delay=0,Batch=4,Interval=4,Ranges=new[]{Range(0,41,24,8)}}}},
            new Level{Name="A Clear Purpose",Outline=Clipped(36,38,5),Spacing=new Vector2(30,32),Entry=new Vector2(0,-14),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Clear Fuses",10,new[]{Range(-10,0,4,16),Range(10,0,4,16)},x:2)}},
            new Level{Name="Final Stand",Outline=Rectangle(32,96),Spacing=new Vector2(30,94),Entry=new Vector2(0,-36),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave{Name="Final Armor",B=10,A=2,X=4,D=4,Batch=4,Interval=4,NextAfter=6,Ranges=new[]{Range(0,41,24,8)}},
                W("Final Fuses",11,new[]{Range(0,41,24,8)},x:5,d:5,r:1,delay:0,next:6,batch:4,interval:4),
                new Wave{Name="Final Casters",B=12,R=6,D=4,Wizard=2,Delay=0,Batch=4,Interval=4,Ranges=new[]{Range(0,41,24,8)}}}},
            new Level{Name="Claim the Ground",Outline=Rectangle(100,100),Spacing=new Vector2(96,96),Entry=new Vector2(0,-16),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                new Wave {Name="North Claim",B=15,Ninja=2,Mushroom=6,R=7,D=6,LinkedPairs=2,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(0,33,58,14)}},
                new Wave {Name="West Reaction",B=18,Dragon=2,AlienSpike=6,X=13,D=5,LinkedPairs=2,Batch=8,Interval=6,Delay=0,NextAfter=6,Ranges=new[]{Range(-36,0,12,44)}},
                new Wave {Name="East Battery",B=22,A=8,E=2,R=9,X=9,LinkedPairs=3,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(36,0,12,44)}},
                new Wave {Name="Split North Casters",B=22,Snake=2,Sporella=5,Trail=6,Dragon=1,D=8,X=6,LinkedPairs=3,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(-20,33,20,16),Range(20,33,20,16)}},
                new Wave {Name="Split Side Charges",B=25,Ninja=3,Sporella=5,Mushroom=7,R=6,D=8,X=4,LinkedPairs=3,Batch=8,Interval=6,Delay=0,NextAfter=6,Ranges=new[]{Range(-36,0,12,40),Range(36,0,12,40)}},
                new Wave {Name="Last Claim",B=27,Snake=1,Dragon=2,AlienSpike=4,A=6,Wizard=2,R=8,X=10,D=6,LinkedPairs=3,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(-20,34,26,16),Range(20,34,26,16)}}}},
            new Level{Name="Room to Breathe",Outline=new[]{new Vector2(-14,-17),new Vector2(14,-17),new Vector2(19,-10),new Vector2(17,11),new Vector2(13,17),new Vector2(-13,17),new Vector2(-17,11),new Vector2(-19,-10)},
                Spacing=new Vector2(32,28),Entry=new Vector2(0,-12),Lanes=Array.Empty<Vector4>(),Waves=new[]{
                W("Room for Fuses",8,new[]{Range(-9,7,6,6),Range(9,7,6,6)},x:2,next:6,batch:4,interval:5),
                W("Clean Last Launches",8,new[]{Range(-9,7,6,6),Range(9,7,6,6)},r:2,delay:0,batch:4,interval:5)}}
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
            Debug.Log("Chapter Eight authored: levels 71-80, timed defenses, 336-enemy arena and Dino rematch.");
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
                    CampaignRock(arena,new Vector2(5,-5),new Vector2Int(3,8)); break;
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
                    CampaignRock(arena,new Vector2(18,1),new Vector2Int(8,6)); break;
                case 79: CampaignRock(arena,new Vector2(0,12),new Vector2Int(4,3)); break;
            }
            int gateIndex = Array.IndexOf(CampaignBarricadeLevels, number);
            if (gateIndex > 0) AuthorBarricadeRooms(sub, number, gateIndex);
            AuthorChapterEightPressureHazards(sub, number, encounter);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub); AssetDatabase.SaveAssets();
        }

        private static void ChapterEightDefense(int number,EnemyWaveSequenceAuthoring encounter)
        {
            var defense=encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
            defense.settings=CampaignSettings<ProtectedPointSettings>("ProtectedPointSettings",$"L{number}_Defense");
            defense.settings.breachThreshold=1; defense.settings.maximumPlayerAttackers=3; EditorUtility.SetDirty(defense.settings);
            var armorAmmunition = default(EnemySpawnSettings);
            foreach(var wave in encounter.Waves)
            {
                armorAmmunition = wave.ArmoredAmmunitionProfile ?? armorAmmunition;
                ConfigureChapterEightDefenseWave(wave, armorAmmunition);
            }
            Box("Protected Zone 8 x 4",encounter.transform,new Vector3(0,-.97f,-46),new Vector3(8,.035f,4),MaterialAsset("ProtectedPointZone",new Color(.15f,.75f,.68f)),false);
        }

        private static void ConfigureChapterEightDefenseWave(EnemyWaveSettings wave, EnemySpawnSettings armorAmmunition)
        {
            // PROTECT-001/004: the existing armor fallback also covers surviving armor from timed phases.
            var finite = new SerializedObject(wave);
            finite.FindProperty("replenishWhileBossLives").boolValue = false;
            finite.FindProperty("armoredAmmunitionProfile").objectReferenceValue = armorAmmunition;
            finite.FindProperty("wizardAmmunitionProfile").objectReferenceValue = null;
            finite.FindProperty("waitForPersistentHazards").boolValue = false;
            finite.ApplyModifiedPropertiesWithoutUndo();
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
