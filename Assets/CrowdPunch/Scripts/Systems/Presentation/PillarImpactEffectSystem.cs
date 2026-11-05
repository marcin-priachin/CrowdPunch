using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // PILLAR-003/005/006: discrete visual events follow authoritative contact and fall state.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct PillarImpactEffectSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool available = PlayerBridgeRegistry.TryGetBridge(out var bridge);
            foreach (var (pillar, tuning, feedback) in SystemAPI.Query<RefRO<FallingPillar>, RefRO<PillarTuning>, RefRW<PillarFeedbackState>>())
            {
                var p = pillar.ValueRO;
                var t = tuning.ValueRO;
                ref var shown = ref feedback.ValueRW;
                if (p.FallSequence != shown.FallSequence || p.Phase == PillarPhase.Upright)
                    shown = new PillarFeedbackState { FallSequence = p.FallSequence };
                if (p.FallSequence == 0 || p.Phase == PillarPhase.Upright) continue;
                float3 axis = new float3(p.Direction.x * math.sin(p.Angle), math.cos(p.Angle), p.Direction.z * math.sin(p.Angle));
                if (p.HitBoss != 0 && shown.BossShown == 0)
                {
                    shown.BossShown = 1;
                    if (available && SystemAPI.HasComponent<LocalTransform>(p.Boss))
                    {
                        float3 boss = SystemAPI.GetComponent<LocalTransform>(p.Boss).Position;
                        float along = math.clamp(math.dot(boss - t.InitialPosition, axis), 0, t.Height);
                        float3 contact = t.InitialPosition + axis * along;
                        bridge.ReceivePillarImpact(contact, p.Direction, t.Width, true);
                    }
                }
                if (p.Angle >= math.PI * .5f - .0001f && shown.GroundShown == 0)
                {
                    shown.GroundShown = 1;
                    if (available)
                        for (int i = 1; i <= 5; i++)
                            bridge.ReceivePillarImpact(t.InitialPosition + p.Direction * (t.Height * i / 5f)
                                + new float3(0, .18f, 0), p.Direction, t.Width, false);
                }
            }
        }
    }
}
