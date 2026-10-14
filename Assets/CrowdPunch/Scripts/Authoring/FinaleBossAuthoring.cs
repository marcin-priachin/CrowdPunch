using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeAuthoring))]
    public sealed class FinaleBossAuthoring : MonoBehaviour
    {
        public FinaleSettings settings;
    }
}
