using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    /// <summary>
    /// Converts scene-level game settings into ECS singleton data.
    /// </summary>
    public sealed class GameSettingsBaker : Baker<GameSettingsAuthoring>
    {
        public override void Bake(GameSettingsAuthoring authoring)
        {
            if (authoring.Settings == null)
            {
                return;
            }

            DependsOn(authoring.Settings);
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new MatchState
            {
                IsRunning = authoring.Settings.StartRunning,
                ElapsedSeconds = 0f
            });
            AddComponent<PlayerSnapshot>(entity);
            AddComponent<PlayerHealthSnapshot>(entity);
            AddComponent(entity, new EnemyLaunchSettings
            {
                MinimumPropagationImpulse = authoring.Settings.MinimumPropagationImpulse,
                MinimumDamageImpulse = authoring.Settings.MinimumDamageImpulse,
                BaseCollisionDamageMultiplier = authoring.Settings.BaseCollisionDamageMultiplier,
                DamageMultiplierPerExcessImpulse = authoring.Settings.DamageMultiplierPerExcessImpulse,
                MaximumCollisionDamageMultiplier = authoring.Settings.MaximumCollisionDamageMultiplier,
                UsefulMomentumSpeed = authoring.Settings.UsefulMomentumSpeed,
                LowMomentumPeriod = authoring.Settings.LowMomentumPeriod,
                RecoveryDuration = authoring.Settings.RecoveryDuration
            });
            AddComponent<PunchRequest>(entity);
            SetComponentEnabled<PunchRequest>(entity, false);
        }
    }
}
