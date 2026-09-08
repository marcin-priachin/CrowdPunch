using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    public partial struct EnemyGroundConstraintSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<EnemyGroundConstraint>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionWorld world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var hits = new NativeList<RaycastHit>(Allocator.Temp);
            foreach (var (ground, transform, velocity, collider) in
                     SystemAPI.Query<RefRW<EnemyGroundConstraint>, RefRW<LocalTransform>,
                         RefRW<PhysicsVelocity>, RefRO<PhysicsCollider>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>())
            {
                if (ground.ValueRO.HasGround == 0 && collider.ValueRO.IsValid)
                {
                    hits.Clear();
                    float3 position = transform.ValueRO.Position;
                    var ray = new RaycastInput
                    {
                        Start = position,
                        End = position - new float3(0f, 1000f, 0f),
                        Filter = collider.ValueRO.Value.Value.GetCollisionFilter()
                    };
                    world.CastRay(ray, ref hits);
                    float fraction = float.MaxValue;
                    foreach (RaycastHit hit in hits)
                    {
                        // Other enemies must never become the floor of a stack.
                        if (hit.RigidBodyIndex < world.NumDynamicBodies || hit.SurfaceNormal.y < 0.99f
                            || hit.Fraction >= fraction) continue;
                        fraction = hit.Fraction;
                        Aabb bounds = collider.ValueRO.Value.Value.CalculateAabb(
                            new RigidTransform(transform.ValueRO.Rotation, position), transform.ValueRO.Scale);
                        ground.ValueRW.Height = hit.Position.y + position.y - bounds.Min.y;
                        ground.ValueRW.HasGround = 1;
                    }
                }
                EnemyGroundProjection.Apply(ref ground.ValueRW, ref transform.ValueRW, ref velocity.ValueRW);
            }
            hits.Dispose();
        }
    }

    internal static class EnemyGroundProjection
    {
        public static void Apply(ref EnemyGroundConstraint ground, ref LocalTransform transform,
            ref PhysicsVelocity velocity)
        {
            if (ground.HasGround == 0) return;
            if (ground.IsLocked == 0 && ground.SnapRequested == 0
                && transform.Position.y > ground.Height + 0.02f) return;
            ground.IsLocked = 1;
            ground.SnapRequested = 0;
            // COMBAT-018: only the constrained axis is corrected; solver XZ momentum survives.
            transform.Position.y = ground.Height;
            velocity.Linear.y = 0f;
        }
    }
}
