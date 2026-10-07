using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Bakers
{
    public sealed class RollingBossBaker : Baker<RollingBossAuthoring>
    {
        public override void Bake(RollingBossAuthoring a)
        {
            if(a.settings==null) return;
            DependsOn(a.settings);
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var t=a.settings.Bake();
            t.InitialPosition=a.transform.position; t.InitialRotation=a.transform.rotation;
            AddComponent(e,t);
            AddComponent(e,new RollingBoss { Stage=1,CycleStage=1,Phase=RollingPhase.Pause,
                Remaining=t.OpeningPause,PreviousPosition=t.InitialPosition,Direction=math.forward(t.InitialRotation) });
            AddComponent(e,new Health { Current=t.Health,Max=t.Health });
            AddBuffer<RollingHit>(e); AddBuffer<RollingHitHistory>(e); AddBuffer<RollingCrowdContact>(e);
            var mat=Unity.Physics.Material.Default;
            mat.CollisionResponse=CollisionResponsePolicy.CollideRaiseCollisionEvents;
            mat.Friction=0; mat.Restitution=.25f;
            var blob=CapsuleCollider.Create(new CapsuleGeometry { Radius=t.BodyRadius,
                Vertex0=new float3(0,-math.max(0,t.BodyHeight*.5f-t.BodyRadius),0),
                Vertex1=new float3(0,math.max(0,t.BodyHeight*.5f-t.BodyRadius),0) },
                new CollisionFilter { BelongsTo=1u<<8,CollidesWith=uint.MaxValue },mat);
            AddBlobAsset(ref blob,out _);
            AddComponent(e,new PhysicsCollider { Value=blob });
            var mass=PhysicsMass.CreateDynamic(blob.Value.MassProperties,t.Mass);
            mass.InverseInertia=float3.zero;
            AddComponent(e,mass);
            AddComponent<PhysicsVelocity>(e); AddComponent(e,new PhysicsGravityFactor { Value=0 });
            AddSharedComponent(e,new PhysicsWorldIndex());
        }
    }
}
