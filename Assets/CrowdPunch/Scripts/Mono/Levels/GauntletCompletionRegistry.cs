namespace CrowdPunch.Mono.Levels
{
    /// <summary>Process-local handoff from ECS encounter completion to scene flow.</summary>
    public static class GauntletCompletionRegistry
    {
        public static uint Sequence { get; private set; }

        public static void ReportCompletion()
        {
            Sequence++;
        }
    }
}
