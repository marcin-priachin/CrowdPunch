using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class ChickenBossAuthoring : MonoBehaviour
    {
        public ChickenBossSettings settings;
        public GameObject projectilePrefab;
        public Vector2 arenaCenter;
        public Vector2 arenaHalfSize = new Vector2(18,18);
    }
}
