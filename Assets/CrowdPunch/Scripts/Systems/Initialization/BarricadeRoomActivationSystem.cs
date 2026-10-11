using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Initialization
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    [UpdateBefore(typeof(EnemyWaveSpawnSystem))]
    public partial struct BarricadeRoomActivationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PlayerSnapshot>()) return;
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            foreach (var room in SystemAPI.Query<RefRW<BarricadeRoom>>())
            {
                if (room.ValueRO.Activated != 0) continue;
                var previous = room.ValueRO.PreviousGate;
                if (previous != Entity.Null && (!SystemAPI.HasComponent<Barricade>(previous)
                    || SystemAPI.GetComponent<Barricade>(previous).HitsRemaining > 0)) continue;
                if (math.all(player.Position.xz >= room.ValueRO.Minimum) && math.all(player.Position.xz <= room.ValueRO.Maximum))
                    room.ValueRW.Activated = 1;
            }
        }
    }
}
