using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct TrackObject : IComponentData
    {
        public float3 Start, Direction;
        public float Length, SlideDuration, PushDamage;
        public int RequiredNetHits;
        public byte PlayerBodiesOnly, FilterLaunchedExplosions, ConsumeNonMovingHits, DamagingPush, PushDamagesPlayer;
    }

    public struct TrackObjectState : IComponentData
    {
        public int TargetStep;
        public float Distance, SlideStart, Elapsed, NextDistance;
        public uint SlideSequence;
        public byte Moving, Locked;
    }

    public struct TrackPushHistory : IBufferElementData
    {
        // Entity.Null identifies the GameObject player; no MonoBehaviour stores enemy identities.
        public Entity Character;
    }

    public struct TrackSocketVisual : IComponentData
    {
        public Entity Object;
        public float4 OpenColor;
    }
}
