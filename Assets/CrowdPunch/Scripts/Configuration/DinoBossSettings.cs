using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Dino Pillar Boss Settings")]
    public sealed class DinoBossSettings : ScriptableObject
    {
        [Header("Pillar-only health and chase stages")]
        [Range(1,3)] public int requiredSuccessfulHits = 3;
        public Vector3 chaseSpeeds = new Vector3(3.8f,4.5f,5.2f);
        public Vector3 chaseDurations = new Vector3(7,5.5f,4);
        [Min(1)] public float burstSpeedMultiplier = 1.9f;
        [Min(0)] public float chaseTurnDegrees = 180;
        [Min(0)] public float burstTurnDegrees = 38;
        [Min(.1f)] public float acceleration = 18;
        [Min(.05f)] public float warningDuration = 1;
        [Min(.05f)] public float burstDuration = 2.2f;
        [Min(.05f)] public float staggerDuration = 1.3f;
        [Header("Boss contacts")]
        [Min(.1f)] public float bodyRadius = 1.5f;
        [Min(.2f)] public float bodyHeight = 4;
        [Min(1)] public float mass = 300;
        [Min(0)] public float crowdPush = 6;
        [Min(0)] public float contactDamage = 12;
        [Min(0)] public float playerPush = 10;
        [Min(0)] public float playerProtection = .8f;
        [Min(.05f)] public float contactCooldown = .9f;
        [Header("Three pillars")]
        public PillarFallDirection fallDirection = PillarFallDirection.TowardBoss;
        public PillarImpactMode impactMode = PillarImpactMode.DamageAndPush;
        [Min(1)] public float pillarHeight = 9;
        [Min(.25f)] public float pillarWidth = 1.5f;
        [Min(.1f)] public float fallDuration = 1.35f;
        [Min(0)] public float fallenVisibility = .65f;
        [Min(0)] public float regenerationDelay = 4;
        [Min(0)] public float pillarEnemyDamage = 35;
        [Min(0)] public float pillarPlayerDamage = 18;
        [Min(0)] public float pillarPush = 9;
        [Min(.1f)] public float pillarLaunchSpeed = 17;
        [Header("In-world phase colors")]
        public Color chaseColor = new Color(.7f,1,.65f);
        public Color warningColor = new Color(1.5f,.85f,.15f);
        public Color burstColor = new Color(1.6f,.3f,.15f);
        public Color staggerColor = new Color(.3f,1.3f,1.7f);

        public DinoTuning BakeBoss() => new DinoTuning {
            RequiredHits=math.clamp(requiredSuccessfulHits,1,3),
            ChaseSpeeds=math.max(.1f,(float3)chaseSpeeds), ChaseDurations=math.max(.05f,(float3)chaseDurations),
            BurstMultiplier=math.max(1,burstSpeedMultiplier), ChaseTurnDegrees=math.max(0,chaseTurnDegrees),
            BurstTurnDegrees=math.min(math.max(0,burstTurnDegrees),math.max(0,chaseTurnDegrees)),
            Acceleration=math.max(.1f,acceleration), WarningDuration=math.max(.05f,warningDuration),
            BurstDuration=math.max(.05f,burstDuration), StaggerDuration=math.max(.05f,staggerDuration),
            BodyRadius=math.max(.1f,bodyRadius), BodyHeight=math.max(2*bodyRadius,bodyHeight), Mass=math.max(1,mass),
            CrowdPush=math.max(0,crowdPush), PlayerDamage=math.max(0,contactDamage), PlayerPush=math.max(0,playerPush),
            PlayerProtection=math.max(0,playerProtection), ContactInterval=math.max(.05f,contactCooldown),
            ChaseColor=Rgb(chaseColor), WarningColor=Rgb(warningColor), BurstColor=Rgb(burstColor), StaggerColor=Rgb(staggerColor)
        };
        public PillarTuning BakePillar() => new PillarTuning {
            Height=math.max(1,pillarHeight), Width=math.max(.25f,pillarWidth), FallDuration=math.max(.1f,fallDuration),
            FallenDuration=math.max(0,fallenVisibility), RegenerationDelay=math.max(0,regenerationDelay),
            EnemyDamage=math.max(0,pillarEnemyDamage), PlayerDamage=math.max(0,pillarPlayerDamage),
            PushSpeed=math.max(0,pillarPush), LaunchSpeed=math.max(.1f,pillarLaunchSpeed),
            PlayerProtection=math.max(0,playerProtection), FallDirection=fallDirection, ImpactMode=impactMode
        };
        private static float3 Rgb(Color c) => new float3(c.r,c.g,c.b);
    }
}
