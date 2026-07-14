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
                TurnSpeed = authoring.TurnSpeed,
                StoppingDistance = authoring.StoppingDistance
            });
            AddComponent<DesiredMovement>(entity);
            AddComponent<ExternalImpulse>(entity);
            AddComponent<KnockbackRecovery>(entity);
            AddComponent<RespawnRequest>(entity);

            SetComponentEnabled<ExternalImpulse>(entity, false);
            SetComponentEnabled<KnockbackRecovery>(entity, false);
            SetComponentEnabled<RespawnRequest>(entity, false);
        }
    }
}
