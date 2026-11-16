using System;
using Unity.Entities;
using UnityEngine;

namespace CrowdPunch.Components
{
    [Serializable]
    public struct MushroomShooterSettings : IComponentData
    {
        [Min(.01f)] public float TravelRange, Height;
        [Min(.01f)] public float BeamDuration;
        [Min(0f)] public float EnemyDamage;

        public static MushroomShooterSettings Default => new MushroomShooterSettings
        {
            TravelRange = 45f, Height = .95f,
            BeamDuration = .25f, EnemyDamage = 5f
        };
    }
}
