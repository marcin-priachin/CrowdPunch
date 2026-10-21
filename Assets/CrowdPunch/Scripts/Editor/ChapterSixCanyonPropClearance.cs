using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-004/005: decorative supplies need space around masonry and yard walls.
    public static class ChapterSixCanyonPropClearance
    {
        private const float Gap=.2f;

        [MenuItem("Crowd Punch/Art Direction/Separate Chapter Six Supplies From Buildings and Fences")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before correcting scenery.");
            var setup=EditorSceneManager.GetSceneManagerSetup();int moved=0;
            try
            {
                for(int stage=51;stage<=60;stage++)
                {
                    var main=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.MainPath(stage));
                    var sub=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.SubPath(stage),OpenSceneMode.Additive);
                    var floors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
                        .Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
                    var court=floors[0].bounds;foreach(var floor in floors.Skip(1))court.Encapsulate(floor.bounds);
                    var root=main.GetRootGameObjects().Single(g=>g.name==ChapterSixCanyonBuilder.RootName).transform;
                    moved+=Apply(root,court);
                    EditorSceneManager.CloseScene(sub,true);
                    EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);
                }
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            Debug.Log($"Separated {moved} canyon supply props; shadow rebake required.");
        }

        internal static int Apply(Transform root,Bounds court)
        {
            var units=Units(root);
            var movable=new HashSet<Renderer>(units.SelectMany(u=>u).SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)));
            var fixedBounds=root.GetComponentsInChildren<Renderer>(true)
                .Where(r=>!movable.Contains(r) && r.name!="Continuous canyon earth" && r.name!="Continuous canyon mesa rim")
                .Select(r=>r.bounds).ToList();
            // Keep all proposed positions outside the original combat envelope, including long routes.
            court.Expand(new Vector3(4,0,4));
            int moved=0;
            foreach(var unit in units)
            {
                var bounds=BoundsOf(unit);
                bool Clear(Vector3 offset)
                {
                    var candidate=bounds;candidate.center+=offset;
                    if(candidate.max.x>court.min.x && candidate.min.x<court.max.x &&
                        candidate.max.z>court.min.z && candidate.min.z<court.max.z)return false;
                    candidate.Expand(Gap*2);
                    return !fixedBounds.Any(b=>candidate.Intersects(b));
                }
                if(!Clear(Vector3.zero))
                {
                    // Nearest deterministic point on a 25cm grid; no random drift on reapplication.
                    var offsets=new List<Vector3>();
                    for(int z=-48;z<=48;z++)for(int x=-48;x<=48;x++)offsets.Add(new Vector3(x*.25f,0,z*.25f));
                    var offset=offsets.OrderBy(p=>p.sqrMagnitude).FirstOrDefault(Clear);
                    if(!Clear(offset))throw new InvalidOperationException("No clear decorative placement for "+unit[0].name);
                    foreach(var transform in unit)transform.position+=offset;
                    bounds.center+=offset;moved++;
                }
                fixedBounds.Add(bounds);
            }
            return moved;
        }

        internal static Transform[][] Units(Transform root)
        {
            var transforms=root.GetComponentsInChildren<Transform>(true);
            return transforms.Where(t=>(t.parent!=null && (t.parent==root || t.parent.name=="Caravan service store" || t.parent.name=="Wind-eroded outpost")) &&
                    (t.name=="Caravan supply chest" || t.name=="cactus_short" || t.name=="cactus_tall"))
                .Select(t=>t.name!="Caravan supply chest"?new[]{t}:new[]{t}.Concat(transforms.Where(s=>
                    s.name=="Chest iron straps" && s.parent==t.parent &&
                    Mathf.Abs(s.localPosition.z-t.localPosition.z)<.01f &&
                    Mathf.Abs(Mathf.Abs(s.localPosition.x-t.localPosition.x)-.32f)<.01f)).ToArray()).ToArray();
        }

        internal static Bounds BoundsOf(Transform[] unit)
        {
            var renderers=unit.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
    }
}
