using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct ChickenTurnPlayback : IComponentData
    {
        public int Motion;
        public float AccumulatedYaw;
        public float StoppedTime;
        public float Weight;
    }

    public struct ChickenAnimationTuning : IComponentData
    {
        // Angles/rates are radians and radians per second; durations are seconds.
        public float MinimumTurnAngle;
        public float MinimumTurnSpeed;
        public float BlendInDuration;
        public float BlendOutDuration;
        public float StopDelay;

        public static ChickenAnimationTuning Default => new ChickenAnimationTuning
        {
            MinimumTurnAngle = math.radians(10f), MinimumTurnSpeed = math.radians(15f),
            BlendInDuration = .2f, BlendOutDuration = .08f, StopDelay = .06f
        };
    }
}
