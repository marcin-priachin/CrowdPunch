namespace CrowdPunch.Mono.Levels
{
    /// <summary>Objective failure handoff from ECS to level flow, without exposing entities.</summary>
    public static class GauntletFailureRegistry
    {
        public static uint Sequence { get; private set; }
        public static void ReportFailure() => Sequence++;
    }
}
