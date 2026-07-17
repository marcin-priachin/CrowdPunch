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
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new MatchState
            {
                IsRunning = authoring.StartRunning,
                ElapsedSeconds = 0f
            });
            AddComponent<PlayerSnapshot>(entity);
            AddComponent<PlayerHealthSnapshot>(entity);
            AddComponent<PunchRequest>(entity);
            SetComponentEnabled<PunchRequest>(entity, false);
        }
    }
}
