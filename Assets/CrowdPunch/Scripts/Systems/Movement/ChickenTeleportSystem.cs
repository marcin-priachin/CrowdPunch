using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(ChickenAttackSystem))]
    [UpdateBefore(typeof(ChickenMotionSystem))]
    public partial struct ChickenTeleportSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PlayerSnapshot>()) return;
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            foreach (var (boss, tuning, pose, velocity) in SystemAPI.Query<RefRW<ChickenBoss>, RefRO<ChickenTuning>,
                         RefRW<LocalTransform>, RefRW<PhysicsVelocity>>())
            {
                if (boss.ValueRO.Phase != ChickenPhase.Teleport) continue;
                var position = FarthestPosition(player.Position, pose.ValueRO.Position.y, tuning.ValueRO);
                // Explicit teleport before the physics world is built. Normal movement remains velocity-owned.
                pose.ValueRW.Position = position;
                velocity.ValueRW = default;
                boss.ValueRW.Destination = position;
                boss.ValueRW.PreviousPosition = position; // Never sweep contact across the teleport path.
                boss.ValueRW.Facing = quaternion.LookRotationSafe(math.normalizesafe(
                    new float3(player.Position.x-position.x, 0, player.Position.z-position.z), new float3(0,0,1)), math.up());
                pose.ValueRW.Rotation = boss.ValueRO.Facing;
                boss.ValueRW.Phase = ChickenPhase.Pause;
                boss.ValueRW.Remaining = ChickenAttackSystem.PauseDuration(boss.ValueRO.Stage, tuning.ValueRO);
            }
        }

        internal static float3 FarthestPosition(float3 player, float height, in ChickenTuning tuning)
        {
            // The authored Chicken arenas are open rectangles. Squared distance is maximized at a corner.
            float2 inset = math.max(float2.zero, tuning.ArenaHalfSize-tuning.BodyRadius-.15f);
            float2 best = tuning.ArenaCenter;
            float farthest = -1;
            for (int corner = 0; corner < 4; corner++)
            {
                float2 candidate = tuning.ArenaCenter + inset * new float2((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1);
                float distance = math.distancesq(candidate, player.xz);
                if (distance > farthest) { farthest = distance; best = candidate; }
            }
            return new float3(best.x, height, best.y);
        }
    }
}
