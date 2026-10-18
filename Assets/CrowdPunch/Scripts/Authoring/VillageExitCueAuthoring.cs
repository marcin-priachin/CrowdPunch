using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class VillageExitCueAuthoring : MonoBehaviour
    {
        public BarricadeAuthoring gate;
        public Color lockedColor=new Color(.28f,.40f,.42f);
        public Color openColor=new Color(.35f,1,.75f);
        public float phase;
        public bool arrow;
    }
}
