using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CrowdPunch.Editor.ChapterFourObservatoryAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: fit fixed roof geometry in each building's axes, including rotated galleries.
    public static class ChapterFourObservatoryRoofs
    {
        internal const float Overhang = .12f;

        internal static void FitGallery(Transform gallery)
        {
            var masonry=gallery.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_",StringComparison.Ordinal)
                || t.name=="Recessed intact gallery back" || t.name=="Cloister return wall");
            var points=masonry.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>())
                .SelectMany(f=>f.sharedMesh.vertices.Select(v=>gallery.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            if(points.Length==0)throw new InvalidOperationException("Gallery masonry is missing.");
            var footprint=new Bounds(points[0],Vector3.zero);
            foreach(var point in points.Skip(1))footprint.Encapsulate(point);
            var caps=gallery.Cast<Transform>().Where(t=>t.name=="Gallery capping course").ToArray();
            var roof=caps.Length==0 ? Shape(gallery,"Gallery capping course",Vector3.zero,Vector3.one,Surface("Limestone",true)).transform : caps[0];
            // A continuous cap closes the bay seams and extends over both return-wall corners.
            roof.localRotation=Quaternion.identity;
            roof.localPosition=new Vector3(footprint.center.x,footprint.max.y+.16f,footprint.center.z);
            roof.localScale=new Vector3(footprint.size.x+Overhang*2,.32f,footprint.size.z+Overhang*2);
            roof.GetComponent<MeshRenderer>().sharedMaterial=Surface("Limestone",true);
            foreach(var cap in caps.Skip(1))UnityEngine.Object.DestroyImmediate(cap.gameObject);
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Four Roofs")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning
                || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before refreshing roofs.");
            var setup=EditorSceneManager.GetSceneManagerSetup();Prepare();
            try
            {
                for(int stage=31;stage<=40;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(ChapterFourObservatoryBuilder.MainPath(stage));
                    var root=scene.GetRootGameObjects().Single(g=>g.name==ChapterFourObservatoryBuilder.RootName);
                    foreach(var gallery in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Root-framed survey cloister").ToArray())FitGallery(gallery);
                    foreach(var dome in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Patinated closed observatory dome"))dome.sharedMaterial=Surface("Limestone",true);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
    }
}
