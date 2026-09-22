using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Boss Encounter Settings")]
    public sealed class BossEncounterSettings : ScriptableObject
    {
        [Header("BOSS-001: baked tuning (changes require SubScene rebaking)")]
        [Min(1)] public float health = 600;
        [Min(0)] public float hitInvulnerability = .85f;
        [Range(.05f,.95f)] public float stageTwoThreshold = .666667f;
        [Range(.01f,.9f)] public float stageThreeThreshold = .333333f;
        [Min(.5f)] public float transitionDuration = 2.4f;
        [Header("Rounded perimeter; all coordinates relative to arena center")]
        public Vector2 center;
        public Vector2 routeExtents = new Vector2(15, 13);
        public Vector2 boundsExtents = new Vector2(21, 19);
        [Min(1)] public float cornerRadius = 5;
        [Min(.1f)] public float moveSpeed = 3;
        [Min(0)] public float moveDistance = 7;
        public float headHeight = 1.05f, handHeight = .4f;
        [Header("Hands and openings")]
        [Min(1)] public float handSpeed = 22;
        [Min(1)] public float handAcceleration = 80;
        [Min(2)] public float openOffset = 4.7f;
        [Min(0)] public float shieldOffset = 1.45f;
        [Min(1)] public float shieldForward = 3.5f;
        [Min(1.25f)] public float openingDuration = 3;
        [Range(.75f,1)] public float stageThreeTiming = .9f;
        [Min(.5f)] public float coordinationDelay = 1.2f;
        [Header("Stagger")]
        [Min(0)] public float staggerDuration = 1.4f;
        [Min(0)] public float staggerProtection = 2.5f;
        [Min(0)] public float staggerMinimumImpulse = 2;
        [Header("Committed attacks: anticipation / active / recovery / reach / width")]
        public BossAttackTuning slam = new BossAttackTuning { Anticipation=1.6f, Active=.45f, Recovery=2.8f, Reach=17, Width=3 };
        public BossAttackTuning lunge = new BossAttackTuning { Anticipation=1.4f, Active=.7f, Recovery=2.8f, Reach=17, Width=1.5f };
        public BossAttackTuning sweep = new BossAttackTuning { Anticipation=1.8f, Active=1.2f, Recovery=3, Reach=12, Width=6 };
        [Header("Player hits and boss-owned crowd scattering")]
        [Min(0), Tooltip("Health points; ReceiveEnemyHit converts these to the player health fraction.")]
        public float playerDamage = 15;
        [Min(0)] public float playerKnockback = 12, playerInvulnerability = .8f;
        [Min(0)] public float scatterDamage = 10, scatterSpeed = 15;

        public BossTuning Bake() => new BossTuning
        {
            Health=math.max(1,health), Invulnerability=math.max(.05f,hitInvulnerability),
            StageTwoThreshold=math.clamp(stageTwoThreshold,.1f,.95f),
            StageThreeThreshold=math.clamp(stageThreeThreshold,.01f,math.clamp(stageTwoThreshold,.1f,.95f)-.05f),
            TransitionDuration=math.max(.5f,transitionDuration), Center=(float2)center,
            BoundsExtents=math.max(new float2(9), (float2)boundsExtents),
            RouteExtents=math.clamp((float2)routeExtents,new float2(4),math.max(new float2(4),(float2)boundsExtents-6)),
            RouteCornerRadius=math.max(1,cornerRadius), MoveSpeed=math.max(.1f,moveSpeed), MoveDistance=math.max(0,moveDistance),
            HeadHeight=headHeight, HandHeight=handHeight, HandSpeed=math.max(1,handSpeed), HandAcceleration=math.max(1,handAcceleration),
            OpenOffset=math.max(2,openOffset), ShieldOffset=math.max(0,shieldOffset), ShieldForward=math.max(1,shieldForward),
            OpeningDuration=math.max(1.25f,openingDuration), StageThreeTiming=math.clamp(stageThreeTiming,.75f,1),
            CoordinationDelay=math.max(.5f,coordinationDelay), StaggerDuration=math.max(0,staggerDuration),
            StaggerProtection=math.max(0,staggerProtection), StaggerMinimumImpulse=math.max(0,staggerMinimumImpulse),
            PlayerDamage=math.max(0,playerDamage), PlayerKnockback=math.max(0,playerKnockback), PlayerInvulnerability=math.max(0,playerInvulnerability),
            ScatterDamage=math.max(0,scatterDamage), ScatterSpeed=math.max(0,scatterSpeed),
            Slam=Clamp(slam), Lunge=Clamp(lunge), Sweep=Clamp(sweep)
        };

        private static BossAttackTuning Clamp(BossAttackTuning a)
        {
            a.Anticipation=math.max(.8f,a.Anticipation); a.Active=math.max(.2f,a.Active);
            a.Recovery=math.max(1.25f,a.Recovery); a.Reach=math.max(2,a.Reach); a.Width=math.max(1,a.Width); return a;
        }
    }
}
