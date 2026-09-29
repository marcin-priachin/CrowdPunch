using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeAuthoring))]
    public sealed class ShellTargetAuthoring : MonoBehaviour
    {
        public ShellTargetSettings settings;
    }
}
