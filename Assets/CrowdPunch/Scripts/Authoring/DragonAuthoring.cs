using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class DragonAuthoring : MonoBehaviour
    {
        public DragonSettings settings;
        [HideInInspector] public Vector3[] mouthFrames;
    }
}
