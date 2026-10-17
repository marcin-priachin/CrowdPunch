using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    // Preserve detailed visible tiles but cast a clean, open gable roof silhouette.
    public static class ChapterOneVillageRoofShadows
    {
        public static int Apply(Transform scenery)
        {
            string path=ChapterOneVillageAssets.Output+"/Landscape/CleanRoofShadow.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=new Mesh {name="Clean two-plane gable shadow"};
                mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),
                    new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f)};
                mesh.triangles=new[]{0,3,5,0,5,2,1,2,5,1,5,4};
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,path);
            }
            int count=0;
            foreach(var roof in scenery.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Roof_RoundTiles_8x8"))
            {
                if(roof.Find("Clean roof shadow")!=null) continue;
                var renderers=roof.GetComponentsInChildren<MeshRenderer>();
                var bounds=new Bounds(); bool first=true;
                foreach(var renderer in renderers)
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(filter==null || filter.sharedMesh==null) continue;
                    var local=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        var point=roof.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                        if(first) {bounds=new Bounds(point,Vector3.zero);first=false;} else bounds.Encapsulate(point);
                    }
                }
                if(first) continue;
                foreach(var renderer in renderers) renderer.shadowCastingMode=ShadowCastingMode.Off;
                var go=new GameObject("Clean roof shadow",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(roof,false);
                go.transform.localPosition=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                go.transform.localScale=bounds.size; go.isStatic=true;
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var caster=go.GetComponent<MeshRenderer>();
                caster.sharedMaterial=renderers[0].sharedMaterial;
                caster.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                caster.receiveShadows=false; caster.forceMeshLod=0;
                count++;
            }
            return count;
        }
    }
}
