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
        private static readonly string[] ChapterTwoHints =
        {
            "Launch Exploders into the crowd. Choose where the blast will land.",
            "Break the shell with three explosions. Then attack the exposed core again.",
            "Launch bodies or Exploders into the barricade, then reach the exit.",
            "Dashers commit to a direction. Step aside and send them into the crowd.",
            "Move out of the firing lane. Use an Exploder to reach the grouped threats.",
            "Launch bodies into the block from behind to move it into the blue socket. Far-side hits push it back.",
            "Send Exploders through the moving opening. Blasts outside the cover cannot reach the target.",
            "Clear the large waves. Place explosions in dense groups and leave room to dodge charges.",
            "Blast behind the block to push it toward the socket. Use the outer lane to get into position.",
            "Punch the bouncing shots back, or launch enemies at the Chicken. Returned shots and launched bodies still hurt you."
        };

        private static Level[] ChapterTwoDesigns() => new[]
        {
            new Level { Name="First Fuse", Outline=OvalCourt(28,30), Spacing=new Vector2(22,24), Entry=new Vector2(0,-10), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("First Fuse",6,new[]{Range(-4,5,6,8),Range(4,5,6,8)},x:1),
                W("Fuse Chains",10,new[]{Range(0,6,16,8)},x:2) } },
            new Level { Name="Crack the Shell", Outline=Clipped(32,32,4), Spacing=new Vector2(26,26), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Shell Ammunition",8,new[]{Range(-9,0,4,20),Range(9,0,4,20)},x:2) } },
            new Level { Name="Blast the Gate", Outline=Clipped(30,42,3), Spacing=new Vector2(24,36), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Blast Ammunition",10,new[]{Range(-8,-2,4,22),Range(8,-2,4,22)},x:2) } },
            new Level { Name="Committed Charge", Outline=Clipped(30,40,5), Spacing=new Vector2(24,34), Entry=new Vector2(0,-14), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("First Charge",8,new[]{Range(0,10,20,8)},d:1),
                W("Paired Charge",12,new[]{Range(-6,10,6,9),Range(6,10,6,9)},d:2) } },
            new Level { Name="Crossed Threats", Outline=new[]{new Vector2(-9,-17),new Vector2(9,-17),new Vector2(18,2),new Vector2(0,17),new Vector2(-18,2)}, Spacing=new Vector2(24,26), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("West Threats",12,new[]{Range(-10,0,4,12)},r:2,d:1),
                W("East Threats",16,new[]{Range(10,0,4,12)},r:2,x:2,d:2) } },
            new Level { Name="Push the Block", Outline=Clipped(32,38,3), Spacing=new Vector2(26,32), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Block Ammunition",8,new[]{Range(-9,0,5,24),Range(9,0,5,24)}) } },
            new Level { Name="Moving Window", Outline=Clipped(36,32,4), Spacing=new Vector2(30,26), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Window Ammunition",10,new[]{Range(0,10,18,4),Range(0,-10,18,4)},x:2) } },
            new Level { Name="Crowd Reaction", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("North Reaction",48,new[]{Range(0,23,34,16)},x:4),
                W("West Charges",64,new[]{Range(-27,0,16,36)},r:4,d:2),
                W("East Fuses",80,new[]{Range(27,0,16,36)},x:6,d:2),
                W("Split Reaction",96,new[]{Range(-22,28,20,20),Range(22,28,20,20)},r:4,x:4,d:4) } },
            new Level { Name="Push from Behind", Outline=Clipped(30,42,4), Spacing=new Vector2(24,36), Entry=new Vector2(2,-15), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Behind the Block",8,new[]{Range(-9,-2,4,24),Range(11,0,3,24)},x:2) } }
        };

        private static Vector2[] OvalCourt(float width, float depth) => Enumerable.Range(0,12)
            .Select(i => new Vector2(Mathf.Cos(i*Mathf.PI/6)*width/2, Mathf.Sin(i*Mathf.PI/6)*depth/2)).ToArray();

        [MenuItem("Crowd Punch/Campaign/Create Chapter Two")]
        public static void BuildChapterTwo()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            if (catalog == null || catalog.Count != 80) throw new InvalidOperationException("Chapter One's 80-level catalog is required.");
            for (int number=11; number<=20; number++)
                if (catalog.Get(number-1).Available || File.Exists($"{CampaignScenes}Campaign_{number:00}.unity"))
                    throw new InvalidOperationException("Chapter Two content already exists. Edit its saved assets; this recipe never overwrites authored encounters.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var designs = ChapterTwoDesigns();
                for (int i=0; i<designs.Length; i++)
                {
                    var profiles = LoadCampaignProfiles();
                    BuildLevel(i+10, designs[i], profiles, AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray), MaterialAsset("GauntletLanes",Color.gray),
                        MaterialAsset("GauntletBackdrop",Color.gray), CampaignData);
                    AuthorChapterTwoInterior(i+11, designs[i]);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+11);
                    SetCampaignHint(i+11, ChapterTwoHints[i]);
                }
                CopyCampaignChicken();
                // Single-scene loads can unload cached Unity asset wrappers.
                catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                for (int number=11; number<=20; number++)
                    catalog.Get(number-1).scenePath = $"{CampaignScenes}Campaign_{number:00}.unity";
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Chapter Two authored: levels 11-20, shell/track objectives, 318-enemy large arena and preserved Chicken arena.");
        }

        private static EnemySpawnSettings[] LoadCampaignProfiles() => ProfileNames.Select(n =>
            AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+n+".asset")
            ?? throw new InvalidOperationException("Missing enemy profile: "+n)).ToArray();

        private static void AuthorChapterTwoInterior(int number, Level design)
        {
            var sub = EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation = arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor = true;
            navigation.participationAnchor = ChapterTwoNavigationAnchor(number);
            var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var data = new SerializedObject(encounter);
            data.FindProperty("minimumPlayerDistance").floatValue = number==18 ? 10 : 3;
            data.FindProperty("placementAttemptsPerEnemy").intValue = 48;
            data.ApplyModifiedPropertiesWithoutUndo();
            switch (number)
            {
                case 12: CopyCampaignObjective(sub,arena,encounter,number,false,Vector3.zero); break;
                case 13:
                    CampaignRock(arena,new Vector2(0,-4),new Vector2Int(3,13));
                    CampaignBarricade(number,encounter); break;
                case 15:
                    CampaignRock(arena,new Vector2(-2,-1),new Vector2Int(3,4));
                    CampaignRock(arena,new Vector2(0,2),new Vector2Int(3,4)); break;
                case 16: CopyCampaignObjective(sub,arena,encounter,number,true,Vector3.zero); break;
                case 17:
                    CampaignRock(arena,new Vector2(-13,0),new Vector2Int(3,7));
                    CampaignRock(arena,new Vector2(13,0),new Vector2Int(3,7));
                    CampaignCover(arena,encounter,number,90,25); break;
                case 18:
                    CampaignRock(arena,new Vector2(-12,9),new Vector2Int(6,8));
                    CampaignRock(arena,new Vector2(12,-7),new Vector2Int(6,8));
                    CampaignRock(arena,new Vector2(-10,-27),new Vector2Int(7,6)); break;
                case 19: CopyCampaignObjective(sub,arena,encounter,number,true,new Vector3(5,0,0)); break;
            }
            EditorSceneManager.MarkSceneDirty(sub);
            EditorSceneManager.SaveScene(sub);
            AssetDatabase.SaveAssets();
        }

        private static Vector2 ChapterTwoNavigationAnchor(int number) => number switch
        {
            13 => new Vector2(-7,-10),
            14 => new Vector2(0,-11),
            16 => new Vector2(0,-10),
            18 => new Vector2(0,-16),
            19 => new Vector2(2,-12),
            _ => new Vector2(0,-9)
        };

        // Reuse the complete authored objective visuals and remap cross-root references.
        // The legacy scenes are opened read-only; campaign tuning is always a separate asset.
        private static void CopyCampaignObjective(Scene scene, ArenaAuthoring arena,
            EnemyWaveSequenceAuthoring encounter, int number, bool isTrack, Vector3 offset)
        {
            int legacy = isTrack ? 16 : 15;
            var source = EditorSceneManager.OpenScene($"{Scenes}Gauntlet_{legacy:00}/Gauntlet_{legacy:00} Sub Scene.unity",OpenSceneMode.Additive);
            BarricadeAuthoring target;
            try
            {
                var original = source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeAuthoring>(true)).Single();
                target = CloneObjectivePart(original.gameObject,scene,offset).GetComponent<BarricadeAuthoring>();
                target.transform.SetParent(arena.transform,true);
                if (isTrack)
                {
                    var track = target.GetComponent<TrackObjectAuthoring>();
                    var socket = CloneObjectivePart(original.GetComponent<TrackObjectAuthoring>().destination.gameObject,scene,offset);
                    track.destination = socket.transform;
                    foreach (var visual in socket.GetComponentsInChildren<TrackSocketVisualAuthoring>()) visual.trackObject = track;
                    foreach (var rail in source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                        .Where(t=>t.name=="Track rail" || t.name=="Direction arrow"))
                        CloneObjectivePart(rail.gameObject,scene,offset).transform.SetParent(arena.transform,true);
                }
            }
            finally { EditorSceneManager.CloseScene(source,true); SceneManager.SetActiveScene(scene); }
            encounter.barricade = target;
            target.settings = CampaignSettings<BarricadeSettings>(isTrack?"TrackSolidSettings":"ShellSolidSettings",$"L{number:00}_Solid");
            target.settings.replenishDelay = 4;
            EditorUtility.SetDirty(target.settings);
            if (isTrack)
            {
                var tuning = CampaignSettings<TrackObjectSettings>("TrackObjectSettings",$"L{number:00}_Track");
                tuning.requiredNetHits = 5;
                target.GetComponent<TrackObjectAuthoring>().settings = tuning;
                EditorUtility.SetDirty(tuning);
            }
            else
            {
                var tuning = CampaignSettings<ShellTargetSettings>("ShellTargetSettings",$"L{number:00}_Shell");
                tuning.requiredExplosions = 3;
                tuning.exploderReplacementDelay = 4;
                tuning.coreMaxHealth = LoadCampaignProfiles()[0].Health.Max;
                target.GetComponent<ShellTargetAuthoring>().settings = tuning;
                EditorUtility.SetDirty(tuning);
            }
        }

        private static GameObject CloneObjectivePart(GameObject source, Scene scene, Vector3 offset)
        {
            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name;
            clone.transform.SetParent(null,true);
            // Instantiate without a parent can retain local coordinates from a parented source.
            clone.transform.SetPositionAndRotation(source.transform.position+offset,source.transform.rotation);
            clone.transform.localScale = source.transform.lossyScale;
            SceneManager.MoveGameObjectToScene(clone,scene);
            return clone;
        }

        private static void CopyCampaignChicken()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(20)));
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(Scenes+"Gauntlet_20/Gauntlet_20 Sub Scene.unity",CampaignSub(20))
                || !AssetDatabase.CopyAsset(Scenes+"Gauntlet_20.unity",CampaignScenes+"Campaign_20.unity"))
                throw new InvalidOperationException("Could not copy the authored Chicken arena.");
            var sub = EditorSceneManager.OpenScene(CampaignSub(20),OpenSceneMode.Single);
            var chicken = UnityEngine.Object.FindFirstObjectByType<ChickenBossAuthoring>();
            chicken.settings = CampaignSettings<ChickenBossSettings>("ChickenBossSettings","L20_Chicken");
            var wave = WaveAsset("CP20_01_Chicken_Crowd",W("Chicken Crowd",6,new[]{Range(0,0,28,28)}),LoadCampaignProfiles(),CampaignData+"Waves/");
            var wd = new SerializedObject(wave);
            wd.FindProperty("replenishWhileBossLives").boolValue = true;
            wd.FindProperty("bossReplenishDelay").floatValue = 4;
            wd.ApplyModifiedPropertiesWithoutUndo();
            var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            encounter.chickenBoss = chicken;
            var data = new SerializedObject(encounter);
            data.FindProperty("waves").arraySize = 1;
            data.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue = wave;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
            AssetDatabase.SaveAssets();
            var main = EditorSceneManager.OpenScene(CampaignScenes+"Campaign_20.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(20));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
            SetCampaignHint(20,ChapterTwoHints[9]);
        }
    }
}
