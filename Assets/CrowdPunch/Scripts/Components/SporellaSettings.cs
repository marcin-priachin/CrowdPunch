using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Components
{
    public enum SporellaOwnershipInteractions : byte
    {
        Both,
        PunchRedirectOnly,
        LaunchedEnemyTriggerOnly
    }

    public enum SporellaImpactDirection : byte
    {
        IncomingOrOutgoing,
        IncomingOnly
    }

    [Serializable]
    public struct SporellaSettings : IComponentData
    {
        public SporellaOwnershipInteractions OwnershipInteractions;
        public SporellaImpactDirection ImpactDirection;
        [FormerlySerializedAs("ShotsPerBurst"), Min(1)] public int ShotsPerPass;
        [Min(1)] public int SweepPasses;
        [Min(.01f)] public float ShotInterval;
        [Min(.01f)] public float Windup;
        [Min(.01f)] public float ForwardSpeed;
        [Min(0f), Tooltip("Lateral distance from the aim point to the outside shot, in meters.")]
        public float SweepHalfWidth;
        [Min(.01f)] public float Range;
        [Min(.01f)] public float Height;
        [Min(.01f)] public float EnemyDamage;

        public bool CanRedirect => OwnershipInteractions != SporellaOwnershipInteractions.LaunchedEnemyTriggerOnly;
        public bool CanTrigger(bool outgoing) => OwnershipInteractions != SporellaOwnershipInteractions.PunchRedirectOnly
            && (!outgoing || ImpactDirection == SporellaImpactDirection.IncomingOrOutgoing);

        public static SporellaSettings Default => new SporellaSettings
        {
            OwnershipInteractions = SporellaOwnershipInteractions.Both,
            ImpactDirection = SporellaImpactDirection.IncomingOrOutgoing,
            ShotsPerPass = 3, SweepPasses = 2, ShotInterval = .18f, Windup = .65f, ForwardSpeed = 14f,
            SweepHalfWidth = 1.5f, Range = 26f, Height = .94f, EnemyDamage = 10f
        };
    }
}
