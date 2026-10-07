using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(ChickenAttackSystem))]
    public partial struct ChickenMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt=math.max(.0001f,SystemAPI.Time.DeltaTime);
            foreach(var (boss,tuning,transform,mass,velocity) in SystemAPI.Query<RefRO<ChickenBoss>,RefRO<ChickenTuning>,RefRO<LocalTransform>,RefRO<PhysicsMass>,RefRW<PhysicsVelocity>>())
            {
                var b=boss.ValueRO; var t=tuning.ValueRO;
                float3 delta=b.Destination-transform.ValueRO.Position;
                float speed=t.RushSpeed*(b.Stage==3?t.FinalRushMultiplier:1);
                velocity.ValueRW.Linear=b.Phase==ChickenPhase.Rush?math.normalizesafe(delta)*math.min(speed,math.length(delta)/dt):float3.zero;
                velocity.ValueRW.Angular=PhysicsVelocity.CalculateVelocityToTarget(
                    mass.ValueRO,
                    transform.ValueRO.Position,transform.ValueRO.Rotation,
                    new RigidTransform(b.Facing,transform.ValueRO.Position),1/dt).Angular;
            }
        }
    }
}
