using CrowdPunch.Configuration;
using UnityEngine;
namespace CrowdPunch.Authoring
{
    public sealed class RollingBossAuthoring : MonoBehaviour
    {
        public RollingBossSettings settings;
        [HideInInspector] public Vector3 animationPivot;
    }
}
