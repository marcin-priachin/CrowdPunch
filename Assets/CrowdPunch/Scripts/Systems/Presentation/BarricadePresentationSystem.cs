using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct BarricadePresentationSystem : ISystem
    {
        private uint reportedHit;
        private Entity reportedWall;

        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (wall, entity) in SystemAPI.Query<RefRO<Barricade>>().WithEntityAccess())
            {
                if (reportedWall != entity) { reportedWall = entity; reportedHit = 0; }
                if (wall.ValueRO.HitSequence == reportedHit) continue;
                reportedHit = wall.ValueRO.HitSequence;
                if (reportedHit != 0 && PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
                    bridge.ReceiveImpact(new CombatFeedbackMessage { Kind = CombatImpactKind.Environment,
                        Position = wall.ValueRO.LastHitPosition, Direction = math.up(),
                        Intensity = wall.ValueRO.HitsRemaining == 0 ? 1 : .65f });
            }
            foreach (var (visual, transform, color) in
                SystemAPI.Query<RefRW<BarricadeVisual>, RefRW<LocalTransform>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                if (!SystemAPI.HasComponent<Barricade>(visual.ValueRO.Barricade)) continue;
                var wall = SystemAPI.GetComponent<Barricade>(visual.ValueRO.Barricade);
                if (visual.ValueRO.Scale < 0)
                {
                    visual.ValueRW.Scale = transform.ValueRO.Scale;
                    visual.ValueRW.Position = transform.ValueRO.Position;
                }
                float elapsed = (float)(now - wall.LastHitTime);
                float progress = wall.HitsRemaining == 0 ? math.saturate(elapsed / wall.DebrisDuration) : 0;
                bool visible = visual.ValueRO.CrackStage == 0
                    || wall.RequiredHits - wall.HitsRemaining >= visual.ValueRO.CrackStage;
                transform.ValueRW.Scale = visible ? visual.ValueRO.Scale * (1 - progress) : 0;
                transform.ValueRW.Position = visual.ValueRO.Position + visual.ValueRO.DebrisDirection * progress;
                float flash = wall.HitSequence == 0 ? 0 : math.saturate(1 - elapsed / wall.FlashDuration);
                color.ValueRW.Value = math.lerp(visual.ValueRO.Color, new float4(1), flash);
            }
        }
    }
}
