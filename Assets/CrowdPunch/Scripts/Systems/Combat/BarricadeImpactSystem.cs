using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    // Resolve swept contacts before the physics build so a destroying shot retains its incoming velocity.
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(EnemyLaunchHomingSystem))]
    [UpdateAfter(typeof(EnemyGroundConstraintSystem))]
    public partial struct BarricadeImpactSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Barricade>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            double now = SystemAPI.Time.ElapsedTime;
            bool hasIntactWall = false;
            foreach (var wall in SystemAPI.Query<RefRO<Barricade>>()) hasIntactWall |= wall.ValueRO.HitsRemaining > 0;
            if (!hasIntactWall) return;
            foreach (var history in SystemAPI.Query<DynamicBuffer<BarricadeHitHistory>>())
                for (int i = history.Length - 1; i >= 0; i--)
                {
                    var h = history[i];
                    if (!em.Exists(h.Source) || !em.HasComponent<EnemyLaunchState>(h.Source)
                        || em.GetComponentData<EnemyLaunchState>(h.Source).LaunchSequence != h.LaunchSequence
                        || em.HasComponent<RespawnRequest>(h.Source) && em.IsComponentEnabled<RespawnRequest>(h.Source))
                        history.RemoveAtSwapBack(i);
                }

            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            using var wallBounds = new NativeList<Aabb>(Allocator.Temp);
            foreach (var (wall, transform) in SystemAPI.Query<RefRO<Barricade>, RefRO<LocalTransform>>())
                if (wall.ValueRO.HitsRemaining > 0)
                    wallBounds.Add(wall.ValueRO.IntactCollider.Value.CalculateAabb(
                        new RigidTransform(transform.ValueRO.Rotation, transform.ValueRO.Position), transform.ValueRO.Scale));
            var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach (var (transform, collider, launch, velocity, source) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsCollider>, RefRO<EnemyLaunchState>, RefRO<PhysicsVelocity>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched || !collider.ValueRO.Value.IsCreated) continue;
                float3 incoming = velocity.ValueRO.Linear;
                if (math.lengthsq(incoming.xz) < .0001f) continue;
                float3 displacement = incoming * SystemAPI.Time.DeltaTime;
                var bounds = collider.ValueRO.Value.Value.CalculateAabb(
                    new RigidTransform(transform.ValueRO.Rotation, transform.ValueRO.Position), transform.ValueRO.Scale);
                bounds.Min = math.min(bounds.Min, bounds.Min + displacement);
                bounds.Max = math.max(bounds.Max, bounds.Max + displacement);
                bool nearWall = false;
                foreach (var wallAabb in wallBounds) nearWall |= bounds.Overlaps(wallAabb);
                if (!nearWall) continue;
                hits.Clear();
                var input = new ColliderCastInput(collider.ValueRO.Value, transform.ValueRO.Position,
                    transform.ValueRO.Position + displacement, transform.ValueRO.Rotation, transform.ValueRO.Scale);
                world.CastCollider(input, ref hits);
                ColliderCastHit closest = default;
                float fraction = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.Entity == source || math.abs(hit.SurfaceNormal.y) > .7f || hit.Fraction >= fraction) continue;
                    if (em.HasComponent<Barricade>(hit.Entity) && em.GetComponentData<Barricade>(hit.Entity).HitsRemaining <= 0) continue;
                    if (math.dot(incoming, hit.SurfaceNormal) >= -.001f) continue;
                    closest = hit; fraction = hit.Fraction;
                }
                if (fraction == float.MaxValue || !em.HasComponent<Barricade>(closest.Entity)) continue;
                var wall = em.GetComponentData<Barricade>(closest.Entity);
                if (BarricadeHitResolution.Allows(wall.Sources, launch.ValueRO.Owner))
                    BarricadeHitResolution.TryHit(em, closest.Entity, source, launch.ValueRO.LaunchSequence, now, closest.Position);

                // An explosive body requests its ordinary explosion even if this launch already hit the wall.
                if (em.HasComponent<ExplosiveDetonationRequest>(source)
                    && em.GetComponentData<ExplosiveEnemyState>(source).HasExploded == 0)
                    em.SetComponentEnabled<ExplosiveDetonationRequest>(source, true);

                if (em.GetComponentData<Barricade>(closest.Entity).HitsRemaining > 0)
                {
                    float3 normal = math.normalizesafe(new float3(closest.SurfaceNormal.x, 0, closest.SurfaceNormal.z));
                    em.GetBuffer<BarricadeRebound>(closest.Entity).Add(new BarricadeRebound {
                        Source = source, LaunchSequence = launch.ValueRO.LaunchSequence,
                        ContactCenter = transform.ValueRO.Position + displacement * closest.Fraction,
                        Normal = normal, IncomingVelocity = incoming });
                }
            }
            hits.Dispose();
        }
    }
}
