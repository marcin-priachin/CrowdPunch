using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class ImpactFeedbackRecording
    {
        public static bool IsObstacleContact(float3 normal) => math.abs(normal.y) < .7f;

        public static void RecordWorldContact(ref EnemyImpactFeedback feedback, float3 position, float3 normal,
            float speed, float impulse, in EnemyLaunchState launch, double time)
        {
            Record(ref feedback, IsObstacleContact(normal) ? CombatImpactKind.Obstacle : CombatImpactKind.Environment,
                position, normal, speed, impulse, launch, time);
        }

        public static void Record(ref EnemyImpactFeedback feedback, CombatImpactKind kind,
            float3 position, float3 direction, float speed, float impulse, in EnemyLaunchState launch, double time)
        {
            if (kind != CombatImpactKind.Punch && time < feedback.NextContactTime) return;
            if (kind != CombatImpactKind.Punch && feedback.Pending != 0 && (feedback.Kind == CombatImpactKind.Punch || feedback.Speed > speed)) return;
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
