using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Movement;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    /// <summary>Explicit reproducible authoring of BOSS-001..007. Never rewrites gauntlets 1-10.</summary>
    public static class BossGauntletBuilder
    {
        private const string Root="Assets/CrowdPunch/";
        private const string Art=Root+"Data/Boss/";
        public const string ScenePath=Root+"Scenes/Gauntlets/Gauntlet_11.unity";
        public const string SubPath=Root+"Scenes/Gauntlets/Gauntlet_11/Gauntlet_11 Sub Scene.unity";
        public const string SettingsPath=Root+"Data/Settings/BossEncounterSettings.asset";
        public const string WavePath=Root+"Data/Settings/Waves/Progression/CP11_Boss_Crowd.asset";

        [MenuItem("Crowd Punch/Levels/Build Boss Gauntlet 11")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Path.GetDirectoryName(SubPath)); AssetDatabase.Refresh();
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<BossEncounterSettings>(SettingsPath);
                if(settings==null) { settings=ScriptableObject.CreateInstance<BossEncounterSettings>(); AssetDatabase.CreateAsset(settings,SettingsPath); }
                var wave=CreateWave();
                var gold=Material("Hand Gold",new Color(.9f,.62f,.25f));
                var cuff=Material("Hand Cuffs",new Color(.3f,.14f,.47f));
                var lane=Material("Boss Court Lines",new Color(.55f,.48f,.3f));
                var wall=AssetDatabase.LoadAssetAtPath<Material>(Root+"Data/GauntletLayouts/GauntletWalls.mat");
                var floor=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat");
                var headMesh=HeadMesh(out Material headMaterial);
                Scene sub=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(sub);
                var arena=new GameObject("Boss Arena Bounds").AddComponent<ArenaAuthoring>(); arena.transform.position=new Vector3(0,1,0);
                var arenaData=new SerializedObject(arena); arenaData.FindProperty("spacingSize").vector3Value=new Vector3(36,16,32);
                arenaData.FindProperty("defeatSize").vector3Value=new Vector3(54,22,50); arenaData.ApplyModifiedPropertiesWithoutUndo();
                var game=new GameObject("Shared Game Settings").AddComponent<GameSettingsAuthoring>();
                var gd=new SerializedObject(game); gd.FindProperty("settings").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>(Root+"Data/Settings/GameRuntimeSettings.asset"); gd.ApplyModifiedPropertiesWithoutUndo();
                Box("Arena Floor",new Vector3(0,-1.3f,0),new Vector3(44,.6f,40),floor,true);
                Box("North Rail",new Vector3(0,0,20.75f),new Vector3(47,2,1.5f),wall,true);
                Box("South Rail",new Vector3(0,0,-20.75f),new Vector3(47,2,1.5f),wall,true);
                Box("East Rail",new Vector3(22.75f,0,0),new Vector3(1.5f,2,40),wall,true);
                Box("West Rail",new Vector3(-22.75f,0,0),new Vector3(1.5f,2,40),wall,true);
                Box("Backdrop",new Vector3(0,-2,0),new Vector3(200,.1f,200),wall,false);
                Box("Center Lane",new Vector3(0,-.985f,0),new Vector3(1,.02f,32),lane,false);
                Box("Cross Lane",new Vector3(0,-.985f,0),new Vector3(36,.02f,1),lane,false);
                var crowd=new GameObject("Boss Supporting Crowd").AddComponent<EnemyWaveSequenceAuthoring>();
                var wd=new SerializedObject(crowd); wd.FindProperty("waves").arraySize=1; wd.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave;
                wd.FindProperty("minimumPlayerDistance").floatValue=5; wd.FindProperty("placementAttemptsPerEnemy").intValue=40; wd.FindProperty("randomSeed").longValue=11011; wd.ApplyModifiedPropertiesWithoutUndo();
                var head=new GameObject("Boss Head - Orc"); var encounter=head.AddComponent<BossEncounterAuthoring>(); encounter.settings=settings; encounter.crowd=crowd; crowd.bossEncounter=encounter;
                var t=settings.Bake(); head.transform.position=BossPerimeterRoute.Position(0,t);
                Vector3 inward=(new Vector3(t.Center.x,0,t.Center.y)-head.transform.position); inward.y=0; inward.Normalize();
                head.transform.rotation=Quaternion.LookRotation(inward);
                var hp=head.AddComponent<BossPartAuthoring>(); hp.encounter=encounter; hp.kind=BossPartKind.Head; hp.radius=2; hp.height=4;
                var visual=new GameObject("Orc Head Visual",typeof(MeshFilter),typeof(MeshRenderer)); visual.transform.SetParent(head.transform,false);
                visual.GetComponent<MeshFilter>().sharedMesh=headMesh; visual.GetComponent<MeshRenderer>().sharedMaterial=headMaterial;
                encounter.leftHand=Hand("Left Detached Fist",BossPartKind.LeftHand,encounter,head.transform.position-head.transform.right*t.OpenOffset+inward,gold,cuff);
                encounter.rightHand=Hand("Right Detached Fist",BossPartKind.RightHand,encounter,head.transform.position+head.transform.right*t.OpenOffset+inward,gold,cuff);
                EditorSceneManager.SaveScene(sub,SubPath); EditorSceneManager.CloseScene(sub,true);
                Scene main=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(main);
                var marker=new GameObject("Gauntlet 11 - The Gatekeeper").AddComponent<GauntletLevel>();
                var entry=new GameObject("Player Entry Point").transform; entry.SetParent(marker.transform); entry.position=new Vector3(0,.5f,-10);
                var md=new SerializedObject(marker); md.FindProperty("playerEntryPoint").objectReferenceValue=entry;
                md.FindProperty("openingHint").stringValue="Launch bodies at the head. Dodge the hands, then use their recovery."; md.ApplyModifiedPropertiesWithoutUndo();
                var ss=new GameObject("Boss Arena SubScene").AddComponent<SubScene>(); ss.SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath); ss.AutoLoadScene=true;
                var light=new GameObject("Arena Light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.2f; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(50,-30,0);
                EditorSceneManager.SaveScene(main,ScenePath); EditorSceneManager.CloseScene(main,true);
                Scene bootstrap=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity",OpenSceneMode.Single);
                var sequence=UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
                var sd=new SerializedObject(sequence); var names=sd.FindProperty("levelSceneNames"); var titles=sd.FindProperty("levelDisplayNames");
                if(names.arraySize!=10 && names.arraySize!=11) throw new InvalidOperationException("Expected ten gauntlets before the boss.");
                names.arraySize=titles.arraySize=11; names.GetArrayElementAtIndex(10).stringValue="Gauntlet_11"; titles.GetArrayElementAtIndex(10).stringValue="11 The Gatekeeper";
                sd.ApplyModifiedPropertiesWithoutUndo();
                if(sequence.GetComponent<BossAttackTelegraphs>()==null) sequence.gameObject.AddComponent<BossAttackTelegraphs>();
                EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var scenes=EditorBuildSettings.scenes.ToList();
                if(!scenes.Any(s=>s.path==ScenePath)) scenes.Insert(Math.Min(11,scenes.Count),new EditorBuildSettingsScene(ScenePath,true));
                else scenes.First(s=>s.path==ScenePath).enabled=true;
                EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            Debug.Log("Boss gauntlet 11 authored. Original FBX and gauntlets 1-10 preserved.");
        }

        private static EnemyWaveSettings CreateWave()
        {
            var existing=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(WavePath); if(existing!=null) return existing;
            var w=ScriptableObject.CreateInstance<EnemyWaveSettings>(); var d=new SerializedObject(w);
            d.FindProperty("totalEnemyCount").intValue=13; var entries=d.FindProperty("enemies"); entries.arraySize=2;
            string[] profiles={"EnemySpawnSettings","RangedEnemySpawnSettings"}; int[] counts={12,1};
            for(int i=0;i<2;i++) { var e=entries.GetArrayElementAtIndex(i); e.FindPropertyRelative("Settings").objectReferenceValue=AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+profiles[i]+".asset"); e.FindPropertyRelative("MinimumCount").intValue=counts[i]; e.FindPropertyRelative("Weight").floatValue=0; }
            var ranges=d.FindProperty("spawnRectangles"); ranges.arraySize=1; var r=ranges.GetArrayElementAtIndex(0);
            r.FindPropertyRelative("Center").vector3Value=new Vector3(0,2,0); r.FindPropertyRelative("Width").floatValue=32; r.FindPropertyRelative("Depth").floatValue=28;
            d.FindProperty("spawnMode").intValue=0; d.FindProperty("delayBeforeWave").floatValue=1;
            d.FindProperty("replenishWhileBossLives").boolValue=true; d.FindProperty("bossReplenishDelay").floatValue=4;
            d.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(w,WavePath); return w;
        }

        private static BossPartAuthoring Hand(string name,BossPartKind kind,BossEncounterAuthoring encounter,Vector3 position,Material gold,Material cuff)
        {
            var go=new GameObject(name); position.y=encounter.settings.handHeight; go.transform.position=position; go.transform.rotation=encounter.transform.rotation;
            var part=go.AddComponent<BossPartAuthoring>(); part.encounter=encounter; part.kind=kind; part.radius=1.3f; part.height=2.6f;
            Primitive("Palm",go.transform,new Vector3(0,0,0),new Vector3(2.4f,2,1.9f),gold);
            for(int i=0;i<4;i++) Primitive("Knuckle "+i,go.transform,new Vector3((i-1.5f)*.52f,.42f,.75f),new Vector3(.67f,.85f,.7f),gold);
            Primitive("Thumb",go.transform,new Vector3(kind==BossPartKind.LeftHand?1.05f:-1.05f,-.3f,.15f),new Vector3(.75f,1.3f,.85f),gold);
            Primitive("Cuff",go.transform,new Vector3(0,-.05f,-.8f),new Vector3(2.1f,1.75f,.6f),cuff);
            return part;
        }
        private static void Primitive(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name=name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=scale; go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static void Box(string name,Vector3 p,Vector3 size,Material mat,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=p; go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            else if(name.EndsWith("Rail",StringComparison.Ordinal))
            {
                // The hybrid player's sweep is elevated above its feet, so low visual rails need taller collision.
                var collider=go.GetComponent<BoxCollider>(); collider.center=new Vector3(0,.4f,0); collider.size=new Vector3(1,1.8f,1);
            }
        }
        private static Material Material(string name,Color color)
        {
            string path=Art+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat!=null) return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name,enableInstancing=true }; mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",.3f); AssetDatabase.CreateAsset(mat,path); return mat;
        }
        private static Mesh HeadMesh(out Material material)
        {
            const string source=Root+"Models/UltimateMonsters/Blob/Orc.fbx";
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try
            {
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                string matPath=Art+"Orc Head.mat"; material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(material==null) { material=new Material(skin.sharedMaterial) { enableInstancing=true }; material.color=new Color(.52f,.77f,.43f); AssetDatabase.CreateAsset(material,matPath); }
                var baked=new Mesh(); skin.BakeMesh(baked);
                var vertices=baked.vertices;
                for(int i=0;i<vertices.Length;i++) vertices[i]=skin.transform.TransformPoint(vertices[i]);
                baked.vertices=vertices; baked.RecalculateBounds();
                Bounds bounds=baked.bounds; float scale=4.4f/bounds.size.y;
                for(int i=0;i<vertices.Length;i++) vertices[i]=(vertices[i]-bounds.center)*scale+new Vector3(0,.2f,0);
                baked.vertices=vertices; baked.RecalculateNormals(); baked.RecalculateBounds(); baked.name="Orc Head Mesh";
                string path=Art+"Orc Head Mesh.asset";
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old!=null) { EditorUtility.CopySerialized(baked,old); UnityEngine.Object.DestroyImmediate(baked); return old; }
                AssetDatabase.CreateAsset(baked,path); return baked;
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
    }
}
