using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Bakers
{
    public sealed class ChickenBossBaker : Baker<ChickenBossAuthoring>
    {
        public override void Bake(ChickenBossAuthoring a)
        {
            if(a.settings==null || a.projectilePrefab==null) return;
            DependsOn(a.settings);
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var t=a.settings.Bake();
            t.ProjectilePrefab=GetEntity(a.projectilePrefab,TransformUsageFlags.Dynamic);
            t.InitialPosition=a.transform.position; t.InitialRotation=a.transform.rotation;
            t.ArenaCenter=a.arenaCenter; t.ArenaHalfSize=a.arenaHalfSize;
            AddComponent(e,t);
            AddComponent(e,new ChickenBoss { Stage=1, Phase=ChickenPhase.Pause, Remaining=t.OpeningPause,
                PreviousPosition=t.InitialPosition, Destination=t.InitialPosition, Facing=t.InitialRotation });
            AddComponent(e,new Health { Current=t.Health,Max=t.Health });
            AddBuffer<ChickenHit>(e); AddBuffer<ChickenHitHistory>(e);
            var mat=Unity.Physics.Material.Default;
            mat.CollisionResponse=CollisionResponsePolicy.CollideRaiseCollisionEvents;
            mat.Friction=0; mat.Restitution=.15f;
            var blob=CapsuleCollider.Create(new CapsuleGeometry { Radius=t.BodyRadius,
                Vertex0=new float3(0,-math.max(0,t.BodyHeight*.5f-t.BodyRadius),0),
                Vertex1=new float3(0,math.max(0,t.BodyHeight*.5f-t.BodyRadius),0) },
                new CollisionFilter { BelongsTo=1u<<8,CollidesWith=uint.MaxValue },mat);
            AddBlobAsset(ref blob,out _);
            AddComponent(e,new PhysicsCollider { Value=blob });
            AddComponent(e,PhysicsMass.CreateKinematic(blob.Value.MassProperties));
            AddComponent<PhysicsVelocity>(e); AddComponent(e,new PhysicsGravityFactor { Value=0 });
            AddSharedComponent(e,new PhysicsWorldIndex());
        }
    }
}
