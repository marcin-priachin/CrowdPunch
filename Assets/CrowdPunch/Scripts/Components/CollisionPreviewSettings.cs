namespace CrowdPunch.Components
{
    // Copied presentation tuning; never changes combat targeting or physics.
    public struct CollisionPreviewSettings
    {
        public float ConfirmationTime, StableAngleDegrees, SmoothingTime, FadeInTime, FadeOutTime;
        public float MinimumOpacity, ForecastSeconds, CompetitionWindow, ConflictAngleDegrees;
        public int ForecastSteps;
    }
}
