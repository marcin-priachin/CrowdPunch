using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterThreeQuarryAssets;

namespace CrowdPunch.Editor
{
    internal static class ChapterThreeQuarryScenery
    {
        private const float GravelClearance = .12f;
        private const float TerraceRise = 1.4f;
        private const float AqueductSpan = 9f;
        private const float PierWidth = 1.8f;
        private const float PierDepth = 2.2f;
        private static float exteriorY;
        internal static void Build(Transform root, Bounds[] floors, int stage)
        {
            var court=floors[0];foreach(var b in floors.Skip(1))court.Encapsulate(b);
            float y=floors.Min(b=>b.max.y)-GravelClearance;
            exteriorY=y;
            float firstTerraceTop=floors[0].max.y-GravelClearance+TerraceRise;
            Exterior(root,court,y);
            Escarpment(root,court,y,stage,22,12,false);
            Escarpment(root,court,y,stage,72,30,true);
            // Connected extraction terraces support the architectural frontage; low near-side
            // stock keeps the normal camera clear while cutaway handles the larger silhouettes.
            for(int sector=0;sector<floors.Length;sector++)
            {
                var b=floors[sector];float sy=b.max.y-GravelClearance;
                float westStockZ=stage==27?-6.2f:stage==29?-3.75f:-5;
                Stock(root,new Vector3(b.min.x-7,sy,b.center.z+westStockZ),3+(stage+sector)%3,stage+sector);
                Stock(root,new Vector3(b.max.x+7,sy,b.center.z+(stage==25?10:7)),2+(stage+sector)%3,stage+sector+1);
                float terraceOffset=stage switch { 21 or 28 => 8.75f, 23 => 4, _ => 11 };
                float terraceX=b.min.x-terraceOffset;
                float terraceZ=stage switch { 21 => 3.25f, 22 when sector==0 => 12.35f, 28 => 6.25f, _ => 8 };
                float terraceHeight=sy-y+TerraceRise;
                Shape(root,"Extraction terrace",new Vector3(terraceX,y,b.center.z+terraceZ),new Vector3(stage==23?5.8f:10,terraceHeight,12),Surface("Cliff",true));
                for(int i=0;i<3;i++)
                    Model(root,"kenney_nature-kit","rock_large"+(char)('A'+(stage+i)%6),
                        new Vector3(b.max.x+10+i*4,y,b.min.z+4+i*5),new Vector3(4,3,4),Surface("Rock",true),i*73);
            }
            int spans=Mathf.Clamp(Mathf.CeilToInt((court.size.x+12)/9),4,15);
            float az=court.max.z+13;
            Aqueduct(root,new Vector3(court.center.x,y,az),spans,stage==23?spans/2:-1,stage==29?9:6);
            // Sage grows only on the exterior gravel; every footprint is outside the entire
            // gameplay rectangle and stock/architectural strips, so it cannot enter lanes.
            foreach(int side in new[]{-1,1})for(int i=0;i<5;i++)
            {
                float x=side<0?court.min.x-16:court.max.x+16;
                float z=Mathf.Lerp(court.min.z-7,court.max.z+7,i/4f);
                if(stage==21 && side<0 && i==3) z+=8; // Clear of the stepped extraction face.
                Model(root,"kenney_nature-kit",i%2==0?"tree_pineSmallA":"plant_bushLarge",new Vector3(x,y,z),
                    i%2==0?new Vector3(3,5,3):new Vector3(2.6f,1.5f,2.6f),Surface("Sage",true),stage*17+i*51);
            }
            switch(stage)
            {
                case 21:
                    QuarryFace(root,new Vector3(court.min.x-12,y,court.max.z-5),false);
                    Stock(root,new Vector3(court.center.x+5,y,court.min.z-8),5,0);break;
                case 22:
                    for(int i=0;i<floors.Length;i++)
                    {
                        var b=floors[i];float by=b.max.y-GravelClearance;
                        if(i==0)QuarryFace(root,new Vector3(b.min.x-12,by,b.center.z),false);
                        else { Winch(root,new Vector3(b.max.x+11,by,b.center.z),-90); Stock(root,new Vector3(b.min.x-8,by,b.center.z),6,1); }
                    }
                    break;
                case 23:
                    Aqueduct(root,new Vector3(court.min.x-11,y,court.center.z),4,1,7,90);
                    Stock(root,new Vector3(court.max.x+8,y,court.min.z-6),4,2);break;
                case 24:
                    Shelter(root,new Vector3(court.min.x-10,y,court.min.z+8),90);
                    for(int i=0;i<3;i++)Stock(root,new Vector3(court.max.x+8,y,court.min.z+6+i*16),3,i);
                    break;
                case 25:
                    QuarryFace(root,new Vector3(court.max.x+15,y,court.center.z+3),true);
                    Shelter(root,new Vector3(court.min.x-10,y,court.min.z+7),90);break;
                case 26:
                    Winch(root,new Vector3(court.max.x+10,y,court.center.z),-90);
                    Stock(root,new Vector3(court.min.x-9,y,court.min.z+5),6,1);break;
                case 27:
                    for(int i=0;i<3;i++)
                        Kiln(root,new Vector3(court.min.x-10,i==2?firstTerraceTop:y,court.min.z+7+i*10),i);
                    Shelter(root,new Vector3(court.max.x+11,y,court.max.z-5),-90);break;
                case 28:
                    QuarryFace(root,new Vector3(court.min.x-16,y,court.center.z+15),false);
                    Winch(root,new Vector3(court.max.x+13,y,court.center.z-14),-90);
                    Shelter(root,new Vector3(court.center.x+18,y,court.min.z-11),0);
                    Stock(root,new Vector3(court.center.x-15,y,court.min.z-9),7,1);break;
                case 29:
                    for(int side=-1;side<=1;side+=2)
                        Shape(root,"Rotunda abutment",new Vector3(court.center.x+side*9,y,court.max.z+8.4f),new Vector3(3,7,5),Surface("Limestone",true));
                    Winch(root,new Vector3(court.min.x-11,y,court.min.z+9),90);break;
                case 30:
                    Winch(root,new Vector3(floors[0].min.x-11,firstTerraceTop,floors[0].center.z+8),-90);
                    Winch(root,new Vector3(court.max.x+13,y,court.center.z+6),90);
                    Stock(root,new Vector3(court.center.x-7,y,court.min.z-9),6,2);break;
            }
        }

