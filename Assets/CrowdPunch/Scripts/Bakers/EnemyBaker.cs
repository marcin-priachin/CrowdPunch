using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    /// <summary>
    /// Converts enemy authoring data into the components required by enemy systems.
    /// </summary>
    public sealed class EnemyBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<Enemy>(entity);
            AddComponent(entity, new EnemyMovementSettings
            {
                MoveSpeed = authoring.MoveSpeed,
                WanderSpeed = authoring.WanderSpeed,
                ChargeDistance = authoring.ChargeDistance,
                ChargeSpeedMultiplier = authoring.ChargeSpeedMultiplier,
                Acceleration = authoring.Acceleration,
                BrakingAcceleration = authoring.BrakingAcceleration,
                TurnSpeed = authoring.TurnSpeed,
                StoppingDistance = authoring.StoppingDistance
            });
            AddComponent<DesiredMovement>(entity);
            AddComponent<WanderDestination>(entity);
            AddComponent(entity, new Health
            {
                Current = authoring.MaxHealth,
                Max = authoring.MaxHealth
            });
            AddComponent(entity, new HealthBar
            {
                Normalized = 1f
            });
            AddComponent(entity, new EnemyContactDamageSettings
            {
                DamagePercent = authoring.ContactDamagePercent,
                PushStrength = authoring.ContactPushStrength,
                PlayerInvincibilitySeconds = authoring.ContactInvincibilitySeconds,
                ContactRadius = authoring.ContactRadius
            });
            AddComponent<DamageRequest>(entity);
            AddComponent<DeathRequest>(entity);
            AddComponent<ExternalImpulse>(entity);
            AddComponent<KnockbackRecovery>(entity);
            AddComponent<RespawnRequest>(entity);

            SetComponentEnabled<DamageRequest>(entity, false);
            SetComponentEnabled<DeathRequest>(entity, false);
            SetComponentEnabled<ExternalImpulse>(entity, false);
            SetComponentEnabled<KnockbackRecovery>(entity, false);
            SetComponentEnabled<RespawnRequest>(entity, false);
        }
    }
}
