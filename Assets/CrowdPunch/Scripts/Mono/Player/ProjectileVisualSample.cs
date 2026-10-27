using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    public enum ProjectileVisualKind : byte { Ranged, Boss, Snake, RangedCharge }
    public struct ProjectileVisualSample
    {
        public ulong Key;
        public uint Sequence;
        public ProjectileVisualKind Kind;
        public Vector3 Position, Velocity;
        public float Radius;
        public float ChargeProgress;
        public bool Redirected;
    }
}
