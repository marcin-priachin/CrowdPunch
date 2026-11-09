using CrowdPunch.Mono.Player;
using Unity.Entities;

namespace CrowdPunch.Systems.Initialization
{
    // Keep the real-time physics cadence during the clear shot while advancing less game time per step.
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial class LevelClearFixedStepSystem : SystemBase
    {
        private FixedStepSimulationSystemGroup fixedStep;
        private float normalTimestep;

        protected override void OnUpdate()
        {
            float scale = FeedbackTimeController.LevelClearStepScale;
            if (scale >= 1f)
            {
                RestoreTimestep();
                return;
            }

            fixedStep ??= World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedStep == null) return;
            if (normalTimestep == 0f) normalTimestep = fixedStep.Timestep;
            fixedStep.Timestep = normalTimestep * scale;
        }

        protected override void OnStopRunning() => RestoreTimestep();

        private void RestoreTimestep()
        {
            if (normalTimestep == 0f) return;
            fixedStep.Timestep = normalTimestep;
            normalTimestep = 0f;
        }
    }
}
