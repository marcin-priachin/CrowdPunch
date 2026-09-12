using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class ImpactFeedbackRecording
    {
        public static void Record(ref EnemyImpactFeedback feedback, CombatImpactKind kind,
            float3 position, float3 direction, float speed, float impulse, in EnemyLaunchState launch, double time)
        {
            if (kind != CombatImpactKind.Punch && time < feedback.NextContactTime) return;
            if (feedback.Pending != 0 && (feedback.Kind == CombatImpactKind.Punch || feedback.Speed > speed)) return;
            feedback.Pending = 1;
            feedback.Kind = kind;
            feedback.Position = position;
            feedback.Direction = math.normalizesafe(direction, math.up());
            feedback.Speed = math.max(0f, speed);
            feedback.Impulse = math.max(0f, impulse);
            feedback.ChainDepth = launch.FeedbackChainDepth;
            feedback.PlayerOwned = (byte)(launch.Owner == EnemyLaunchOwner.Player ? 1 : 0);
        }
    }
}
