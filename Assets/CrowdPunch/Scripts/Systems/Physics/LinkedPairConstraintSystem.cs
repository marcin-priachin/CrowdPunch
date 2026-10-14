using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem)), UpdateAfter(typeof(EnemyLaunchHomingSystem))]
    public partial struct LinkedPairConstraintSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<LinkedPair>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; float dt = SystemAPI.Time.DeltaTime;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            foreach (var (pairRef, tuning) in SystemAPI.Query<RefRO<LinkedPair>, RefRO<LinkedPairTuning>>()) {
                var pair = pairRef.ValueRO; var t = tuning.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(em, pair.A, out _, out _) || !em.IsEnabled(pair.A) || !em.IsEnabled(pair.B)) continue;
                float3 a = em.GetComponentData<LocalTransform>(pair.A).Position, b = em.GetComponentData<LocalTransform>(pair.B).Position;
                var va = em.GetComponentData<PhysicsVelocity>(pair.A); var vb = em.GetComponentData<PhysicsVelocity>(pair.B);
                if (LinkedPairGeometry.Blocked(world, enemies, a, b)) {
                    // A blocked rope cannot apply tension through cover. Solver contacts still own body motion.
                    if (pair.Phase == EnemyLaunchPhase.Launched) { float decay = math.exp(-t.SnagBraking * dt); va.Linear.xz *= decay; vb.Linear.xz *= decay; }
                } else {
                    float2 offset = b.xz - a.xz; float length = math.length(offset);
                    if (length > t.MaximumLength && length > .001f) {
                        float2 axis = offset / length;
                        float acceleration = math.min(t.MaximumTensionAcceleration, t.Tension * (length - t.MaximumLength)
                            + t.RadialDamping * math.max(0, math.dot(vb.Linear.xz - va.Linear.xz, axis)));
                        float2 change = axis * acceleration * dt;
                        va.Linear.xz += change; vb.Linear.xz -= change;
                    }
                }
                // Velocity corrections are swept against solids; never project endpoint positions through a wall.
                if (LinkedPairGeometry.Blocked(world, enemies, a, a + va.Linear * dt)) va.Linear.xz = float2.zero;
                if (LinkedPairGeometry.Blocked(world, enemies, b, b + vb.Linear * dt)) vb.Linear.xz = float2.zero;
                em.SetComponentData(pair.A, va); em.SetComponentData(pair.B, vb);
            }
        }
    }
}
