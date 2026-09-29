using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeAuthoring))]
    public sealed class TrackObjectAuthoring : MonoBehaviour
    {
        public TrackObjectSettings settings;
        public Transform destination;
    }
}
