using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CrowdPunch.Mono.Player;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Bridges ECS simulation results to presentation-only GameObject or rendering state.
    /// </summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct PresentationBridgeSystem : ISystem
    {
        private EntityQuery correctionCandidates;
        private NativeHashMap<Entity, CollisionPreviewState> collisionStates;
        private uint collisionFrame, resetSequence;
        private double lastPreviewTime;
        private int bridgeInstance;
        private Entity observedPlayer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            collisionStates = new NativeHashMap<Entity, CollisionPreviewState>(32, Allocator.Persistent);
            correctionCandidates = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform>().WithAny<Enemy, Barricade>().Build(ref state);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (collisionStates.IsCreated) collisionStates.Dispose();
        }

        // PLAYER-003: clipping the copied decoration must not truncate the committed launch path.
        internal static void PublishDottedTrajectory(PlayerEcsBridge bridge, NativeList<float3> points,
            NativeList<LaunchTrajectoryImpact> impacts, NativeList<LaunchTrajectoryContact> firstContact)
        {
            bool clipped = bridge.TrajectoryRenderToFirstContact && firstContact.Length > 0;
            int last = clipped ? firstContact[0].EndPointIndex : points.Length - 1;
            for (int i = 1; i <= last; i++)
                bridge.AddDottedTrajectory(points[i - 1], clipped && i == last ? firstContact[0].Position : points[i], i == 1);
            foreach (var impact in impacts)
                if (!clipped || math.distancesq(impact.Position, firstContact[0].Position) < .000001f)
                    bridge.AddTrajectoryImpact(impact.Position, impact.Direction);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                collisionStates.Clear();
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<PlayerSnapshot>(out Entity playerEntity))
            {
                collisionStates.Clear();
                bridge.BeginTrajectoryPreview();
                return;
            }
            double now = SystemAPI.Time.ElapsedTime;
            float previewDelta = (float)math.max(0d, now - lastPreviewTime);
            if (observedPlayer != playerEntity || bridgeInstance != bridge.GetInstanceID()
                || resetSequence != bridge.CollisionPreviewResetSequence || now < lastPreviewTime
                || previewDelta > math.max(.25f, bridge.CollisionPreviewTuning.ConfirmationTime * 2f))
                collisionStates.Clear();
            observedPlayer = playerEntity; bridgeInstance = bridge.GetInstanceID();
            resetSequence = bridge.CollisionPreviewResetSequence;
            if (SystemAPI.HasComponent<PunchRequest>(playerEntity))
            {
                PunchRequest punch = SystemAPI.GetComponent<PunchRequest>(playerEntity);
                if (punch.IsResolved)
                {
                    bridge.ReceivePunchResult(punch.Sequence, punch.HitEnemy);
                    if (punch.HitArmor && punch.Sequence == bridge.PunchSequence)
                        bridge.ReceiveGameplaySfx(GameplaySfxCue.ArmorBlock, punch.ArmorContact);
                    punch.IsResolved = false;
                    SystemAPI.SetComponent(playerEntity, punch);
                }
            }

            // Preserve the displayed ECS snapshot until the next fixed step consumes the click.
            if (bridge.HasPendingPunch) return;
            lastPreviewTime = now;
            bridge.BeginTrajectoryPreview(SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var previewGround)
                ? previewGround.SurfaceHeight : 0f);
            foreach (var surface in SystemAPI.Query<RefRO<TrajectoryPreviewSurface>>())
                bridge.AddTrajectoryPreviewSurface(surface.ValueRO);

            if (!bridge.IsPunchPreviewAvailable
                || !bridge.UseDottedTrajectory && bridge.PunchPreviewLength <= 0f && bridge.CollisionPreviewLength <= 0f)
            {
                collisionStates.Clear();
                return;
            }

            float3 origin = bridge.PunchPreviewOrigin;
            float3 punchDirection = math.normalizesafe(bridge.PunchPreviewDirection);
            PunchSpecification volume = new PunchSpecification
            {
                Origin = origin,
                Direction = punchDirection,
                Radius = bridge.PunchPreviewRadius,
                Range = bridge.PunchPreviewRange,
                Cause = EnemyLaunchCause.PlayerPunch
            };
            float positionWeight = math.saturate(bridge.PunchPreviewPositionWeight);
            using var sources = new NativeList<LaunchPreviewSource>(Allocator.Temp);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState, RefRO<Health> health,
                         RefRO<EnemyTier> tier, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyTier>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                float3 enemyPosition = transform.ValueRO.Position;
                if (!PunchResolution.Contains(state.EntityManager, enemy, enemyPosition, volume)) continue;
                if (SystemAPI.HasComponent<BossPart>(enemy) || ArmorHitResolution.IsProtected(state.EntityManager, enemy)
                    || SnakeHitRules.Protected(state.EntityManager, enemy)
                    || !EnemyLaunchTransition.IsLaunchable(tier.ValueRO)
                    || !EnemyLaunchTransition.CanReceivePlayerPunch(launchState.ValueRO, health.ValueRO))
                {
                    continue;
                }

                float3 toEnemy = enemyPosition - origin;
                if (!SnakePunchOcclusion.CanReach(state.EntityManager, enemy, volume.Origin))
                {
                    continue;
                }

                float3 positionDirection = math.normalizesafe(toEnemy, punchDirection);
                float3 launchDirection;
                if (!PunchAimAssist.TryGetLockedDirection(
                        state.EntityManager, enemy, enemyPosition, out launchDirection))
                {
                    launchDirection = math.normalizesafe(
                        math.lerp(punchDirection, positionDirection, positionWeight), positionDirection);
                }
                bool hasPoint = PunchAimAssist.TryGetLockedPoint(state.EntityManager, enemy, enemyPosition, out float3 aimPoint);
                Entity aimTarget = hasPoint ? SystemAPI.GetComponent<PunchAimAssistTarget>(enemy).Target : Entity.Null;
                sources.Add(new LaunchPreviewSource { Entity = enemy, Direction = launchDirection,
                    HasAimPoint = hasPoint ? (byte)1 : (byte)0, AimPoint = aimPoint, AimTarget = aimTarget,
                    Damage = bridge.PunchPreviewDamage,
                    Speed = SystemAPI.HasComponent<DasherSettings>(enemy)
                        ? SystemAPI.GetComponent<DasherSettings>(enemy).DashSpeed : bridge.PunchPreviewStrength });
                if ((bridge.UseDottedTrajectory || bridge.PunchPreviewLength > 0f) && SystemAPI.HasComponent<PunchAimAssistTarget>(enemy))
                {
                    var aim = SystemAPI.GetComponent<PunchAimAssistTarget>(enemy);
                    aim.PreviewDirection = launchDirection;
                    aim.PreviewTarget = PunchAimAssist.IsValidTarget(state.EntityManager, enemy, aim.Target) ? aim.Target : Entity.Null;
                    aim.PreviewRevision = bridge.TrajectoryPreviewRevision;
                    aim.PreviewAimPoint = aimPoint;
                    aim.PreviewHasAimPoint = hasPoint ? (byte)1 : (byte)0;
                    aim.PreviewTargetGeneration = CommittedLaunchPlan.Generation(state.EntityManager, aimTarget);
                    aim.PreviewPlanSeconds = 0f;
                    aim.PreviewHasAuthoritativePath = 0;
                    if (SystemAPI.HasBuffer<DisplayedLaunchRebound>(enemy))
                        SystemAPI.GetBuffer<DisplayedLaunchRebound>(enemy).Clear();
                    if (SystemAPI.HasBuffer<DisplayedLaunchPathPoint>(enemy))
                        SystemAPI.GetBuffer<DisplayedLaunchPathPoint>(enemy).Clear();
                    SystemAPI.SetComponent(enemy, aim);
                }
                if (!bridge.UseDottedTrajectory && bridge.PunchPreviewLength > 0f)
                    bridge.AddTrajectoryPreview(enemyPosition,
                        enemyPosition + launchDirection * bridge.PunchPreviewLength);
            }
            foreach (var (pose, shot) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ChickenProjectile>().WithEntityAccess())
            {
                if (!PunchResolution.Contains(pose.ValueRO.Position, volume)) continue;
                var projectile = SystemAPI.GetComponent<ChickenProjectile>(shot);
                if (!SystemAPI.HasComponent<ChickenTuning>(projectile.Boss)
                    || !SystemAPI.HasComponent<ChickenBoss>(projectile.Boss)
                    || SystemAPI.GetComponent<ChickenBoss>(projectile.Boss).Phase == ChickenPhase.Defeated) continue;
                volume.PositionWeight = positionWeight;
                var direction = ChickenProjectilePunch.Direction(state.EntityManager, shot, pose.ValueRO.Position, volume);
                if ((bridge.UseDottedTrajectory || bridge.PunchPreviewLength > 0f) && SystemAPI.HasComponent<PunchAimAssistTarget>(shot))
                {
                    var aim = SystemAPI.GetComponent<PunchAimAssistTarget>(shot);
                    aim.PreviewDirection = direction; aim.PreviewTarget = aim.Target;
                    aim.PreviewTargetGeneration = CommittedLaunchPlan.Generation(state.EntityManager, aim.Target);
                    aim.PreviewRevision = bridge.TrajectoryPreviewRevision;
                    SystemAPI.SetComponent(shot, aim);
                }
                if (bridge.UseDottedTrajectory)
                {
                    SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var projectileLaunchSettings);
                    using var shotPoints = new NativeList<float3>(Allocator.Temp);
                    using var shotImpacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
                    using var shotContact = new NativeList<LaunchTrajectoryContact>(Allocator.Temp);
                    ChickenProjectileTrajectoryPrediction.Trace(state.EntityManager, shot, pose.ValueRO.Position,
                        direction, projectileLaunchSettings, SystemAPI.GetComponent<PlayerSnapshot>(playerEntity),
                        bridge.TrajectoryMaximumSeconds, bridge.TrajectorySteps,
                        state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>()?.Timestep
                            ?? DirectLaunchTrajectoryPrediction.DefaultTimestep, shotPoints, shotImpacts, shotContact);
                    PublishDottedTrajectory(bridge, shotPoints, shotImpacts, shotContact);
                }
                else if (bridge.PunchPreviewLength > 0f)
                    bridge.AddTrajectoryPreview(pose.ValueRO.Position, pose.ValueRO.Position + direction * bridge.PunchPreviewLength);
            }

            if (bridge.UseDottedTrajectory)
            {
                collisionStates.Clear();
                if (sources.Length == 0 || !SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var trajectoryPhysics)
                    || !SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var trajectorySettings)) return;
                using var points = new NativeList<float3>(Allocator.Temp);
                using var firstContact = new NativeList<LaunchTrajectoryContact>(Allocator.Temp);
                using var impacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
                using var rebounds = new NativeList<LaunchReboundPlan>(Allocator.Temp);
                using var path = new NativeList<LaunchPathPoint>(Allocator.Temp);
                float3 gravity = SystemAPI.TryGetSingleton<PhysicsStep>(out var step) ? step.Gravity : PhysicsStep.Default.Gravity;
                EnemyDefeatBounds? defeatBounds = SystemAPI.TryGetSingleton<EnemyDefeatBounds>(out var bounds) ? bounds : null;
                float fixedTimestep = state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>()?.Timestep
                    ?? DirectLaunchTrajectoryPrediction.DefaultTimestep;
                foreach (var source in sources)
                {
                    DirectLaunchTrajectoryPrediction.Trace(state.EntityManager, trajectoryPhysics.CollisionWorld,
                        source, sources.AsArray(), trajectorySettings, bridge.TrajectoryMaximumSeconds,
                        bridge.TrajectorySteps, points, gravity, defeatBounds, impacts,
                        SystemAPI.GetComponent<PlayerSnapshot>(playerEntity), fixedTimestep, rebounds: rebounds, path: path, firstContact: firstContact);
                    if (SystemAPI.HasBuffer<DisplayedLaunchPathPoint>(source.Entity))
                    {
                        var displayedPath = SystemAPI.GetBuffer<DisplayedLaunchPathPoint>(source.Entity);
                        displayedPath.Clear();
                        foreach (var point in path) displayedPath.Add(new DisplayedLaunchPathPoint { Value = point });
                    }
                    if (SystemAPI.HasBuffer<DisplayedLaunchRebound>(source.Entity))
                    {
                        var displayed = SystemAPI.GetBuffer<DisplayedLaunchRebound>(source.Entity);
                        displayed.Clear();
                        foreach (var rebound in rebounds) displayed.Add(new DisplayedLaunchRebound { Value = rebound });
                    }
                    if (SystemAPI.HasComponent<PunchAimAssistTarget>(source.Entity))
                    {
                        var aim = SystemAPI.GetComponent<PunchAimAssistTarget>(source.Entity);
                        aim.PreviewPlanSeconds = math.min(bridge.TrajectoryMaximumSeconds,
                            bridge.TrajectorySteps * fixedTimestep);
                        aim.PreviewHasAuthoritativePath = path.Length > 1 && SystemAPI.HasBuffer<DisplayedLaunchPathPoint>(source.Entity)
                            && SystemAPI.HasComponent<AuthoritativeLaunchPath>(source.Entity) && SystemAPI.HasComponent<PhysicsMassOverride>(source.Entity)
                            ? (byte)1 : (byte)0;
                        SystemAPI.SetComponent(source.Entity, aim);
                    }
                    PublishDottedTrajectory(bridge, points, impacts, firstContact);
                }
                return;
            }

            if (sources.Length == 0 || bridge.CollisionPreviewLength <= 0f
                || bridge.CollisionPredictionDistance <= 0f
                || !SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics)
                || !SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var settings))
            { collisionStates.Clear(); return; }

            using var candidates = correctionCandidates.ToEntityArray(Allocator.Temp);
            using var candidateTransforms = correctionCandidates.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            using var forecasts = new NativeList<LaunchImpactForecast>(Allocator.Temp);
            using var choicesOwner = new NativeHashMap<Entity, int>(math.max(1, sources.Length), Allocator.Temp);
            var choices = choicesOwner;
            var tuning = bridge.CollisionPreviewTuning;
            foreach (var source in sources)
            {
                if (!LaunchImpactPrediction.TryForecast(state.EntityManager, physics.CollisionWorld,
                        source, sources.AsArray(), candidates, candidateTransforms, bridge.CollisionPredictionDistance,
                        bridge.PunchPreviewStrength, settings, now, hits, tuning, out var forecast)) continue;
                int index = forecasts.Length;
                forecasts.Add(forecast);
                if (!choices.TryGetValue(forecast.Victim, out int previous)
                    || CollisionPreviewStability.Earlier(forecast, forecasts[previous])) choices[forecast.Victim] = index;
            }
            collisionFrame++;
            if (collisionFrame == 0) { collisionStates.Clear(); collisionFrame++; }
            using var victims = choices.GetKeyArray(Allocator.Temp);
            foreach (Entity victim in victims)
            {
                var forecast = forecasts[choices[victim]];
                bool conflicting = false;
                foreach (var other in forecasts)
                    conflicting |= CollisionPreviewStability.Conflicts(forecast, other, tuning);
                if (conflicting) continue;
                collisionStates.TryGetValue(victim, out var stable);
                bool previousStillValid = stable.Seen == 0;
                foreach (var other in forecasts)
                    previousStillValid |= other.Source == stable.Source && other.Victim == victim;
                if (!previousStillValid) stable = default;
                bool visible = CollisionPreviewStability.Update(ref stable, forecast, tuning, now, previewDelta, collisionFrame, out float opacity);
                collisionStates[victim] = stable;
                if (!visible) continue;
                float3 position = state.EntityManager.GetComponentData<LocalTransform>(victim).Position;
                bridge.AddTrajectoryPreview(position, position + stable.DisplayedDirection * bridge.CollisionPreviewLength, true, opacity);
            }
            using var remembered = collisionStates.GetKeyArray(Allocator.Temp);
            foreach (Entity victim in remembered)
            {
                // Missing, blocked, protected, reused or ambiguous predictions never retain an old arrow.
                if (collisionStates[victim].Seen != collisionFrame) collisionStates.Remove(victim);
            }
        }
    }
}
