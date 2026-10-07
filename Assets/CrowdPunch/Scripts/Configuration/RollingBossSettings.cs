using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Rolling Boss Settings")]
    public sealed class RollingBossSettings : ScriptableObject
    {
        [Header("Health and three stages")]
        [Min(1)] public float health = 1800;
        [Range(.02f,.99f)] public float stageTwoThreshold = .66f;
        [Range(.01f,.98f)] public float stageThreeThreshold = .33f;
        public Vector3 pauseDurations = new Vector3(4,3.2f,2.5f);
        public Vector3 rollSpeeds = new Vector3(9,11,13);
        [Header("Cycle")]
        [Min(0)] public float openingPause = 3;
        [Min(.05f)] public float windUp = 1.2f;
        [Min(.1f)] public float rollDuration = 5;
        [Min(.1f)] public float maximumRollDuration = 9;
        [Min(1)] public int bounceLimit = 4;
        [Min(0)] public float damageProtection = .75f;
        public RollingAim rollingAim = RollingAim.ReaimOnCollision;
        public RollingEnd rollEnding = RollingEnd.Duration;
        public RollingBodyOwnership bodyOwnership = RollingBodyOwnership.PlayerOnly;
        public RollingContactDanger contactDanger = RollingContactDanger.RollingOnly;
        public RollingTargeting targeting = RollingTargeting.EveryLivingState;
        public RollingCrowdDirection crowdDirection = RollingCrowdDirection.RollDirection;
        [Header("Solid body and contacts")]
        [Min(.1f)] public float bodyRadius = 1.8f;
        [Min(.2f)] public float bodyHeight = 3.6f;
        [Min(1)] public float mass = 250;
        [Min(0)] public float playerDamage = 14;
        [Min(0)] public float playerProtection = .8f;
        [Min(0)] public float playerPush = 14;
        [Min(.05f)] public float contactInterval = .8f;
        [Min(0)] public float crowdDamage = 8;
        [Min(.1f)] public float launchSpeed = 16;
        [Min(0)] public float resistantPush = 7;
        [Min(0)] public float bodyDamageMultiplier = 1;
        [Header("Presentation")]
        public Color pauseColor = new Color(.45f,1,.35f);
        public Color windUpColor = new Color(1.6f,.7f,.08f);
        public Color rollColor = new Color(1.5f,.18f,.08f);
        public Color protectionColor = new Color(.3f,1.4f,1.8f);
        [Min(0)] public float protectionPulse = 20;
        [Min(0)] public float visualRollDegreesPerSecond = 480;

        public RollingTuning Bake() => new RollingTuning {
            Health=math.max(1,health), StageTwoThreshold=math.clamp(stageTwoThreshold,.02f,.99f),
            StageThreeThreshold=math.clamp(stageThreeThreshold,.01f,math.clamp(stageTwoThreshold,.02f,.99f)-.01f),
            PauseDurations=math.max(new float3(.05f),(float3)pauseDurations),
            RollSpeeds=math.max(new float3(.1f),(float3)rollSpeeds), OpeningPause=math.max(0,openingPause),
            WindUp=math.max(.05f,windUp), RollDuration=math.max(.1f,rollDuration),
            MaximumRollDuration=math.max(.1f,maximumRollDuration), BounceLimit=math.max(1,bounceLimit),
            Invulnerability=math.max(0,damageProtection), BodyRadius=math.max(.1f,bodyRadius),
            BodyHeight=math.max(bodyHeight,2*math.max(.1f,bodyRadius)), Mass=math.max(1,mass),
            PlayerDamage=math.max(0,playerDamage), PlayerProtection=math.max(0,playerProtection),
            PlayerPush=math.max(0,playerPush), ContactInterval=math.max(.05f,contactInterval),
            CrowdDamage=math.max(0,crowdDamage), LaunchSpeed=math.max(.1f,launchSpeed),
            ResistantPush=math.max(0,resistantPush), BodyDamageMultiplier=math.max(0,bodyDamageMultiplier),
            Aim=rollingAim, End=rollEnding, Ownership=bodyOwnership, ContactDanger=contactDanger,
            Targeting=targeting, CrowdDirection=crowdDirection,
            PauseColor=new float3(pauseColor.r,pauseColor.g,pauseColor.b),
            WindUpColor=new float3(windUpColor.r,windUpColor.g,windUpColor.b),
            RollColor=new float3(rollColor.r,rollColor.g,rollColor.b),
            ProtectionColor=new float3(protectionColor.r,protectionColor.g,protectionColor.b),
            ProtectionPulse=math.max(0,protectionPulse), VisualRollDegreesPerSecond=math.max(0,visualRollDegreesPerSecond)
        };
    }
}
