using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // VISION-004/006: metre-proportioned islands keep long walls from stretching cube charts.
    internal static class ChapterSevenVaultShadows
    {
        internal const float BoxDensityScale = 4;
        private const float ChartSpan = 12;
        private const float ChartGutter = .1f;

        internal static bool Applies(MeshRenderer renderer, string output)
        {
            if(output != ChapterSevenVaultAssets.Output + "/BakedShadows" ||
                !renderer.GetComponentsInParent<Transform>(true).Any(t => t.name == ChapterSevenVaultBuilder.RootName)) return false;
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if(mesh == null) return false;
            if(mesh.name.StartsWith("Vault metric box ")) return true;
            return mesh.vertexCount <= 36 && mesh.triangles.Length == 36 && mesh.vertices.All(v =>
                Mathf.Abs(Mathf.Abs(v.x)-.5f)<.00001f && Mathf.Abs(Mathf.Abs(v.y)-.5f)<.00001f && Mathf.Abs(Mathf.Abs(v.z)-.5f)<.00001f);
        }

        internal static Mesh ForRenderer(MeshRenderer renderer, string output)
        {
            var scale = renderer.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            string revision = string.Join("/", new[] {scale.x,scale.y,scale.z}.Select(v => v.ToString("R",CultureInfo.InvariantCulture)));
            string key = Hash128.Compute("metric-box-v1/" + revision).ToString();
            string folder = output + "/Meshes/MetricBoxes";
            if(!Directory.Exists(folder)){Directory.CreateDirectory(folder);AssetDatabase.Refresh();}
            string path = folder + "/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing != null) return existing;

            var vertices = new List<Vector3>();var normals = new List<Vector3>();var tangents = new List<Vector4>();
            var uv = new List<Vector2>();var charts = new List<Vector2>();var triangles = new List<int>();
            float area = 2*(scale.x*scale.y+scale.x*scale.z+scale.y*scale.z);
            float rowLimit = Mathf.Max(ChartSpan+ChartGutter*2,Mathf.Sqrt(area)*1.25f);
            float cursorX = ChartGutter,cursorY = ChartGutter,rowHeight = 0,width = 0;
            void Face(Vector3 normal,Vector3 u,Vector3 v)
            {
                float lengthU = Vector3.Scale(u,scale).magnitude,lengthV = Vector3.Scale(v,scale).magnitude;
                int countU = Mathf.Max(1,Mathf.CeilToInt(lengthU/ChartSpan)),countV = Mathf.Max(1,Mathf.CeilToInt(lengthV/ChartSpan));
                float cellU = lengthU/countU,cellV = lengthV/countV;
                for(int y=0;y<countV;y++)for(int x=0;x<countU;x++)
                {
                    if(cursorX+cellU+ChartGutter>rowLimit){cursorX=ChartGutter;cursorY+=rowHeight+ChartGutter;rowHeight=0;}
                    int first = vertices.Count;
                    foreach(var corner in new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)})
                    {
                        var faceUV = new Vector2((x+corner.x)/countU,(y+corner.y)/countV);
                        vertices.Add(normal*.5f+u*(faceUV.x-.5f)+v*(faceUV.y-.5f));normals.Add(normal);
                        tangents.Add(new Vector4(u.x,u.y,u.z,1));uv.Add(faceUV);
                        charts.Add(new Vector2(cursorX+corner.x*cellU,cursorY+corner.y*cellV));
                    }
                    triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                    width=Mathf.Max(width,cursorX+cellU+ChartGutter);cursorX+=cellU+ChartGutter;rowHeight=Mathf.Max(rowHeight,cellV);
                }
            }
            Face(Vector3.right,Vector3.back,Vector3.up);Face(Vector3.left,Vector3.forward,Vector3.up);
            Face(Vector3.up,Vector3.right,Vector3.back);Face(Vector3.down,Vector3.right,Vector3.forward);
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.left,Vector3.up);
            float extent = Mathf.Max(width,cursorY+rowHeight+ChartGutter);
            for(int i=0;i<charts.Count;i++)charts[i]/=extent;
            var mesh = new Mesh{name="Vault metric box " + key};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetUVs(0,uv);mesh.SetUVs(1,charts);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
