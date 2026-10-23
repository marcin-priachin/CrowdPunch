using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid.Baking;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct GatekeeperHandFootprintBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (rig, animation) in SystemAPI.Query<RefRO<GatekeeperRig>, RefRO<EnemyAnimation>>())
            {
                var boss = SystemAPI.GetComponent<BossEncounter>(animation.ValueRO.Owner);
                var tuning = SystemAPI.GetComponent<BossTuning>(animation.ValueRO.Owner);
                Add(ref state, commands, boss.LeftHand, rig.ValueRO.FistScale.x, tuning);
                Add(ref state, commands, boss.RightHand, rig.ValueRO.FistScale.y, tuning);
            }
            commands.Playback(state.EntityManager);
        }

        private static void Add(ref SystemState state, EntityCommandBuffer commands, Entity hand, float sizeMultiplier, in BossTuning tuning)
        {
            float physical = state.EntityManager.GetComponentData<BossPart>(hand).Radius;
            var footprint = new GatekeeperHandFootprint { Radius = math.max(physical, physical * math.max(.1f, sizeMultiplier)) + .05f };
            if (tuning.HandCourtRadius > 0 && footprint.Radius >= tuning.HandCourtRadius)
                throw new System.InvalidOperationException("Gatekeeper fist scale exceeds the court size. Reduce Left Fist Scale / Right Fist Scale.");
            if (state.EntityManager.HasComponent<GatekeeperHandFootprint>(hand)) commands.SetComponent(hand, footprint);
            else commands.AddComponent(hand, footprint);
        }
    }
}
