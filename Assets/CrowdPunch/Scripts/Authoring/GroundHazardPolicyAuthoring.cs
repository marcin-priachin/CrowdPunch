using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyWaveSequenceAuthoring))]
    public sealed class GroundHazardPolicyAuthoring : MonoBehaviour
    {
        public GroundHazardSettings settings;
    }
}
