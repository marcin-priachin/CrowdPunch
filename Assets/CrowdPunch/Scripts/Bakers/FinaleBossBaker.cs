using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class FinaleBossBaker : Baker<FinaleBossAuthoring>
    {
        public override void Bake(FinaleBossAuthoring a)
        {
            if (a.settings == null) throw new System.InvalidOperationException("Finale settings are required.");
            DependsOn(a.settings);
            var t = a.settings;
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new FinaleTuning {
                Health = math.max(1,t.health), FlightHeight = t.flightHeight, FlightOffset = t.flightOffset,
                FlightSpeed = t.flightSpeed, TurnSpeed = math.radians(t.turnDegreesPerSecond), GroundHeight = t.colliderSize.y * .5f - 1,
                FallSeconds = math.max(.1f,t.fallSeconds), DownedSeconds = math.max(1,t.downedSeconds), StunSeconds = t.stunSeconds,
                TakeoffSeconds = math.max(.1f,t.takeoffSeconds), VictorySeconds = t.victorySeconds,
                StageTwo = t.stageTwoThreshold, StageThree = t.stageThreeThreshold, ReboundSafetyDegrees = t.reboundSafetyDegrees,
                AirIntervals = t.airborneIntervals, WarningSeconds = t.warningSeconds,
                StrikeRadius = t.strikeRadius, SweepLength = t.sweepLength, SweepWidth = t.sweepWidth,
                SlamLength = t.slamLength, SlamWidth = t.slamWidth, ShockwaveRadius = t.shockwaveRadius,
                LandingRadius = t.landingRadius, PlayerDamage = t.playerDamage, EnemyDamage = t.enemyDamage,
                PushSpeed = t.pushSpeed, GroundInterval = t.groundInterval });
            AddComponent(e, new FinaleBoss { Health = t.health, Stage = 1, AttackRemaining = 4 });
            AddBuffer<FinaleHit>(e);
        }
    }
}
