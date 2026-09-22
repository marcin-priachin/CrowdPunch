using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Bakers
{
    public sealed class BossPartBaker : Baker<BossPartAuthoring>
    {
        public override void Bake(BossPartAuthoring a)
        {
            if(a.encounter==null) return;
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var material=Unity.Physics.Material.Default;
            material.CollisionResponse=CollisionResponsePolicy.CollideRaiseCollisionEvents;
            material.Friction=0; material.Restitution=.15f;
            // Dedicated category: launched Dashers ignore ordinary enemies, never physical boss parts.
            var collider=CapsuleCollider.Create(new CapsuleGeometry {
                Radius=a.radius, Vertex0=new float3(0,-math.max(0,a.height*.5f-a.radius),0),
                Vertex1=new float3(0,math.max(0,a.height*.5f-a.radius),0) },
                new CollisionFilter { BelongsTo=1u<<8, CollidesWith=uint.MaxValue },material);
            AddBlobAsset(ref collider,out _);
            AddComponent(e,new PhysicsCollider { Value=collider });
            AddComponent(e,PhysicsMass.CreateKinematic(collider.Value.MassProperties));
            AddComponent<PhysicsVelocity>(e);
            AddComponent(e,new PhysicsGravityFactor { Value=0 });
            AddSharedComponent(e,new PhysicsWorldIndex());
            AddComponent(e,new BossPart { Encounter=GetEntity(a.encounter,TransformUsageFlags.Dynamic), Kind=a.kind,
                Radius=a.radius, InitialPosition=a.transform.position, InitialRotation=a.transform.rotation });
            AddComponent(e,new BossMotionTarget { Position=a.transform.position, Rotation=a.transform.rotation });
            AddComponent<BossImpactFeedback>(e);
            AddBuffer<CollisionDamageHistory>(e);
            if(a.kind!=BossPartKind.Head)
            {
                AddComponent(e,new BossHand { Phase=BossHandPhase.Returning, PreviousPosition=a.transform.position });
                AddBuffer<BossScatterHistory>(e);
            }
        }
    }
}
