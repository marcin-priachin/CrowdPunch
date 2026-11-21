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
        internal const string UpperWall = "Recessed limestone frieze";

        internal static void FitGallery(Transform gallery)
        {
            var arches=gallery.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_",StringComparison.Ordinal)).ToArray();
            var masonry=gallery.Cast<Transform>().Where(t=>arches.Contains(t)
                || t.name=="Recessed intact gallery back" || t.name=="Cloister return wall");
            var footprint=LocalMeshBounds(masonry.ToArray(),gallery);
            var caps=gallery.Cast<Transform>().Where(t=>t.name=="Gallery capping course").ToArray();
            var roof=caps.Length==0 ? Shape(gallery,"Gallery capping course",Vector3.zero,Vector3.one,Surface("Limestone",true)).transform : caps[0];
            // A continuous cap closes the bay seams and extends over both return-wall corners.
            roof.localRotation=Quaternion.identity;
            roof.localPosition=new Vector3(footprint.center.x,footprint.max.y+.16f,footprint.center.z);
            roof.localScale=new Vector3(footprint.size.x+Overhang*2,.32f,footprint.size.z+Overhang*2);
            roof.GetComponent<MeshRenderer>().sharedMaterial=Surface("Limestone",true);
            foreach(var cap in caps.Skip(1))UnityEngine.Object.DestroyImmediate(cap.gameObject);

            var front = LocalMeshBounds(arches, gallery);
            var returns = gallery.Cast<Transform>().Where(t => t.name == "Cloister return wall")
                .OrderBy(t => t.localPosition.x).ToArray();
            float left = LocalMeshBounds(new[] { returns[0] }, gallery).max.x;
            float right = LocalMeshBounds(new[] { returns[1] }, gallery).min.x;
            // Meet the cap and close the gap behind the intact arch crowns and broken stonework.
            const float depth = .28f;
            float bottom = front.max.y - .55f;
            var frieze = gallery.Find(UpperWall) ??
                Shape(gallery, UpperWall, Vector3.zero, Vector3.one, roof.GetComponent<MeshRenderer>().sharedMaterial).transform;
            frieze.localScale = new Vector3(right - left, footprint.max.y - bottom, depth);
            frieze.localPosition = new Vector3((left + right) * .5f, (bottom + footprint.max.y) * .5f,
                front.max.z + .02f + depth * .5f);
        }

        private static Bounds LocalMeshBounds(Transform[] parts, Transform gallery)
        {
            var points = parts.SelectMany(t => t.GetComponentsInChildren<MeshFilter>())
                .SelectMany(f => f.sharedMesh.vertices.Select(v => gallery.InverseTransformPoint(f.transform.TransformPoint(v))));
            using var vertices = points.GetEnumerator();
            if (!vertices.MoveNext()) throw new InvalidOperationException("Gallery masonry is missing.");
            var bounds = new Bounds(vertices.Current, Vector3.zero);
            while (vertices.MoveNext()) bounds.Encapsulate(vertices.Current);
            return bounds;
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
                    foreach (var instrument in root.GetComponentsInChildren<Transform>()
                        .Where(t => t.name.EndsWith("armillary landmark", StringComparison.Ordinal)))
                        ChapterFourObservatoryStructures.NestInstrumentHoops(instrument);
                    ChapterFourObservatoryScenery.ClearLandmarkIntersections(root.transform);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
    }
}
