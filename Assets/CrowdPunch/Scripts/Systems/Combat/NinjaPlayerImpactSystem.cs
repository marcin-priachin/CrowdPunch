using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(Physics.NinjaCollisionSystem)), UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct NinjaPlayerImpactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<NinjaState>(); state.RequireForUpdate<PlayerSnapshot>(); }
        public static bool Intersects(float3 start, float3 end, float3 player, float radius)
        {
            float2 delta = end.xz - start.xz;
            float fraction = math.saturate(math.dot(player.xz - start.xz, delta) / math.max(.000001f, math.lengthsq(delta)));
            return math.distancesq(start.xz + fraction * delta, player.xz) <= radius * radius;
        }
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            bool hasBridge = player.IsAvailable && PlayerBridgeRegistry.TryGetBridge(out _);
            foreach (var (ninja, tuning, launch, transform, velocity, agent) in
                     SystemAPI.Query<RefRW<NinjaState>, RefRO<NinjaTuning>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<NavigationAgent>>()
                         .WithNone<RespawnRequest>())
            {
                ref var n = ref ninja.ValueRW;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    if (n.OccupiesAmbushSlot || n.HasDashSegment != 0)
                    {
                        NinjaScheduling.Interrupt(ref n, tuning.ValueRO, SystemAPI.Time.ElapsedTime);
                        n.WasInactive = 1; n.ObservedLaunchSequence = launch.ValueRO.LaunchSequence;
                    }
                    continue;
                }
                if (n.HasDashSegment == 0) continue;
                float3 end = transform.ValueRO.Position;
                n.DistanceTravelled += math.distance(n.SegmentStart.xz, end.xz);
                if (hasBridge && n.HasHitPlayer == 0 && Intersects(n.SegmentStart, end, player.Position, player.Radius + agent.ValueRO.Radius))
                {
                    n.HasHitPlayer = 1;
                    PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge);
                    bridge.ReceiveEnemyHit(tuning.ValueRO.PlayerDamage, tuning.ValueRO.PlayerInvulnerability,
                        n.LockedDirection * math.max(0, tuning.ValueRO.PlayerKnockback));
                }
                n.PreviousPosition = end;
                n.HasDashSegment = 0;
                if (n.Phase == NinjaPhase.Recovering) velocity.ValueRW.Linear.xz = float2.zero;
            }
        }
    }
}
