using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // ENEMY-014: hide equipment without destroying pooled entities or changing model scale.
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct EnemyArmorVisualSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ArmorVisibilityJob
            {
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile, WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct ArmorVisibilityJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;

            private void Execute(in EnemyArmorVisual visual, EnabledRefRW<MaterialMeshInfo> visible)
            {
                visible.ValueRW = Armors.HasComponent(visual.Owner)
                    && Armors[visual.Owner].Stages >= visual.StagesRequired;
            }
        }
    }
}
