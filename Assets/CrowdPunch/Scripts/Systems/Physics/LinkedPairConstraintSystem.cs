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
                var phaseA = em.GetComponentData<EnemyLaunchState>(pair.A).Phase;
                var phaseB = em.GetComponentData<EnemyLaunchState>(pair.B).Phase;
                bool walking = phaseA == EnemyLaunchPhase.Active && phaseB == EnemyLaunchPhase.Active;
                bool flyingA = phaseA == EnemyLaunchPhase.Launched;
                bool flyingB = phaseB == EnemyLaunchPhase.Launched;
                if (!walking && !flyingA && !flyingB) continue;

                float3 a = em.GetComponentData<LocalTransform>(pair.A).Position, b = em.GetComponentData<LocalTransform>(pair.B).Position;
                var va = em.GetComponentData<PhysicsVelocity>(pair.A); var vb = em.GetComponentData<PhysicsVelocity>(pair.B);
                if (LinkedPairGeometry.Blocked(world, enemies, a, b)) {
                    float decay = math.exp(-t.SnagBraking * dt);
                    if (flyingA) va.Linear.xz *= decay;
                    if (flyingB) vb.Linear.xz *= decay;
                } else {
                    float2 offset = b.xz - a.xz; float length = math.length(offset);
                    if (length > t.MaximumLength && length > .001f) {
                        float2 axis = offset / length;
                        float extension = length - t.MaximumLength;
                        if (walking)
                        {
                            float change = TensionStep(t, extension, math.dot(vb.Linear.xz - va.Linear.xz, axis), dt);
                            va.Linear.xz += axis * change;
                            vb.Linear.xz -= axis * change;
                        }
                        else
                        {
                            // Each launched endpoint takes its own tether correction.
                            if (flyingA) va.Linear.xz += axis * TensionStep(t, extension, -math.dot(va.Linear.xz, axis), dt);
                            if (flyingB) vb.Linear.xz -= axis * TensionStep(t, extension, math.dot(vb.Linear.xz, axis), dt);
                        }
                    }
                }
                // Velocity corrections are swept against solids; never project endpoint positions through a wall.
                if (walking || flyingA)
                {
                    if (LinkedPairGeometry.Blocked(world, enemies, a, a + va.Linear * dt)) va.Linear.xz = float2.zero;
                    em.SetComponentData(pair.A, va);
                }
                if (walking || flyingB)
                {
                    if (LinkedPairGeometry.Blocked(world, enemies, b, b + vb.Linear * dt)) vb.Linear.xz = float2.zero;
                    em.SetComponentData(pair.B, vb);
                }
            }
        }

        private static float TensionStep(in LinkedPairTuning tuning, float extension, float outwardSpeed, float dt)
            => math.min(tuning.MaximumTensionAcceleration,
                tuning.Tension * extension + tuning.RadialDamping * math.max(0f, outwardSpeed)) * dt;
    }
}
