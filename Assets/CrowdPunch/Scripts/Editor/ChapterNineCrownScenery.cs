using System.Collections.Generic;
using UnityEngine;
using static CrowdPunch.Editor.ChapterNineCrownAssets;

namespace CrowdPunch.Editor
{
    internal static class ChapterNineCrownScenery
    {
        internal static void Build(Transform root)
        {
            // Exterior terrace meets the original square floor, with 14 cm depth separation.
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Royal terrace",new Vector3(0,-2.14f,side*62),new Vector3(148,1,24),Surface("Terrace"));
                Shape(root,"Royal terrace",new Vector3(side*62,-2.14f,0),new Vector3(24,1,100),Surface("Terrace"));
            }
            CloudSea(root,110,35,0,"Near cloud banks",false);
            CloudSea(root,225,75,6,"Distant cloud banks",true);
            Shape(root,"Continuous cloud earth",new Vector3(0,-18,0),new Vector3(850,.5f,850),Surface("Cloud",false,true));
            for(int side=0;side<4;side++)
            {
                var wing = new GameObject(new[]{"North crown palace","East bell gallery","South sky gate","West observatory cloister"}[side]).transform;
                wing.SetParent(root,false); wing.localRotation = Quaternion.Euler(0,side*90,0);
                Gallery(wing);
                // Each facing side has a different focal silhouette behind its connected gallery.
                if(side == 0)
                {
                    Shape(wing,"Royal palace",new Vector3(0,-1.14f,83),new Vector3(30,19,14),Surface("Facade",true));
                    Shape(wing,"Blue palace roof",new Vector3(0,17.86f,83),new Vector3(32,1.3f,16),Surface("Fabric",true));
                    for(int i=-1;i<=1;i++) Tower(wing,new Vector3(i*11,-1.14f,83),i==0?36:27);
                }
                else if(side == 1) { Tower(wing,new Vector3(-10,-1.14f,84),30); Tower(wing,new Vector3(12,-1.14f,84),23); }
                else if(side == 2)
                {
                    foreach(int sign in new[]{-1,1}) Tower(wing,new Vector3(sign*15,-1.14f,84),24);
                    Shape(wing,"Sky gate crown",new Vector3(0,18,84),new Vector3(31,3,5),Surface("Fabric",true));
                    Shape(wing,"Gold crest",new Vector3(0,21,84),new Vector3(4,4,3),Surface("Ochre",true),true);
                }
                else
                {
                    Shape(wing,"Observatory rotunda",new Vector3(0,-1.14f,86),new Vector3(23,17,23),Surface("Facade",true),true);
                    Shape(wing,"Observatory blue cap",new Vector3(0,15.86f,86),new Vector3(25,2,25),Surface("Fabric",true),true);
                    Tower(wing,new Vector3(0,17.86f,86),18);
                }
                // More distant islands remain complete behind gaps in the near architecture.
                for(int i=-1;i<=1;i++) Tower(wing,new Vector3(i*52,-8,170+Mathf.Abs(i)*10),28+(side*7+i*3+12)%17,true);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Tower(root,new Vector3(x*66,-1.14f,z*66),20);
        }

        private static void Gallery(Transform parent)
        {
            // Connected front piers, recessed rear wall and full roof; all bounds beyond combat.
            Shape(parent,"Gallery rear wall",new Vector3(0,-1.14f,69),new Vector3(116,9,1.5f),Surface("Facade",true));
            Shape(parent,"Gallery blue cornice",new Vector3(0,7.86f,65),new Vector3(118,.8f,11),Surface("Fabric",true));
            for(int i=-5;i<=5;i++)
            {
                Shape(parent,"Ivory gallery pier",new Vector3(i*10,-1.14f,61),new Vector3(1.7f,9,2),Surface("Limestone",true));
                Shape(parent,"Gold column capital",new Vector3(i*10,7.25f,61),new Vector3(2.3f,.5f,2.6f),Surface("Ochre",true));
                Shape(parent,"Recessed royal panel",new Vector3(i*10,.9f,68.13f),new Vector3(4.8f,4.8f,.12f),Surface("Fabric",true));
                Shape(parent,"Painted window glint",new Vector3(i*10-.8f,1.3f,68.04f),new Vector3(.14f,3.6f,.035f),Surface("Chalk",true));
            }
            foreach(int sign in new[]{-1,1})
                Shape(parent,"Gallery return",new Vector3(sign*57.25f,-1.14f,64.6f),new Vector3(1.5f,9,7.3f),Surface("Facade",true));
        }

        private static void Tower(Transform parent,Vector3 bottom,float height,bool distant=false)
        {
            Shape(parent,"Ivory sky tower",bottom,new Vector3(7,height,7),Surface("Limestone",true,distant));
            Shape(parent,"Crown tower cornice",bottom+Vector3.up*(height-.6f),new Vector3(8,.6f,8),Surface("Ochre",true,distant));
            var roof=Shape(parent,"Royal tower spire",bottom+Vector3.up*height,new Vector3(7,7,7),Surface("Fabric",true,distant));
            roof.GetComponent<MeshFilter>().sharedMesh=Spire();
            Shape(parent,"Gold tower finial",bottom+Vector3.up*(height+7),new Vector3(.5f,1.4f,.5f),Surface("Ochre",true,distant),true);
        }

        private static Mesh Spire()
        {
            var v=new List<Vector3>();var t=new List<int>();
            var corners=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,-.5f,.5f)};
            for(int i=0;i<4;i++){int n=v.Count;v.AddRange(new[]{corners[i],Vector3.up*.5f,corners[(i+1)%4]});t.AddRange(new[]{n,n+1,n+2});}
            Quad(v,t,corners[3],corners[2],corners[1],corners[0]);return SaveMesh("Royal pyramid spire",v,t);
        }

        private static void CloudSea(Transform root,float radius,float width,float y,string name,bool distant)
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=192;
            Vector3 Point(int i,int j)
            {
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/12;
                float lobe=1+.23f*Mathf.Sin(a*19)+.10f*Mathf.Sin(a*37);
                float r=radius+Mathf.Cos(b)*width*.5f*lobe;
                return new Vector3(Mathf.Sin(a)*r,y+Mathf.Sin(b)*(6+7*lobe)+4*Mathf.Sin(a*19),Mathf.Cos(a)*r);
            }
            for(int i=0;i<segments;i++)for(int j=0;j<12;j++) Quad(v,t,Point(i,j),Point(i+1,j),Point(i+1,j+1),Point(i,j+1));
            MeshObject(root,name,SaveMesh(name,v,t),Surface("Cloud",true,distant));
        }
    }
}
