using System;
using Unity.Entities;
using UnityEngine;

namespace CrowdPunch.Components
{
    [Serializable]
    public struct MushroomShooterSettings : IComponentData
    {
        [Min(0f)] public float TrackingSeconds, LockedSeconds;
        [Min(.01f)] public float TravelRange, Height;
        [Min(0f)] public float EnemyDamage;

        public static MushroomShooterSettings Default => new MushroomShooterSettings
        {
            TrackingSeconds = .3f, LockedSeconds = .45f, TravelRange = 45f, Height = .95f, EnemyDamage = 5f
        };
    }
}
