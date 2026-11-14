using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Enemies/Alien Spike Settings")]
    public sealed class AlienSpikeSettings : ScriptableObject
    {
        public AlienSpikeVolleyMode volleyMode = AlienSpikeVolleyMode.TimerWhileMoving;
        public AlienSpikeExploderMode exploderMode = AlienSpikeExploderMode.DetonateOnHit;
        public AlienSpikeAimMode aimMode = AlienSpikeAimMode.ChickenStyle;
        [Min(.01f)] public float cooldown = 3.6f;
        [Min(.01f)] public float telegraphDuration = .9f;
        [Min(.5f)] public float engagementRadius = 11f;
        [Min(.5f)] public float preferredFiringDistance = 7f;
        [Min(.1f)] public float speed = 9f;
        [Min(.01f)] public float radius = .13f;
        [Min(.1f)] public float lifetime = 2.2f;
        [Min(0)] public float normalDamage = 8f;
        [Min(0)] public float burstDamage = 15f;
        [Min(0)] public float playerInvincibility = .65f;
    }
}

