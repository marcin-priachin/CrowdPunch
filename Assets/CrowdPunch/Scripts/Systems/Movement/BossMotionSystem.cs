using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    // Sole runtime velocity owner for all three kinematic parts. Physics integrates every move.
    [UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(BossHandCoordinationSystem))]
    public partial struct BossMotionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt=SystemAPI.Time.DeltaTime; if(dt<=0) return;
            foreach(var (part,target,transform,velocity) in SystemAPI.Query<RefRO<BossPart>,RefRO<BossMotionTarget>,RefRO<LocalTransform>,RefRW<PhysicsVelocity>>())
            {
                var t=SystemAPI.GetComponent<BossTuning>(part.ValueRO.Encounter);
                var boss=SystemAPI.GetComponent<BossEncounter>(part.ValueRO.Encounter);
                if(boss.Cycle==BossCycle.Defeated || !SystemAPI.HasSingleton<PlayerSnapshot>() || !SystemAPI.GetSingleton<PlayerSnapshot>().IsAvailable)
                { velocity.ValueRW=default; continue; }
                float3 goal=BossPerimeterRoute.Clamp(target.ValueRO.Position,t,part.ValueRO.Radius);
                float3 offset=goal-transform.ValueRO.Position;
                float maxSpeed=part.ValueRO.Kind==BossPartKind.Head?t.MoveSpeed:t.HandSpeed;
                float3 desired=math.normalizesafe(offset)*math.min(maxSpeed,math.length(offset)/dt);
                float3 delta=desired-velocity.ValueRO.Linear;
                velocity.ValueRW.Linear+=math.normalizesafe(delta)*math.min(math.length(delta),t.HandAcceleration*dt);
                // Do not overshoot a stationary goal while braking.
                if(math.lengthsq(velocity.ValueRO.Linear*dt)>math.lengthsq(offset) && math.dot(velocity.ValueRO.Linear,offset)>0)
                    velocity.ValueRW.Linear=offset/dt;
                // Retargeting can reverse a fast hand at the edge; cap the next physics step
                // before residual outward momentum carries its enlarged collider out of court.
                velocity.ValueRW.Linear=LimitToBounds(transform.ValueRO.Position,velocity.ValueRO.Linear,dt,t,part.ValueRO.Radius);
                float3 forward=math.forward(transform.ValueRO.Rotation), wanted=math.forward(target.ValueRO.Rotation);
                float yaw=math.atan2(wanted.x,wanted.z)-math.atan2(forward.x,forward.z);
                yaw=math.atan2(math.sin(yaw),math.cos(yaw));
                velocity.ValueRW.Angular=new float3(0,math.clamp(yaw/dt,-1.2f,1.2f),0);
            }
        }

        internal static float3 LimitToBounds(float3 position,float3 velocity,float dt,in BossTuning t,float radius)
        {
            float2 minimum=t.Center-t.BoundsExtents+radius;
            float2 maximum=t.Center+t.BoundsExtents-radius;
            velocity.xz=math.clamp(velocity.xz,(minimum-position.xz)/dt,(maximum-position.xz)/dt);
            return velocity;
        }
    }
}
