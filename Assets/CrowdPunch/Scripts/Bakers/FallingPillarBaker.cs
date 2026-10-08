using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Bakers
{
    public sealed class FallingPillarBaker : Baker<FallingPillarAuthoring>
    {
        public override void Bake(FallingPillarAuthoring a)
        {
            if(a.boss==null || a.boss.settings==null) return;
            DependsOn(a.boss.settings);
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var t=a.boss.settings.BakePillar(); t.InitialPosition=a.transform.position;
            var geometry=new BoxGeometry { Center=new float3(0,t.Height*.5f,0),
                Size=new float3(t.Width,t.Height,t.Width),Orientation=quaternion.identity,BevelRadius=0 };
            var material=Unity.Physics.Material.Default; material.Friction=0;
            t.UprightCollider=BoxCollider.Create(geometry,new CollisionFilter { BelongsTo=1,CollidesWith=uint.MaxValue },material);
            t.UnavailableCollider=BoxCollider.Create(geometry,new CollisionFilter(),material);
            AddBlobAsset(ref t.UprightCollider,out _); AddBlobAsset(ref t.UnavailableCollider,out _);
            AddComponent(e,t); AddComponent(e,new FallingPillar { Boss=GetEntity(a.boss,TransformUsageFlags.Dynamic) });
            AddBuffer<PillarFallHit>(e); AddComponent(e,new PhysicsCollider { Value=t.UprightCollider });
            // Moving scripted geometry remains a static body; no normal enemy motor owns this transform.
            AddSharedComponent(e,new PhysicsWorldIndex());
        }
    }
}
