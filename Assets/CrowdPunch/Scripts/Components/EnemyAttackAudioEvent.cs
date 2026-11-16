using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    internal enum EnemyAttackAudioKind : byte
    {
        AlienSpikeWindup, AlienSpikeVolley, AlienSpikeBurst,
        MushroomWindup, MushroomFire,
        SporellaWindup, SporellaFire, SporellaImpactFire
    }

    [InternalBufferCapacity(EnemyAttackAudioEvent.Capacity)]
    internal struct EnemyAttackAudioEvent : IBufferElementData
    {
        internal const int Capacity = 32;
        public EnemyAttackAudioKind Kind;
        public float3 Position;
    }
}
