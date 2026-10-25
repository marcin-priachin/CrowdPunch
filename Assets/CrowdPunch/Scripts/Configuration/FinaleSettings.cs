using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Finale Settings")]
    public sealed class FinaleSettings : ScriptableObject
    {
        [Min(1)] public float health = 6000;
        public float flightHeight = 6, flightOffset = 22, flightSpeed = 9, turnDegreesPerSecond = 60;
        public float cameraLookHeight = 4;
        public Vector3 colliderSize = new(10, 8, 10);
        public float fallSeconds = 2.5f, downedSeconds = 24, stunSeconds = 7, takeoffSeconds = 2.5f, victorySeconds = 3;
        [Range(0,1)] public float stageTwoThreshold = .67f, stageThreeThreshold = .34f;
        public Vector3 airborneIntervals = new(4, 3.2f, 2.6f), warningSeconds = new(1.5f, 1.3f, 1.1f);
        public float strikeRadius = 4, sweepLength = 26, sweepWidth = 4, slamLength = 13, slamWidth = 7;
        public float shockwaveRadius = 12, landingRadius = 11, groundInterval = 3;
        public float playerDamage = 1, enemyDamage = 15, pushSpeed = 13;
        [Range(0,80)] public float reboundSafetyDegrees = 35;
    }
}
