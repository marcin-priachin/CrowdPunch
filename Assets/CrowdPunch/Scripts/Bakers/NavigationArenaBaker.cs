using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Bakers
{
    public sealed class NavigationArenaBaker : Baker<NavigationArenaAuthoring>
    {
        public override void Bake(NavigationArenaAuthoring authoring)
        {
            if(authoring.settings==null)throw new System.InvalidOperationException("Navigation arena needs NavigationSettings.");
            DependsOn(authoring.settings);var arena=GetComponent<ArenaAuthoring>();
            var obstacles=GetComponentsInChildren<SolidObstacleAuthoring>();
            var rectangles=new NativeArray<NavigationRectangle>(obstacles.Length,Allocator.Temp);
            for(int i=0;i<obstacles.Length;i++)
            {
                var obstacle=obstacles[i];DependsOn(obstacle);DependsOn(obstacle.transform);
                float2 p=new float2(obstacle.transform.position.x,obstacle.transform.position.z);
                float2 half=new float2(obstacle.Size.x,obstacle.Size.y)*.5f;
                rectangles[i]=new NavigationRectangle { Minimum=p-half,Maximum=p+half };
            }
            float2 center=new float2(authoring.transform.position.x,authoring.transform.position.z)+arena.SpacingCenterOffset.xz;
            float3 radii=(float3)authoring.settings.clearanceRadii+authoring.settings.clearanceMargin;
            var blob=NavigationGridConstruction.Build(center-arena.SpacingSize.xz*.5f,center+arena.SpacingSize.xz*.5f,
                authoring.settings.cellSize,radii,rectangles,Allocator.Persistent);
            blob.Value.Margin=authoring.settings.clearanceMargin;
            rectangles.Dispose();
            AddBlobAsset(ref blob,out _);var entity=GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity,new NavigationGrid { Data=blob,ParticipationAnchor=authoring.participationAnchor });
            AddComponent(entity,authoring.settings.Runtime);AddComponent<NavigationDiagnostics>(entity);
            for(int c=0;c<3;c++) if(NavigationGeometry.Anchor(ref blob.Value,authoring.participationAnchor,c)<0)
                UnityEngine.Debug.LogError("Navigation participation anchor lacks clearance for class "+c,authoring);
        }
    }
}
