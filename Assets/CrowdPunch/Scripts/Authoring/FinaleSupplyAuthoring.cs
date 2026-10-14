using UnityEngine;
namespace CrowdPunch.Authoring
{
    public sealed class FinaleSupplyAuthoring : MonoBehaviour
    {
        [Range(1,3)] public int minimumStage=1;
        public bool pairOnly, localAmmunition;
        public Vector3 bank;
        public float recallDistance=30, playerBankDistance=25;
    }
}
