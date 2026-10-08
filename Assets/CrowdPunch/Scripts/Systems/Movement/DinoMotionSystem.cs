using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(AI.DinoCycleSystem))]
    public partial struct DinoMotionSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PhysicsWorldSingleton>()) return;
            var world=SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            var hits=new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach(var (boss,tuning,pose,velocity,collider,entity) in
                SystemAPI.Query<RefRW<DinoBoss>,RefRO<DinoTuning>,RefRW<LocalTransform>,RefRW<PhysicsVelocity>,RefRO<PhysicsCollider>>().WithEntityAccess())
            {
                ref var b=ref boss.ValueRW; var t=tuning.ValueRO;
                b.PreviousPosition=pose.ValueRO.Position;
                if(!player.IsAvailable || b.Phase==DinoPhase.Stagger || b.Phase==DinoPhase.Defeated)
                { velocity.ValueRW=new PhysicsVelocity(); continue; }
                float dt=SystemAPI.Time.DeltaTime;
                float speed=t.ChaseSpeeds[math.clamp(b.Stage-1,0,2)]*(b.Phase==DinoPhase.Burst?t.BurstMultiplier:1);
                float3 desired=math.normalizesafe(new float3(player.Position.x-pose.ValueRO.Position.x,0,player.Position.z-pose.ValueRO.Position.z),b.Direction);
                float3 halfAxis=new float3(0,math.max(0,t.BodyHeight*.5f-t.BodyRadius),0);
                // Sweep ahead and follow the entering surface's tangent. This keeps pursuit
                // around pillars and walls without writing physics-driven XZ positions.
                hits.Clear();
                world.CapsuleCastAll(pose.ValueRO.Position-halfAxis,pose.ValueRO.Position+halfAxis,t.BodyRadius+.08f,
                    desired,math.max(2,speed*.65f),ref hits,collider.ValueRO.Value.Value.GetCollisionFilter());
                float nearest=2; float3 normal=float3.zero;
                foreach(var h in hits)
                    if(h.Entity!=entity && h.RigidBodyIndex>=world.NumDynamicBodies && math.abs(h.SurfaceNormal.y)<.5f
                        && math.dot(desired,h.SurfaceNormal)<-.01f && h.Fraction<nearest
                        && (!SystemAPI.HasComponent<FallingPillar>(h.Entity) || SystemAPI.GetComponent<FallingPillar>(h.Entity).Phase==PillarPhase.Upright))
                    { nearest=h.Fraction; normal=math.normalizesafe(new float3(h.SurfaceNormal.x,0,h.SurfaceNormal.z)); }
                if(nearest<=1)
                {
                    float3 tangent=desired-normal*math.dot(desired,normal);
                    if(math.lengthsq(tangent)<.01f) tangent=new float3(-normal.z,0,normal.x);
                    desired=math.normalizesafe(tangent+normal*.15f,b.Direction);
                }
                float turn=math.radians(b.Phase==DinoPhase.Burst?t.BurstTurnDegrees:t.ChaseTurnDegrees)*dt;
                b.Direction=math.normalizesafe(EnemyLaunchHoming.RotateHorizontalVelocity(b.Direction,desired,turn),desired);
                pose.ValueRW.Rotation=quaternion.LookRotationSafe(b.Direction,math.up());
                float2 delta=b.Direction.xz*speed-velocity.ValueRO.Linear.xz;
                float length=math.length(delta);
                velocity.ValueRW.Linear.xz+=length>0?delta*math.min(1,t.Acceleration*dt/length):float2.zero;
                velocity.ValueRW.Linear.y=0; velocity.ValueRW.Angular=float3.zero;
            }
            hits.Dispose();
        }
    }
}
