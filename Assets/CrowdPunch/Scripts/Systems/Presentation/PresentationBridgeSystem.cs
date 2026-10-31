using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CrowdPunch.Mono.Player;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Bridges ECS simulation results to presentation-only GameObject or rendering state.
    /// </summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct PresentationBridgeSystem : ISystem
    {
        private EntityQuery correctionCandidates;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            correctionCandidates = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform>().WithAny<Enemy, Barricade>().Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerSnapshot>();
            if (SystemAPI.HasComponent<PunchRequest>(playerEntity))
            {
                PunchRequest punch = SystemAPI.GetComponent<PunchRequest>(playerEntity);
                if (punch.IsResolved)
                {
                    bridge.ReceivePunchResult(punch.Sequence, punch.HitEnemy);
                    punch.IsResolved = false;
                    SystemAPI.SetComponent(playerEntity, punch);
                }
            }

            // Preserve the displayed ECS snapshot until the next fixed step consumes the click.
            if (bridge.HasPendingPunch) return;
            bridge.BeginTrajectoryPreview();

            if (!bridge.IsPunchPreviewAvailable
                || bridge.PunchPreviewLength <= 0f && bridge.CollisionPreviewLength <= 0f)
            {
                return;
            }

            float3 origin = bridge.PunchPreviewOrigin;
            float3 punchDirection = math.normalizesafe(bridge.PunchPreviewDirection);
            PunchSpecification volume = new PunchSpecification
            {
                Origin = origin,
                Direction = punchDirection,
                Radius = bridge.PunchPreviewRadius,
                Range = bridge.PunchPreviewRange,
                Cause = EnemyLaunchCause.PlayerPunch
            };
            float positionWeight = math.saturate(bridge.PunchPreviewPositionWeight);
            using var sources = new NativeList<LaunchPreviewSource>(Allocator.Temp);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState, RefRO<Health> health,
                         RefRO<EnemyTier> tier, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyTier>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                float3 enemyPosition = transform.ValueRO.Position;
                if (!PunchResolution.Contains(state.EntityManager, enemy, enemyPosition, volume)) continue;
                if (ArmorHitResolution.IsProtected(state.EntityManager, enemy)
                    || !EnemyLaunchTransition.IsLaunchable(tier.ValueRO)
                    || !EnemyLaunchTransition.CanReceivePlayerPunch(launchState.ValueRO, health.ValueRO))
                {
                    continue;
                }

                float3 toEnemy = enemyPosition - origin;
                if (!SnakePunchOcclusion.CanReach(state.EntityManager, enemy, volume.Origin))
                {
                    continue;
                }

                float3 positionDirection = math.normalizesafe(toEnemy, punchDirection);
                float3 launchDirection;
                if (!PunchAimAssist.TryGetLockedDirection(
                        state.EntityManager, enemy, enemyPosition, out launchDirection))
                {
                    launchDirection = math.normalizesafe(
                        math.lerp(punchDirection, positionDirection, positionWeight), positionDirection);
                }
                sources.Add(new LaunchPreviewSource { Entity = enemy, Direction = launchDirection });
                if (bridge.PunchPreviewLength > 0f && SystemAPI.HasComponent<PunchAimAssistTarget>(enemy))
                {
                    var aim = SystemAPI.GetComponent<PunchAimAssistTarget>(enemy);
                    aim.PreviewDirection = launchDirection;
                    aim.PreviewTarget = PunchAimAssist.IsValidTarget(state.EntityManager, enemy, aim.Target) ? aim.Target : Entity.Null;
                    aim.PreviewRevision = bridge.TrajectoryPreviewRevision;
                    SystemAPI.SetComponent(enemy, aim);
                }
                if (bridge.PunchPreviewLength > 0f)
                    bridge.AddTrajectoryPreview(enemyPosition,
                        enemyPosition + launchDirection * bridge.PunchPreviewLength);
            }
            foreach (var (pose, shot) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ChickenProjectile>().WithEntityAccess())
            {
                if (!PunchResolution.Contains(pose.ValueRO.Position, volume)) continue;
                volume.PositionWeight = positionWeight;
                var direction = ChickenProjectilePunch.Direction(state.EntityManager, shot, pose.ValueRO.Position, volume);
                if (bridge.PunchPreviewLength > 0f && SystemAPI.HasComponent<PunchAimAssistTarget>(shot))
                {
                    var aim = SystemAPI.GetComponent<PunchAimAssistTarget>(shot);
                    aim.PreviewDirection = direction; aim.PreviewTarget = aim.Target;
                    aim.PreviewRevision = bridge.TrajectoryPreviewRevision;
                    SystemAPI.SetComponent(shot, aim);
                }
                if (bridge.PunchPreviewLength > 0f)
                    bridge.AddTrajectoryPreview(pose.ValueRO.Position, pose.ValueRO.Position + direction * bridge.PunchPreviewLength);
            }

            if (sources.Length == 0 || bridge.CollisionPreviewLength <= 0f
                || bridge.CollisionPredictionDistance <= 0f
                || !SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics)
                || !SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var settings)) return;

            using var candidates = correctionCandidates.ToEntityArray(Allocator.Temp);
            using var candidateTransforms = correctionCandidates.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            using var shown = new NativeHashSet<Entity>(sources.Length, Allocator.Temp);
            foreach (var source in sources)
            {
                if (!LaunchImpactPrediction.TryPredict(state.EntityManager, physics.CollisionWorld,
                        source, sources.AsArray(), candidates, candidateTransforms, bridge.CollisionPredictionDistance,
                        bridge.PunchPreviewStrength, settings, SystemAPI.Time.ElapsedTime,
                        hits, out Entity target, out float3 direction, shown) || !shown.Add(target)) continue;
                float3 position = state.EntityManager.GetComponentData<LocalTransform>(target).Position;
                bridge.AddTrajectoryPreview(position, position + direction * bridge.CollisionPreviewLength, true);
            }
        }
    }
}
