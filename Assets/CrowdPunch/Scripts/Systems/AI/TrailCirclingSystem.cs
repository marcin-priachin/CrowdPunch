using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateAfter(typeof(EliteCrowdSupportSystem))]
    [UpdateBefore(typeof(WizardHazardAvoidanceSystem))]
    public partial struct TrailCirclingSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            foreach (var (settings, emitter, launch, pose, movement, navigation, entity) in
                SystemAPI.Query<RefRO<TrailSettings>, RefRW<TrailEmitter>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                    RefRW<DesiredMovement>, RefRW<NavigationIntent>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable)
                { movement.ValueRW = default; navigation.ValueRW = default; continue; }
                // An elite's selected projectile remains anchored under the existing crowd-support rule.
                if (SystemAPI.HasComponent<ElitePunchReservation>(entity) &&
                    SystemAPI.GetComponent<ElitePunchReservation>(entity).Owner != Entity.Null) continue;
                var s = settings.ValueRO;
                ref var e = ref emitter.ValueRW;
                if (e.OrbitSign == 0) e.OrbitSign = 1;
                e.ReverseRemaining -= SystemAPI.Time.DeltaTime;
                if (e.ReverseRemaining <= 0)
                { e.OrbitSign = -e.OrbitSign; e.ReverseRemaining += math.max(.01f, s.ReversalInterval); }
                float3 radial = pose.ValueRO.Position - player.Position; radial.y = 0;
                float distance = math.length(radial);
                radial = math.normalizesafe(radial, new float3(1, 0, 0));
                float3 tangent = new float3(-radial.z, 0, radial.x) * e.OrbitSign;
                float error = distance - math.max(0, s.CirclingDistance);
                float3 direction = math.normalizesafe(tangent - radial * math.clamp(error, -2, 2));
                float speed = error > 1 ? s.ApproachSpeed : error < -1 ? s.RetreatSpeed : s.OrbitSpeed;
                float3 separation = navigation.ValueRO.Separation;
                movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(direction + separation), Speed = math.max(0, speed) };
                navigation.ValueRW = NavigationIntent.Travel(pose.ValueRO.Position + direction * 3, math.max(0, speed), .1f, separation);
            }
        }
    }
}
