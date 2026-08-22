using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Maintains ray-selected launch targets while enemies remain in the live punch volume.</summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    [UpdateBefore(typeof(PunchDetectionSystem))]
    public partial struct PunchAimAssistSystem : ISystem
    {
        private EntityQuery candidates;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            candidates = state.GetEntityQuery(ComponentType.ReadOnly<Enemy>(),
                ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyLaunchState>(),
                ComponentType.ReadOnly<Health>());
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasPreview = PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)
                && bridge.IsPunchPreviewAvailable
                && bridge.PunchPreviewAimAssistRange > 0f;
            float3 direction = hasPreview ? bridge.PunchPreviewDirection : default;
            direction.y = 0f;
            direction = math.normalizesafe(direction);
            PunchSpecification volume = hasPreview
                ? new PunchSpecification
                {
                    Origin = bridge.PunchPreviewOrigin,
                    Direction = direction,
                    Radius = bridge.PunchPreviewRadius,
                    Range = bridge.PunchPreviewRange,
                    AffectActive = 1,
                    AffectRecovering = 1,
                    AffectLaunched = 1
                }
                : default;

            using NativeArray<Entity> allCandidates = candidates.ToEntityArray(Allocator.Temp);
            NativeList<RaycastHit> hits = new NativeList<RaycastHit>(Allocator.Temp);
            CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState,
                         RefRO<Health> health, RefRW<PunchAimAssistTarget> aimTarget, Entity source) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>,
                             RefRW<PunchAimAssistTarget>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                bool isInPunchVolume = hasPreview
                    && PunchResolution.IsEligible(launchState.ValueRO, health.ValueRO, volume)
                    && PunchResolution.Contains(transform.ValueRO.Position, volume);
                if (!isInPunchVolume)
                {
                    aimTarget.ValueRW = default;
                    continue;
                }

                Entity rayTarget = RaycastTarget(collisionWorld, source, transform.ValueRO.Position, direction,
                    bridge.PunchPreviewAimAssistRange, ref hits);
                if (PunchAimAssist.IsValidTarget(state.EntityManager, source, rayTarget))
                {
                    aimTarget.ValueRW.Target = rayTarget;
                }
                else if (aimTarget.ValueRO.IsAiming == 0
                    || !PunchAimAssist.IsValidTarget(state.EntityManager, source, aimTarget.ValueRO.Target))
                {
                    PunchAimAssist.TryGetFallbackTarget(state.EntityManager, source, transform.ValueRO.Position,
                        direction, bridge.PunchPreviewAimAssistRange, allCandidates, out Entity fallback);
                    aimTarget.ValueRW.Target = fallback;
                }
                aimTarget.ValueRW.IsAiming = 1;
            }
            hits.Dispose();
        }

        private static Entity RaycastTarget(CollisionWorld world, Entity source, float3 start, float3 direction,
            float range, ref NativeList<RaycastHit> hits)
        {
            hits.Clear();
            RaycastInput input = new RaycastInput
            {
                Start = start,
                End = start + direction * math.max(0f, range),
                Filter = CollisionFilter.Default
            };
            if (!world.CastRay(input, ref hits)) return Entity.Null;

            Entity closest = Entity.Null;
            float closestFraction = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.Entity == source || hit.Fraction >= closestFraction) continue;
                closest = hit.Entity;
                closestFraction = hit.Fraction;
            }
            return closest;
        }
    }
}
