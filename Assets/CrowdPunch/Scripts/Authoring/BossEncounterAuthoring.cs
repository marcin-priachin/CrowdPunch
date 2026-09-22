using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class BossEncounterAuthoring : MonoBehaviour
    {
        public BossEncounterSettings settings;
        public BossPartAuthoring leftHand, rightHand;
        public EnemyWaveSequenceAuthoring crowd;
    }
}
