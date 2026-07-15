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

            SystemAPI.SetComponent(playerStateEntity, new PlayerSnapshot
            {
                Position = bridge.Position,
                Forward = bridge.Forward,
                Radius = bridge.Radius,
                IsAvailable = true
            });

            if (bridge.HasPendingPunch)
            {
                SystemAPI.SetComponent(playerStateEntity, new PunchRequest
                {
                    Origin = bridge.PunchOrigin,
                    Direction = bridge.PunchDirection,
                    Radius = bridge.PunchRadius,
                    Range = bridge.PunchRange,
                    Strength = bridge.PunchStrength,
                    Sequence = bridge.PunchSequence
                });
                SystemAPI.SetComponentEnabled<PunchRequest>(playerStateEntity, true);
                bridge.ClearPunch();
            }
            else
            {
                SystemAPI.SetComponentEnabled<PunchRequest>(playerStateEntity, false);
            }
        }
    }
}
