using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    /// <summary>
    /// Copies MonoBehaviour player bridge data into ECS singleton components.
    /// </summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderFirst = true)]
    public partial struct PlayerBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            Entity playerStateEntity = SystemAPI.GetSingletonEntity<PlayerSnapshot>();
            bool canAct = !bridge.PlayerDefeated && !FeedbackTimeController.IsLevelClear;

            SystemAPI.SetComponent(playerStateEntity, new PlayerSnapshot
            {
                Position = bridge.Position,
                Velocity = bridge.Velocity,
                Forward = bridge.Forward,
                Radius = bridge.Radius,
                CollisionLayer = bridge.CollisionLayer,
                IsAvailable = canAct
            });
            SystemAPI.SetComponent(playerStateEntity, new PlayerHealthSnapshot
            {
                Current = bridge.CurrentHealth,
                Max = bridge.MaxHealth,
                IsAvailable = bridge.MaxHealth > 0f
            });

            if (bridge.HasPendingPunch && canAct)
            {
                if (SystemAPI.TryGetSingletonRW<LevelClearScore>(out var score))
                    score.ValueRW.Punches++;
                SystemAPI.SetComponent(playerStateEntity, new PunchRequest
                {
                    Origin = bridge.PunchOrigin,
                    Direction = bridge.PunchDirection,
                    Radius = bridge.PunchRadius,
                    Range = bridge.PunchRange,
                    Strength = bridge.PunchStrength,
                    EliteKnockbackMultiplier = bridge.PunchEliteKnockbackMultiplier,
                    Damage = bridge.PunchDamage,
                    PushDirectionPositionWeight = bridge.PunchPushDirectionPositionWeight,
                    Sequence = bridge.PunchSequence,
                    AimRevision = bridge.PunchAimRevision
                });
                SystemAPI.SetComponentEnabled<PunchRequest>(playerStateEntity, true);
                bridge.ClearPunch();
            }
            else
            {
                if (bridge.HasPendingPunch) bridge.ClearPunch();
                SystemAPI.SetComponentEnabled<PunchRequest>(playerStateEntity, false);
            }
        }
    }
}
