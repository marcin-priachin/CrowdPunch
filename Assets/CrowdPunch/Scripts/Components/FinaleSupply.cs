using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Components
{
    public struct FinaleSupply : IComponentData
    {
        public int MinimumStage;
        public byte PairOnly, LocalAmmunition;
        public float3 Bank;
        public float RecallDistance, PlayerBankDistance;
    }
}
