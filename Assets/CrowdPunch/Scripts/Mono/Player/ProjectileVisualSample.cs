using UnityEngine;
using Unity.Entities;
using CrowdPunch.Components;

namespace CrowdPunch.Mono.Player
{
    public enum ProjectileVisualKind : byte { Ranged, Boss, Snake, RangedCharge, AlienSpike, MushroomBeam, MushroomLane, SporellaEnemy, SporellaPlayer }
    public struct ProjectileVisualSample
    {
        public ulong Key;
        public uint Sequence;
        public ProjectileVisualKind Kind;
        public Vector3 Position, Velocity;
        public float Radius;
        public float ChargeProgress;
        public float LaneLength;
        public bool Redirected;
        public BlobAssetReference<AlienSpikeColorGradient> SpikeColorGradient;
    }
}
