using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(EnemyWaveSequenceAuthoring))]
    public sealed class BarricadeRoomAuthoring : MonoBehaviour
    {
        public BarricadeAuthoring previousGate;
        public Vector2 minimum, maximum;
    }
}
