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
            AddComponent(entity, new KnockbackResponse { Tier = KnockbackResponseTier.Normal });
            AddComponent<EnemyMovementSettings>(entity);
            AddComponent<EnemySeparationDistance>(entity);
            AddComponent<DesiredMovement>(entity);
            AddComponent(entity, new EnemyLaunchState
            {
                Phase = EnemyLaunchPhase.Active
            });
            AddComponent<WanderDestination>(entity);
            AddComponent<Health>(entity);
            AddComponent(entity, new HealthBar
            {
                Normalized = 1f
            });
            AddComponent<EnemyHealthBarVisibility>(entity);
            AddComponent<EnemyContactDamageSettings>(entity);
            AddComponent<EnemyContactAttemptState>(entity);
            AddComponent<DamageRequest>(entity);
            AddBuffer<CollisionDamageHistory>(entity);
            AddComponent<EnemyDamageState>(entity);
            AddComponent<DeathRequest>(entity);
            AddComponent<ExternalImpulse>(entity);
            AddComponent<KnockbackRecovery>(entity);
            AddComponent<RespawnRequest>(entity);

            SetComponentEnabled<DamageRequest>(entity, false);
            SetComponentEnabled<EnemyHealthBarVisibility>(entity, false);
            SetComponentEnabled<DeathRequest>(entity, false);
            SetComponentEnabled<ExternalImpulse>(entity, false);
            SetComponentEnabled<KnockbackRecovery>(entity, false);
            SetComponentEnabled<RespawnRequest>(entity, false);
        }
    }
}
