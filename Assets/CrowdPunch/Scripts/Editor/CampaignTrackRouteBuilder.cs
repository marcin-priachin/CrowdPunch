using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        public static readonly int[] CampaignTrackLevels = { 16, 19, 26, 32, 39, 46, 54, 69 };

        [MenuItem("Crowd Punch/Campaign/Apply Progressive Track Routes")]
        public static void ApplyProgressiveTrackRoutes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // The introduction retains its original 10m straight rail and five-hit tuning.
                for (int i = 1; i < CampaignTrackLevels.Length; i++)
                {
                    int number = CampaignTrackLevels[i];
                    var scene = EditorSceneManager.OpenScene(CampaignSub(number));
                    AuthorProgressiveTrackRoute(scene, number, i + 1);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    GauntletNatureEnvironment.ApplyCampaignLevel(number);
                    var main = EditorSceneManager.OpenScene($"{CampaignScenes}Campaign_{number:00}.unity");
                    var marker = UnityEngine.Object.FindFirstObjectByType<GauntletLevel>();
                    var entry = new SerializedObject(marker);
                    var start = ProgressiveTrackPoints(i + 1)[0];
                    ((Transform)entry.FindProperty("playerEntryPoint").objectReferenceValue)
                        .SetPositionAndRotation(start + new Vector3(0, -.2f, -6), Quaternion.identity);
                    entry.FindProperty("openingHint").stringValue = "Launch bodies or blast the block along the turning arrows into the blue socket. Change your shooting side at each corner; opposite hits push it back.";
                    entry.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("TRACK-006: campaign rails now progress from 10 to 80 metres, with 2m hits and alternating left/right turns.");
        }

        public static Vector3[] ProgressiveTrackPoints(int legs)
        {
            var points = new Vector3[legs + 1];
            int side = legs % 2 == 0 ? -1 : 1;
            for (int i = 0; i < legs; i++)
                points[i + 1] = points[i] + (i % 2 == 0 ? Vector3.forward : Vector3.right * side) * 10;
            Vector3 center = (points[0] + points[legs]) * .5f;
            for (int i = 0; i < points.Length; i++) points[i] = points[i] - center + Vector3.up * .7f;
            return points;
        }

        private static void AuthorProgressiveTrackRoute(Scene scene, int number, int legs)
        {
            var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
            var track = UnityEngine.Object.FindFirstObjectByType<TrackObjectAuthoring>();
            var crowd = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
            var layout = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Layout - ")).transform;
            var points = ProgressiveTrackPoints(legs);
            var span = points[legs] - points[0];
            float width = Mathf.Max(34, Mathf.Abs(span.x) + 22), depth = Mathf.Max(40, span.z + 22);
            var outline = Clipped(width, depth, legs % 3 + 3);

            foreach (var solid in UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(solid.gameObject);
            foreach (var rail in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Track rail" || t.name == "Direction arrow").ToArray())
                UnityEngine.Object.DestroyImmediate(rail.gameObject);
            foreach (var child in layout.Cast<Transform>().Where(t => t.name.StartsWith("Perimeter ")).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            if (track.corners != null) foreach (var corner in track.corners)
                if (corner != null) UnityEngine.Object.DestroyImmediate(corner.gameObject);
            var cornerRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Track route corners");
            if (cornerRoot != null) UnityEngine.Object.DestroyImmediate(cornerRoot);

            var floor = layout.Find("Continuous Convex Floor");
            var mesh = FloorMesh($"Campaign_{number:00}", outline, CampaignData + "Layouts/");
            floor.GetComponent<MeshFilter>().sharedMesh = mesh; floor.GetComponent<MeshCollider>().sharedMesh = mesh;
            var wallMaterial = MaterialAsset("GauntletWalls", Color.gray);
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length]; var edge = b - a;
                var center = (a + b) * .5f + new Vector2(edge.y, -edge.x).normalized * .75f;
                var wall = Box($"Perimeter {i+1:00}", layout, new Vector3(center.x,-.25f,center.y), new Vector3(1.5f,1.5f,edge.magnitude+1.5f), wallMaterial, true);
                wall.transform.rotation = Quaternion.LookRotation(new Vector3(edge.x,0,edge.y));
                wall.GetComponent<BoxCollider>().center = new Vector3(0,.5f,0); wall.GetComponent<BoxCollider>().size = new Vector3(1,2,1);
            }
            var bounds = new SerializedObject(arena);
            bounds.FindProperty("spacingSize").vector3Value = new Vector3(width-4,16,depth-4);
            bounds.FindProperty("defeatSize").vector3Value = new Vector3(width+8,18,depth+8);
            bounds.ApplyModifiedPropertiesWithoutUndo();
            var navigation = arena.GetComponent<NavigationArenaAuthoring>();
            navigation.overrideParticipationAnchor = true; navigation.participationAnchor = new Vector2(points[0].x,points[0].z-6);
            layout.Find("Entry Apron").position = new Vector3(points[0].x,-.98f,points[0].z-6);

            // Keep the solid's orientation fixed while it follows the route, preserving swept-box collision semantics.
            track.transform.SetPositionAndRotation(points[0], Quaternion.identity);
            var cornerParent = new GameObject("Track route corners").transform;
            track.corners = new Transform[legs-1];
            for (int i = 1; i < legs; i++)
            {
                var corner = new GameObject($"Corner {i:00}").transform;
                corner.SetParent(cornerParent); corner.position = points[i]; track.corners[i-1] = corner;
            }
            Vector3 lastDirection = (points[legs]-points[legs-1]).normalized;
            // The legacy socket's child offsets are authored about its original world z=5 pose.
            track.destination.SetPositionAndRotation(new Vector3(0,.7f,5), Quaternion.identity);
            track.destination.SetPositionAndRotation(points[legs], Quaternion.LookRotation(lastDirection));
            track.settings.requiredNetHits = legs * 5; EditorUtility.SetDirty(track.settings);

            var metal = AssetDatabase.LoadAssetAtPath<Material>(Layouts+"TrackMetal.mat");
            var arrowMaterial = AssetDatabase.LoadAssetAtPath<Material>(Layouts+"TrackArrow.mat");
            for (int i = 0; i < legs; i++)
            {
                Vector3 forward = (points[i+1]-points[i]).normalized, right = new Vector3(forward.z,0,-forward.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    var railStart = TrackRailVertex(points, i, side);
                    var railEnd = TrackRailVertex(points, i + 1, side);
                    var rail = Box("Track rail", arena.transform, (railStart+railEnd)*.5f+Vector3.up*(-1.65f), new Vector3(.18f,.08f,Vector3.Distance(railStart,railEnd)),metal,false);
                    rail.transform.rotation = Quaternion.LookRotation(railEnd-railStart);
                    for (int mark = 1; mark <= 4; mark++) for (int arm = -1; arm <= 1; arm += 2)
                    {
                        var chevron = Box("Direction arrow",arena.transform,points[i]+forward*(mark*2)+right*(side*2.6f+arm*.28f)+Vector3.up*(-1.64f),new Vector3(.16f,.06f,1.1f),arrowMaterial,false);
                        chevron.transform.rotation = Quaternion.LookRotation(forward)*Quaternion.Euler(0,arm*-35,0);
                    }
                }
            }
            foreach (var w in crowd.Waves)
            {
                var serialized = new SerializedObject(w); var ranges = serialized.FindProperty("spawnRectangles"); ranges.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var r = ranges.GetArrayElementAtIndex(i);
                    r.FindPropertyRelative("Center").vector3Value = new Vector3((i==0?-1:1)*(width*.5f-5),2,0);
                    r.FindPropertyRelative("Width").floatValue = 4; r.FindPropertyRelative("Depth").floatValue = depth-24;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var patches = UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None).OrderBy(p=>p.cycleOffset).ToArray();
            int hand = legs % 2 == 0 ? -1 : 1;
            for (int i = 0; i < patches.Length; i++)
                patches[i].transform.position = new Vector3((i==0?hand:-hand)*(width*.5f-6), patches[i].transform.position.y, (i==0?-1:1)*(depth*.5f-6));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Vector3 TrackRailVertex(Vector3[] points, int index, int side)
        {
            var before = (points[Mathf.Max(1,index)]-points[Mathf.Max(0,index-1)]).normalized;
            var after = (points[Mathf.Min(points.Length-1,index+1)]-points[Mathf.Min(points.Length-2,index)]).normalized;
            var rightBefore = new Vector3(before.z,0,-before.x);
            var rightAfter = new Vector3(after.z,0,-after.x);
            // Offset both rails to their shared miter vertex so the visible line stays
            // continuous around each turn instead of leaving gaps beside the block.
            return points[index] + (rightBefore+rightAfter)*(side*1.55f/(1+Vector3.Dot(rightBefore,rightAfter)));
        }
    }
}
