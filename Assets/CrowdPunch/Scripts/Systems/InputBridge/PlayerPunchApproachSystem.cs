using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PunchDetectionSystem))]
    public partial struct PlayerPunchApproachSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity entity = SystemAPI.GetSingletonEntity<PlayerSnapshot>();
            if (!SystemAPI.HasComponent<PunchRequest>(entity)) return;

            PunchRequest request = SystemAPI.GetComponent<PunchRequest>(entity);
            if (!request.IsResolved || math.lengthsq(request.ApproachDisplacement) <= 1e-10f) return;

            float3 displacement = request.ApproachDisplacement;
            request.ApproachDisplacement = float3.zero;
            SystemAPI.SetComponent(entity, request);

            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)
                || bridge.PlayerDefeated || !bridge.CanApplyPunchApproach)
                return;

            PlayerSnapshot player = SystemAPI.GetComponent<PlayerSnapshot>(entity);
            float groundHeight = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground)
                ? ground.SurfaceHeight : player.Position.y;
            displacement = PlayerObstacleCollisionSystem.LimitPunchApproach(
                SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld, player, groundHeight, displacement);
            if (math.lengthsq(displacement) <= 1e-10f) return;

            bridge.ApplyPunchApproach(displacement);
            player.Position = bridge.Position;
            SystemAPI.SetComponent(entity, player);
        }
    }
}
