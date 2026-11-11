using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateBefore(typeof(GauntletCompletionSystem))]
    public partial class GameplaySfxBridgeSystem : SystemBase
    {
        private const float FinaleFlightFadeSeconds = .7f;
        private Dictionary<(Entity, GameplaySfxCue), uint> previous = new();
        private Dictionary<(Entity, GameplaySfxCue), uint> current = new();
        private PlayerEcsBridge bridge;
        private uint restart;

        protected override void OnUpdate()
        {
            bool hasAudio = PlayerBridgeRegistry.TryGetBridge(out var activeBridge) && activeBridge.GameplayAudio != null;
            if (SystemAPI.TryGetSingletonRW<LinkedPairEnemyShock>(out var shock) && shock.ValueRO.Pending != 0)
            {
                if (hasAudio) activeBridge.ReceiveGameplaySfx(GameplaySfxCue.LinkedPairEnemyShock, shock.ValueRO.Position);
                shock.ValueRW = default;
            }
            if (!hasAudio)
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
            foreach (var (health, pose, respawn, entity) in SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>, RefRO<RespawnRequest>>()
                         .WithAll<ElitePunchState>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (respawn.ValueRO.IsPooled != 0) continue;
                ObserveHealth(entity, GameplaySfxCue.GiantGoblinHit, health.ValueRO.Current, pose.ValueRO.Position);
            }

            foreach (var (armor, pose, entity) in SystemAPI.Query<RefRO<EnemyArmor>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.ArmorBreak, armor.ValueRO.HitSequence, pose.ValueRO.Position);

            foreach (var (shot, entity) in SystemAPI.Query<RefRO<RangedProjectile>>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.RangerCast, 1u, shot.ValueRO.Start, playFirst: true);

            foreach (var (cast, pose, entity) in SystemAPI.Query<RefRO<WizardCastState>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.FlowerWindup, cast.ValueRO.Phase == WizardCastPhase.Telegraph
                    ? 1u : 0u, pose.ValueRO.Position);

            bridge.BeginFlowerSporeEmissions();
            foreach (var (zone, entity) in SystemAPI.Query<RefRO<WizardZone>>().WithEntityAccess())
            {
                var hazard = zone.ValueRO;
                if (hazard.Follow != 0)
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    bridge.ReceiveFlowerSporeEmission(id, hazard.Position);
                }
                Observe(entity, GameplaySfxCue.FlowerSporeBurst, hazard.Active != 0
                    ? 1u : 0u, hazard.Position, playFirst: true);
            }
            bridge.EndFlowerSporeEmissions();

            bridge.BeginDragonFlames();
            foreach (var (tuning, flame, launch, pose, life, health, respawn, entity) in
                SystemAPI.Query<RefRO<DragonTuning>, RefRO<DragonFlameState>, RefRO<EnemyLaunchState>,
                    RefRO<LocalTransform>, RefRO<EnemyLifetime>, RefRO<Health>, RefRO<RespawnRequest>>()
                    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (respawn.ValueRO.IsPooled != 0) continue;
                ObserveHealth(entity, GameplaySfxCue.DragonHit, health.ValueRO.Current, pose.ValueRO.Position);
                if (SystemAPI.IsComponentEnabled<RespawnRequest>(entity)) continue;

                bool emitting = DragonFlameGeometry.TryCone(entity, pose.ValueRO, tuning.ValueRO, flame.ValueRO,
                    launch.ValueRO, life.ValueRO.Generation, out var cone) && cone.Active != 0;
                Observe(entity, GameplaySfxCue.DragonFlameBurst, emitting ? life.ValueRO.Generation + 1u : 0u,
                    cone.Mouth, playFirst: true);
                if (emitting)
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    bridge.ReceiveDragonFlameAudio(id, cone.Mouth);
                }
            }
            bridge.EndDragonFlames();

            foreach (var (ninja, pose, entity) in SystemAPI.Query<RefRO<NinjaState>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
            {
                // Distance is measured after physics and retained until the next successful teleport.
                var state = ninja.ValueRO;
                Observe(entity, GameplaySfxCue.NinjaDash, state.DistanceTravelled > .001f ? state.TeleportSequence : 0u,
                    pose.ValueRO.Position, playFirst: true);
            }

            foreach (var (emitter, pose, entity) in SystemAPI.Query<RefRO<TrailEmitter>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
                Observe(entity, GameplaySfxCue.SquidInk, emitter.ValueRO.Emitting == 0
                    ? 0u : 1u + emitter.ValueRO.Launched, pose.ValueRO.Position, playFirst: true);

            foreach (var (segment, pose, entity) in SystemAPI.Query<RefRO<SnakeSegment>, RefRO<LocalTransform>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
            {
                var blob = segment.ValueRO;
                Observe(entity, GameplaySfxCue.BlobDetach, blob.Detached != 0 ? 1u : 0u, pose.ValueRO.Position, playFirst: true);
                Observe(entity, GameplaySfxCue.BlobBounce, blob.WallBounceSequence, blob.WallBouncePosition, playFirst: true);
            }

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
            ObserveFinale();
            ObserveGatekeeper();
            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<ChickenBoss>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var state = boss.ValueRO;
                bool charging = state.Phase == ChickenPhase.WindUp || state.Phase == ChickenPhase.Cast;
                Observe(entity, GameplaySfxCue.GoblinWizardCharge, charging ? (uint)state.Phase : 0u, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GoblinWizardCast, state.ShotSequence, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GoblinWizardTeleport, state.TeleportSequence, state.TeleportDeparture);
                Observe(entity, GameplaySfxCue.GoblinWizardTeleportArrival, state.TeleportSequence, pose.ValueRO.Position);
                bool defeated = state.Phase == ChickenPhase.Defeated;
                Observe(entity, GameplaySfxCue.GiantGoblinHit, defeated ? 0u : state.AcceptedHits, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GiantGoblinDefeat, defeated ? state.AcceptedHits : 0u, pose.ValueRO.Position);
            }
            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<DinoBoss>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var state = boss.ValueRO;
                var position = pose.ValueRO.Position;
                bool defeated = state.Phase == DinoPhase.Defeated;
                Observe(entity, GameplaySfxCue.ArmoredBossMovement, state.Phase == DinoPhase.Burst ? 1u : 0u, position);
                Observe(entity, GameplaySfxCue.GiantGoblinHit, defeated ? 0u : (uint)state.SuccessfulHits, position, playFirst: true);
                Observe(entity, GameplaySfxCue.GiantGoblinDefeat, defeated ? (uint)state.SuccessfulHits : 0u, position, playFirst: true);
                Observe(entity, GameplaySfxCue.ArmoredBossClang, defeated ? 0u : state.ArmorContactSequence,
                    state.ArmorContactPosition, playFirst: true);
            }

            bridge.BeginRollingBossRolls();
            foreach (var (boss, pose, entity) in SystemAPI.Query<RefRO<RollingBoss>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (boss.ValueRO.Phase == RollingPhase.Roll)
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    bridge.ReceiveRollingBossRoll(id, pose.ValueRO.Position);
                }
                Observe(entity, GameplaySfxCue.SpikedTurtleWallHit, (uint)boss.ValueRO.Bounces, pose.ValueRO.Position);
            }
            bridge.EndRollingBossRolls();
        }

        private void ObserveFinale()
        {
            if (!SystemAPI.TryGetSingletonEntity<FinaleBoss>(out var entity))
            {
                bridge.ReceiveFinaleFlight(default, 0f);
                bridge.ReceiveFinaleWarning(0, FinaleAttack.None, default, 0f);
                return;
            }

            var state = SystemAPI.GetComponent<FinaleBoss>(entity);
            var position = SystemAPI.GetComponent<LocalTransform>(entity).Position;
            var tuning = SystemAPI.GetComponent<FinaleTuning>(entity);
            float flightGain = state.Phase switch
            {
                FinalePhase.Flying => 1f,
                FinalePhase.Falling => math.smoothstep(0f, math.clamp(tuning.FallSeconds, .01f, FinaleFlightFadeSeconds), state.Remaining),
                FinalePhase.TakingOff => 1f - math.smoothstep(0f, math.clamp(tuning.TakeoffSeconds, .01f, FinaleFlightFadeSeconds), state.Remaining),
                _ => 0f
            };
            bridge.ReceiveFinaleFlight(position, flightGain);

            float warningDuration = state.Attack == FinaleAttack.Landing ? tuning.FallSeconds
                : tuning.WarningSeconds[math.clamp(state.Stage - 1, 0, 2)];
            bridge.ReceiveFinaleWarning(state.Attack == FinaleAttack.None ? 0u : state.AttackSequence, state.Attack,
                state.WarningCenter, 1f - math.saturate(state.AttackRemaining / math.max(.01f, warningDuration)));

            bool defeated = state.Phase == FinalePhase.Defeated;
            Observe(entity, GameplaySfxCue.FinaleAnchorOverload, state.Phase == FinalePhase.Falling
                ? state.AttackSequence : 0u, state.TetherOrigin, playFirst: true);
            Observe(entity, GameplaySfxCue.FinaleTakeoff, state.Phase == FinalePhase.TakingOff ? 1u : 0u, position);
            Observe(entity, GameplaySfxCue.FinaleDefeat, defeated ? 1u : 0u, position);
            if (defeated) return;

            ObserveHealth(entity, GameplaySfxCue.FinaleHit, state.Health, position);
            var impact = state.ResolvedAttack switch
            {
                FinaleAttack.Strike => GameplaySfxCue.FinaleLightningStrike,
                FinaleAttack.Sweep => GameplaySfxCue.FinaleAirSweep,
                FinaleAttack.Slam => GameplaySfxCue.FinaleWingSweep,
                FinaleAttack.Shockwave => GameplaySfxCue.FinaleShockwave,
                FinaleAttack.Landing => GameplaySfxCue.FinaleLanding,
                _ => GameplaySfxCue.Count
            };
            if (impact != GameplaySfxCue.Count)
                Observe(entity, impact, state.ResolvedAttackSequence, state.ResolvedAttackCenter, playFirst: true);
        }

        private void ObserveGatekeeper()
        {
            foreach (var (boss, pose, impact, entity) in SystemAPI.Query<RefRO<BossEncounter>, RefRO<LocalTransform>,
                         RefRO<BossImpactFeedback>>().WithEntityAccess())
            {
                var state = boss.ValueRO;
                bool defeated = state.Cycle == BossCycle.Defeated;
                Observe(entity, GameplaySfxCue.GatekeeperMove, state.Cycle == BossCycle.Relocating ? 1u : 0u, pose.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GatekeeperHeadHit, defeated ? 0u : state.AcceptedHits, impact.ValueRO.Position);
                Observe(entity, GameplaySfxCue.GatekeeperDefeat, defeated ? 1u : 0u, pose.ValueRO.Position);
            }

            foreach (var (hand, part, pose, entity) in SystemAPI.Query<RefRO<BossHand>, RefRO<BossPart>,
                         RefRO<LocalTransform>>().WithEntityAccess())
            {
                var attack = hand.ValueRO;
                var position = pose.ValueRO.Position;
                uint windup = attack.Phase == BossHandPhase.Anticipation ? attack.AttackSequence : 0u;
                float3 windupPosition = windup != 0 ? SystemAPI.GetComponent<LocalTransform>(part.ValueRO.Encounter).Position : position;
                Observe(entity, GameplaySfxCue.GatekeeperWindup, windup, windupPosition);
                Observe(entity, GameplaySfxCue.GatekeeperStrikeImpact, attack.StrikeContactSequence, attack.StrikeContactPosition);
                // The slam becomes dangerous at the bottom of the arc. Recovery covers an
                // active-to-recovery transition that occurs between presentation frames.
                bool recovery = attack.Phase == BossHandPhase.Recovery;
                bool active = attack.Phase == BossHandPhase.Active;
                Observe(entity, GameplaySfxCue.GatekeeperSlam, attack.Attack == BossAttack.Slam
                    && (recovery || active && position.y <= attack.Target.y + .5f) ? attack.AttackSequence : 0u, attack.Target);
                Observe(entity, GameplaySfxCue.GatekeeperLunge, attack.Attack == BossAttack.Lunge
                    && (active || recovery) ? attack.AttackSequence : 0u, position);
                Observe(entity, GameplaySfxCue.GatekeeperSweep, attack.Attack == BossAttack.Sweep
                    && (active || recovery) ? attack.AttackSequence : 0u, position);
            }
        }

        private void ObserveHealth(Entity entity, GameplaySfxCue cue, float health, float3 position)
        {
            var key = (entity, cue);
            current[key] = math.asuint(health);
            if (previous.TryGetValue(key, out uint lastHealth) && health < math.asfloat(lastHealth))
                bridge.ReceiveGameplaySfx(cue, position);
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
