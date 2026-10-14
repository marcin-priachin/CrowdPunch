using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
namespace CrowdPunch.Bakers
{
    public sealed class FinaleSupplyBaker : Baker<FinaleSupplyAuthoring>
    {
        public override void Bake(FinaleSupplyAuthoring a) => AddComponent(GetEntity(TransformUsageFlags.None),new FinaleSupply {
            MinimumStage=a.minimumStage,PairOnly=(byte)(a.pairOnly?1:0),LocalAmmunition=(byte)(a.localAmmunition?1:0),
            Bank=a.bank,RecallDistance=a.recallDistance,PlayerBankDistance=a.playerBankDistance });
    }
}
