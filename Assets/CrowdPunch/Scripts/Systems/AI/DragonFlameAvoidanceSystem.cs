using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(DragonPositioningSystem)), UpdateAfter(typeof(TrailAvoidanceSystem)), UpdateAfter(typeof(NinjaDecisionSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct DragonFlameAvoidanceSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<DragonTuning>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var cones = new NativeList<DragonCone>(Allocator.TempJob);
            foreach (var (tuning, flame, launch, pose, life, source) in
                SystemAPI.Query<RefRO<DragonTuning>, RefRO<DragonFlameState>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<EnemyLifetime>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
                if (tuning.ValueRO.Avoidance == DragonAvoidanceMode.DamagingFlame &&
                    DragonFlameGeometry.TryCone(source, pose.ValueRO, tuning.ValueRO, flame.ValueRO, launch.ValueRO, life.ValueRO.Generation, out var cone) && cone.Active != 0 && cone.Profile.EnemyDamage > 0)
                    cones.Add(cone);
            if (cones.Length == 0) return;
            var job = new AvoidJob { Cones = cones.AsArray(), World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true), Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Dragons = SystemAPI.GetComponentLookup<DragonTuning>(true), Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                Ninjas = SystemAPI.GetComponentLookup<NinjaState>(true), Wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                WizardTuning = SystemAPI.GetComponentLookup<WizardSettings>(true), Reservations = SystemAPI.GetComponentLookup<ElitePunchReservation>(true),
                Now = SystemAPI.Time.ElapsedTime };
            // Complete before disposing shared cones; no per-enemy managed allocations or structural changes.
            job.ScheduleParallel(state.Dependency).Complete(); state.Dependency = default;
        }

        [BurstCompile, WithAll(typeof(Enemy)), WithNone(typeof(RespawnRequest), typeof(BossPart), typeof(SnakeSegment))]
        private partial struct AvoidJob : IJobEntity
        {
            [ReadOnly] public NativeArray<DragonCone> Cones;
            [ReadOnly] public CollisionWorld World;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<DragonTuning> Dragons;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<WizardCastState> Wizards;
            [ReadOnly] public ComponentLookup<WizardSettings> WizardTuning;
            [ReadOnly] public ComponentLookup<ElitePunchReservation> Reservations;
            public double Now;
            private void Execute(Entity entity, in LocalTransform pose, in EnemyLaunchState launch,
                in NavigationAgent agent, ref NavigationIntent navigation, ref DesiredMovement movement)
            {
                if (launch.Phase != EnemyLaunchPhase.Active || Dashers.HasComponent(entity) && Dashers[entity].Phase != DasherPhase.Positioning ||
                    Ninjas.HasComponent(entity) && Ninjas[entity].Phase != NinjaPhase.Positioning ||
                    Wizards.HasComponent(entity) && Wizards[entity].StopsMovement(WizardTuning[entity]) ||
                    Reservations.HasComponent(entity) && Reservations[entity].Owner != Entity.Null) return;
                bool armor = Armors.HasComponent(entity) && (Armors[entity].Stages > 0 || Now < Armors[entity].ProtectedUntil);
                float3 away = 0;
                foreach (var cone in Cones)
                {
                    if (!DragonFlameGeometry.EnemyEligible(cone, entity, Dragons.HasComponent(entity), armor) ||
                        !DragonFlameGeometry.Exposed(cone, pose.Position, agent.Radius + .6f, World, Enemies, cone.Tuning.GroundY + .25f)) continue;
                    float2 delta = pose.Position.xz - cone.Mouth.xz;
                    float2 right = new float2(cone.Forward.y, -cone.Forward.x);
                    // Exit toward the nearest side, rather than walking forward along an advancing cone.
                    float2 lateral = right * (math.dot(delta, right) >= 0 ? 1 : -1);
                    away += new float3(lateral.x, 0, lateral.y) * 3;
                }
                if (math.lengthsq(away) <= .0001f) return;
                navigation.Separation += away; navigation.Speed = math.max(navigation.Speed, 1.5f);
                movement.Direction = math.normalizesafe(movement.Direction + away); movement.Speed = math.max(movement.Speed, 1.5f);
            }
        }
    }
}
