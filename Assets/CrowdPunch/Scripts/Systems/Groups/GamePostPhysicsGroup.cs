using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs cleanup and state reconciliation after Unity Physics has simulated.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class GamePostPhysicsGroup : ComponentSystemGroup
    {
        protected override void OnUpdate()
        {
            if (!FeedbackTimeController.IsTransitioning)
            {
                base.OnUpdate();
                return;
            }

            if (!LevelEntryRegistry.WaitingForPhysics || World != Unity.Entities.World.DefaultGameObjectInjectionWorld)
                return;

            // The regular physics group has rebuilt the collision world for the streamed arena.
            EntityManager.CompleteDependencyBeforeRO<PhysicsWorldSingleton>();
            LevelEntryRegistry.WaitingForPhysics = false;
            FeedbackTimeController.SetTransitionPhysicsWarmup(false);
        }
    }
}
