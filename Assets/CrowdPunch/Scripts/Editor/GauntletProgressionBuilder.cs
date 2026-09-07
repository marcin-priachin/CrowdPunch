using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    /// <summary>Explicit authoring recipe for the ten-gauntlet first playtest (LOOP-002/006).</summary>
    public static class GauntletProgressionBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string Scenes = Root + "Scenes/Gauntlets/";
        private const string Waves = Root + "Data/Settings/Waves/Progression/";
        private const string Layouts = Root + "Data/GauntletLayouts/";
        private static readonly string[] ProfileNames =
        {
            "EnemySpawnSettings", "RangedEnemySpawnSettings", "ExplosiveEnemySpawnSettings",
            "EnemyDasherSpawnSettings", "EliteEnemySpawnSettings"
        };

        private sealed class Wave
        {
            public string Name;
            public int B, R, X, D, E;
            public float Delay = 3f, NextAfter = -1f, Interval = 3f;
            public int Batch;
            public EnemyWaveSettings.SpawnRectangle[] Ranges;
        }

        private sealed class Level
        {
            public string Name;
            public Vector2[] Outline;
            public Vector2 Spacing, Entry;
            public Wave[] Waves;
            public Vector4[] Lanes;
        }

        private static EnemyWaveSettings.SpawnRectangle Range(float x, float z, float width, float depth)
            => new EnemyWaveSettings.SpawnRectangle
            { Center = new Vector3(x, 2f, z), Width = width, Depth = depth };

        private static Wave W(string name, int b, EnemyWaveSettings.SpawnRectangle[] ranges,
            int r = 0, int x = 0, int d = 0, int e = 0, float delay = 3f,
            float next = -1f, int batch = 0, float interval = 3f)
            => new Wave { Name = name, B = b, R = r, X = x, D = d, E = e, Delay = delay,
                NextAfter = next, Batch = batch, Interval = interval, Ranges = ranges };

        private static Vector2[] Rectangle(float width, float depth)
            => new[] { new Vector2(-width/2, -depth/2), new Vector2(width/2, -depth/2),
                new Vector2(width/2, depth/2), new Vector2(-width/2, depth/2) };

        private static Vector2[] Clipped(float width, float depth, float cut)
            => new[] { new Vector2(-width/2+cut,-depth/2), new Vector2(width/2-cut,-depth/2),
                new Vector2(width/2,-depth/2+cut), new Vector2(width/2,depth/2-cut),
                new Vector2(width/2-cut,depth/2), new Vector2(-width/2+cut,depth/2),
                new Vector2(-width/2,depth/2-cut), new Vector2(-width/2,-depth/2+cut) };

        // Recipes are only used by the explicit rebuild command. Saved scenes and wave assets
        // are the playable content and remain independently inspector-editable afterwards.
        private static Level[] Designs() => new[]
        {
            new Level { Name="First Line", Outline=Rectangle(26,32), Spacing=new Vector2(22,28),
                Entry=new Vector2(0,-10), Lanes=new[]{new Vector4(0,0,2,26)}, Waves=new[]{
                    W("First Punch",1,new[]{Range(0,0,10,10)},delay:2),
                    W("Through The Crowd",8,new[]{Range(0,4,16,16)}),
                    W("Choose An Angle",10,new[]{Range(0,0,20,24)}) } },
            new Level { Name="Side Step", Outline=Rectangle(38,28), Spacing=new Vector2(34,24),
                Entry=new Vector2(0,-8), Lanes=new[]{new Vector4(-9,0,2,22),new Vector4(9,0,2,22),new Vector4(0,-5,30,2)}, Waves=new[]{
                    W("Left Bank",6,new[]{Range(-10,1,10,20)},delay:2,next:3),
                    W("Right Bank",6,new[]{Range(10,1,10,20)},delay:0),
                    W("Cross The Banks",14,new[]{Range(-10,0,10,20),Range(10,0,10,20)},batch:7,interval:4) } },
            new Level { Name="Far Bank", Outline=new[]{new Vector2(-17,-18),new Vector2(17,-18),new Vector2(13,18),new Vector2(-13,18)},
                Spacing=new Vector2(22,30), Entry=new Vector2(0,-11), Lanes=new[]{new Vector4(-5,0,2,28),new Vector4(5,0,2,28)}, Waves=new[]{
                    W("Ammunition",6,new[]{Range(0,-1,22,12)},delay:2,next:2),
                    W("One Shooter",0,new[]{Range(0,10,22,8)},r:1,delay:0),
                    W("Cross Shot",12,new[]{Range(0,-1,24,20)},next:3),
                    W("Two Shooters",0,new[]{Range(0,10,22,8)},r:2,delay:0) } },
            new Level { Name="Fuse Court", Outline=Clipped(40,32,6), Spacing=new Vector2(28,22),
                Entry=new Vector2(0,-9), Lanes=new[]{new Vector4(-8,0,2,22),new Vector4(8,0,2,22),new Vector4(0,5,28,2)}, Waves=new[]{
                    W("Chain Bodies",10,new[]{Range(0,-1,26,14)},delay:2,next:1),
                    W("Distant Fuse",0,new[]{Range(0,9,24,8)},x:1,delay:0),
                    W("Choose The Detonation",16,new[]{Range(0,0,30,20)},next:3),
                    W("Second Fuse",0,new[]{Range(0,-9,24,8)},x:1,delay:0) } },
            new Level { Name="Runway", Outline=Rectangle(28,44), Spacing=new Vector2(24,38),
                Entry=new Vector2(0,-13), Lanes=new[]{new Vector4(0,0,3,36),new Vector4(0,-7,22,2),new Vector4(0,7,22,2)}, Waves=new[]{
                    W("Runway Bodies",12,new[]{Range(0,0,22,24)},delay:2,next:1),
                    W("First Dasher",0,new[]{Range(0,14,22,8)},d:1,delay:0),
                    W("Reverse The Shot",18,new[]{Range(0,0,22,28)},next:4),
                    W("Return Dasher",0,new[]{Range(0,-14,22,8)},d:1,delay:0) } },
            new Level { Name="Two Problems", Outline=new[]{new Vector2(-22,-17),new Vector2(12,-17),new Vector2(22,17),new Vector2(-12,17)},
                Spacing=new Vector2(24,26), Entry=new Vector2(-4,-10), Lanes=new[]{new Vector4(-6,-5,24,2),new Vector4(6,5,24,2)}, Waves=new[]{
                    W("First Exchange",16,new[]{Range(0,0,24,22)},delay:2,next:1),
                    W("High Bank",0,new[]{Range(4,9,24,8)},r:1,delay:0,next:3),
                    W("Low Fuse",0,new[]{Range(-4,-9,24,8)},x:1,delay:0),
                    W("Second Exchange",20,new[]{Range(0,0,24,22)},next:2,batch:10),
                    W("Low Bank",0,new[]{Range(-4,-9,24,8)},r:1,delay:0,next:2),
                    W("High Fuse",0,new[]{Range(4,9,24,8)},x:1,delay:0) } },
            new Level { Name="Borrowed Fist", Outline=Rectangle(38,38), Spacing=new Vector2(30,30),
                Entry=new Vector2(0,-11), Lanes=new[]{new Vector4(0,0,3,30),new Vector4(0,0,30,3)}, Waves=new[]{
                    W("Open The Court",20,new[]{Range(0,0,30,28)},delay:2,batch:10,interval:4),
                    W("Borrowed Fist",22,new[]{Range(0,0,30,28)},e:1,delay:4) } },
            new Level { Name="Oblique Angles", Outline=Clipped(46,32,5), Spacing=new Vector2(34,22),
                Entry=new Vector2(-8,-8), Lanes=new[]{new Vector4(-10,-4,2,18),new Vector4(10,4,2,18),new Vector4(0,0,32,2)}, Waves=new[]{
                    W("Offset Bodies",18,new[]{Range(0,0,36,20)},delay:2,next:2),
                    W("Far Shooter",0,new[]{Range(3,9,28,8)},r:1,delay:0,next:2),
                    W("Near Dasher",0,new[]{Range(-3,-9,28,8)},d:1,delay:0),
                    W("Changing Banks",22,new[]{Range(0,0,36,20)},next:3,batch:11),
                    W("Crossfire",0,new[]{Range(0,9,30,8)},r:2,delay:0) } },
            new Level { Name="Relay", Outline=Clipped(44,40,7), Spacing=new Vector2(30,26),
                Entry=new Vector2(0,-12), Lanes=new[]{new Vector4(-10,0,2,26),new Vector4(10,0,2,26),new Vector4(0,7,32,2)}, Waves=new[]{
                    W("First Relay",16,new[]{Range(0,5,30,16)},delay:2,next:6),
                    W("Finite Reinforcement",10,new[]{Range(0,-8,30,12)},x:1,delay:0,next:3),
                    W("Relay Shooter",0,new[]{Range(0,12,26,8)},r:1,delay:0),
                    W("Final Relay",22,new[]{Range(0,0,32,28)},next:1),
                    W("Two Commitments",0,new[]{Range(0,11,26,10)},r:1,d:1,delay:0) } },
            new Level { Name="Crowd Punch", Outline=Clipped(48,44,6), Spacing=new Vector2(34,30),
                Entry=new Vector2(0,-13), Lanes=new[]{new Vector4(-11,1,2,30),new Vector4(11,1,2,30),new Vector4(0,0,36,3),new Vector4(0,0,3,34)}, Waves=new[]{
                    W("Commitment",24,new[]{Range(0,0,36,30)},d:1,delay:2),
                    W("Chain Reaction",28,new[]{Range(0,0,36,30)},r:1,x:1),
                    W("The Last Exchange",24,new[]{Range(0,0,36,30)},r:1,d:1,e:1,delay:4) } }
        };

        [MenuItem("Crowd Punch/Levels/Rebuild Ten Gauntlets")]
        private static void RebuildFromMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild ten gauntlets",
                    "Replace the ten generated layouts and Progression wave assets with the recorded first-playtest recipe? Inspector edits to those assets will be replaced.",
                    "Rebuild", "Cancel")) Build();
        }

        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Waves);
            Directory.CreateDirectory(Layouts);
            AssetDatabase.Refresh();
            var profiles = new EnemySpawnSettings[ProfileNames.Length];
            for (int i=0;i<profiles.Length;i++)
                profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root+"Data/Settings/Enemies/"+ProfileNames[i]+".asset")
                    ?? throw new InvalidOperationException("Missing existing enemy profile: "+ProfileNames[i]);
            Material ground = AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ground.mat");
            Material wall = MaterialAsset("GauntletWalls",new Color(0.13f,0.19f,0.24f));
            Material stripe = MaterialAsset("GauntletLanes",new Color(0.43f,0.48f,0.40f));
            Level[] designs = Designs();
            try
            {
                for (int i=0;i<designs.Length;i++) BuildLevel(i,designs[i],profiles,ground,wall,stripe);
                Scene bootstrap = EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity",OpenSceneMode.Single);
                GauntletSequence sequence = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
                var serialized = new SerializedObject(sequence);
                var names = serialized.FindProperty("levelSceneNames");
                var titles = serialized.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = designs.Length;
                var build = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(bootstrap.path,true) };
                for (int i=0;i<designs.Length;i++)
                {
                    string id = $"Gauntlet_{i+1:00}";
                    names.GetArrayElementAtIndex(i).stringValue = id;
                    titles.GetArrayElementAtIndex(i).stringValue = $"{i+1:00} {designs[i].Name}";
                    build.Add(new EditorBuildSettingsScene(Scenes+id+".unity",true));
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap);
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            Debug.Log("Authored ten gauntlets and 39 exact-composition wave assets. Enemy profiles and prefabs were only read.");
        }

        private static void BuildLevel(int index, Level design, EnemySpawnSettings[] profiles,
            Material ground, Material wall, Material stripe)
        {
            string id = $"Gauntlet_{index+1:00}";
            string directory = Scenes+id;
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            var waves = new EnemyWaveSettings[design.Waves.Length];
            for (int i=0;i<waves.Length;i++) waves[i] = WaveAsset($"CP{index+1:00}_{i+1:00}_"+design.Waves[i].Name.Replace(' ','_'),design.Waves[i],profiles);

            Scene sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(sub);
            var arena = new GameObject("Arena Bounds").AddComponent<ArenaAuthoring>();
            arena.transform.position = new Vector3(0,1,0);
            var bounds = new SerializedObject(arena);
            bounds.FindProperty("spacingSize").vector3Value = new Vector3(design.Spacing.x,16,design.Spacing.y);
            float maxX=0,maxZ=0;
            foreach(Vector2 point in design.Outline) { maxX=Mathf.Max(maxX,Mathf.Abs(point.x)); maxZ=Mathf.Max(maxZ,Mathf.Abs(point.y)); }
            bounds.FindProperty("defeatSize").vector3Value = new Vector3(2*maxX+8,18,2*maxZ+8);
            bounds.ApplyModifiedPropertiesWithoutUndo();
            var settings = new GameObject("Shared Game Settings").AddComponent<GameSettingsAuthoring>();
            var settingsData = new SerializedObject(settings);
            settingsData.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>(Root+"Data/Settings/GameRuntimeSettings.asset");
            settingsData.ApplyModifiedPropertiesWithoutUndo();
            var authoring = new GameObject("Encounter - "+design.Name).AddComponent<EnemyWaveSequenceAuthoring>();
            var sequence = new SerializedObject(authoring);
            var waveList = sequence.FindProperty("waves");
            waveList.arraySize = waves.Length;
            for(int i=0;i<waves.Length;i++) waveList.GetArrayElementAtIndex(i).objectReferenceValue=waves[i];
            sequence.FindProperty("randomSeed").longValue=101+index*997;
            sequence.FindProperty("minimumPlayerDistance").floatValue=index<2?3:8;
            sequence.FindProperty("placementAttemptsPerEnemy").intValue=32;
            sequence.ApplyModifiedPropertiesWithoutUndo();

            var layout = new GameObject("Layout - "+design.Name).transform;
            var floor = new GameObject("Continuous Convex Floor",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            floor.transform.SetParent(layout);
            Mesh mesh=FloorMesh(id,design.Outline);
            floor.GetComponent<MeshFilter>().sharedMesh=mesh;
            floor.GetComponent<MeshRenderer>().sharedMaterial=ground;
            floor.GetComponent<MeshCollider>().sharedMesh=mesh;
            for(int i=0;i<design.Outline.Length;i++)
            {
                Vector2 a=design.Outline[i], b=design.Outline[(i+1)%design.Outline.Length];
                Vector2 edge=b-a, outward=new Vector2(edge.y,-edge.x).normalized;
                Vector2 center=(a+b)*0.5f+outward*0.75f;
                GameObject rail=Box($"Perimeter {i+1:00}",layout,new Vector3(center.x,-0.25f,center.y),new Vector3(1.5f,1.5f,edge.magnitude+1.5f),wall,true);
                rail.transform.rotation=Quaternion.LookRotation(new Vector3(edge.x,0,edge.y));
                // The hybrid player's cast is centred above its transform. A low visible rail
                // needs a taller collision face to contain both that cast and launched capsules.
                rail.GetComponent<BoxCollider>().center=new Vector3(0,0.5f,0);
                rail.GetComponent<BoxCollider>().size=new Vector3(1,2,1);
            }
            foreach(Vector4 lane in design.Lanes)
                Box("Launch Lane",layout,new Vector3(lane.x,-0.985f,lane.y),new Vector3(lane.z,0.015f,lane.w),stripe,false);
            Box("Entry Apron",layout,new Vector3(design.Entry.x,-0.98f,design.Entry.y),new Vector3(4,0.02f,4),stripe,false);
            string subPath=directory+"/"+id+" Sub Scene.unity";
            EditorSceneManager.SaveScene(sub,subPath);
            EditorSceneManager.CloseScene(sub,true);

            Scene main=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(main);
            var marker=new GameObject(id+" - "+design.Name).AddComponent<GauntletLevel>();
            var entry=new GameObject("Player Entry Point").transform;
            entry.SetParent(marker.transform);
            entry.position=new Vector3(design.Entry.x,0.5f,design.Entry.y);
            var markerData=new SerializedObject(marker);
            markerData.FindProperty("playerEntryPoint").objectReferenceValue=entry;
            markerData.FindProperty("openingHint").stringValue=index==0
                ? "Move: WASD / left stick     Aim: mouse / right stick     Punch: LMB / X (Square)"
                : index==1 ? "Dash sideways: RMB / right shoulder. Aim through one body into the next." : string.Empty;
            markerData.ApplyModifiedPropertiesWithoutUndo();
            var subScene=new GameObject("Arena SubScene").AddComponent<SubScene>();
            subScene.SceneAsset=AssetDatabase.LoadAssetAtPath<SceneAsset>(subPath);
            subScene.AutoLoadScene=true;
            var light=new GameObject("Arena Light").AddComponent<Light>();
            light.type=LightType.Directional;
            light.intensity=1.2f;
            light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(50,-30,0);
            EditorSceneManager.SaveScene(main,Scenes+id+".unity");
            EditorSceneManager.CloseScene(main,true);
        }

        private static EnemyWaveSettings WaveAsset(string name, Wave recipe, EnemySpawnSettings[] profiles)
        {
            string path=Waves+name+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(path);
            bool created=asset==null;
            if(created) asset=ScriptableObject.CreateInstance<EnemyWaveSettings>();
            var data=new SerializedObject(asset);
            int[] counts={recipe.B,recipe.R,recipe.X,recipe.D};
            data.FindProperty("totalEnemyCount").intValue=recipe.B+recipe.R+recipe.X+recipe.D;
            var entries=data.FindProperty("enemies");
            entries.ClearArray();
            for(int i=0;i<counts.Length;i++)
            {
                if(counts[i]==0) continue;
                int n=entries.arraySize++;
                var item=entries.GetArrayElementAtIndex(n);
                item.FindPropertyRelative("Settings").objectReferenceValue=profiles[i];
                item.FindPropertyRelative("MinimumCount").intValue=counts[i];
                item.FindPropertyRelative("Weight").floatValue=0;
            }
            var elites=data.FindProperty("eliteEnemies");
            elites.arraySize=recipe.E>0?1:0;
            if(recipe.E>0)
            {
                elites.GetArrayElementAtIndex(0).FindPropertyRelative("Settings").objectReferenceValue=profiles[4];
                elites.GetArrayElementAtIndex(0).FindPropertyRelative("Count").intValue=recipe.E;
            }
            var ranges=data.FindProperty("spawnRectangles");
            ranges.arraySize=recipe.Ranges.Length;
            for(int i=0;i<recipe.Ranges.Length;i++)
            {
                var item=ranges.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Center").vector3Value=recipe.Ranges[i].Center;
                item.FindPropertyRelative("Width").floatValue=recipe.Ranges[i].Width;
                item.FindPropertyRelative("Depth").floatValue=recipe.Ranges[i].Depth;
            }
            data.FindProperty("delayBeforeWave").floatValue=recipe.Delay;
            data.FindProperty("activationMode").intValue=recipe.NextAfter>=0?1:2;
            data.FindProperty("duration").floatValue=Mathf.Max(0,recipe.NextAfter);
            data.FindProperty("spawnMode").intValue=recipe.Batch>0?1:0;
            data.FindProperty("batchSize").intValue=Mathf.Max(1,recipe.Batch);
            data.FindProperty("batchInterval").floatValue=recipe.Interval;
            data.ApplyModifiedPropertiesWithoutUndo();
            if(created) AssetDatabase.CreateAsset(asset,path);
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path=Layouts+name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name, color=color };
            material.SetFloat("_Smoothness",0.1f);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision)
        {
            GameObject box=GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name=name;
            box.transform.SetParent(parent);
            box.transform.position=position;
            box.transform.localScale=size;
            box.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(!collision) UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        private static Mesh FloorMesh(string id, Vector2[] outline)
        {
            string path=Layouts+id+"_Floor.asset";
            Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created=mesh==null;
            if(created) mesh=new Mesh {name=id+" Floor"};
            mesh.Clear();
            int n=outline.Length;
            var vertices=new Vector3[n*2];
            var uv=new Vector2[n*2];
            var triangles=new List<int>();
            for(int i=0;i<n;i++)
            {
                vertices[i]=new Vector3(outline[i].x,-1,outline[i].y);
                vertices[i+n]=new Vector3(outline[i].x,-2,outline[i].y);
                uv[i]=uv[i+n]=outline[i]/8f;
                int j=(i+1)%n;
                triangles.AddRange(new[]{i,j,j+n,i,j+n,i+n});
            }
            for(int i=1;i<n-1;i++) triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});
            mesh.vertices=vertices;
            mesh.uv=uv;
            mesh.triangles=triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if(created) AssetDatabase.CreateAsset(mesh,path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
