using System;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Attempt totals and comparison rule for scored wave clears.</summary>
    [Serializable]
    public struct LevelClearScore : IComponentData
    {
        public const int PointsPerDefeat = 100;

        public bool IsScored;
        public int Defeats;
        public int Punches;
        public int Points => Defeats * PointsPerDefeat;

        public bool IsBetterThan(LevelClearScore previous) => IsScored &&
            (!previous.IsScored || Defeats > previous.Defeats ||
                (Defeats == previous.Defeats && Punches < previous.Punches));
    }

    [InternalBufferCapacity(8)]
    public struct ScoredDefeat : IBufferElementData
    {
        public uint PunchSequence;
        public float3 WorldPosition;
    }
}
