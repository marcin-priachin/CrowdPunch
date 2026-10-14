using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem)), UpdateAfter(typeof(EliteCrowdSupportSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct LinkedPairPositioningSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<LinkedPair>(); state.RequireForUpdate<PlayerSnapshot>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            var em = state.EntityManager;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            float now = (float)SystemAPI.Time.ElapsedTime;
            foreach (var (pairRef, tuning) in SystemAPI.Query<RefRO<LinkedPair>, RefRO<LinkedPairTuning>>()) {
                var pair = pairRef.ValueRO; var t = tuning.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(em, pair.A, out _, out _) || !em.IsEnabled(pair.A) || !em.IsEnabled(pair.B) || pair.Phase != EnemyLaunchPhase.Active) continue;
                float3 a = em.GetComponentData<LocalTransform>(pair.A).Position, b = em.GetComponentData<LocalTransform>(pair.B).Position;
                float2 axis = math.normalizesafe(a.xz - b.xz, new float2(1, 0));
                // Stable opposite sides with a slowly sweeping offset cross the player's current position.
                float2 lateral = new float2(-axis.y, axis.x);
                float2 sweep = lateral * math.sin(now * t.SweepSpeed + pair.A.Index) * 1.8f;
                bool blocked = LinkedPairGeometry.Blocked(world, enemies, a, b);
                float spacing = math.min(t.StraddleDistance, t.MaximumLength * .45f);
                Write(pair.A, a, player.Position.xz + axis * spacing + sweep);
                Write(pair.B, b, blocked ? a.xz + axis * spacing : player.Position.xz - axis * spacing + sweep);

                void Write(Entity endpoint, float3 position, float2 goal) {
                    var nav = em.GetComponentData<NavigationIntent>(endpoint);
                    float3 destination = new float3(goal.x, position.y, goal.y);
                    em.SetComponentData(endpoint, NavigationIntent.Travel(destination, t.MoveSpeed, .15f, nav.Separation * .4f));
                    em.SetComponentData(endpoint, new DesiredMovement { Direction = math.normalizesafe(destination - position + nav.Separation * .4f), Speed = t.MoveSpeed });
                }
            }
        }
    }
}
