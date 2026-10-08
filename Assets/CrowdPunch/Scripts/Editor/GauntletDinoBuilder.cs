using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string DinoModel=Root+"Models/UltimateMonsters/Big/Dino.fbx";
        private const string DinoController=DinoArt+"Dino.controller";

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

        private static void AddDinoModel(GameObject root,DinoBossSettings settings)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(DinoModel);
            if(source==null) throw new InvalidOperationException("Missing supplied Dino.fbx model.");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
            visual.name="Dino Model";
            var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(renderers.Length==0) throw new InvalidOperationException("Dino model needs skinned renderers.");
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale*=Mathf.Min(settings.bodyHeight/Mathf.Max(.01f,bounds.size.y),
                2*settings.bodyRadius/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z)));
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition-=root.transform.InverseTransformPoint(bounds.center);
            var animator=visual.GetComponent<Animator>(); if(animator==null) animator=visual.AddComponent<Animator>();
            string[] suffixes={"Walk","Run","Walk","HitReact","Death"};
            var clips=suffixes.Select(s=>AssetDatabase.LoadAllAssetsAtPath(DinoModel).OfType<AnimationClip>()
                .First(c=>!c.name.StartsWith("__preview__") && c.name.EndsWith("|"+s))).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(DinoController);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(DinoController);
                for(int i=0;i<clips.Length;i++) controller.layers[0].stateMachine.AddState("Motion"+i).motion=clips[i];
            }
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var template=AssetDatabase.LoadAssetAtPath<Material>(Root+"Animation/EnemySkinning.mat");
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            for(int i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i]; renderer.rootBone=animator.transform; renderer.quality=SkinQuality.Bone4;
                string materialPath=DinoArt+(i==0?"Dino.mat":"DinoSpikes.mat");
                var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(mat==null)
                {
                    mat=new Material(template) { enableInstancing=true };
                    var original=renderer.sharedMaterial;
                    if(original!=null && original.mainTexture!=null) mat.mainTexture=original.mainTexture;
                    AssetDatabase.CreateAsset(mat,materialPath);
                }
                renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>mat).ToArray();
                AssetDatabase.SaveAssets();
                string samplePath=DinoArt+(i==0?"DinoMovement.bytes":"DinoSpikesMovement.bytes");
                WriteBossSamples(animator,renderer,clips,samplePath,DinoController);
                var animation=renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Samples=AssetDatabase.LoadAssetAtPath<TextAsset>(samplePath);
            }
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

