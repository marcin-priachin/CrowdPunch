using System;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent]
    public sealed class AlienSpikeAuthoring : MonoBehaviour
    {
        // Normal volley releases at the first forward extension of Projectile Attack.
        public const float ReleasePhase = .4f;
        public const int SpawnMotion = 3;
        internal const float DefeatGroundClearance = .02f;
        public AlienSpikeSettings settings;
        public AnimationClip idle, move, projectileAttack, spawn, hurt, death;
        public SpikeSocket[] sockets;

#if UNITY_EDITOR
        internal static string SampleHash(AnimationClip[] clips)
        {
            var hash = new System.Text.StringBuilder();
            foreach (var clip in clips)
                hash.Append(UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(clip)));
            return hash.ToString();
        }

        internal AnimationClip[] SampleClips()
            => new[] { idle, move, projectileAttack, spawn, idle, hurt, hurt, idle, idle, idle, death };
#endif

        [Serializable]
        public struct SpikeSocket
        {
            public Vector3 idleTip, releaseTip;
            public Vector2 idleDirection, releaseDirection;
        }
    }
}

