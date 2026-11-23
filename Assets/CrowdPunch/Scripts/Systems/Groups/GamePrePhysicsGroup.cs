using Unity.Entities;
using Unity.Physics.Systems;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs gameplay intent systems before Unity Physics steps the world.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial class GamePrePhysicsGroup : ComponentSystemGroup
    {
        private uint observedRestart;
        private bool physicsRebuildPending;

        protected override void OnCreate()
        {
            base.OnCreate();
            observedRestart = GameRestartRegistry.Sequence;
        }

        /// <summary>Allows Unity Physics to rebuild after restart destroys collider-owning entities.</summary>
        public void RequestPhysicsRebuild() { physicsRebuildPending = true; }

        public bool CanQueryPhysicsWorld => !physicsRebuildPending && observedRestart == GameRestartRegistry.Sequence;

        protected override void OnUpdate()
        {
            if (!CanQueryPhysicsWorld)
            {
                observedRestart = GameRestartRegistry.Sequence;
                physicsRebuildPending = false;
                // Pre-physics queries normally read the previous step's collision world. After
                // scene teardown or pooled-Dasher destruction those collider blobs can be freed.
                // Let the following PhysicsSystemGroup rebuild once before querying them again.
                return;
            }
            if (FeedbackTimeController.IsTransitioning) return;
            base.OnUpdate();
        }
    }
}
