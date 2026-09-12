using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum CombatImpactKind : byte { Punch, EnemyCollision, Environment, PlayerDamage, DashStart, DashMove, DashEnd }

    // One bounded, strongest-pending mailbox per body, independent of gameplay requests.
    public struct EnemyImpactFeedback : IComponentData
    {
        public float3 Position, Direction, IncomingVelocity;
        public float Speed, Impulse;
        public double NextContactTime;
        public int ChainDepth;
        public CombatImpactKind Kind;
        public byte Pending, PlayerOwned;
        public float VisualRemaining, VisualDuration, FlashRemaining, FlashDuration, Intensity, Squash, Flash;
        public float3 VisualDirection;
    }
}
