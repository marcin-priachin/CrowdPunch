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
        public const string RollingSettingsPath=Root+"Data/Settings/RollingBossSettings.asset";
        public const string RollingWavePath=Waves+"CP21_01_Rolling_Crowd.asset";
        private const string RollingArt=Root+"Data/RollingBoss/";
        private const string RollingModel=Root+"Models/UltimateMonsters/Blob/GreenSpikyBlob.fbx";
        private const string RollingController=RollingArt+"RollingBlob.controller";

        [MenuItem("Crowd Punch/Levels/Build Rolling Blob Gauntlet 21")]
        public static void BuildRolling()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(RollingArt); AssetDatabase.Refresh();
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<RollingBossSettings>(RollingSettingsPath);
                if(settings==null) { settings=ScriptableObject.CreateInstance<RollingBossSettings>(); AssetDatabase.CreateAsset(settings,RollingSettingsPath); }
                var profiles=new EnemySpawnSettings[ProfileNames.Length];
                for(int i=0;i<profiles.Length;i++) profiles[i]=AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+ProfileNames[i]+".asset");
                var oldWave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(RollingWavePath);
                string savedWave=oldWave!=null?EditorJsonUtility.ToJson(oldWave):null;
                var design=new Level { Name="Rolling Blob",Outline=Rectangle(40,40),Spacing=new Vector2(36,36),Entry=new Vector2(0,-14),
                    Lanes=Array.Empty<Vector4>(), Waves=new[] { new Wave { Name="Rolling Crowd",B=3,Delay=1.5f,
                        Ranges=new[] { Range(-13,0,6,28),Range(13,0,6,28),Range(0,-9,16,6) } } } };
                var wall=MaterialAsset("GauntletWalls",new Color(.13f,.19f,.24f));
                BuildLevel(20,design,profiles,AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),wall,
                    MaterialAsset("GauntletLanes",new Color(.43f,.48f,.4f)),MaterialAsset("GauntletBackdrop",new Color(.08f,.105f,.13f)));
                var wave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(RollingWavePath);
                if(savedWave!=null) EditorJsonUtility.FromJsonOverwrite(savedWave,wave);
                else
                {
                    var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
                    wd.FindProperty("bossReplenishDelay").floatValue=3; wd.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(wave);
                var sub=EditorSceneManager.OpenScene(Scenes+"Gauntlet_21/Gauntlet_21 Sub Scene.unity",OpenSceneMode.Single);
                var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
                var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var crowdData=new SerializedObject(crowd); crowdData.FindProperty("minimumPlayerDistance").floatValue=4; crowdData.ApplyModifiedPropertiesWithoutUndo();
                RollingObstacle("Interior Obstacle West",arena.transform,new Vector3(-8,-1,-3),new Vector2Int(3,4),wall);
                RollingObstacle("Interior Obstacle East",arena.transform,new Vector3(8,-1,3),new Vector2Int(3,4),wall);
                RollingObstacle("Interior Obstacle North",arena.transform,new Vector3(0,-1,7),new Vector2Int(4,3),wall);
                var root=new GameObject("Rolling Blob Boss");
                root.transform.SetPositionAndRotation(new Vector3(0,-1+settings.bodyHeight*.5f,14),Quaternion.Euler(0,180,0));
                var boss=root.AddComponent<RollingBossAuthoring>(); boss.settings=settings; crowd.rollingBoss=boss;
                AddRollingModel(root,settings);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main=EditorSceneManager.OpenScene(Scenes+"Gauntlet_21.unity",OpenSceneMode.Single);
                var marker=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue="Dodge the red roll. Launch enemies into the Blob while it pauses or winds up. Launched bodies still hurt you.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity",OpenSceneMode.Single);
                var sequence=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names=sequence.FindProperty("levelSceneNames"); var titles=sequence.FindProperty("levelDisplayNames");
                names.arraySize=titles.arraySize=Math.Max(21,names.arraySize);
                names.GetArrayElementAtIndex(20).stringValue="Gauntlet_21"; titles.GetArrayElementAtIndex(20).stringValue="21 Rolling Blob";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s=>s.path==Scenes+"Gauntlet_21.unity");
                int last=build.FindIndex(s=>s.path==Scenes+"Gauntlet_20.unity");
                build.Insert(last+1,new EditorBuildSettingsScene(Scenes+"Gauntlet_21.unity",true)); EditorBuildSettings.scenes=build.ToArray();
                GauntletNatureEnvironment.ApplyLevel(21);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void AddRollingModel(GameObject root,RollingBossSettings settings)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(RollingModel);
            if(source==null) throw new InvalidOperationException("Missing supplied GreenSpikyBlob.fbx model.");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
            visual.name="Green Spiky Blob Model";
            var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(renderers.Length==0) throw new InvalidOperationException("Blob model needs skinned renderers.");
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale*=Mathf.Min(settings.bodyHeight/Mathf.Max(.01f,bounds.size.y),
                2*settings.bodyRadius/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z)));
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition-=root.transform.InverseTransformPoint(bounds.center);
            var animator=visual.GetComponent<Animator>(); if(animator==null) animator=visual.AddComponent<Animator>();
            string[] suffixes={"Idle","Walk","Bite_Front","HitRecieve","Death"};
            var clips=suffixes.Select(s=>AssetDatabase.LoadAllAssetsAtPath(RollingModel).OfType<AnimationClip>()
                .First(c=>!c.name.StartsWith("__preview__") && c.name.EndsWith("|"+s))).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(RollingController);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(RollingController);
                for(int i=0;i<clips.Length;i++) controller.layers[0].stateMachine.AddState(suffixes[i]).motion=clips[i];
            }
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var template=AssetDatabase.LoadAssetAtPath<Material>(Root+"Animation/EnemySkinning.mat");
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 pivot=animator.transform.InverseTransformPoint(bounds.center);
            root.GetComponent<RollingBossAuthoring>().animationPivot=pivot;
            for(int i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i]; renderer.rootBone=animator.transform; renderer.quality=SkinQuality.Bone4;
                string materialPath=RollingArt+(i==0?"RollingBlob.mat":"RollingBlobSpikes.mat");
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
                string samplePath=RollingArt+(i==0?"RollingBlobMovement.bytes":"RollingBlobSpikesMovement.bytes");
                WriteBossSamples(animator,renderer,clips,samplePath,RollingController);
                var sampled=renderer.localBounds;
                float radius=sampled.extents.magnitude+Vector3.Distance(sampled.center,pivot);
                renderer.localBounds=new Bounds(pivot,Vector3.one*radius*2);
                var animation=renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Samples=AssetDatabase.LoadAssetAtPath<TextAsset>(samplePath);
            }
        }

        private static void RollingObstacle(string name,Transform arena,Vector3 position,Vector2Int footprint,Material material)
        {
            var root=new GameObject(name); root.transform.SetParent(arena); root.transform.position=position;
            var solid=root.AddComponent<SolidObstacleAuthoring>(); solid.footprint=footprint; solid.height=3.5f;
            Box("Stone Visual",root.transform,position+Vector3.up*1.75f,new Vector3(footprint.x,3.5f,footprint.y),material,false);
        }
    }
}
