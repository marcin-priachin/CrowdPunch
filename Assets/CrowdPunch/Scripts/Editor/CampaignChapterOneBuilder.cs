using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Utilities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        private const string CampaignData = Root + "Data/Campaign/";
        private const string CampaignScenes = Root + "Scenes/Campaign/";
        private static string CampaignSub(int number) => $"{CampaignScenes}Campaign_{number:00}/Campaign_{number:00} Sub Scene.unity";
        private static readonly string[] ChapterOneHints =
        {
            "Line up an enemy with the crowd, then punch. The short line shows the launch direction.",
            "Move around the rocks to find a clear angle. Dash to reposition.",
            "Use nearby enemies to reach the shooters. Keep moving between shots.",
            "Launch bodies into the barricade, then reach the exit.",
            "Step out of the firing lane and send a body back across it.",
            "Find an angle around the divider and break through.",
            "Send bodies through the rotating opening. Watch for returned shots.",
            "Clear each large wave. Find a line through the crowd and reposition as the next wave arrives.",
            "Set up a clear line through the crowd to the distant threat.",
            "Launch enemies into the head. Dodge the hands and shoot through the openings."
        };

        private static Level[] ChapterOneDesigns() => new[]
        {
            new Level { Name="First Line", Outline=Clipped(24,30,3), Spacing=new Vector2(18,24), Entry=new Vector2(0,-10), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("First Line",3,new[]{Range(0,1,8,8)}), W("Two Banks",6,new[]{Range(-5,5,5,8),Range(5,5,5,8)}), W("First Chains",8,new[]{Range(0,4,14,12)}) } },
            new Level { Name="Choose the Angle", Outline=new[]{new Vector2(-14,-16),new Vector2(14,-16),new Vector2(11,16),new Vector2(-11,16)}, Spacing=new Vector2(20,26), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("West Bank",8,new[]{Range(-6,6,5,8)}), W("East Bank",12,new[]{Range(6,6,5,8)}) } },
            new Level { Name="Across the Court", Outline=Clipped(30,36,5), Spacing=new Vector2(22,28), Entry=new Vector2(0,-12), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("First Shooter",6,new[]{Range(0,7,16,8)},r:1), W("East Shooters",10,new[]{Range(7,4,5,16)},r:2) } },
            new Level { Name="Break Through", Outline=new[]{new Vector2(-12,-19),new Vector2(12,-19),new Vector2(9,19),new Vector2(-9,19)}, Spacing=new Vector2(16,30), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Gate Ammunition",8,new[]{Range(-5,-2,4,16),Range(5,-2,4,16)}) } },
            new Level { Name="Crossfire", Outline=Clipped(34,32,7), Spacing=new Vector2(24,22), Entry=new Vector2(0,-9), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("West Fire",12,new[]{Range(-8,3,5,12)},r:2), W("East Fire",16,new[]{Range(8,3,5,12)},r:3) } },
            new Level { Name="Open the Lane", Outline=new[]{new Vector2(-14,-20),new Vector2(10,-20),new Vector2(14,0),new Vector2(12,20),new Vector2(-12,20),new Vector2(-14,0)}, Spacing=new Vector2(18,32), Entry=new Vector2(0,-13), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Lane Ammunition",10,new[]{Range(-6,-1,4,20),Range(6,-1,4,20)}) } },
            new Level { Name="Through the Opening", Outline=Clipped(34,34,5), Spacing=new Vector2(28,28), Entry=new Vector2(0,-11), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Opening Ammunition",8,new[]{Range(-10,0,4,18),Range(0,-10,16,4)}) } },
            new Level { Name="Changing Sides", Outline=Rectangle(100,100), Spacing=new Vector2(96,96), Entry=new Vector2(0,-16), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("North Crowd",40,new[]{Range(0,18,34,16)}), W("West Crowd",56,new[]{Range(-25,0,16,36)},r:4),
                W("East Crowd",72,new[]{Range(25,0,16,36)},r:4), W("Last Crowd",88,new[]{Range(22,24,28,24)},r:6) } },
            new Level { Name="One Good Shot", Outline=new[]{new Vector2(-9,-15),new Vector2(9,-15),new Vector2(15,8),new Vector2(10,15),new Vector2(-10,15),new Vector2(-15,8)}, Spacing=new Vector2(18,24), Entry=new Vector2(0,-10), Lanes=Array.Empty<Vector4>(), Waves=new[]{
                W("Clear Line",10,new[]{Range(-5,5,5,10),Range(5,5,5,10)}), W("Distant Threat",12,new[]{Range(0,6,14,8)},r:1) } }
        };

        [MenuItem("Crowd Punch/Campaign/Create Chapter One")]
        public static void BuildChapterOne()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            if (AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath) != null)
                throw new InvalidOperationException("Chapter One already exists. Edit its saved scenes and settings directly; this creation recipe does not overwrite campaign tuning.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(CampaignData + "Waves");
            Directory.CreateDirectory(CampaignData + "Layouts");
            Directory.CreateDirectory(CampaignData + "Settings");
            Directory.CreateDirectory(CampaignScenes);
            AssetDatabase.Refresh();
            var profiles = ProfileNames.Select(n => AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+n+".asset")).ToArray();
            var designs = ChapterOneDesigns();
            try
            {
                for (int i=0;i<designs.Length;i++)
                {
                    // Single-scene loads can unload assets even while managed wrappers remain cached.
                    profiles = ProfileNames.Select(n => AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+n+".asset")).ToArray();
                    BuildLevel(i,designs[i],profiles,AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                        MaterialAsset("GauntletWalls",Color.gray),MaterialAsset("GauntletLanes",Color.gray),MaterialAsset("GauntletBackdrop",Color.gray),CampaignData);
                    AuthorCampaignInterior(i+1, designs[i]);
                    GauntletNatureEnvironment.ApplyCampaignLevel(i+1);
                    SetCampaignHint(i+1);
                }
                CopyCampaignBoss(profiles);
                var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
                catalog.chapterNames = new[]{"First Impact","Moving Ammunition","Pressure and Space","Living Hazards","Better Angles","Deliberate Reactions","Control the Ground","Crowd Mastery"};
                var rows = File.ReadAllLines("Docs/Design/Campaign80Proposal.md")
                    .Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^\| \d{2} \|")).ToArray();
                if (rows.Length != 80) throw new InvalidOperationException("Approved campaign table must contain 80 rows.");
                catalog.levels = rows.Select((line,index) => new CampaignCatalog.Level {
                    id=$"cp-{index+1:000}", title=line.Split('|')[2].Split('/')[0].Trim(),
                    scenePath=index<10 ? $"{CampaignScenes}Campaign_{index+1:00}.unity" : string.Empty }).ToArray();
                AssetDatabase.CreateAsset(catalog,CampaignBuildRegistration.CatalogPath);
                var bootstrap = EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath,OpenSceneMode.Single);
                catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                sequence.FindProperty("campaign").objectReferenceValue=catalog;
                sequence.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                AssetDatabase.SaveAssets();
                CampaignBuildRegistration.Apply();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Campaign Chapter One authored: ten encounters, campaign-owned tuning, 270-enemy large arena, preserved legacy content.");
        }

        private static void AuthorCampaignInterior(int number, Level design)
        {
            var sub=EditorSceneManager.OpenScene(CampaignSub(number),OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var navigation=arena.gameObject.AddComponent<NavigationArenaAuthoring>();
            navigation.settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
            navigation.overrideParticipationAnchor=true; navigation.participationAnchor=design.Entry;
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var ed=new SerializedObject(encounter); ed.FindProperty("minimumPlayerDistance").floatValue=number==8?10:3;
            ed.FindProperty("placementAttemptsPerEnemy").intValue=48; ed.ApplyModifiedPropertiesWithoutUndo();
            switch(number)
            {
                case 2: CampaignRock(arena,new Vector2(-2,1),new Vector2Int(4,5)); break;
                case 3: CampaignRock(arena,new Vector2(-10,-2),new Vector2Int(3,4)); CampaignRock(arena,new Vector2(10,-4),new Vector2Int(3,4)); break;
                case 5: CampaignRock(arena,new Vector2(-4,2),new Vector2Int(3,4)); CampaignRock(arena,new Vector2(4,-3),new Vector2Int(3,4)); break;
                case 6: CampaignRock(arena,new Vector2(0,0),new Vector2Int(3,9)); break;
                case 8: CampaignRock(arena,new Vector2(-12,6),new Vector2Int(8,12)); CampaignRock(arena,new Vector2(12,-8),new Vector2Int(8,12)); break;
                case 9: CampaignRock(arena,new Vector2(-8,-2),new Vector2Int(3,5)); break;
            }
            if(number==4 || number==6) CampaignBarricade(number,encounter);
            if(number==7) CampaignCover(arena,encounter);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
        }

        private static void CampaignRock(ArenaAuthoring arena,Vector2 position,Vector2Int footprint)
        {
            var obstacle=new GameObject("Rock island").AddComponent<SolidObstacleAuthoring>();
            obstacle.transform.SetParent(arena.transform); obstacle.transform.position=new Vector3(position.x,-1,position.y);
            obstacle.footprint=footprint; obstacle.height=2.4f;
            var mesh=GauntletNatureEnvironment.TileModel("rock_largeA",1,1,out string[] slots);
            string path=CampaignData+$"Layouts/Rock_{footprint.x}_{footprint.y}.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; } else UnityEngine.Object.DestroyImmediate(mesh);
            var visual=new GameObject("Nature rock",typeof(MeshFilter),typeof(MeshRenderer));
            visual.transform.SetParent(obstacle.transform,false); visual.transform.localPosition=Vector3.up*1.2f;
            visual.transform.localScale=new Vector3(footprint.x,2.4f,footprint.y);
            visual.GetComponent<MeshFilter>().sharedMesh=saved;
            var materials=GauntletNatureEnvironment.CreateMaterials();
            visual.GetComponent<MeshRenderer>().sharedMaterials=slots.Select(n=>materials[n]).ToArray();
        }

        private static T CampaignSettings<T>(string source,string name) where T : ScriptableObject
        {
            string destination=CampaignData+"Settings/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<T>(destination);
            if(existing!=null) return existing;
            if(!AssetDatabase.CopyAsset(Root+"Data/Settings/"+source+".asset",destination))
                throw new InvalidOperationException("Could not copy "+source);
            return AssetDatabase.LoadAssetAtPath<T>(destination);
        }

        private static void CampaignBarricade(int number,EnemyWaveSequenceAuthoring encounter)
        {
            var tuning=CampaignSettings<BarricadeSettings>("BarricadeSettings",$"L{number:00}_Barricade");
            tuning.requiredHits=3; tuning.replenishDelay=4; EditorUtility.SetDirty(tuning);
            float width=number==4?22:28;
            var target=new GameObject("Barricade").AddComponent<BarricadeAuthoring>();
            target.settings=tuning; target.size=new Vector3(width,4,1.2f); target.transform.position=new Vector3(0,1,11);
            var exit=new GameObject("Exit").transform; exit.position=new Vector3(0,.5f,16); target.exit=exit;
            encounter.barricade=target;
            var metal=MaterialAsset("BarricadeMetal",new Color(.22f,.34f,.39f));
            var crack=MaterialAsset("BarricadeCracks",new Color(.025f,.035f,.04f));
            BarricadePiece(target,"Solid plate",Vector3.zero,target.size,metal,0,new Vector3(0,-3,2));
            for(int stage=1;stage<=2;stage++)
                for(int i=-2;i<=2;i++) BarricadePiece(target,"Damage crack",new Vector3(i*3,stage-.9f,-.65f),new Vector3(2,.18f,.08f),crack,stage,Vector3.down);
            var green=MaterialAsset("BarricadeExit",new Color(.2f,.95f,.55f));
            Box("Exit landing",exit,new Vector3(0,-.97f,16),new Vector3(5,.06f,3),green,false);
            Box("Exit post left",exit,new Vector3(-2.5f,.5f,17),new Vector3(.3f,3,.3f),green,false);
            Box("Exit post right",exit,new Vector3(2.5f,.5f,17),new Vector3(.3f,3,.3f),green,false);
        }

        private static void CampaignCover(ArenaAuthoring arena,EnemyWaveSequenceAuthoring encounter)
        {
            var targetSettings=CampaignSettings<BarricadeSettings>("RotatingTargetSettings","L07_Target");
            targetSettings.requiredHits=3; targetSettings.replenishDelay=4; EditorUtility.SetDirty(targetSettings);
            var tuning=CampaignSettings<RotatingCoverSettings>("RotatingCoverSettings","L07_Cover");
            tuning.openingDegrees=100; tuning.degreesPerSecond=20; tuning.rotationMode=0; tuning.hitResponse=0;
            tuning.reflectionSpeedMultiplier=1; EditorUtility.SetDirty(tuning);
            var target=new GameObject("Central Target").AddComponent<BarricadeAuthoring>();
            target.settings=targetSettings; target.size=new Vector3(2.4f,4,2.4f); target.transform.position=new Vector3(0,1,0);
            target.completeOnDestruction=true; encounter.barricade=target;
            var cover=new GameObject("Rotating Cover").AddComponent<RotatingCoverAuthoring>();
            cover.transform.SetParent(arena.transform); cover.transform.position=new Vector3(0,1,0);
            cover.settings=tuning; cover.target=target; target.cover=cover;
            var metal=MaterialAsset("RotatingCoverMetal",new Color(.15f,.29f,.36f));
            var edge=MaterialAsset("RotatingCoverEdge",new Color(1,.56f,.12f));
            for(int i=0;i<CoverGeometry.PanelCount;i++)
            {
                var panel=Box("Cover panel "+i,cover.transform,cover.transform.position,Vector3.one,
                    i==0||i==CoverGeometry.PanelCount-1?edge:metal,false).AddComponent<CoverPanelAuthoring>();
                panel.cover=cover; panel.index=i;
            }
            var core=MaterialAsset("RotatingTargetCore",new Color(.28f,.85f,.65f));
            BarricadePiece(target,"Target core",Vector3.zero,target.size,core,0,Vector3.down);
            var crack=MaterialAsset("BarricadeCracks",Color.black);
            for(int stage=1;stage<=2;stage++)
                for(int face=0;face<4;face++)
                {
                    var rotation=Quaternion.Euler(0,face*90,0);
                    var piece=BarricadePiece(target,"Damage band",rotation*new Vector3(0,stage-1.5f,-1.24f),new Vector3(2.3f,.18f,.07f),crack,stage,Vector3.down);
                    piece.transform.localRotation=rotation;
                }
            var plinth=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name="Central enclosure plinth"; plinth.transform.position=new Vector3(0,-.75f,0);
            plinth.transform.localScale=new Vector3((tuning.radius-tuning.thickness)*2,.25f,(tuning.radius-tuning.thickness)*2);
            UnityEngine.Object.DestroyImmediate(plinth.GetComponent<Collider>()); plinth.GetComponent<Renderer>().sharedMaterial=metal;
        }

        private static void SetCampaignHint(int number)
        {
            var main=EditorSceneManager.OpenScene($"{CampaignScenes}Campaign_{number:00}.unity",OpenSceneMode.Single);
            var marker=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
            marker.FindProperty("openingHint").stringValue=ChapterOneHints[number-1]; marker.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
        }

        private static void CopyCampaignBoss(EnemySpawnSettings[] profiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CampaignSub(10))); AssetDatabase.Refresh();
            AssetDatabase.CopyAsset(BossGauntletBuilder.SubPath,CampaignSub(10));
            AssetDatabase.CopyAsset(BossGauntletBuilder.ScenePath,CampaignScenes+"Campaign_10.unity");
            var tuning=CampaignSettings<BossEncounterSettings>("BossEncounterSettings","L10_Gatekeeper");
            var td=new SerializedObject(tuning);
            td.FindProperty("health").floatValue=300; td.FindProperty("openingDuration").floatValue=1.5f;
            td.FindProperty("slam").FindPropertyRelative("Anticipation").floatValue=.8f;
            td.FindProperty("lunge").FindPropertyRelative("Anticipation").floatValue=.8f;
            td.FindProperty("sweep").FindPropertyRelative("Anticipation").floatValue=1;
            td.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(tuning);
            var sub=EditorSceneManager.OpenScene(CampaignSub(10),OpenSceneMode.Single);
            tuning=AssetDatabase.LoadAssetAtPath<BossEncounterSettings>(CampaignData+"Settings/L10_Gatekeeper.asset");
            UnityEngine.Object.FindFirstObjectByType<BossEncounterAuthoring>().settings=tuning;
            profiles = ProfileNames.Select(n => AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+n+".asset")).ToArray();
            var wave=WaveAsset("CP10_01_Gatekeeper",W("Gatekeeper",10,new[]{Range(0,0,22,22)}),profiles,CampaignData+"Waves/");
            var wd=new SerializedObject(wave); wd.FindProperty("bossReplenishDelay").floatValue=4; wd.ApplyModifiedPropertiesWithoutUndo();
            var crowd=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>());
            crowd.FindProperty("waves").arraySize=1; crowd.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave; crowd.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
            var main=EditorSceneManager.OpenScene(CampaignScenes+"Campaign_10.unity",OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<SubScene>().SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSub(10));
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
            SetCampaignHint(10);
        }
    }
}
