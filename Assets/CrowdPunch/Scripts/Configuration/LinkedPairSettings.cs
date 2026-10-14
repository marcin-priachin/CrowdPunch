using CrowdPunch.Components;
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

        public LinkedPairTuning Tuning => new LinkedPairTuning {
            Response = response, MaximumLength = Mathf.Max(.1f, maximumLength), Tension = Mathf.Max(0, tension),
            RadialDamping = Mathf.Max(0, radialDamping), MaximumTensionAcceleration = Mathf.Max(0, maximumTensionAcceleration),
            MoveSpeed = Mathf.Max(0, moveSpeed), StraddleDistance = Mathf.Max(0, straddleDistance), SweepSpeed = Mathf.Max(0, sweepSpeed),
            Width = Mathf.Max(.05f, width), PlayerDamage = Mathf.Max(0, playerDamage), EnemyDamage = Mathf.Max(0, enemyDamage),
            TickInterval = Mathf.Max(.01f, tickInterval), LaunchSpeed = Mathf.Max(0, launchSpeed), PushSpeed = Mathf.Max(0, pushSpeed),
            FlightLimit = Mathf.Max(.1f, flightLimit), SettleSpeed = Mathf.Max(0, settleSpeed), SettlePeriod = Mathf.Max(0, settlePeriod),
            RecoveryDuration = Mathf.Max(0, recoveryDuration), SnagBraking = Mathf.Max(0, snagBraking) };
    }
}
