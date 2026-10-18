using System;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterOneVillageLandscape;

namespace CrowdPunch.Editor
{
    // VISION-005/006: keep the entire plant silhouette on the green verges.
    internal static class ChapterOneVillagePlanting
    {
        private const float Clearance=.35f;

        internal static bool IsPlant(Transform item) => item.name=="Bush_Common" ||
            item.name=="Bush_Common_Flowers" || item.name=="Grass_Common_Short" ||
            item.name=="Flower_4_Group" || item.name.StartsWith("CommonTree_",StringComparison.Ordinal);

        internal static Transform[] Plants(Transform root) => root.GetComponentsInChildren<Transform>(true)
            .Where(t=>IsPlant(t) && !t.GetComponentsInParent<Transform>(true).Any(p=>p!=t && IsPlant(p))).ToArray();

        internal static Bounds VisualBounds(Transform item)
        {
            var renderers=item.GetComponentsInChildren<MeshRenderer>(true);
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Rect Footprint(Bounds bounds) => new(bounds.min.x-Clearance,bounds.min.z-Clearance,
            bounds.size.x+Clearance*2,bounds.size.z+Clearance*2);

        internal static Rect[] ProtectedGround(Bounds floor)
        {
            // VillageSurface's two noise terms can move a path edge by 1.025 m.
            // Include its full blend width, rather than relying on CPU/GPU noise matching.
            float sideHalfWidth=3.2f+1.025f,northHalfWidth=4+1.025f;
            float sideLength=floor.extents.z+18,northLength=floor.extents.z+40;
            Rect Side(int sign) => new(floor.center.x+sign*(floor.extents.x+5)-sideHalfWidth,
                floor.center.z-sideLength,sideHalfWidth*2,sideLength*2);
            return new[] {
                new Rect(floor.min.x,floor.min.z,floor.size.x,floor.size.z),
                Side(-1),Side(1),
                new Rect(floor.center.x-northHalfWidth,floor.center.z-northLength,northHalfWidth*2,northLength*2)
            };
        }

        internal static int ClearPaths(Transform root,Bounds floor,int stage)
        {
            var plants=Plants(root);
            var protectedGround=ProtectedGround(floor);
            // Keep relocated greenery out of fences, buildings and village props as well.
            var occupied=root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>!r.GetComponentsInParent<Transform>(true).Any(IsPlant) &&
                    !r.GetComponentsInParent<Transform>(true).Any(t=>t.name=="Rolling meadows - visual only"))
                .Select(r=>Footprint(r.bounds)).ToArray();
            int moved=0;
            foreach(var plant in plants)
            {
                var bounds=VisualBounds(plant);
                var original=Footprint(bounds);
                if(!protectedGround.Any(p=>p.Overlaps(original))) continue;
                Vector3 offset=Vector3.zero;
                bool found=false;
                // Nearest safe verge, deterministic and independent of the camera.
                for(float radius=.75f;radius<=60 && !found;radius+=.75f)
                for(int direction=0;direction<48;direction++)
                {
                    float angle=direction*Mathf.PI*2/48;
                    var delta=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    var candidate=new Rect(original.position+delta,original.size);
                    if(protectedGround.Any(p=>p.Overlaps(candidate)) || occupied.Any(p=>p.Overlaps(candidate))) continue;
                    offset=new Vector3(delta.x,0,delta.y);
                    offset.y=Height(bounds.center.x+offset.x,bounds.center.z+offset.z,floor,stage)-
                        Height(bounds.center.x,bounds.center.z,floor,stage);
                    found=true;
                    break;
                }
                if(!found) throw new InvalidOperationException("No green verge available for "+plant.name);
                plant.position+=offset;
                moved++;
            }
            return moved;
        }
    }
}
