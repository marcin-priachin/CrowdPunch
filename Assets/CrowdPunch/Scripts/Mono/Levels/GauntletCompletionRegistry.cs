using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Process-local handoff from ECS encounter completion to scene flow.</summary>
    public static class GauntletCompletionRegistry
    {
        public static uint Sequence { get; private set; }

        public static LevelClearScore Score { get; private set; }
        public static Vector3? Focus { get; private set; }

        public static void ReportCompletion(LevelClearScore score = default, Vector3? focus = null)
        {
            Score = score;
            Focus = focus;
            Sequence++;
        }

        internal static void UpdateFocus(Vector3 position) => Focus = position;
    }
}
