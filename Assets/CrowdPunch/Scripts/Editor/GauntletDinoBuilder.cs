using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        public const string DinoSettingsPath=Root+"Data/Settings/DinoBossSettings.asset";
        public const string DinoWavePath=Waves+"CP22_01_Dino_Crowd.asset";
        private const string DinoArt=Root+"Data/DinoBoss/";

        [MenuItem("Crowd Punch/Levels/Build Dino Pillars Gauntlet 22")]
        public static void BuildDino()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(DinoArt); AssetDatabase.Refresh();
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<DinoBossSettings>(DinoSettingsPath);
                if(settings==null) { settings=ScriptableObject.CreateInstance<DinoBossSettings>(); AssetDatabase.CreateAsset(settings,DinoSettingsPath); }
                var profiles=new EnemySpawnSettings[ProfileNames.Length];
                for(int i=0;i<profiles.Length;i++) profiles[i]=AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+ProfileNames[i]+".asset");
                var oldWave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(DinoWavePath);
                string savedWave=oldWave!=null?EditorJsonUtility.ToJson(oldWave):null;
                var design=new Level { Name="Dino Pillars",Outline=Rectangle(40,40),Spacing=new Vector2(36,36),Entry=new Vector2(0,-14),
                    Lanes=Array.Empty<Vector4>(), Waves=new[] { new Wave { Name="Dino Crowd",B=8,Delay=1.5f,
                        Ranges=new[] { Range(-13,0,6,28),Range(13,0,6,28),Range(0,-9,16,6) } } } };
                var wall=MaterialAsset("GauntletWalls",new Color(.13f,.19f,.24f));
                BuildLevel(21,design,profiles,AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),wall,
                    MaterialAsset("GauntletLanes",new Color(.43f,.48f,.4f)),MaterialAsset("GauntletBackdrop",new Color(.08f,.105f,.13f)));
                var wave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(DinoWavePath);
                if(savedWave!=null) EditorJsonUtility.FromJsonOverwrite(savedWave,wave);
                else
                {
                    var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
                    wd.FindProperty("bossReplenishDelay").floatValue=3; wd.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(wave);
                var sub=EditorSceneManager.OpenScene(Scenes+"Gauntlet_22/Gauntlet_22 Sub Scene.unity",OpenSceneMode.Single);
                var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
                var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var crowdData=new SerializedObject(crowd); crowdData.FindProperty("minimumPlayerDistance").floatValue=4; crowdData.ApplyModifiedPropertiesWithoutUndo();
                var root=new GameObject("Dino Pillars Boss");
                root.transform.SetPositionAndRotation(new Vector3(0,-1+settings.bodyHeight*.5f,13),Quaternion.Euler(0,180,0));
                var boss=root.AddComponent<DinoBossAuthoring>(); boss.settings=settings; crowd.dinoBoss=boss;
                AddDinoModel(root,settings);
                AddPillar(boss,new Vector3(-8,-1,-3));
                AddPillar(boss,new Vector3(8,-1,-3));
                AddPillar(boss,new Vector3(0,-1,7));
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main=EditorSceneManager.OpenScene(Scenes+"Gauntlet_22.unity",OpenSceneMode.Single);
                var marker=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue="Launch enemies into the three pillars to topple them onto Dino. Dodge his red speed bursts. Missed pillars return; falling pillars hurt everyone.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity",OpenSceneMode.Single);
                var sequence=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names=sequence.FindProperty("levelSceneNames"); var titles=sequence.FindProperty("levelDisplayNames");
                names.arraySize=titles.arraySize=Math.Max(22,names.arraySize);
                names.GetArrayElementAtIndex(21).stringValue="Gauntlet_22"; titles.GetArrayElementAtIndex(21).stringValue="22 Dino Pillars";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s=>s.path==Scenes+"Gauntlet_22.unity");
                int last=build.FindIndex(s=>s.path==Scenes+"Gauntlet_21.unity");
                build.Insert(last+1,new EditorBuildSettingsScene(Scenes+"Gauntlet_22.unity",true)); EditorBuildSettings.scenes=build.ToArray();
                GauntletNatureEnvironment.ApplyLevel(22);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        internal static void AddDinoModel(GameObject root,DinoBossSettings settings)
        {
            DinoModelBuilder.AddModel(root, settings);
        }

        private static void AddPillar(DinoBossAuthoring boss,Vector3 position)
        {
            var t=boss.settings;
            var root=new GameObject("Falling Pillar"); root.transform.position=position;
            root.AddComponent<FallingPillarAuthoring>().boss=boss;
            var stone=MaterialAsset("PillarStone",new Color(.7f,.8f,.85f));
            Box("Pillar Shaft",root.transform,position+Vector3.up*t.pillarHeight*.5f,
                new Vector3(t.pillarWidth,t.pillarHeight,t.pillarWidth),stone,false);
            // Decorative bands stay inside the collision silhouette and turn with the shaft.
            var band=MaterialAsset("PillarBands",new Color(.22f,.4f,.45f));
            foreach(float height in new[]{.5f,t.pillarHeight-.5f})
                Box("Stone Band",root.transform,position+Vector3.up*height,
                    new Vector3(t.pillarWidth,.3f,t.pillarWidth),band,false);
        }
    }
}
