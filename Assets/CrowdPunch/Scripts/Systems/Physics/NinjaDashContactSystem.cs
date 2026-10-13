using CrowdPunch.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Physics
{
    // NINJA-003: omit ordinary solver pairs, retaining launched-body interruptions.
    [BurstCompile, UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateBodyPairsGroup)), UpdateBefore(typeof(PhysicsCreateContactsGroup))]
    public partial struct NinjaDashContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<NinjaState>(); state.RequireForUpdate<SimulationSingleton>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new PassThroughJob { Ninjas = SystemAPI.GetComponentLookup<NinjaState>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true) }
                .Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }
        public static bool Suppress(in NinjaState ninja, EnemyLaunchPhase ninjaLaunch, EnemyLaunchPhase otherLaunch, EnemyCombatTier otherTier)
            => ninjaLaunch == EnemyLaunchPhase.Active && (ninja.Phase == NinjaPhase.Dashing || ninja.HasDashSegment != 0)
                && otherTier == EnemyCombatTier.Normal && otherLaunch != EnemyLaunchPhase.Launched;

        [BurstCompile]
        private struct PassThroughJob : IBodyPairsJob
        {
            [ReadOnly] public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            public void Execute(ref ModifiableBodyPair pair)
            {
                if (Skip(pair.EntityA, pair.EntityB) || Skip(pair.EntityB, pair.EntityA)) pair.Disable();
            }
            private bool Skip(Entity ninja, Entity other) => Ninjas.HasComponent(ninja) && Launches.HasComponent(ninja)
                && Launches.HasComponent(other) && Tiers.HasComponent(other)
                && Suppress(Ninjas[ninja], Launches[ninja].Phase, Launches[other].Phase, Tiers[other].Value);
        }
    }
}
