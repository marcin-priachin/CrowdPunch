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
        [MenuItem("Crowd Punch/Campaign/Apply Chapter Seven Encounter Revision")]
        public static void ApplyChapterSevenEncounterRevision()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before revising encounters.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before revising encounters.");

            int[] levels = { 63, 65, 67, 68 };
            var designs = ChapterSevenDesigns();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // LOOP-011: preserve the authored spawn banks, wave GUIDs and encounter wiring.
                foreach (int level in levels)
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyWaveSequenceAuthoring>()).Single();
                    var waves = designs[level - 61].Waves;
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
                foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14 })
                    if (profiles[i].EnemyPrefab == null)
                        throw new InvalidOperationException($"Missing prefab for {profiles[i].name}.");

                foreach (int level in levels)
                {
                    var waves = designs[level - 61].Waves;
                    for (int phase = 0; phase < waves.Length; phase++)
                    {
                        var recipe = waves[phase];
                        WaveAsset(ChapterTwoWaveName(level, phase, recipe), recipe, profiles, CampaignData + "Waves/");
                    }
                }
                foreach (int level in levels)
                {
                    var scene = EditorSceneManager.OpenScene(CampaignSub(level), OpenSceneMode.Single);
                    var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    AuthorChapterSevenPressureHazards(scene, level, encounter);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("LOOP-011: Chapter Seven revised in place. Timed mixed groups at 63/65/67/68 and 328 enemies at Shifting Front.");
        }

        private static void AuthorChapterSevenPressureHazards(Scene scene, int level, EnemyWaveSequenceAuthoring encounter)
        {
            void Strip(string name, float x, float z, float offset, string previousName = null)
            {
                var patch = scene.GetRootGameObjects().SingleOrDefault(g => g.name == name || g.name == previousName)
                    ?.GetComponent<GroundHazardAuthoring>();
                if (patch == null) patch = CampaignGround(encounter, level, name, new Vector2(x, z), true, offset);
                patch.gameObject.name = name;
                patch.transform.position = new Vector3(x, -.965f, z);
                patch.depth = 6;
                patch.cycleOffset = offset;
            }

            switch (level)
            {
                case 63:
                    Strip("Southwest retreat warning", -10, -15, 0);
                    Strip("North crossing warning", 0, 11, 8f / 3);
                    Strip("Southeast retreat warning", 10, -15, 16f / 3);
                    break;
                case 65:
                    Strip("Far side crossing", 10, -13, 0);
                    Strip("Middle approach warning", -.5f, -6, 8f / 3);
                    Strip("Southwest approach warning", -10, -16, 16f / 3);
                    break;
                case 67:
                    Strip("West island crossing", -4, -5, 0);
                    Strip("East island crossing", 3, 5.5f, 8f / 3);
                    Strip("Southwest approach warning", -11, -14, 16f / 3);
                    break;
                case 68:
                    Strip("Northwest approach warning", -7, 15, 0, "Northwest outer warning");
                    Strip("Northeast approach warning", 7, 15, 4f / 3, "Northeast outer warning");
                    Strip("East crossing warning", 14, 0, 8f / 3);
                    Strip("Southeast approach warning", 7, -17, 4);
                    Strip("Southwest approach warning", -7, -17, 16f / 3, "Southwest outer warning");
                    Strip("West crossing warning", -14, 0, 20f / 3);
                    break;
                default: return;
            }
            ChapterSevenVaultHazards.Apply(scene);
        }

        private static Level[] ChapterSevenDesigns() => new[]
        {
            new Level { Name="Stable Footing", Outline=new[]{new Vector2(-18,-17),new Vector2(18,-17),new Vector2(18,3),new Vector2(15,3),new Vector2(15,17),new Vector2(-15,17),new Vector2(-15,7),new Vector2(-18,7)},
                Spacing=new Vector2(30,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="Steady Armor",B=6,Snake=1,Ranges=new[]{Range(-5,9,14,5)}},new Wave {Name="Far Shooters",B=10,Snake=1,Ranges=new[]{Range(4,9,16,5)}}} },
            new Level { Name="Side Door", Outline=new[]{new Vector2(-18,-22),new Vector2(18,-22),new Vector2(18,22),new Vector2(3,22),new Vector2(-8,16),new Vector2(-18,3)},
                Spacing=new Vector2(30,38), Entry=new Vector2(0,-17), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Side Door Ammunition",14,new[]{Range(-12,-5,4,18),Range(13,-4,4,20)},x:2)} },
            new Level { Name="Draw a Line", Outline=Clipped(42,38,4), Spacing=new Vector2(36,32), Entry=new Vector2(0,-14), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="West Lines",B=7,Mushroom=2,Trail=3,R=2,LinkedPairs=1,Batch=6,Interval=5,NextAfter=6,Ranges=new[]{Range(-15,1,4,22)}},
                new Wave {Name="East Charges",B=8,Mushroom=2,Trail=2,D=4,Dragon=1,LinkedPairs=1,R=1,Batch=6,Interval=5,Delay=0,Ranges=new[]{Range(15,1,4,22)}}} },
            new Level { Name="Shell Island", Outline=new[]{new Vector2(-19,-19),new Vector2(-4,-19),new Vector2(-4,-17),new Vector2(4,-17),new Vector2(4,-19),new Vector2(19,-19),new Vector2(19,-4),new Vector2(17,-4),new Vector2(17,4),new Vector2(19,4),new Vector2(19,19),new Vector2(4,19),new Vector2(4,17),new Vector2(-4,17),new Vector2(-4,19),new Vector2(-19,19),new Vector2(-19,4),new Vector2(-17,4),new Vector2(-17,-4),new Vector2(-19,-4)},
                Spacing=new Vector2(32,32), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Island Fuses",14,new[]{Range(-11,0,4,16),Range(11,0,4,16)},x:2)} },
            new Level { Name="Three Approaches", Outline=new[]{new Vector2(-21,-14),new Vector2(-14,-21),new Vector2(14,-21),new Vector2(21,-14),new Vector2(21,12),new Vector2(10,21),new Vector2(-10,21),new Vector2(-21,12)},
                Spacing=new Vector2(36,36), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Armor",B=8,Snake=1,Sporella=2,LinkedPairs=1,R=3,D=4,Batch=6,Interval=5,NextAfter=6,Ranges=new[]{Range(0,14,18,4)}},
                new Wave {Name="West Casters",B=9,AlienSpike=3,X=6,D=4,Batch=6,Interval=5,Delay=0,NextAfter=6,Ranges=new[]{Range(-15,0,4,20)}},
                new Wave {Name="East Trails",B=10,A=2,Dragon=1,LinkedPairs=1,R=5,Trail=2,Wizard=2,Batch=6,Interval=5,Delay=0,Ranges=new[]{Range(15,0,4,20)}}} },
            new Level { Name="Set the Pace", Outline=new[]{new Vector2(-19,-20),new Vector2(19,-20),new Vector2(19,10),new Vector2(12,20),new Vector2(-11,20),new Vector2(-19,12)},
                Spacing=new Vector2(32,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Pause Ammunition",12,new[]{Range(-13,-1,4,20),Range(13,0,4,22)},x:2)} },
            new Level { Name="Keep It Moving", Outline=Clipped(42,40,6), Spacing=new Vector2(36,34), Entry=new Vector2(0,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Pressure",B=7,Mushroom=1,Dragon=1,R=3,D=3,X=3,Batch=6,Interval=5,NextAfter=6,Ranges=new[]{Range(0,14,24,4)}},
                new Wave {Name="East Armor",B=9,Dragon=1,Snake=1,A=2,D=4,X=3,Batch=6,Interval=5,Delay=0,Ranges=new[]{Range(15,0,4,24)}}} },
            new Level { Name="Shifting Front", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                new Wave {Name="North Front",B=16,Mushroom=6,R=6,D=7,Dragon=1,LinkedPairs=2,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(0,33,52,14)}},
                new Wave {Name="West Fuses",B=19,AlienSpike=6,X=15,D=4,LinkedPairs=2,Batch=8,Interval=6,Delay=0,NextAfter=6,Ranges=new[]{Range(-33,0,14,44)}},
                new Wave {Name="East Armor",B=23,A=8,R=6,X=9,Dragon=2,LinkedPairs=3,E=2,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(33,0,14,44)}},
                new Wave {Name="South Trails",B=23,Snake=2,Sporella=4,Trail=6,D=10,X=7,LinkedPairs=2,Batch=8,Interval=6,NextAfter=6,Ranges=new[]{Range(0,-34,52,14)}},
                new Wave {Name="Split Casters",B=26,Dragon=1,Sporella=5,Mushroom=7,R=5,D=10,X=6,LinkedPairs=2,Batch=8,Interval=6,Delay=0,NextAfter=6,Ranges=new[]{Range(-33,0,14,40),Range(33,0,14,40)}},
                new Wave {Name="Final Front",B=26,Snake=1,Dragon=2,AlienSpike=4,X=6,A=5,R=8,D=4,Wizard=2,LinkedPairs=3,Batch=8,Interval=6,Delay=0,Ranges=new[]{Range(0,34,64,16)}}} },
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
                    PositionCampaignTrackEntry(i+61);
                }
                CopyCampaignRollingRematch();
                catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for(int n=61;n<=70;n++) catalog.Get(n-1).scenePath=$"{CampaignScenes}Campaign_{n:00}.unity";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Seven authored: levels 61-70, offset gate, four-explosion shell, hit-pause cover, 328-enemy arena and reflecting Rolling Blob.");
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
                    CampaignRock(arena,new Vector2(6,5),new Vector2Int(4,5)); break;
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
                    CampaignRock(arena,new Vector2(6,-6),new Vector2Int(4,5)); break;
                case 68:
                    CampaignRock(arena,new Vector2(-17,17),new Vector2Int(7,9));
                    CampaignRock(arena,new Vector2(17,17),new Vector2Int(9,7));
                    CampaignRock(arena,new Vector2(-17,-17),new Vector2Int(9,7));
                    CampaignRock(arena,new Vector2(17,-17),new Vector2Int(7,9)); break;
                case 69: CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero); break;
            }
            int trackIndex = Array.IndexOf(CampaignTrackLevels, number);
            if (trackIndex > 0) AuthorProgressiveTrackRoute(sub, number, trackIndex + 1);
            int gateIndex = Array.IndexOf(CampaignBarricadeLevels, number);
            if (gateIndex > 0) AuthorBarricadeRooms(sub, number, gateIndex);
            AuthorChapterSevenPressureHazards(sub, number, encounter);
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
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
        }
    }
}
