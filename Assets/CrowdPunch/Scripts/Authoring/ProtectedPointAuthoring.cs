using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(EnemyWaveSequenceAuthoring))]
    public sealed class ProtectedPointAuthoring : MonoBehaviour
    {
        public ProtectedPointSettings settings;
        public Vector2 zoneCenter = new Vector2(0, -46);
        public Vector2 zoneSize = new Vector2(8, 4);
    }
}
