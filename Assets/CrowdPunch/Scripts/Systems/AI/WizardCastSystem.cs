using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(CrowdPunch.Systems.InputBridge.PlayerBridgeSystem))]
    [UpdateAfter(typeof(CrowdPunch.Systems.Initialization.EnemyWaveSpawnSystem))]
    [UpdateBefore(typeof(WizardPositioningSystem))]
    public partial struct WizardCastSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();

        public static float Chance(WizardSettings s, float distance, float approachSpeed) => math.saturate(
            s.BaseChance + s.ProximityBonus * math.saturate((s.EngagementRange - distance) /
            math.max(.001f, s.EngagementRange - s.Radius)) + s.ApproachBonus *
            math.saturate(approachSpeed / math.max(.001f, s.FullApproachSpeed)));

        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (settings, castRef, launch, pose, entity) in
                SystemAPI.Query<RefRO<WizardSettings>, RefRW<WizardCastState>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                var s = settings.ValueRO;
                var cast = castRef.ValueRO;
                bool unavailable = launch.ValueRO.Phase != EnemyLaunchPhase.Active ||
                    SystemAPI.IsComponentEnabled<RespawnRequest>(entity);
                if (unavailable)
                {
                    if (state.EntityManager.Exists(cast.MovingZone)) commands.DestroyEntity(cast.MovingZone);
                    cast.MovingZone = Entity.Null; cast.Phase = WizardCastPhase.Cooldown;
                    cast.Remaining = math.max(0, s.Cooldown);
                    castRef.ValueRW = cast; continue;
                }
                cast.Remaining -= SystemAPI.Time.DeltaTime;
                if (cast.Remaining <= 0)
                {
                    switch (cast.Phase)
                    {
                        case WizardCastPhase.Cooldown:
                            cast.Phase = WizardCastPhase.Checking; cast.Remaining = 0; break;
                        case WizardCastPhase.Telegraph:
                            cast.Phase = WizardCastPhase.Active; cast.Remaining = math.max(0, s.ActiveDuration);
                            if (state.EntityManager.Exists(cast.MovingZone))
                            {
                                var zone = SystemAPI.GetComponent<WizardZone>(cast.MovingZone);
                                zone.Active = 1; zone.ExpiresAt = now + cast.Remaining;
                                SystemAPI.SetComponent(cast.MovingZone, zone);
                            }
                            break;
                        case WizardCastPhase.Active:
                            if (state.EntityManager.Exists(cast.MovingZone)) commands.DestroyEntity(cast.MovingZone);
                            cast.MovingZone = Entity.Null; cast.Phase = WizardCastPhase.Cooldown;
                            cast.Remaining = math.max(0, s.Cooldown); break;
                    }
                }
                if (cast.Phase == WizardCastPhase.Checking && cast.Remaining <= 0 && player.IsAvailable)
                {
                    float3 towardWizard = pose.ValueRO.Position - player.Position; towardWizard.y = 0;
                    float distance = math.length(towardWizard);
                    if (distance <= s.EngagementRange)
                    {
                        var random = new Random(math.max(1u, cast.RandomState));
                        float approach = math.dot(player.Velocity, math.normalizesafe(towardWizard));
                        bool start = random.NextFloat() < Chance(s, distance, approach);
                        cast.RandomState = random.state; cast.Remaining = math.max(.001f, s.CheckInterval);
                        if (start)
                        {
                            cast.Phase = WizardCastPhase.Telegraph; cast.Remaining = math.max(0, s.TelegraphDuration);
                            cast.MovingZone = WizardZoneCreation.Create(commands, state.EntityManager, entity,
                                pose.ValueRO.Position, s, now, true, false);
                        }
                    }
                }
                // The deferred entity reference is remapped by ECB playback.
                commands.SetComponent(entity, cast);
            }
            commands.Playback(state.EntityManager);
        }
    }
}
