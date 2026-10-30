using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
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
        public const string ChickenSettingsPath=Root+"Data/Settings/ChickenBossSettings.asset";
        public const string ChickenWavePath=Waves+"CP20_01_Chicken_Crowd.asset";
        private const string ChickenArt=Root+"Data/ChickenBoss/";

        [MenuItem("Crowd Punch/Levels/Build Chicken Boss Gauntlet 20")]
        public static void BuildChicken()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(ChickenArt); AssetDatabase.Refresh();
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<ChickenBossSettings>(ChickenSettingsPath);
                if(settings==null) { settings=ScriptableObject.CreateInstance<ChickenBossSettings>(); AssetDatabase.CreateAsset(settings,ChickenSettingsPath); }
                var projectile=ChickenProjectilePrefab();
                var profiles=new EnemySpawnSettings[ProfileNames.Length];
                for(int i=0;i<profiles.Length;i++) profiles[i]=AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+ProfileNames[i]+".asset");
                var oldWave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(ChickenWavePath);
                string savedWave=oldWave!=null?EditorJsonUtility.ToJson(oldWave):null;
                var design=new Level { Name="Chicken Run", Outline=new[] { new Vector2(-18,-18),new Vector2(18,-18),new Vector2(18,18),new Vector2(-18,18) },
                    Spacing=new Vector2(32,32),Entry=new Vector2(0,-12),Lanes=Array.Empty<Vector4>(),
                    Waves=new[] { new Wave { Name="Chicken Crowd",B=12,Delay=1.5f,Ranges=new[] { Range(0,0,28,28) } } } };
                BuildLevel(19,design,profiles,AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls",new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes",new Color(.43f,.48f,.4f)),MaterialAsset("GauntletBackdrop",new Color(.08f,.105f,.13f)));
                var wave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(ChickenWavePath);
                if(savedWave!=null) EditorJsonUtility.FromJsonOverwrite(savedWave,wave);
                else
                {
                    var wd=new SerializedObject(wave); wd.FindProperty("replenishWhileBossLives").boolValue=true;
                    wd.FindProperty("bossReplenishDelay").floatValue=3; wd.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(wave);
                var sub=EditorSceneManager.OpenScene(Scenes+"Gauntlet_20/Gauntlet_20 Sub Scene.unity",OpenSceneMode.Single);
                var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings=AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root+"Data/Settings/NavigationSettings.asset");
                var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var crowdData=new SerializedObject(crowd); crowdData.FindProperty("minimumPlayerDistance").floatValue=4; crowdData.ApplyModifiedPropertiesWithoutUndo();
                var root=new GameObject("Chicken Boss");
                root.transform.SetPositionAndRotation(new Vector3(0,.7f,10),Quaternion.Euler(0,180,0));
                var chicken=root.AddComponent<ChickenBossAuthoring>(); chicken.settings=settings; chicken.projectilePrefab=projectile;
                chicken.arenaHalfSize=new Vector2(18,18); crowd.chickenBoss=chicken;
                AddChickenModel(root,settings);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main=EditorSceneManager.OpenScene(Scenes+"Gauntlet_20.unity",OpenSceneMode.Single);
                var marker=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue="Punch the bouncing shots back, or launch enemies at the Chicken. Returned shots and launched bodies still hurt you.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity",OpenSceneMode.Single);
                var sequence=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names=sequence.FindProperty("levelSceneNames"); var titles=sequence.FindProperty("levelDisplayNames");
                names.arraySize=titles.arraySize=Math.Max(20,names.arraySize);
                names.GetArrayElementAtIndex(19).stringValue="Gauntlet_20"; titles.GetArrayElementAtIndex(19).stringValue="20 Chicken Run";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s=>s.path==Scenes+"Gauntlet_20.unity");
                int last=build.FindIndex(s=>s.path==Scenes+"Gauntlet_19.unity");
                build.Insert(last+1,new EditorBuildSettingsScene(Scenes+"Gauntlet_20.unity",true)); EditorBuildSettings.scenes=build.ToArray();
                GauntletNatureEnvironment.ApplyLevel(20);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static GameObject ChickenProjectilePrefab()
        {
            string path=ChickenArt+"ChickenProjectile.prefab";
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(old!=null) return old;
            var root=GameObject.CreatePrimitive(PrimitiveType.Sphere); root.name="ChickenProjectile";
            try
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
                var mat=new Material(Shader.Find("CrowdPunch/Cel/Environment")) { enableInstancing=true };
                mat.color=Color.white; mat.SetFloat("_Smoothness",.7f); AssetDatabase.CreateAsset(mat,ChickenArt+"Projectile.mat");
                root.GetComponent<MeshRenderer>().sharedMaterial=mat; root.AddComponent<ChickenProjectileAuthoring>();
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        internal static void AddChickenModel(GameObject root,ChickenBossSettings settings)
        {
            ChickenModelBuilder.AddModel(root, settings);
        }
        internal static void WriteBossSamples(Animator animator,SkinnedMeshRenderer renderer,AnimationClip[] clips,string path,string controllerPath, bool humanoid = false, bool inPlaceTurns = false, int reverseMotion = -1, bool groundEachFrame = false)
        {
            const int frames=32;
            animator.Rebind();
            var matrices=new Matrix4x4[EnemyAnimationSamples.MotionCount*frames*renderer.bones.Length];
            var durations=new float[EnemyAnimationSamples.MotionCount]; var bindposes=renderer.sharedMesh.bindposes;
            var vertices=renderer.sharedMesh.vertices; var weights=renderer.sharedMesh.boneWeights;
            Bounds bounds=default; bool hasBounds=false;
            for(int motion=0;motion<EnemyAnimationSamples.MotionCount;motion++)
            {
                var clip=clips[Math.Min(motion,clips.Length-1)]; durations[motion]=clip.length;
                float initialYaw=0;
                for(int frame=0;frame<frames;frame++)
                {
                    float phase=frame/(float)(motion<2?frames:frames-1);
                    if (motion == reverseMotion) phase = 1 - phase;
                    if (humanoid) { animator.Play("Motion" + Math.Min(motion, clips.Length - 1), 0, phase); animator.Update(0f); }
                    else clip.SampleAnimation(animator.gameObject,phase*clip.length);
                    var correction=Matrix4x4.identity;
                    if(inPlaceTurns && (motion==6 || motion==7))
                    {
                        // Remove authored body-yaw change; physics owns facing. Retain only the turn steps.
                        Vector3 forward=Quaternion.Inverse(animator.transform.rotation)*animator.bodyRotation*Vector3.forward;
                        float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
                        if(frame==0) initialYaw=yaw;
                        correction=Matrix4x4.Rotate(Quaternion.Euler(0,-Mathf.DeltaAngle(initialYaw,yaw),0));
                    }
                    int start=(motion*frames+frame)*renderer.bones.Length;
                    for(int bone=0;bone<renderer.bones.Length;bone++)
                        matrices[start+bone]=correction*animator.transform.worldToLocalMatrix*renderer.bones[bone].localToWorldMatrix*bindposes[bone];
                    float support = 0;
                    if (groundEachFrame)
                    {
                        // Translate below-floor authored geometry; never resize the supplied model.
                        for (int i=0; i<vertices.Length; i++)
                        {
                            var w=weights[i]; var v=vertices[i];
                            float y=(matrices[start+w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[start+w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                                +matrices[start+w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[start+w.boneIndex3].MultiplyPoint3x4(v)*w.weight3).y;
                            support=Mathf.Max(support,-y);
                        }
                        for (int bone=0; bone<renderer.bones.Length; bone++) matrices[start+bone][1,3]+=support;
                    }
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i]; var v=vertices[i];
                        Vector3 p=matrices[start+w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[start+w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                            +matrices[start+w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[start+w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                        if(!hasBounds) { bounds=new Bounds(p,Vector3.zero); hasBounds=true; } else bounds.Encapsulate(p);
                    }
                }
            }
            string hash=AssetDatabase.GetAssetDependencyHash(controllerPath).ToString()
                +AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                +AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x43504133); writer.Write(hash); writer.Write(renderer.bones.Length); writer.Write(frames);
                foreach(var bone in renderer.bones) writer.Write(bone.name);
                foreach(var d in durations) writer.Write(d);
                foreach(var m in matrices) for(int col=0;col<4;col++) for(int row=0;row<3;row++) writer.Write(m[row,col]);
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            bounds.Expand(.2f); renderer.localBounds=bounds;
            if (humanoid) { animator.Play("Motion0", 0, 0); animator.Update(0f); }
            else clips[0].SampleAnimation(animator.gameObject,0);
        }
    }
}
