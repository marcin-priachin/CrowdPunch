namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Process-local restart request bridge from GameObject UI into ECS systems.
    /// </summary>
    public static class GameRestartRegistry
    {
        private static uint sequence;

        public static uint Sequence => sequence;
        internal static uint CompletedSequence { get; set; }

        public static void RequestRestart()
        {
            sequence++;
        }
    }
}
