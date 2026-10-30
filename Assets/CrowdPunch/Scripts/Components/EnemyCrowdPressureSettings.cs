using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum CrowdParticipation : byte { Regional, WholeArena }

    /// <summary>Baked crowd tactics tuning; Baselines use groups rather than a global attacker cap.</summary>
    public struct EnemyCrowdPressureSettings : IComponentData
    {
        public CrowdParticipation Participation;
        public float PursuitShare;
        public float EngagementRadius;
        public float DisengagementMargin;
        public float PredictionSeconds;
        public float MaximumPredictionDistance;
        public float FlankDistance;
        public float CommitmentSeconds;
        public float CloseInSeconds;
        public float GroupRadius;
        public int ReserveGroupCount;
        public int MinimumInterceptionGroupSize;
        public int MaximumApproachingExplosives;
        public float SurroundReassignmentSeconds;
        public float SurroundMovementRefreshDistance;
        public int MinimumDirectContactPursuers;
        public int MaximumDirectContactPursuers;
        public float DirectContactPursuerResampleSeconds;
    }
}
