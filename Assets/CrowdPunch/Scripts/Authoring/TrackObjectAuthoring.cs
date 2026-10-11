using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeAuthoring))]
    public sealed class TrackObjectAuthoring : MonoBehaviour
    {
        public TrackObjectSettings settings;
        public Transform destination;
        [Tooltip("Ordered turns between the block's starting position and destination. Empty keeps a straight rail.")]
        public Transform[] corners = System.Array.Empty<Transform>();
    }
}
