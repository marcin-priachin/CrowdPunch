using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Applies launched-body collision damage to the hybrid player.</summary>
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Physics.EnemyRecoverySystem))]
    [UpdateBefore(typeof(PlayerContactDamageSystem))]
    public partial class LaunchedEnemyPlayerImpactSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSnapshot>();
            RequireForUpdate<EnemyLaunchSettings>();
        }

        protected override void OnUpdate()
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable || !PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            EnemyLaunchSettings settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();
            float minimumImpulse = math.max(0f, settings.MinimumDamageImpulse);
            float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
            bool hasImpact = false;
            float strongestDamage = 0f;
            float strongestInvincibility = 0f;

            foreach ((RefRO<LocalTransform> transform, RefRO<PhysicsVelocity> velocity,
                         RefRO<PhysicsMass> mass, RefRO<EnemyContactDamageSettings> contactSettings,
                         RefRW<EnemyLaunchState> launchState) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PhysicsMass>,
                             RefRO<EnemyContactDamageSettings>, RefRW<EnemyLaunchState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>())
            {
                if (!CanDamagePlayer(launchState.ValueRO))
                {
                    continue;
                }

                float hitRadius = math.max(0f, player.Radius)
                    + math.max(0f, contactSettings.ValueRO.ContactRadius);
                float3 currentPosition = transform.ValueRO.Position;
                float3 previousPosition = currentPosition - velocity.ValueRO.Linear * deltaTime;
                if (!SegmentIntersectsSphere(previousPosition, currentPosition, player.Position, hitRadius))
                {
                    continue;
                }

                float estimatedImpulse = EstimateImpactImpulse(velocity.ValueRO.Linear, mass.ValueRO.InverseMass);
                if (estimatedImpulse < minimumImpulse)
                {
                    continue;
                }

                float damage = EnemyCollisionDamage.Calculate(
                    launchState.ValueRO.LaunchDamage,
                    estimatedImpulse,
                    settings);
                if (damage <= 0f)
                {
                    continue;
                }

                // Consume this source launch even if another simultaneous impact wins player invulnerability.
                launchState.ValueRW.PlayerImpactLaunchSequence = launchState.ValueRO.LaunchSequence;
                if (!hasImpact || damage > strongestDamage)
                {
                    hasImpact = true;
                    strongestDamage = damage;
                    strongestInvincibility = math.max(
                        0f,
                        contactSettings.ValueRO.PlayerInvincibilitySeconds);
                }
            }

            if (hasImpact)
            {
                bridge.ReceiveEnemyHit(strongestDamage, strongestInvincibility, float3.zero);
            }
        }

        public static float EstimateImpactImpulse(float3 linearVelocity, float inverseMass)
        {
            if (inverseMass <= 0.0001f)
            {
                return 0f;
            }

            return math.length(linearVelocity) / inverseMass;
        }

        public static bool CanDamagePlayer(in EnemyLaunchState launchState)
        {
            return launchState.Phase == EnemyLaunchPhase.Launched
                && launchState.PlayerImpactLaunchSequence != launchState.LaunchSequence;
        }

        public static bool SegmentIntersectsSphere(
            float3 segmentStart,
            float3 segmentEnd,
            float3 center,
            float radius)
        {
            float3 segment = segmentEnd - segmentStart;
            float lengthSq = math.lengthsq(segment);
            float segmentTime = lengthSq <= 0.0001f
                ? 0f
                : math.saturate(math.dot(center - segmentStart, segment) / lengthSq);
            float3 closestPoint = segmentStart + segment * segmentTime;
            float clampedRadius = math.max(0f, radius);
            return math.distancesq(closestPoint, center) <= clampedRadius * clampedRadius;
        }
    }
}
