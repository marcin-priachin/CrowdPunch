using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Lifetime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRespawnSystem))]
    [UpdateAfter(typeof(DasherRotationLockSystem))]
    public partial struct EnemyFacingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            state.Dependency = new FacePlayerJob
            {
                Wizards = SystemAPI.GetComponentLookup<WizardSettings>(true),
                Dragons = SystemAPI.GetComponentLookup<DragonTuning>(true),
                Defending = SystemAPI.HasSingleton<ProtectedPoint>(),
                Movements = SystemAPI.GetComponentLookup<DesiredMovement>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                Elites = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Archetypes = SystemAPI.GetComponentLookup<EnemyArchetype>(true),
                DeltaTime = SystemAPI.Time.DeltaTime,
                PlayerPosition = player.Position,
                PlayerAvailable = player.IsAvailable,
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct FacePlayerJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<WizardSettings> Wizards;
            [ReadOnly] public ComponentLookup<DragonTuning> Dragons;
            public bool Defending;
            [ReadOnly] public ComponentLookup<DesiredMovement> Movements;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<ElitePunchState> Elites;
            [ReadOnly] public ComponentLookup<EnemyArchetype> Archetypes;
            public float DeltaTime;
            public float3 PlayerPosition;
            public bool PlayerAvailable;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;

            private void Execute(Entity entity, ref LocalTransform transform, ref PhysicsVelocity velocity,
                in EnemyLaunchState launch)
            {
                bool launched = launch.Phase == EnemyLaunchPhase.Launched;
                bool charging = launch.Phase == EnemyLaunchPhase.Active
                    && Archetypes.HasComponent(entity) && Archetypes[entity].Value == EnemyArchetypeKind.Baseline
                    && Contacts.HasComponent(entity) && Contacts[entity].IsAttempting != 0;
                if (!launched && ((!PlayerAvailable && !charging) ||
                    (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)))
                    return;
                bool committedDash = Dashers.HasComponent(entity)
                    && Dashers[entity].Phase == DasherPhase.Dashing;
                bool committedSweep = Elites.HasComponent(entity) && Elites[entity].Phase == ElitePunchPhase.CommittedSweep;
                float3 toward = launched
                    ? velocity.Linear
                    : committedDash
                        ? Dashers[entity].LockedDirection
                        : PlayerPosition - transform.Position;
                toward.y = 0f;
                if (!launched && committedSweep) toward = Elites[entity].CommittedDirection;
                if (charging)
                {
                    // Match resolved motion after collisions; never steer the committed charge toward the player.
                    toward = new float3(velocity.Linear.x, 0f, velocity.Linear.z);
                    if (math.lengthsq(toward) <= .0001f && Movements.HasComponent(entity) && Movements[entity].Speed > 0)
                        toward = new float3(Movements[entity].Direction.x, 0f, Movements[entity].Direction.z);
                    if (math.lengthsq(toward) <= .0001f) toward = Contacts[entity].CommittedDirection;
                    toward.y = 0f;
                }
                if (Defending && !launched && !committedDash && !charging && Movements.HasComponent(entity) && Movements[entity].Speed > 0)
                    toward = Movements[entity].Direction;
                if (math.lengthsq(toward) <= 0.0001f) return;
                // Keep the physics capsule upright; animation supplies launched visual pitch.
                var originalRotation = transform.Rotation;
                var facing = quaternion.LookRotationSafe(toward, math.up());
                transform.Rotation = !launched && Wizards.HasComponent(entity)
                    ? math.slerp(transform.Rotation, facing, 1 - math.exp(-math.max(0, Wizards[entity].TurnSpeed) * DeltaTime)) : facing;
                if (!launched && Dragons.HasComponent(entity))
                {
                    float current = math.atan2(math.forward(originalRotation).x, math.forward(originalRotation).z);
                    float target = math.atan2(toward.x, toward.z);
                    float delta = math.atan2(math.sin(target - current), math.cos(target - current));
                    float step = math.radians(Dragons[entity].TurnDegreesPerSecond) * DeltaTime;
                    // Use the original yaw: ordinary facing above is instantaneous for other archetypes.
                    transform.Rotation = quaternion.RotateY(current + math.clamp(delta, -step, step));
                }
                velocity.Angular.y = 0f;
            }
        }
    }
}
