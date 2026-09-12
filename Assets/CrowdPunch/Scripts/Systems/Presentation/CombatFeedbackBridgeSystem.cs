using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(PresentationBridgeSystem))]
    [UpdateBefore(typeof(EnemyImpactVisualSystem))]
    public partial struct CombatFeedbackBridgeSystem : ISystem
    {
        private uint restart;
        public void OnUpdate(ref SystemState state)
        {
            bool available = PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)
                && bridge.FeedbackSettings != null;
            var settings = available ? bridge.FeedbackSettings : null;
            bool reset = restart != GameRestartRegistry.Sequence || !available || !bridge.gameObject.activeInHierarchy
                || FeedbackTimeController.IsSuspended;
            restart = GameRestartRegistry.Sequence;
            float dt = UnityEngine.Time.unscaledDeltaTime;
            int emitted = 0;
            int trails = 0;
            bridge?.BeginTrails();
            foreach (var (feedback, launch, transform, velocity, entity) in
                     SystemAPI.Query<RefRW<EnemyImpactFeedback>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>()
                         .WithEntityAccess())
            {
                bool pooled = SystemAPI.HasComponent<RespawnRequest>(entity)
                    && SystemAPI.IsComponentEnabled<RespawnRequest>(entity);
                if (reset || pooled)
                {
                    feedback.ValueRW = default;
                    continue;
                }
                ref var f = ref feedback.ValueRW;
                f.VisualRemaining = math.max(0f, f.VisualRemaining - dt);
                f.FlashRemaining = math.max(0f, f.FlashRemaining - dt);
                if (f.Pending != 0)
                {
                    f.Pending = 0;
                    bool punch = f.Kind == CombatImpactKind.Punch;
                    bool environment = f.Kind == CombatImpactKind.Environment;
                    float minimum = environment ? settings.EnvironmentMinimumSpeed : settings.MinimumCollisionSpeed;
                    if (punch || f.Speed >= minimum && f.Impulse >= settings.MinimumCollisionImpulse)
                    {
                        f.NextContactTime = SystemAPI.Time.ElapsedTime + settings.ContactFeedbackInterval;
                        float intensity = math.saturate(settings.SingleImpactIntensity * (punch ? math.max(.4f, settings.Intensity(f.Speed)) : settings.Intensity(f.Speed))
                            * settings.ChainMultiplier(f.PlayerOwned != 0 ? f.ChainDepth : 0));
                        f.Intensity = intensity;
                        f.VisualDirection = f.Direction;
                        f.Squash = settings.SquashAmount;
                        f.Flash = settings.EnemyFlashIntensity;
                        f.VisualDuration = settings.SquashDuration;
                        f.FlashDuration = settings.FlashDuration;
                        f.VisualRemaining = intensity >= settings.DeformationThreshold ? f.VisualDuration : 0f;
                        f.FlashRemaining = intensity >= settings.DeformationThreshold ? f.FlashDuration : 0f;
                        if (emitted++ < settings.MaximumImpactsPerFrame)
                            bridge.ReceiveImpact(new CombatFeedbackMessage { Kind = f.Kind, Position = f.Position,
                                Direction = f.Direction, Intensity = intensity, ChainDepth = f.ChainDepth, PlayerOwned = f.PlayerOwned != 0 });
                    }
                }
                float speed = math.length(velocity.ValueRO.Linear);
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched && speed >= settings.TrailMinimumSpeed
                    && trails++ < settings.MaximumTrails)
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    bridge.ReceiveTrail(id, launch.ValueRO.LaunchSequence, transform.ValueRO.Position, speed,
                        launch.ValueRO.Owner == EnemyLaunchOwner.Player ? launch.ValueRO.FeedbackChainDepth : 0);
                }
            }
            bridge?.EndTrails();
        }
    }
}
