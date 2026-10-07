using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Chicken Boss Settings")]
    public sealed class ChickenBossSettings : ScriptableObject
    {
        [Header("Health and stages")]
        [Min(1)] public float health = 1800;
        [Range(.01f,.99f)] public float stageTwoThreshold = .66f;
        [Range(.01f,.99f)] public float stageThreeThreshold = .33f;
        public ChickenPattern stageOnePattern = ChickenPattern.Single;
        public ChickenPattern stageTwoPattern = ChickenPattern.Paired;
        public ChickenPattern stageThreePattern = ChickenPattern.Paired;
        [Header("Attack cycle")]
        public ChickenPauseResponse pauseResponse = ChickenPauseResponse.InterruptAndFlee;
        public ChickenShotAim shotAim = ChickenShotAim.PlayerPosition;
        [Min(0)] public float openingPause = 2.5f;
        [Min(.05f)] public float windUp = .8f;
        [Tooltip("Minimum time between releases, including reposition and next wind-up.")]
        [Min(.1f)] public float shotSpacing = 1.5f;
        [Min(.05f)] public float pause = 1.6f;
        [Range(.1f,1)] public float finalPauseMultiplier = .55f;
        [Header("Committed rush")]
        [Min(.1f)] public float rushSpeed = 13;
        [Min(1)] public float finalRushMultiplier = 1.35f;
        [Min(1)] public float rushDistance = 9;
        [Min(0)] public float sidewaysWeight = 1.5f;
        [Min(0)] public float proximityDistance = 7;
        [Min(.05f)] public float stagger = .65f;
        [Min(0)] public float invulnerability = 1.15f;
        [Min(.1f)] public float bodyRadius = 1.7f;
        [Min(.2f)] public float bodyHeight = 3.4f;
        [Min(0)] public float playerDamage = 14;
        [Min(0)] public float playerProtection = .8f;
        [Min(0)] public float playerPush = 14;
        [Header("Bouncing projectiles")]
        [Min(.05f)] public float projectileRadius = .45f;
        public float projectileHeight = 1;
        [Min(.1f)] public float fireSpeed = 10;
        [Min(.1f)] public float returnSpeed = 22;
        [Min(.1f)] public float lifetime = 9;
        [Min(1)] public int bounceLimit = 5;
        [Min(0)] public float projectileDamage = 12;
        [Min(.1f)] public float projectileLaunchSpeed = 16;
        [Min(0)] public float returnedBossDamage = 75;
        [Min(0)] public float bodyDamageMultiplier = 1;

        public ChickenTuning Bake() => new ChickenTuning {
            Health=math.max(1,health), StageTwoThreshold=math.clamp(stageTwoThreshold,.02f,.99f),
            StageThreeThreshold=math.clamp(stageThreeThreshold,.01f,math.max(.01f,stageTwoThreshold-.01f)),
            StageOnePattern=stageOnePattern, StageTwoPattern=stageTwoPattern, StageThreePattern=stageThreePattern,
            PauseResponse=pauseResponse, ShotAim=shotAim, OpeningPause=math.max(0,openingPause),
            WindUp=math.max(.05f,windUp), ShotSpacing=math.max(.1f,shotSpacing), Pause=math.max(.05f,pause),
            FinalPauseMultiplier=math.clamp(finalPauseMultiplier,.1f,1), RushSpeed=math.max(.1f,rushSpeed),
            FinalRushMultiplier=math.max(1,finalRushMultiplier), RushDistance=math.max(1,rushDistance),
            SidewaysWeight=math.max(0,sidewaysWeight), ProximityDistance=math.max(0,proximityDistance),
            Stagger=math.max(.05f,stagger), Invulnerability=math.max(0,invulnerability),
            BodyRadius=math.max(.1f,bodyRadius), BodyHeight=math.max(bodyHeight,2*bodyRadius),
            PlayerDamage=math.max(0,playerDamage), PlayerProtection=math.max(0,playerProtection), PlayerPush=math.max(0,playerPush),
            ProjectileRadius=math.max(.05f,projectileRadius), ProjectileHeight=projectileHeight,
            FireSpeed=math.max(.1f,fireSpeed), ReturnSpeed=math.max(.1f,returnSpeed), Lifetime=math.max(.1f,lifetime),
            BounceLimit=math.max(1,bounceLimit), ProjectileDamage=math.max(0,projectileDamage),
            ProjectileLaunchSpeed=math.max(.1f,projectileLaunchSpeed), ReturnedBossDamage=math.max(0,returnedBossDamage),
            BodyDamageMultiplier=math.max(0,bodyDamageMultiplier)
        };
    }
}
