using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // VISION-006: use the rendered triangles, not the unsampled hill formula.
    internal sealed class ChapterOneVillageGrounding
    {
        private const float CellSize=6;
        private readonly Dictionary<Vector2Int,List<(Vector3 a,Vector3 b,Vector3 c)>> cells=new();

        internal ChapterOneVillageGrounding(Transform root)
        {
            var terrain=root.Find("Rolling meadows - visual only");
            foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>(true))
            {
                var vertices=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                var indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]]; var b=vertices[indices[i+1]]; var c=vertices[indices[i+2]];
                    int x0=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/CellSize);
                    int x1=Mathf.FloorToInt(Mathf.Max(a.x,b.x,c.x)/CellSize);
                    int z0=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/CellSize);
                    int z1=Mathf.FloorToInt(Mathf.Max(a.z,b.z,c.z)/CellSize);
                    for(int x=x0;x<=x1;x++) for(int z=z0;z<=z1;z++)
                    {
                        var key=new Vector2Int(x,z);
                        if(!cells.TryGetValue(key,out var triangles)) cells.Add(key,triangles=new());
                        triangles.Add((a,b,c));
                    }
                }
            }
        }

        internal float Height(float x,float z)
        {
            if(cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/CellSize),Mathf.FloorToInt(z/CellSize)),out var triangles))
            foreach(var (a,b,c) in triangles)
            {
                float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                if(Mathf.Abs(determinant)<.00001f) continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/determinant;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/determinant;
                if(u>=-.00001f && v>=-.00001f && u+v<=1.00001f) return u*a.y+v*b.y+(1-u-v)*c.y;
            }
            throw new InvalidOperationException($"No visible village terrain at {x}, {z}.");
        }

        internal static Transform[] Targets(Transform root)
        {
            bool GroundModel(Transform t) => ChapterOneVillagePlanting.IsPlant(t) ||
                t.name.StartsWith("Rock_",StringComparison.Ordinal) || t.name.StartsWith("FarmCrate_",StringComparison.Ordinal) ||
                t.name=="Terracotta village house" || t.name=="Barrel" || t.name=="Bench" ||
                t.name=="Prop_Crate" || t.name=="Prop_Wagon" || t.name=="Stall_Cart_Empty" ||
                t.name=="Anvil_Log" || t.name=="Prop_WoodenFence_Single" ||
                t.name=="Limestone doorstep" || t.name=="Waypost" || t.name=="Raised garden stone bed";
            var stalls=root.Cast<Transform>().Where(t=>t.name=="Stall_Cart_Empty").ToArray();
            bool OnMarketTable(Transform t) => t.name=="FarmCrate_Carrot" && t.parent==root &&
                stalls.Any(s=>Vector2.Distance(new Vector2(s.position.x,s.position.z),new Vector2(t.position.x,t.position.z))<1);
            return root.GetComponentsInChildren<Transform>(true)
                .Where(t=>t.parent!=null && (t.parent==root || t.parent.name=="Planted verges and village life") &&
                    GroundModel(t) && !OnMarketTable(t)).ToArray();
        }

        internal float Gap(Transform item)
        {
            var bounds=ChapterOneVillagePlanting.VisualBounds(item);
            // Raised garden plants deliberately sit on their stone beds.
            if(ChapterOneVillagePlanting.IsPlant(item))
            foreach(var bed in item.parent.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Raised garden stone bed"))
                if(bounds.center.x>=bed.bounds.min.x && bounds.center.x<=bed.bounds.max.x &&
                    bounds.center.z>=bed.bounds.min.z && bounds.center.z<=bed.bounds.max.z &&
                    Mathf.Abs(bounds.min.y-bed.bounds.max.y)<.15f) return 0;
            var points=item.GetComponentsInChildren<MeshFilter>(true)
                .SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToArray();
            float bottom=points.Min(p=>p.y);
            float band=item.name.StartsWith("Rock_",StringComparison.Ordinal)?.15f:.04f;
            float gap=float.NegativeInfinity;
            foreach(var point in points)
            {
                if(point.y>bottom+band) continue;
                // Compare the base plane, not the curved sides or rising grass blades.
                gap=Mathf.Max(gap,bottom-Height(point.x,point.z));
            }
            if(float.IsNegativeInfinity(gap)) throw new InvalidOperationException("No ground contact vertices for "+item.name);
            return gap;
        }

        internal static int Apply(Transform root)
        {
            var terrain=new ChapterOneVillageGrounding(root);
            int moved=0;
            foreach(var item in Targets(root))
            {
                float gap=terrain.Gap(item);
                if(gap<=.03f) continue;
                // A small overlap hides the seam around roots and irregular rock bases.
                item.position-=Vector3.up*(gap+.08f);
                moved++;
            }
            return moved;
        }
    }
}
