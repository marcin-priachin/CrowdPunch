using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class EnemyAttackAudioRecording
    {
        internal static void Record(this DynamicBuffer<EnemyAttackAudioEvent> events,
            EnemyAttackAudioKind kind, float3 position)
        {
            if (events.IsCreated && events.Length < EnemyAttackAudioEvent.Capacity)
                events.Add(new EnemyAttackAudioEvent { Kind = kind, Position = position });
        }
    }
}
