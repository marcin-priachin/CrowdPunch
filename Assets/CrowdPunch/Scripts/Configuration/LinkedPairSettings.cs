using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Linked Pair Settings")]
    public sealed class LinkedPairSettings : ScriptableObject
    {
        public LinkedPairResponse response = LinkedPairResponse.DamageAndLaunch;
        [Min(.1f)] public float maximumLength = 7;
        [Tooltip("Spring acceleration per metre of excess length, in 1/s^2."), Min(0)] public float tension = 45;
        [Tooltip("Damping of outward radial relative speed, in 1/s."), Min(0)] public float radialDamping = 8;
        [Tooltip("Acceleration cap per endpoint, in m/s^2."), Min(0)] public float maximumTensionAcceleration = 100;
        [Min(0)] public float moveSpeed = 4, straddleDistance = 2.8f, sweepSpeed = 1.6f;
        [Tooltip("Full damaging diameter in metres."), Min(.05f)] public float width = .45f;
        [Min(0)] public float playerDamage = 8, enemyDamage = 10;
        [Min(.01f)] public float tickInterval = .5f;
        [Tooltip("Initial link-victim horizontal velocity in m/s."), Min(0)] public float launchSpeed = 15;
        [Tooltip("Added horizontal speed in m/s; never starts a flight."), Min(0)] public float pushSpeed = 5;
        [Min(.1f)] public float flightLimit = 5;
        [Min(0)] public float settleSpeed = 2, settlePeriod = .35f, recoveryDuration = 1.25f;
        [Tooltip("Exponential speed decay when the link is blocked, in 1/s."), Min(0)] public float snagBraking = 12;

        [Header("Energy Streams")]
        [Range(1, 6)] public int streamCount = 3;
        [Tooltip("Full width of the brightest stream core in metres."), Min(.01f)] public float streamWidth = .09f;
        [Tooltip("How far the strands weave from the centre in metres."), Min(0)] public float streamSpread = .396f;
        [Tooltip("Distance between the main wave crests in metres."), Min(1)] public float streamWavelength = 8.27f;
        [Tooltip("Glow width as a multiple of the stream core width."), Min(1)] public float glowWidth = 4;
        [Tooltip("Ground flow in cycles per second."), Min(0)] public float groundFlowSpeed = .4f;
        [Tooltip("Airborne flow in cycles per second."), Min(0)] public float flightFlowSpeed = .73f;

        [Header("Ground Palette")]
        public Color groundCore = new(1, .79f, .43f);
        public Color groundHighlight = new(1, .98f, .86f);
        [Tooltip("Alpha controls the glow strength.")] public Color groundGlow = new(1, .59f, .16f, .36f);

        [Header("Flight Palette")]
        public Color flightCore = new(1, .58f, .21f);
        public Color flightHighlight = new(1, .94f, .73f);
        [Tooltip("Alpha controls the glow strength.")] public Color flightGlow = new(1, .31f, .045f, .42f);

        [Header("Sparks")]
        public Color sparkColor = new(1, .92f, .64f);
        [Tooltip("Sparks per second per link. Set to zero to disable."), Range(0, 48)] public float sparkRate = 18;
        [Tooltip("Spark lifetime in seconds."), Range(.05f, .65f)] public float sparkLifetime = .32f;
        [Tooltip("Spark streak length in metres."), Min(.01f)] public float sparkLength = .36f;
        [Tooltip("Distance a spark travels away from the stream in metres."), Min(0)] public float sparkTravel = .9f;

        public LinkedPairTuning Tuning => new LinkedPairTuning {
            Response = response, MaximumLength = Mathf.Max(.1f, maximumLength), Tension = Mathf.Max(0, tension),
            RadialDamping = Mathf.Max(0, radialDamping), MaximumTensionAcceleration = Mathf.Max(0, maximumTensionAcceleration),
            MoveSpeed = Mathf.Max(0, moveSpeed), StraddleDistance = Mathf.Max(0, straddleDistance), SweepSpeed = Mathf.Max(0, sweepSpeed),
            Width = Mathf.Max(.05f, width), PlayerDamage = Mathf.Max(0, playerDamage), EnemyDamage = Mathf.Max(0, enemyDamage),
            TickInterval = Mathf.Max(.01f, tickInterval), LaunchSpeed = Mathf.Max(0, launchSpeed), PushSpeed = Mathf.Max(0, pushSpeed),
            FlightLimit = Mathf.Max(.1f, flightLimit), SettleSpeed = Mathf.Max(0, settleSpeed), SettlePeriod = Mathf.Max(0, settlePeriod),
            RecoveryDuration = Mathf.Max(0, recoveryDuration), SnagBraking = Mathf.Max(0, snagBraking) };

        public LinkedPairAppearance Appearance => new LinkedPairAppearance
        {
            StreamCount = Mathf.Clamp(streamCount, 1, 6),
            StreamWidth = Mathf.Max(.01f, streamWidth),
            StreamSpread = Mathf.Max(0, streamSpread),
            StreamWavelength = Mathf.Max(1, streamWavelength),
            GlowWidth = Mathf.Max(1, glowWidth),
            GroundFlowSpeed = Mathf.Max(0, groundFlowSpeed),
            FlightFlowSpeed = Mathf.Max(0, flightFlowSpeed),
            GroundCore = Rgba(groundCore), GroundHighlight = Rgba(groundHighlight), GroundGlow = Rgba(groundGlow),
            FlightCore = Rgba(flightCore), FlightHighlight = Rgba(flightHighlight), FlightGlow = Rgba(flightGlow),
            SparkColor = Rgba(sparkColor),
            SparkRate = Mathf.Clamp(sparkRate, 0, 48),
            SparkLifetime = Mathf.Clamp(sparkLifetime, .05f, .65f),
            SparkLength = Mathf.Max(.01f, sparkLength),
            SparkTravel = Mathf.Max(0, sparkTravel)
        };

        private static float4 Rgba(Color color) => new(color.r, color.g, color.b, color.a);
    }
}
