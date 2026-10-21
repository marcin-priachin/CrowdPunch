using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(GroundHazardPresentationSystem))]
    public partial class GroundHazardParticlePresentationSystem : SystemBase
    {
        private Mesh mesh;
        private Material material;
        private readonly List<Vector3> vertices=new(4096);
        private readonly List<Color> colors=new(4096);
        private readonly List<int> triangles=new(8192);
        private Vector3 right;

        protected override void OnCreate()
        {
            mesh=new Mesh{name="Ground hazard cartoon particles",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
            RequireForUpdate<GroundHazardAppearance>();
        }

        protected override void OnUpdate()
        {
            mesh.Clear();vertices.Clear();colors.Clear();triangles.Clear();
            if(material==null)material=Resources.Load<Material>("GroundHazardParticles");
            var camera=Camera.main;if(material==null || camera==null)return;
            right=Vector3.ProjectOnPlane(camera.transform.right,Vector3.up).normalized;
            foreach(var (patch,state,appearance) in SystemAPI.Query<RefRO<GroundHazard>,RefRO<GroundHazardState>,RefRO<GroundHazardAppearance>>())
            {
                if(appearance.ValueRO.Style!=GroundHazardStyle.Quarry && appearance.ValueRO.Style!=GroundHazardStyle.Observatory && appearance.ValueRO.Style!=GroundHazardStyle.Necropolis && appearance.ValueRO.Style!=GroundHazardStyle.Canyon && appearance.ValueRO.Style!=GroundHazardStyle.Vault)continue;
                for(int lane=0;lane<GroundHazardParticleGeometry.Count(patch.ValueRO,state.ValueRO);lane++)
                    if(GroundHazardParticleGeometry.TrySample(patch.ValueRO,state.ValueRO,SystemAPI.Time.ElapsedTime,lane,out var particle))Draw(particle);
            }
            if(vertices.Count==0)return;
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }

        private void Draw(in GroundHazardParticleSample p)
        {
            Vector3 position=p.Position;
            if(p.Kind==GroundHazardParticleKind.Smoke)
            {
                Puff(position,p.Radius,new Color(.30f,.34f,.32f,p.Opacity));
                Puff(position+right*p.Radius*.48f+Vector3.up*p.Radius*.32f,p.Radius*.72f,new Color(.40f,.43f,.39f,p.Opacity*.85f));
                Puff(position-right*p.Radius*.45f+Vector3.up*p.Radius*.25f,p.Radius*.65f,new Color(.36f,.39f,.36f,p.Opacity*.85f));
            }
            else if(p.Kind==GroundHazardParticleKind.Ember)
            {
                int n=vertices.Count;var tint=new Color(1,.75f,.16f,p.Opacity);
                Add(position-right*p.Radius,tint);Add(position+Vector3.up*p.Radius*1.8f,tint);Add(position+right*p.Radius,tint);Add(position-Vector3.up*p.Radius,tint);
                Triangle(n,n+1,n+2);Triangle(n,n+2,n+3);
            }
            else
            {
                Flame(position,p.Radius,p.Height,p.Bend,new Color(1,.24f,.025f,p.Opacity));
                Flame(position+Vector3.up*.035f,p.Radius*.48f,p.Height*.62f,p.Bend*.6f,new Color(1,.78f,.16f,p.Opacity));
            }
        }

        private void Puff(Vector3 center,float radius,Color color)
        {
            int start=vertices.Count;Add(center,color);
            for(int i=0;i<8;i++)
            {float a=i*Mathf.PI/4;Add(center+right*(Mathf.Cos(a)*radius)+Vector3.up*(Mathf.Sin(a)*radius*.82f),color);}
            for(int i=0;i<8;i++)Triangle(start,start+1+i,start+1+(i+1)%8);
        }

        private void Flame(Vector3 center,float radius,float height,float bend,Color color)
        {
            int start=vertices.Count;Add(center+Vector3.up*height*.35f,color);
            // The notched outline and bent tip give each rising tongue a drawn silhouette.
            Point(-.55f,0);Point(-1,.25f);Point(-.65f,.62f);Point(-.45f,.42f);
            Point(bend,1);Point(.7f,.66f);Point(1,.25f);Point(.55f,0);
            for(int i=0;i<8;i++)Triangle(start,start+1+i,start+1+(i+1)%8);
            void Point(float x,float y)=>Add(center+right*(x*radius)+Vector3.up*(y*height),color);
        }

        private void Add(Vector3 p,Color c){vertices.Add(p);colors.Add(c);}
        private void Triangle(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        protected override void OnStopRunning(){if(mesh!=null)mesh.Clear();}
        protected override void OnDestroy(){if(mesh!=null)Object.Destroy(mesh);}
    }
}
