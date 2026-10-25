using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid.Baking;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // Entities Graphics creates separate rendering entities for skinned submeshes.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct EnemyAnimationVisualBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (animation, additional, animationEntity) in SystemAPI.Query<RefRO<EnemyAnimation>, DynamicBuffer<AdditionalEntitiesBakingData>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                bool chicken = SystemAPI.HasComponent<ChickenBoss>(animation.ValueRO.Owner);
                bool rolling = SystemAPI.HasComponent<RollingBoss>(animation.ValueRO.Owner);
                bool dino = SystemAPI.HasComponent<DinoBoss>(animation.ValueRO.Owner);
                bool gatekeeper = SystemAPI.HasComponent<BossPart>(animation.ValueRO.Owner);
                if (animation.ValueRO.SpawnMotion >= 0 && animation.ValueRO.SpawnMotion < EnemyAnimationSamples.MotionCount
                    && SystemAPI.HasComponent<EnemySpawnAnimation>(animation.ValueRO.Owner))
                    commands.SetComponent(animation.ValueRO.Owner, new EnemySpawnAnimation
                    {
                        Duration = animation.ValueRO.Samples.Value.Durations[animation.ValueRO.SpawnMotion]
                    });
                if (!chicken && !rolling && !dino && !gatekeeper) commands.AddComponent(animation.ValueRO.Owner, new EnemyLandingAnimation
                {
                    Duration = animation.ValueRO.Samples.Value.Durations[EnemyAnimationSamples.ImpactMotion],
                    OrdinaryDefeatDuration = animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Explosive
                        ? animation.ValueRO.Samples.Value.Durations[8] : 0
                });
                foreach (var child in additional)
                {
                    Entity entity = child.Value;
                    if (!SystemAPI.HasComponent<RenderBounds>(entity)) continue;
                    if (SystemAPI.HasComponent<EnemyAnimationGrounding>(animationEntity))
                        commands.AddComponent(entity, new EnemyGroundingVisual
                        {
                            Animation = animationEntity,
                            AuthoredBounds = SystemAPI.GetComponent<RenderBounds>(entity).Value
                        });
                    if (animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Wizard)
                        commands.AddComponent(entity, new EnemyAnimationBounds
                        {
                            Animation = animationEntity,
                            Original = SystemAPI.GetComponent<RenderBounds>(entity).Value
                        });
                    if (entity != animationEntity && SystemAPI.HasComponent<EnemyArmorVisual>(animationEntity))
                        commands.AddComponent(entity, SystemAPI.GetComponent<EnemyArmorVisual>(animationEntity));
                    if (gatekeeper) commands.AddComponent(entity, new BossVisualOwner { Value = animation.ValueRO.Owner, Color = new float4(1) });
                    else if (dino) commands.AddComponent(entity, new DinoVisualOwner { Value = animation.ValueRO.Owner });
                    else if (rolling) continue; // Spike uses authored animation and its constant source palette.
                    else if (chicken) commands.AddComponent(entity, new ChickenVisualOwner { Value = animation.ValueRO.Owner });
                    else
                    {
                        commands.AddComponent<EnemyVisualDeformation>(entity);
                        commands.AddComponent(entity, new EnemyVisualOwner { Value = animation.ValueRO.Owner, UsesAtlas = 1,
                            AnimationDriven = (byte)(animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Goblin
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.GoblinArmored
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Dasher
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Dragon
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Ninja
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Trail
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Wizard
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Slime
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.SlimeSegment
                                || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.Explosive ? 1 : 0) });
                        if (SystemAPI.HasComponent<SnakeHead>(animation.ValueRO.Owner)
                            || animation.ValueRO.Profile == (byte)EnemyAnimationProfile.SlimeSegment)
                        {
                            commands.AddComponent(entity, new SnakeBodyColor { Value = new float4(1f) });
                            commands.AddComponent<SnakeBodyTintAmount>(entity);
                        }
                    }
                    if (!gatekeeper) commands.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
                }
            }
            commands.Playback(state.EntityManager);
        }
    }
}
