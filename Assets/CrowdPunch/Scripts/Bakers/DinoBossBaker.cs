using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Bakers
{
    public sealed class DinoBossBaker : Baker<DinoBossAuthoring>
    {
        public override void Bake(DinoBossAuthoring a)
        {
            if(a.settings==null) return;
            DependsOn(a.settings);
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var t=a.settings.BakeBoss(); t.InitialPosition=a.transform.position; t.InitialRotation=a.transform.rotation;
            AddComponent(e,t);
            AddComponent(e,new DinoBoss { Stage=1,Phase=DinoPhase.Chase,Remaining=t.ChaseDurations.x,
                Direction=math.forward(t.InitialRotation),PreviousPosition=t.InitialPosition });
            AddComponent(e,new Health { Current=t.RequiredHits,Max=t.RequiredHits });
            var material=Unity.Physics.Material.Default; material.Friction=0;
            material.CollisionResponse=CollisionResponsePolicy.CollideRaiseCollisionEvents;
            float axis=math.max(0,t.BodyHeight*.5f-t.BodyRadius);
            var collider=CapsuleCollider.Create(new CapsuleGeometry { Radius=t.BodyRadius,
                Vertex0=new float3(0,-axis,0),Vertex1=new float3(0,axis,0) },
                new CollisionFilter { BelongsTo=1u<<8,CollidesWith=uint.MaxValue },material);
            AddBlobAsset(ref collider,out _); AddComponent(e,new PhysicsCollider { Value=collider });
            var mass=PhysicsMass.CreateDynamic(collider.Value.MassProperties,t.Mass); mass.InverseInertia=float3.zero;
            AddComponent(e,mass); AddComponent<PhysicsVelocity>(e);
            AddComponent(e,new PhysicsGravityFactor { Value=0 }); AddSharedComponent(e,new PhysicsWorldIndex());
        }
    }
}
