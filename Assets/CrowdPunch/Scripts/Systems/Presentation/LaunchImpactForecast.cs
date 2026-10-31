using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    internal struct LaunchImpactForecast
    {
        public Entity Source, Victim, Followup;
        public uint SourceLifetime, VictimLifetime, FollowupLifetime, SourceLaunchSequence;
        public float3 Direction, VictimPosition;
        public float ArrivalSeconds, Impulse, Confidence;
    }
}
