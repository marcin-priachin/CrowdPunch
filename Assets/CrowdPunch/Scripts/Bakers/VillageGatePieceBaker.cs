using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class VillageGatePieceBaker : Baker<VillageGatePieceAuthoring>
    {
        public override void Bake(VillageGatePieceAuthoring a)
        {
            var visual=GetComponent<BarricadeVisualAuthoring>();
            if(visual.barricade==null) return;
            AddComponent(GetEntity(TransformUsageFlags.Dynamic),new VillageGatePiece {
                Gate=GetEntity(visual.barricade,TransformUsageFlags.Dynamic),
                Kind=(byte)a.kind,DamageStage=(byte)a.damageStage,Velocity=a.velocity,Spin=a.spin,
                Duration=a.duration,RestingHeight=a.restingHeight,Seed=a.seed
            });
        }
    }
}