        private static void Exterior(Transform root,Bounds court,float y)
        {
            const int cells=64;var v=new List<Vector3>();var t=new List<int>();
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)v.Add(new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f));
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;t.AddRange(new[]{i,i+cells+1,i+1,i+1,i+cells+1,i+cells+2});}
            var go=MeshObject(root,"Continuous quarry gravel",SaveMesh("Subdivided exterior gravel",v,t),Surface("Gravel"));
            go.transform.localPosition=new Vector3(court.center.x,y,court.center.z);go.transform.localScale=new Vector3(court.size.x+500,1,court.size.z+500);
        }

        private static void Escarpment(Transform root,Bounds court,float y,int stage,float distance,float height,bool distant)
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=64;
            var center=new Vector3(court.center.x,y,court.center.z);
            // A closed superellipse gives rectangular courts continuous cliff backing, including
            // corners and the camera-facing side. Facets have distinct flat normals and no seams.
            for(int i=0;i<segments;i++)
            {
                Vector3 Point(int index,float extra,float level)
                {
                    float angle=index*Mathf.PI*2/segments;float sx=Mathf.Sin(angle),sz=Mathf.Cos(angle);
                    float px=Mathf.Sign(sx)*Mathf.Pow(Mathf.Abs(sx),.45f)*(court.extents.x+distance+extra);
                    float pz=Mathf.Sign(sz)*Mathf.Pow(Mathf.Abs(sz),.45f)*(court.extents.z+distance+extra);
                    float h=level<=0?0:height+Mathf.Sin(index*1.7f+stage)*height*.13f;
                    return center+new Vector3(px,h,pz);
                }
                var a=Point(i,0,0);var b=Point(i+1,0,0);var c=Point(i+1,0,1);var d=Point(i,0,1);
                Quad(v,t,a,b,c,d);Quad(v,t,d,c,Point(i+1,12,1),Point(i,12,1));
                Quad(v,t,Point(i,12,0),Point(i,12,1),Point(i+1,12,1),Point(i+1,12,0));
            }
            for(int i=0;i<t.Count;i+=3){int swap=t[i+1];t[i+1]=t[i+2];t[i+2]=swap;}
            MeshObject(root,distant?"Distant highland ridge":"Continuous quarry escarpment",SaveMesh($"Escarpment {stage} {distance}",v,t),Surface("Cliff",true,distant));
            if(!distant)for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI*2/12;
                var p=center+new Vector3(Mathf.Sin(angle)*(court.extents.x+distance+2),0,Mathf.Cos(angle)*(court.extents.z+distance+2));
                Model(root,"kenney_nature-kit","rock_tall"+(char)('A'+(stage+i)%10),p,new Vector3(11,16+i%3*3,11),Surface("Rock",true),i*57);
            }
        }

        private static void Aqueduct(Transform root,Vector3 p,int spans,int broken,float leg,float yaw=0)
        {
            var structure=new GameObject("Aqueduct arcade").transform;structure.SetParent(root,false);structure.localPosition=p;structure.localRotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<=spans;i++)
            {
                float x=(i-spans*.5f)*AqueductSpan;
                Shape(structure,"Aqueduct footing",new Vector3(x,0,0),new Vector3(2.6f,.6f,3.6f),Surface("Slate",true));
                Shape(structure,"Aqueduct pier",new Vector3(x,.6f,0),new Vector3(PierWidth,leg-1f,PierDepth),Surface("Limestone",true));
                Shape(structure,"Pier impost",new Vector3(x,leg-.4f,0),new Vector3(2.3f,.4f,2.7f),Surface("Chalk",true));
            }
            var meshes = new Dictionary<(bool leftEnd, bool rightEnd), Mesh>();
            for(int i=0;i<spans;i++)
            {
                float x=(i-(spans-1)*.5f)*AqueductSpan;
                if(i==broken)
                {
                    Stock(structure,new Vector3(x,0,3),3,3); continue;
                }
                bool leftEnd = i == 0 || i - 1 == broken;
                bool rightEnd = i == spans - 1 || i + 1 == broken;
                if (!meshes.TryGetValue((leftEnd, rightEnd), out var arch))
                {
                    arch = Arch(leftEnd, rightEnd);
                    meshes.Add((leftEnd, rightEnd), arch);
                }
                var go=MeshObject(structure,"Limestone voussoir arch",arch,Surface("Limestone",true));go.transform.localPosition=new Vector3(x,leg,0);
                var courseEdges = ArchCourseEdges(leftEnd, rightEnd);
                float courseWidth = courseEdges.y - courseEdges.x;
                float courseCenter = x + (courseEdges.x + courseEdges.y) * .5f;
                Shape(structure,"Aqueduct upper course",new Vector3(courseCenter,leg+4.5f,0),new Vector3(courseWidth,.6f,2.6f),Surface("Limestone",true));
                foreach(int side in new[]{-1,1})Shape(structure,"Channel parapet",new Vector3(courseCenter,leg+5.1f,side*1.15f),new Vector3(courseWidth,.65f,.3f),Surface("Slate",true));
            }
        }

        private static Vector2 ArchCourseEdges(bool leftEnd, bool rightEnd)
        {
            // Retain the mortar joint only where the next span continues the course.
            const float halfSpan = AqueductSpan * .5f;
            const float endReach = PierWidth * .5f;
            const float jointInset = .02f;
            return new Vector2(-halfSpan + (leftEnd ? -endReach : jointInset),
                halfSpan + (rightEnd ? endReach : -jointInset));
        }

        private static Mesh Arch(bool leftEnd, bool rightEnd)
        {
            const int segments=12;
            const float halfSpan = AqueductSpan * .5f;
            const float endReach = PierWidth * .5f;
            const float halfDepth = PierDepth * .5f;
            const float radius = 3.55f, crown = 4.5f;
            float leftEdge = -halfSpan - (leftEnd ? endReach : 0);
            float rightEdge = halfSpan + (rightEnd ? endReach : 0);
            float leftSpringing = leftEnd ? halfSpan - endReach : radius;
            float rightSpringing = rightEnd ? halfSpan - endReach : radius;
            var vertices=new List<Vector3>();
            var triangles=new List<int>();

            void Strip(Vector2 right,Vector2 left)
            {
                var frontRight=new Vector3(right.x,right.y,-halfDepth);
                var frontLeft=new Vector3(left.x,left.y,-halfDepth);
                var backRight=new Vector3(right.x,right.y,halfDepth);
                var backLeft=new Vector3(left.x,left.y,halfDepth);
                var topFrontRight=new Vector3(right.x,crown,-halfDepth);
                var topFrontLeft=new Vector3(left.x,crown,-halfDepth);
                var topBackRight=new Vector3(right.x,crown,halfDepth);
                var topBackLeft=new Vector3(left.x,crown,halfDepth);
                Quad(vertices,triangles,frontRight,frontLeft,topFrontLeft,topFrontRight);
                Quad(vertices,triangles,topBackRight,topBackLeft,backLeft,backRight);
                Quad(vertices,triangles,backRight,backLeft,frontLeft,frontRight);
                Quad(vertices,triangles,topFrontRight,topFrontLeft,topBackLeft,topBackRight);
            }

            // The flat strips at each springing fill the spandrel above the pier.
            var previous=new Vector2(rightSpringing,0);
            Strip(new Vector2(rightEdge,0),previous);
            for(int i=1;i<segments;i++)
            {
                float angle=i*Mathf.PI/segments;
                float springing = i < segments / 2 ? rightSpringing : leftSpringing;
                var point = new Vector2(Mathf.Cos(angle) * springing, Mathf.Sin(angle) * radius);
                Strip(previous,point);
                previous=point;
            }
            Strip(previous,new Vector2(-leftSpringing,0));
            Strip(new Vector2(-leftSpringing,0),new Vector2(leftEdge,0));
            Quad(vertices,triangles,new Vector3(rightEdge,0,-halfDepth),new Vector3(rightEdge,crown,-halfDepth),
                new Vector3(rightEdge,crown,halfDepth),new Vector3(rightEdge,0,halfDepth));
            Quad(vertices,triangles,new Vector3(leftEdge,0,halfDepth),new Vector3(leftEdge,crown,halfDepth),
                new Vector3(leftEdge,crown,-halfDepth),new Vector3(leftEdge,0,-halfDepth));
            string name = "Closed stone aqueduct arch" + (leftEnd ? " left end" : "") + (rightEnd ? " right end" : "");
            return SaveMesh(name,vertices,triangles);
        }

        private static void Stock(Transform root,Vector3 p,int count,int variant)
        {
            float height=root.TransformPoint(p).y-exteriorY;
            if(height>.03f && root.name==ChapterThreeQuarryBuilder.RootName)
                Shape(root,"Raised stock foundation",new Vector3(p.x,p.y-height,p.z),new Vector3(7,height,2),Surface("Cliff",true));
            for(int i=0;i<count;i++)
            {
                int row=i/3,col=i%3;
                Shape(root,"Cut stone stock",p+new Vector3((col-1)*2.3f,row*1.1f,0),new Vector3(2.15f,1.1f,1.6f),Surface((i+variant)%3==0?"Chalk":"Limestone",true));
            }
        }

        private static void QuarryFace(Transform root,Vector3 p,bool seam)
        {
            for(int tier=0;tier<3;tier++)Shape(root,"Stepped extraction face",p+new Vector3(-tier*1.2f,0,tier*1.8f),new Vector3(8-tier,3+tier*3,5),Surface("Cliff",true));
            if(seam)for(int i=0;i<3;i++)Shape(root,"Inert mineral seam",p+new Vector3(4.02f,1+i*1.1f,0),new Vector3(.08f,.12f,3.6f),Surface("Chalk",true));
        }

        private static void Winch(Transform root,Vector3 p,float yaw)
        {
            var assembly=new GameObject("Quarry lifting station").transform;assembly.SetParent(root,false);assembly.localPosition=p;assembly.localRotation=Quaternion.Euler(0,yaw,0);
            Shape(assembly,"Winch plinth",Vector3.zero,new Vector3(7,.5f,6),Surface("Limestone",true));
            foreach(int side in new[]{-1,1})Shape(assembly,"Timber lifting upright",new Vector3(side*2.5f,.5f,0),new Vector3(.5f,6.7f,.6f),Surface("Timber",true));
            Shape(assembly,"Supported lifting crossbeam",new Vector3(0,7.2f,0),new Vector3(6.5f,.55f,.7f),Surface("Timber",true));
            Shape(assembly,"Stationary winch drum",new Vector3(0,.5f,-1.95f),new Vector3(2.4f,1.2f,1.8f),Surface("Slate",true),true);
            Shape(assembly,"Hanging haul line",new Vector3(0,2.7f,0),new Vector3(.08f,4.5f,.08f),Surface("Timber",true));
            Shape(assembly,"Supported stone load",new Vector3(0,.5f,0),new Vector3(2.1f,2.2f,1.9f),Surface("Chalk",true));
        }

        private static void Shelter(Transform root,Vector3 p,float yaw)
        {
            var assembly=new GameObject("Quarry supply shelter").transform;assembly.SetParent(root,false);assembly.localPosition=p;assembly.localRotation=Quaternion.Euler(0,yaw,0);
            Shape(assembly,"Supply shelter foundation",Vector3.zero,new Vector3(8,.35f,6),Surface("Limestone",true));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Shape(assembly,"Shelter timber post",new Vector3(x*3.6f,.35f,z*2.5f),new Vector3(.3f,3.5f,.3f),Surface("Timber",true));
            Shape(assembly,"Sage shelter canopy",new Vector3(0,3.85f,0),new Vector3(8.4f,.3f,6.4f),Surface("Sage",true));
            Stock(assembly,new Vector3(0,.35f,0),5,2);
        }

        private static void Kiln(Transform root,Vector3 p,int index)
        {
            Shape(root,"Lime kiln body",p,new Vector3(5,4+index*.7f,5),Surface("Limestone",true),true);
            Shape(root,"Kiln slate crown",p+Vector3.up*(4+index*.7f),new Vector3(4.5f,.5f,4.5f),Surface("Slate",true),true);
            // Cold/inert kilns add no flame or glowing ground that could resemble hazards.
            Shape(root,"Cold kiln loading door",p+new Vector3(2.45f,.2f,0),new Vector3(.3f,2,1.5f),Surface("Fracture",true));
        }
    }
}
