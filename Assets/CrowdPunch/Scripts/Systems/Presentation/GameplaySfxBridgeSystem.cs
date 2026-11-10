using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class GameplaySfxBridgeSystem : SystemBase
    {
        private Dictionary<(Entity, GameplaySfxCue), uint> previous = new();
        private Dictionary<(Entity, GameplaySfxCue), uint> current = new();
        private PlayerEcsBridge bridge;
        private uint restart;

        protected override void OnUpdate()
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out var activeBridge) || activeBridge.GameplayAudio == null)
            {
                previous.Clear();
                bridge = null;
                return;
            }
            if (bridge != activeBridge || restart != GameRestartRegistry.Sequence)
            {
                bridge = activeBridge;
                restart = GameRestartRegistry.Sequence;
                previous.Clear();
            }

            current.Clear();
            ObserveEnemies();
            ObserveBosses();
            (previous, current) = (current, previous);
        }

        private void ObserveEnemies()
        {
            foreach (var (dash, launch, pose, entity) in SystemAPI.Query<RefRO<DasherState>, RefRO<EnemyLaunchState>,
                         RefRO<LocalTransform>>().WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.DasherRoll, dash.ValueRO.Phase == DasherPhase.Dashing
                    && launch.ValueRO.Phase == EnemyLaunchPhase.Active ? 1u : 0u, pose.ValueRO.Position);

            foreach (var (armor, pose, entity) in SystemAPI.Query<RefRO<EnemyArmor>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.ArmorBreak, armor.ValueRO.HitSequence, pose.ValueRO.Position);

            foreach (var (shot, entity) in SystemAPI.Query<RefRO<RangedProjectile>>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.RangerCast, 1u, shot.ValueRO.Start, playFirst: true);

            foreach (var (cast, pose, entity) in SystemAPI.Query<RefRO<WizardCastState>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.FlowerWindup, cast.ValueRO.Phase == WizardCastPhase.Telegraph
                    ? 1u : 0u, pose.ValueRO.Position);

            foreach (var (zone, entity) in SystemAPI.Query<RefRO<WizardZone>>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.FlowerSporeBurst, zone.ValueRO.Active != 0
                    ? 1u : 0u, zone.ValueRO.Position, playFirst: true);

            foreach (var (emitter, pose, entity) in SystemAPI.Query<RefRO<TrailEmitter>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.SquidInk, emitter.ValueRO.Emitting == 0
                    ? 0u : 1u + emitter.ValueRO.Launched, pose.ValueRO.Position, playFirst: true);

            foreach (var (segment, pose, entity) in SystemAPI.Query<RefRO<SnakeSegment>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.BlobDetach, segment.ValueRO.Detached != 0
                    ? 1u : 0u, pose.ValueRO.Position);

            if (!SystemAPI.TryGetSingleton<PlayerSnapshot>(out var player)) return;
            foreach (var (exploder, contact, launch, pose, entity) in SystemAPI.Query<RefRO<ExplosiveEnemyState>,
                         RefRO<EnemyContactDamageSettings>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
            {
                float range = contact.ValueRO.ContactRadius + player.Radius + 1.25f;
                bool close = player.IsAvailable && exploder.ValueRO.HasExploded == 0
                    && launch.ValueRO.Phase == EnemyLaunchPhase.Active
                    && math.distancesq(player.Position.xz, pose.ValueRO.Position.xz) <= range * range;
                Observe(entity, GameplaySfxCue.ExploderWarning, close ? 1u : 0u, pose.ValueRO.Position);
            }
        }

        private void ObserveBosses()
        {
            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<ChickenBoss>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var state = boss.ValueRO;
                bool charging = state.Phase == ChickenPhase.WindUp || state.Phase == ChickenPhase.Cast;
                Observe(entity, GameplaySfxCue.GoblinWizardCharge, charging ? (uint)state.Phase : 0u, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GoblinWizardCast, state.ShotSequence, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GoblinWizardTeleport, state.TeleportSequence, state.TeleportDeparture);
            }
            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<DinoBoss>, RefRO<LocalTransform>>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.ArmoredBossMovement, boss.ValueRO.Phase == DinoPhase.Burst
                    ? 1u : 0u, pose.ValueRO.Position);

            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<RollingBoss>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Observe(entity, GameplaySfxCue.SpikedTurtleRoll, boss.ValueRO.RollSequence, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.SpikedTurtleWallHit, (uint)boss.ValueRO.Bounces, pose.ValueRO.Position);
            }
        }

        private void Observe(Entity entity, GameplaySfxCue cue, uint value, float3 position, bool playFirst = false)
        {
            var key = (entity, cue);
            current[key] = value;
            bool changed = previous.TryGetValue(key, out uint last) ? value != last : playFirst;
            if (value != 0 && changed) bridge.ReceiveGameplaySfx(cue, position);
        }
    }
}
