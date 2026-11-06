using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    [UpdateBefore(typeof(PunchDetectionSystem))]
    public partial struct PunchAimAssistSystem : ISystem
    {
        private EntityQuery candidates;
        private EntityQuery gatekeeperHeads;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            candidates = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform>()
                .WithAny<Enemy, BossEncounter, Barricade, ChickenBoss, RollingBoss, FallingPillar>().Build(ref state);
            gatekeeperHeads = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, BossEncounter>().Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            bool available = PlayerBridgeRegistry.TryGetBridge(out var bridge) && bridge.IsPunchPreviewAvailable;
            var volume = available ? new PunchSpecification {
                Origin = bridge.PunchPreviewOrigin, Direction = bridge.PunchPreviewDirection,
                Radius = bridge.PunchPreviewRadius, Range = bridge.PunchPreviewRange,
                Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1, AffectRecovering = 1, AffectLaunched = 1
            } : default;
            SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var tuning);
            using var entities = candidates.ToEntityArray(Allocator.Temp);
            using var poses = candidates.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var heads = gatekeeperHeads.ToEntityArray(Allocator.Temp);
            using var headPoses = gatekeeperHeads.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var hits = new NativeList<RaycastHit>(Allocator.Temp);
            var collision = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var visibility = new LaunchObstructionCollector { Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Covers = SystemAPI.GetComponentLookup<RotatingCover>(true),
                Enclosures = SystemAPI.GetComponentLookup<CoverEnclosure>(true), IgnoreMovingCovers = true,
                BossParts = SystemAPI.GetComponentLookup<BossPart>(true), IgnoreTargetBossHands = true };
            var em = state.EntityManager;
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (pose, launch, health, aim, source) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>, RefRW<PunchAimAssistTarget>>()
                    .WithAll<Enemy>().WithEntityAccess())
            {
                if (!available || !PunchResolution.Contains(em, source, pose.ValueRO.Position, volume)
                    || !PunchResolution.IsEligible(launch.ValueRO, health.ValueRO, volume)
                    || ArmorHitResolution.IsProtected(em, source)
                    || SnakeHitRules.Protected(em, source)
                    || em.HasComponent<RespawnRequest>(source) && em.IsComponentEnabled<RespawnRequest>(source)
                    || !SnakePunchOcclusion.CanReach(em, source, volume.Origin))
                { aim.ValueRW = default; continue; }
                float speed = em.HasComponent<DasherSettings>(source)
                    ? em.GetComponentData<DasherSettings>(source).DashSpeed : bridge.PunchPreviewStrength;
                UpdateAim(em, collision, visibility, source, pose.ValueRO.Position, bridge,
                    entities, poses, heads, headPoses, hits, tuning, now, speed, ref aim.ValueRW);
            }
            foreach (var (pose, projectile, aim, source) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<ChickenProjectile>, RefRW<PunchAimAssistTarget>>().WithEntityAccess())
            {
                if (!available || !PunchResolution.Contains(pose.ValueRO.Position, volume))
                { aim.ValueRW = default; continue; }
                float speed = em.HasComponent<ChickenTuning>(projectile.ValueRO.Boss)
                    ? em.GetComponentData<ChickenTuning>(projectile.ValueRO.Boss).ReturnSpeed : bridge.PunchPreviewStrength;
                UpdateAim(em, collision, visibility, source, pose.ValueRO.Position, bridge,
                    entities, poses, heads, headPoses, hits, tuning, now, speed, ref aim.ValueRW);
            }
        }

        private static void UpdateAim(EntityManager em, CollisionWorld world, LaunchObstructionCollector visibility,
            Entity source, float3 position, PlayerEcsBridge bridge, NativeArray<Entity> entities,
            NativeArray<LocalTransform> poses, NativeArray<Entity> heads, NativeArray<LocalTransform> headPoses,
            NativeList<RaycastHit> hits, in EnemyLaunchSettings tuning,
            double now, float speed, ref PunchAimAssistTarget aim)
        {
            float3 facing = math.normalizesafe(new float3(bridge.PunchPreviewDirection.x, 0, bridge.PunchPreviewDirection.z));
            if (bridge.PunchPreviewAimAssistRange <= 0f)
            {
                // PLAYER-004: keep an unassisted displayed path consumable at the click.
                // Discard a previous assisted/positional snapshot when switching to facing only.
                if (aim.PreviewTarget != Entity.Null || aim.PreviewHasAimPoint != 0
                    || bridge.PunchPreviewPositionWeight <= 0f
                    && math.dot(math.normalizesafe(aim.PreviewDirection), facing) < .999999f)
                    aim = default;
                aim.Target = Entity.Null;
                aim.Challenger = Entity.Null;
                aim.IsAiming = 0;
                return;
            }
            PunchAimAssist.UpdateInputTurn(ref aim, bridge.AimInputRotationDegrees, bridge.AimInputResetSequence, now,
                bridge.PunchPreviewIntentMemorySeconds);
            bool retain = PunchAimAssist.IsValidTarget(em, source, aim.Target)
                && PunchAimAssist.IsWithinAssistLimits(em, aim.Target, position, facing,
                    bridge.PunchPreviewAimAssistRange, bridge.PunchPreviewReleaseAngleDegrees)
                && Clear(em, world, visibility, source, aim.Target, position);
            // BOSS-001/002: prefer the weak spot; shielding hands still intercept the real flight.
            Entity preferred = retain && em.HasComponent<BossEncounter>(aim.Target) ? aim.Target
                : Fallback(em, world, visibility, source, position, facing, bridge, heads, headPoses);
            if (preferred == Entity.Null)
            {
                preferred = RayTarget(em, world, source, position, facing, bridge.PunchPreviewAimAssistRange, hits);
                if (!PunchAimAssist.IsValidTarget(em, source, preferred)
                    || !PunchAimAssist.IsWithinAssistLimits(em, preferred, position, facing,
                        bridge.PunchPreviewAimAssistRange, bridge.PunchPreviewAimAssistMaximumAngleDegrees)
                    || !Clear(em, world, visibility, source, preferred, position))
                    preferred = Fallback(em, world, visibility, source, position, facing, bridge, entities, poses);
            }
            float switchDelay = bridge.PunchPreviewSwitchDelay;
            if (retain && preferred != Entity.Null && preferred != aim.Target
                && !em.HasComponent<BossEncounter>(preferred))
            {
                float3 candidateOffset = em.GetComponentData<LocalTransform>(preferred).Position - position;
                float3 lockedOffset = em.GetComponentData<LocalTransform>(aim.Target).Position - position;
                float candidateScore = PunchAimAssist.TargetScore(facing, candidateOffset,
                    bridge.PunchPreviewAimAssistMaximumAngleDegrees, bridge.PunchPreviewAimAssistRange,
                    bridge.PunchPreviewDistanceWeight);
                float lockedScore = PunchAimAssist.TargetScore(facing, lockedOffset,
                    bridge.PunchPreviewAimAssistMaximumAngleDegrees, bridge.PunchPreviewAimAssistRange,
                    bridge.PunchPreviewDistanceWeight);
                bool deliberate = PunchAimAssist.HasSwitchIntent(aim, facing, candidateOffset, now,
                    bridge.PunchPreviewIntentAngleDegrees, bridge.PunchPreviewIntentMinimumTurnDegrees,
                    bridge.PunchPreviewIntentMemorySeconds);
                if (candidateScore >= lockedScore
                    || !deliberate && candidateScore + bridge.PunchPreviewSwitchScoreMargin >= lockedScore)
                    preferred = aim.Target;
                else if (deliberate)
                    switchDelay = bridge.PunchPreviewIntentSwitchDelay;
            }
            Entity previousTarget = aim.Target;
            LaunchAimPrediction.UpdateLock(ref aim, preferred, retain, now, switchDelay);
            if (aim.Target != previousTarget) aim.RecentInputTurnDegrees = 0f;
            aim.Facing = facing;
            aim.LaunchSpeed = speed;
            aim.LeadSeconds = tuning.AimLeadSeconds;
            aim.LeadMaximumDistance = tuning.AimLeadMaximumDistance;
            aim.DirectionLimitDegrees = bridge.PunchPreviewReleaseAngleDegrees;
        }

        private static Entity Fallback(EntityManager em, CollisionWorld world, LaunchObstructionCollector visibility,
            Entity source, float3 position, float3 facing, PlayerEcsBridge bridge,
            NativeArray<Entity> entities, NativeArray<LocalTransform> poses)
        {
            Entity best = Entity.Null;
            float bestScore = float.MaxValue, bestDistance = float.MaxValue;
            float range = bridge.PunchPreviewAimAssistRange;
            float angleLimit = math.radians(bridge.PunchPreviewAimAssistMaximumAngleDegrees);
            for (int i = 0; i < entities.Length; i++)
            {
                float2 offset = poses[i].Position.xz - position.xz;
                float distanceSq = math.lengthsq(offset);
                if (distanceSq <= .000001f || distanceSq > range * range) continue;
                float distance = math.sqrt(distanceSq);
                float angle = math.acos(math.clamp(math.dot(facing.xz, offset / distance), -1f, 1f));
                if (angle > angleLimit + .000001f) continue;
                float score = PunchAimAssist.TargetScore(math.degrees(angle), distance,
                    bridge.PunchPreviewAimAssistMaximumAngleDegrees, range, bridge.PunchPreviewDistanceWeight);
                Entity candidate = entities[i];
                if (score > bestScore || score == bestScore && distance > bestDistance
                    || score == bestScore && distance == bestDistance && best != Entity.Null && candidate.Index >= best.Index
                    || !PunchAimAssist.IsValidTarget(em, source, candidate)
                    || !Clear(em, world, visibility, source, candidate, position)) continue;
                best = candidate; bestScore = score; bestDistance = distance;
            }
            return best;
        }

        private static bool Clear(EntityManager em, CollisionWorld world, LaunchObstructionCollector visibility,
            Entity source, Entity target, float3 start)
        {
            if (target == Entity.Null || !em.HasComponent<LocalTransform>(target)) return false;
            var collector = visibility; collector.Source = source; collector.Target = target;
            return !world.CastRay(new RaycastInput { Start = start,
                End = em.GetComponentData<LocalTransform>(target).Position, Filter = CollisionFilter.Default }, ref collector);
        }

        private static Entity RayTarget(EntityManager em, CollisionWorld world, Entity source, float3 start,
            float3 direction, float range, NativeList<RaycastHit> hits)
        {
            hits.Clear();
            world.CastRay(new RaycastInput { Start = start, End = start + direction * range,
                Filter = CollisionFilter.Default }, ref hits);
            Entity closest = Entity.Null;
            float fraction = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.Entity == source || em.HasComponent<RotatingCover>(hit.Entity) || em.HasComponent<CoverEnclosure>(hit.Entity) || hit.Fraction >= fraction
                    || SnakeHitRules.Protected(em, hit.Entity)
                    || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                    || hit.Material.CollisionResponse == CollisionResponsePolicy.None
                    || SnakeProjectileCollision.IgnoresOwner(em, source, hit.Entity)) continue;
                closest = hit.Entity; fraction = hit.Fraction;
            }
            return closest;
        }
    }
}
