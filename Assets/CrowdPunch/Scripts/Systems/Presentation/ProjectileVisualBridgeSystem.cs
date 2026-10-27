using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Deformations;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct ProjectileVisualBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out var bridge)) return;
            bridge.BeginProjectileVisuals();
            if (bridge.isActiveAndEnabled && !FeedbackTimeController.IsSuspended)
            {
                foreach (var (origin, attack, settings, launch, entity) in SystemAPI.Query<RefRO<RangedProjectileOrigin>,
                    RefRO<RangedAttackState>, RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
                {
                    if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || attack.ValueRO.Phase != RangedAttackPhase.WindUp) continue;
                    var muzzle = origin.ValueRO;
                    if (!SystemAPI.HasBuffer<SkinMatrix>(muzzle.Animation) || !SystemAPI.HasComponent<LocalToWorld>(muzzle.Root)) continue;
                    var skin = SystemAPI.GetBuffer<SkinMatrix>(muzzle.Animation);
                    if (skin.Length == 0) continue;
                    float3 position = math.transform(SystemAPI.GetComponent<LocalToWorld>(muzzle.Root).Value,
                        math.mul(skin[0].Value, new float4(muzzle.BindPosition, 1)));
                    bridge.ReceiveProjectileVisual(new ProjectileVisualSample
                    {
                        Key = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index,
                        Sequence = attack.ValueRO.ProjectilesSpawned,
                        Kind = ProjectileVisualKind.RangedCharge,
                        Position = position,
                        Radius = muzzle.ChargeRadius,
                        ChargeProgress = math.saturate(1 - attack.ValueRO.SecondsRemaining / math.max(.01f, settings.ValueRO.WindUpDuration))
                    });
                }
                foreach (var (shot, pose, entity) in SystemAPI.Query<RefRO<ChickenProjectile>, RefRO<LocalTransform>>().WithEntityAccess())
                {
                    var p = shot.ValueRO;
                    if (!SystemAPI.HasComponent<ChickenTuning>(p.Boss)) continue;
                    Send(bridge, entity, ProjectileVisualKind.Boss, p.Launch, pose.ValueRO.Position, p.Velocity,
                        SystemAPI.GetComponent<ChickenTuning>(p.Boss).ProjectileRadius, p.Redirected != 0);
                }
                foreach (var (shot, pose, entity) in SystemAPI.Query<RefRO<RangedProjectile>, RefRO<LocalTransform>>().WithEntityAccess())
                {
                    var p = shot.ValueRO;
                    float duration = math.max(.01f, p.TravelDuration);
                    float3 velocity = (p.Target - p.Start) / duration;
                    velocity.y += 4 * p.ArcHeight * (1 - 2 * p.ElapsedSeconds / duration) / duration;
                    Send(bridge, entity, ProjectileVisualKind.Ranged, 0, pose.ValueRO.Position, velocity, p.Radius, false);
                }
                foreach (var (segment, launch, pose, velocity, entity) in SystemAPI.Query<RefRO<SnakeSegment>, RefRO<EnemyLaunchState>,
                    RefRO<LocalTransform>, RefRO<PhysicsVelocity>>().WithNone<RespawnRequest>().WithEntityAccess())
                {
                    if (segment.ValueRO.Detached == 0 || launch.ValueRO.Phase != EnemyLaunchPhase.Launched) continue;
                    Send(bridge, entity, ProjectileVisualKind.Snake, launch.ValueRO.LaunchSequence,
                        pose.ValueRO.Position, velocity.ValueRO.Linear, segment.ValueRO.Radius, false);
                }
            }
            bridge.EndProjectileVisuals();
        }
        private static void Send(PlayerEcsBridge bridge, Entity e, ProjectileVisualKind kind, uint sequence,
            float3 position, float3 velocity, float radius, bool redirected)
        {
            bridge.ReceiveProjectileVisual(new ProjectileVisualSample { Key = ((ulong)(uint)e.Version << 32) | (uint)e.Index,
                Kind = kind, Sequence = sequence, Position = position, Velocity = velocity, Radius = radius, Redirected = redirected });
        }
    }
}
