using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(GroundHazardAuthoring))]
    public sealed class GroundHazardAppearanceAuthoring : MonoBehaviour
    {
        public GroundHazardStyle style = GroundHazardStyle.Quarry;
    }
}
