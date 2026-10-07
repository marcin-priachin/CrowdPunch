using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(RollingCycleSystem))]
    public partial struct RollingMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PhysicsWorldSingleton>()) return;
            var world=SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            var hits=new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach(var (boss,tuning,pose,velocity,collider) in
                SystemAPI.Query<RefRW<RollingBoss>,RefRO<RollingTuning>,RefRW<LocalTransform>,RefRW<PhysicsVelocity>,RefRO<PhysicsCollider>>())
            {
                ref var b=ref boss.ValueRW;
                if(b.Phase==RollingPhase.Roll)
                {
                    // Sweep the baked solid shape against actual static geometry. Only entering
                    // lateral surfaces redirect; an outgoing sustained contact cannot count again.
                    hits.Clear();
                    float3 halfAxis=new float3(0,math.max(0,tuning.ValueRO.BodyHeight*.5f-tuning.ValueRO.BodyRadius),0);
                    world.CapsuleCastAll(pose.ValueRO.Position-halfAxis,pose.ValueRO.Position+halfAxis,tuning.ValueRO.BodyRadius,
                        b.Direction,b.Speed*SystemAPI.Time.DeltaTime+.08f,ref hits,collider.ValueRO.Value.Value.GetCollisionFilter());
                    float nearest=2; float3 normal=float3.zero;
                    foreach(var hit in hits)
                        if(hit.RigidBodyIndex>=world.NumDynamicBodies && math.abs(hit.SurfaceNormal.y)<.5f
                            && math.dot(b.Direction,hit.SurfaceNormal)<-.01f && hit.Fraction<nearest)
                        { nearest=hit.Fraction; normal=hit.SurfaceNormal; }
                    if(nearest<=1)
                    {
                        b.Direction=Redirect(b.Direction,player.IsAvailable?player.Position-pose.ValueRO.Position:float3.zero,normal,tuning.ValueRO.Aim);
                        b.Bounces++;
                    }
                    pose.ValueRW.Rotation=quaternion.LookRotationSafe(b.Direction,math.up());
                    velocity.ValueRW=new PhysicsVelocity { Linear=b.Direction*b.Speed };
                }
                else
                {
                    velocity.ValueRW=new PhysicsVelocity();
                    if(b.Phase==RollingPhase.WindUp) pose.ValueRW.Rotation=quaternion.LookRotationSafe(b.Direction,math.up());
                }
            }
            hits.Dispose();
        }

        internal static float3 Redirect(float3 incoming,float3 playerOffset,float3 normal,RollingAim aim)
        {
            normal=math.normalizesafe(new float3(normal.x,0,normal.z));
            var reflected=math.normalizesafe(math.reflect(incoming,normal),-incoming);
            var desired=math.normalizesafe(new float3(playerOffset.x,0,playerOffset.z),reflected);
            if(aim==RollingAim.Reflect) return reflected;
            // A player behind the contacted obstacle must not pull the boss into the same wall.
            float away=math.dot(desired,normal);
            if(away<.2f) desired=math.normalizesafe(desired+normal*(.2f-away),reflected);
            return math.dot(desired,normal)>.01f?desired:reflected;
        }
    }
}
