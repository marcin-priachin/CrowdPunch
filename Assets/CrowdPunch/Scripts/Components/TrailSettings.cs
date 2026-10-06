using System;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum TrailEnemyDamageMode : byte { LaunchedOnly, Both }
    public enum TrailImmunityMode : byte { OwnSource, AllTrailEnemies, None }
    public enum TrailAvoidanceMode : byte { None, DamagingTrails }

    [Serializable]
    public struct TrailSettings : IComponentData
    {
        public TrailEnemyDamageMode EnemyDamage;
        public TrailImmunityMode Immunity;
        public TrailAvoidanceMode Avoidance;
        public float NormalWidth, NormalLifetime, NormalDamage;
        public float LaunchedWidth, LaunchedLifetime, LaunchedDamage;
        public float TickInterval, PlayerProtection;
        public float CirclingDistance, OrbitSpeed, ApproachSpeed, RetreatSpeed, ReversalInterval;
        public float SectionSpacing, MinimumSpeed;
        public float4 NormalColor, LaunchedColor;

        public static TrailSettings Default => new TrailSettings
        {
            EnemyDamage = TrailEnemyDamageMode.Both, Immunity = TrailImmunityMode.OwnSource,
            Avoidance = TrailAvoidanceMode.DamagingTrails,
            NormalWidth = 1.2f, NormalLifetime = 5, NormalDamage = 4,
            LaunchedWidth = 2, LaunchedLifetime = 6, LaunchedDamage = 8,
            TickInterval = .75f, PlayerProtection = .35f,
            CirclingDistance = 8, OrbitSpeed = 3, ApproachSpeed = 3.5f, RetreatSpeed = 4,
            ReversalInterval = 4, SectionSpacing = .4f, MinimumSpeed = .05f,
            NormalColor = new float4(.05f, .85f, .45f, .55f),
            LaunchedColor = new float4(1, .48f, .08f, .65f)
        };
    }
}
