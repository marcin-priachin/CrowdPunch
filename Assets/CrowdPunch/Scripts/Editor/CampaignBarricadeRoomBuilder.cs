using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        public static readonly int[] CampaignBarricadeLevels = {13,22,34,42,56,62,74};
        private static readonly int[] BarricadeRoomWidths = {30,30,32,34,32,36,38};
        private static readonly int[] BarricadeRoomLengths = {42,40,44,44,42,44,46};
        private const string BarricadeRoomHint = "Break every barricade with launched bodies or explosions. Push through each room, then reach the green exit beyond the final gate.";

        [MenuItem("Crowd Punch/Campaign/Apply Progressive Barricade Rooms")]
        public static void ApplyProgressiveBarricadeRooms()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int i=1;i<CampaignBarricadeLevels.Length;i++)
                {
                    int level=CampaignBarricadeLevels[i];
                    var sub=EditorSceneManager.OpenScene(CampaignSub(level));
                    AuthorBarricadeRooms(sub,level,i);
                    EditorSceneManager.MarkSceneDirty(sub);EditorSceneManager.SaveScene(sub);AssetDatabase.SaveAssets();
                    GauntletNatureEnvironment.ApplyCampaignLevel(level);
                    SetCampaignHint(level,BarricadeRoomHint);
                }
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
            Debug.Log("BARRICADE-007: levels 13/22/34/42/56/62/74 now have 1/2/3/4/5/6/7 room-and-gate segments.");
        }

        private static void AuthorBarricadeRooms(Scene scene,int number,int index)
        {
            int count=index+1,width=BarricadeRoomWidths[index],length=BarricadeRoomLengths[index];
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            foreach(var root in scene.GetRootGameObjects().Where(g=>g.name.StartsWith("Room barricade ")||g.name.StartsWith("Room crowd ")||g.name.StartsWith("Room hazard ")).ToArray())
                UnityEngine.Object.DestroyImmediate(root);
            foreach(var rock in arena.GetComponentsInChildren<SolidObstacleAuthoring>().Where(o=>o.name.Contains(" room ")).ToArray())
                UnityEngine.Object.DestroyImmediate(rock.gameObject);
            var encounter=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var gate=encounter.barricade;
            gate.completeOnDestruction=false;
            gate.exit=scene.GetRootGameObjects().Single(g=>g.name=="Exit").transform;
            gate.exit.position=new Vector3(number==62?8:0,.5f,number==62?18:16);
            var exit=gate.exit;
            var originalWave=encounter.Waves.Single();
            var solids=arena.GetComponentsInChildren<SolidObstacleAuthoring>();
            var hazards=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
            var layout=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Layout - ")).transform;
            foreach(var edge in layout.Cast<Transform>().Where(t=>t.name.StartsWith("Perimeter ")).ToArray())UnityEngine.Object.DestroyImmediate(edge.gameObject);
            float offset=(count-1)*length*.5f,totalLength=count*length;
            var outline=Rectangle(width,totalLength).Select(p=>p+new Vector2(0,offset)).ToArray();
            var floor=layout.Find("Continuous Convex Floor");
            var mesh=FloorMesh($"Campaign_{number:00}",outline,CampaignData+"Layouts/");
            floor.GetComponent<MeshFilter>().sharedMesh=mesh;floor.GetComponent<MeshCollider>().sharedMesh=mesh;
            for(int i=0;i<outline.Length;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Length];var delta=b-a;
                var p=(a+b)*.5f+new Vector2(delta.y,-delta.x).normalized*.75f;
                var edge=Box($"Perimeter {i+1:00}",layout,new Vector3(p.x,-.25f,p.y),new Vector3(1.5f,1.5f,delta.magnitude+1.5f),MaterialAsset("GauntletWalls",Color.gray),true);
                edge.transform.rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.y));
                edge.GetComponent<BoxCollider>().center=new Vector3(0,.5f,0);edge.GetComponent<BoxCollider>().size=new Vector3(1,2,1);
            }
            var bounds=new SerializedObject(arena);
            // Odd width aligns navigation cell centres with each integer-x, 2m gate opening.
            bounds.FindProperty("spacingSize").vector3Value=new Vector3(width-3,16,totalLength-4);
            bounds.FindProperty("spacingCenterOffset").vector3Value=new Vector3(0,0,offset);
            bounds.FindProperty("defeatSize").vector3Value=new Vector3(width+8,18,totalLength+8);
            bounds.FindProperty("defeatCenterOffset").vector3Value=new Vector3(0,0,offset);
            bounds.ApplyModifiedPropertiesWithoutUndo();
            var navigation=arena.GetComponent<NavigationArenaAuthoring>();
            navigation.settings=CampaignSettings<NavigationSettings>("NavigationSettings",$"L{number:00}_RoomsNavigation");
            navigation.settings.maximumPathLength=Mathf.Clamp(Mathf.CeilToInt(totalLength/navigation.settings.cellSize)+32,128,512);
            EditorUtility.SetDirty(navigation.settings);
            // Preserve the original first-room participation anchor and entry.
            var gates=new BarricadeAuthoring[count];var crowds=new EnemyWaveSequenceAuthoring[count];
            gates[0]=gate;crowds[0]=encounter;
            for(int room=1;room<count;room++)
            {
                Vector3 shift=Vector3.forward*(room*length);
                var next=UnityEngine.Object.Instantiate(gate.gameObject);next.name=$"Room barricade {room+1:00}";
                next.transform.SetParent(null,true);next.transform.position=gate.transform.position+shift;
                gates[room]=next.GetComponent<BarricadeAuthoring>();
                foreach(var visual in next.GetComponentsInChildren<BarricadeVisualAuthoring>())visual.barricade=gates[room];
                foreach(var rock in solids)
                {
                    var copy=UnityEngine.Object.Instantiate(rock.gameObject,arena.transform);copy.name=rock.name+$" room {room+1:00}";copy.transform.position=rock.transform.position+shift;
                }
                var crowd=UnityEngine.Object.Instantiate(encounter.gameObject);crowd.name=$"Room crowd {room+1:00}";
                var policy=crowd.GetComponent<GroundHazardPolicyAuthoring>();if(policy!=null)UnityEngine.Object.DestroyImmediate(policy);
                crowds[room]=crowd.GetComponent<EnemyWaveSequenceAuthoring>();crowds[room].barricade=gates[room];
                string wavePath=CampaignData+$"Waves/CP{number:00}_Room{room+1:00}_Ammunition.asset";
                var wave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(wavePath);
                if(wave==null)
                {
                    if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(originalWave),wavePath))throw new InvalidOperationException("Could not copy room ammunition.");
                    wave=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(wavePath);
                }
                EditorUtility.CopySerialized(originalWave,wave);wave.name=$"CP{number:00}_Room{room+1:00}_Ammunition";
                var data=new SerializedObject(wave);var ranges=data.FindProperty("spawnRectangles");
                for(int i=0;i<ranges.arraySize;i++)ranges.GetArrayElementAtIndex(i).FindPropertyRelative("Center").vector3Value+=shift;
                data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(wave);
                var authored=new SerializedObject(crowds[room]);authored.FindProperty("waves").GetArrayElementAtIndex(0).objectReferenceValue=wave;
                authored.FindProperty("randomSeed").longValue=encounter.RandomSeed+room*7919;authored.ApplyModifiedPropertiesWithoutUndo();
                foreach(var hazard in hazards)
                {
                    var copy=UnityEngine.Object.Instantiate(hazard.gameObject);copy.name=$"Room hazard {room+1:00} - {hazard.name}";
                    copy.transform.position=hazard.transform.position+shift;copy.GetComponent<GroundHazardAuthoring>().sequence=crowds[room];
                }
            }
            exit.position+=Vector3.forward*((count-1)*length);
            for(int room=0;room<count;room++)
            {
                gates[room].completeOnDestruction=room<count-1;gates[room].exit=room==count-1?exit:null;
                var activation=crowds[room].GetComponent<BarricadeRoomAuthoring>();if(activation==null)activation=crowds[room].gameObject.AddComponent<BarricadeRoomAuthoring>();
                activation.previousGate=room==0?null:gates[room-1];
                activation.minimum=new Vector2(-width*.5f+1,room==0?-length*.5f:gates[room-1].transform.position.z+.7f);
                activation.maximum=new Vector2(width*.5f-1,gates[room].transform.position.z+.7f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
