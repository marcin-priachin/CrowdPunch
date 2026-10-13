using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(NinjaPlayerImpactSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct NinjaCollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<NinjaState>(); state.RequireForUpdate<SimulationSingleton>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new CollisionJob { Ninjas = SystemAPI.GetComponentLookup<NinjaState>(),
                Tuning = SystemAPI.GetComponentLookup<NinjaTuning>(true), Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Velocity = SystemAPI.GetComponentLookup<PhysicsVelocity>(), Now = SystemAPI.Time.ElapsedTime,
                DynamicCount = world.NumDynamicBodies }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }
        [BurstCompile]
        private struct CollisionJob : ICollisionEventsJob
        {
            public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<NinjaTuning> Tuning;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            public ComponentLookup<PhysicsVelocity> Velocity;
            public double Now;
            public int DynamicCount;
            public void Execute(CollisionEvent collision)
            {
                Resolve(collision.EntityA, collision.EntityB, collision.BodyIndexB >= DynamicCount, collision.Normal);
                Resolve(collision.EntityB, collision.EntityA, collision.BodyIndexA >= DynamicCount, collision.Normal);
            }
            private void Resolve(Entity ninja, Entity other, bool isStatic, float3 normal)
            {
                if (!Ninjas.HasComponent(ninja) || !Tuning.HasComponent(ninja)) return;
                var n = Ninjas[ninja];
                if (!n.OccupiesAmbushSlot && n.HasDashSegment == 0) return;
                bool launched = Launches.HasComponent(ninja) && Launches[ninja].Phase != EnemyLaunchPhase.Active;
                bool struck = Launches.HasComponent(other) && Launches[other].Phase == EnemyLaunchPhase.Launched;
                if (launched || struck)
                {
                    NinjaScheduling.Interrupt(ref n, Tuning[ninja], Now);
                    n.WasInactive = launched ? (byte)1 : (byte)0;
                    if (Launches.HasComponent(ninja)) n.ObservedLaunchSequence = Launches[ninja].LaunchSequence;
                    Ninjas[ninja] = n;
                    return; // Preserve the solver's normal launched momentum.
                }
                if (isStatic && math.abs(normal.y) < .5f && (n.Phase == NinjaPhase.Dashing || n.HasDashSegment != 0))
                {
                    NinjaScheduling.Recover(ref n, Tuning[ninja], Now);
                    Ninjas[ninja] = n;
                    var velocity = Velocity[ninja]; velocity.Linear.xz = float2.zero; velocity.Angular = float3.zero;
                    Velocity[ninja] = velocity;
                }
            }
        }
    }
}
