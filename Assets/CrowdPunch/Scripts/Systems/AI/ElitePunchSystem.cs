using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Movement.EnemyMovementSystem))]
    [UpdateBefore(typeof(PunchDetectionSystem))]
    public partial class ElitePunchSystem : SystemBase
    {
        private EntityQuery candidates;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSnapshot>();
            candidates = GetEntityQuery(ComponentType.ReadOnly<Enemy>(), ComponentType.ReadOnly<EnemyTier>(),
                ComponentType.ReadOnly<EnemyArchetype>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyLaunchState>(), ComponentType.ReadOnly<Health>());
        }

        protected override void OnUpdate()
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            float dt = SystemAPI.Time.DeltaTime;
            using NativeArray<Entity> all = candidates.ToEntityArray(Allocator.Temp);
            foreach ((RefRW<ElitePunchReservation> reservation, Entity target) in
                     SystemAPI.Query<RefRW<ElitePunchReservation>>().WithEntityAccess())
            {
                Entity owner = reservation.ValueRO.Owner;
                if (owner == Entity.Null) continue;
                bool invalidOwner = !EntityManager.Exists(owner)
                    || !EntityManager.HasComponent<ElitePunchState>(owner)
                    || EntityManager.GetComponentData<EnemyLaunchState>(owner).Phase != EnemyLaunchPhase.Active
                    || EntityManager.HasComponent<RespawnRequest>(owner) && EntityManager.IsComponentEnabled<RespawnRequest>(owner);
                bool invalidTarget = EntityManager.GetComponentData<EnemyLaunchState>(target).Phase == EnemyLaunchPhase.Defeated
                    || EntityManager.HasComponent<RespawnRequest>(target) && EntityManager.IsComponentEnabled<RespawnRequest>(target);
                if (invalidOwner || invalidTarget) reservation.ValueRW = default;
            }
            foreach ((RefRW<ElitePunchState> state, RefRO<ElitePunchSettings> settings,
                         RefRW<DesiredMovement> movement, RefRW<LocalTransform> transform,
                         RefRO<EnemyLaunchState> launch, Entity elite) in
                     SystemAPI.Query<RefRW<ElitePunchState>, RefRO<ElitePunchSettings>, RefRW<DesiredMovement>,
                         RefRW<LocalTransform>, RefRO<EnemyLaunchState>>().WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (!player.IsAvailable || launch.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    Cancel(elite, ref state.ValueRW); movement.ValueRW = default; continue;
                }
                state.ValueRW.SecondsRemaining -= dt;
                if (state.ValueRO.Phase == ElitePunchPhase.InitialDelay || state.ValueRO.Phase == ElitePunchPhase.Cooldown)
                {
                    if (state.ValueRO.SecondsRemaining <= 0f) state.ValueRW.Phase = ElitePunchPhase.SelectingTarget;
                    continue;
                }
                if (state.ValueRO.Phase == ElitePunchPhase.SelectingTarget)
                {
                    if (state.ValueRO.SecondsRemaining > 0f) continue;
                    SelectTarget(elite, transform.ValueRO.Position, player, settings.ValueRO, all, ref state.ValueRW);
                    continue;
                }
                if (!TryValidateTarget(elite, player, settings.ValueRO, ref state.ValueRW, out LocalTransform targetTransform))
                {
                    Cancel(elite, ref state.ValueRW); state.ValueRW.Phase = ElitePunchPhase.SelectingTarget; continue;
                }
                state.ValueRW.SetupSeconds += dt;
                state.ValueRW.RetargetSeconds -= dt;
                if (state.ValueRO.SetupSeconds > math.max(0f, settings.ValueRO.MaximumSetupDuration))
                {
                    BeginCooldown(elite, settings.ValueRO, ref state.ValueRW); continue;
                }
                if (state.ValueRO.RetargetSeconds <= 0f)
                {
                    state.ValueRW.RetargetSeconds = math.max(0.02f, settings.ValueRO.RetargetInterval);
                    if (math.distance(player.Position.xz, state.ValueRO.ValidatedPlayerPosition.xz) > settings.ValueRO.PlayerMovementInvalidationDistance
                        || math.distance(targetTransform.Position.xz, state.ValueRO.ValidatedTargetPosition.xz) > settings.ValueRO.TargetMovementInvalidationDistance)
                    {
                        state.ValueRW.ValidatedPlayerPosition = player.Position;
                        state.ValueRW.ValidatedTargetPosition = targetTransform.Position;
                        if (state.ValueRO.Phase == ElitePunchPhase.WindUp) state.ValueRW.Phase = ElitePunchPhase.Repositioning;
                    }
                }
                float3 launchDirection = HorizontalDirection(targetTransform.Position, player.Position);
                EnemyMovementSettings movementSettings = SystemAPI.GetComponent<EnemyMovementSettings>(elite);
                quaternion desiredRotation = quaternion.LookRotationSafe(launchDirection, math.up());
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    desiredRotation,
                    math.saturate(math.max(0f, movementSettings.TurnSpeed) * dt));
                float3 desired = DesiredPosition(targetTransform.Position, player.Position, settings.ValueRO.DesiredPunchDistance);
                desired.y = transform.ValueRO.Position.y;
                float3 toDesired = desired - transform.ValueRO.Position; toDesired.y = 0f;
                float3 setupDirection = math.normalizesafe(toDesired);
                movement.ValueRW.Direction = settings.ValueRO.ApplySeparationDuringSetup != 0
                    ? math.normalizesafe(setupDirection + movement.ValueRO.Direction * 0.35f, setupDirection)
                    : setupDirection;
                float setupDistance = math.length(toDesired);
                float maximumSetupSpeed = movementSettings.MoveSpeed
                    * math.max(0f, settings.ValueRO.SetupMovementSpeedMultiplier);
                float setupBraking = math.min(
                    math.max(0f, movementSettings.Acceleration),
                    math.max(0f, movementSettings.BrakingAcceleration));
                float targetHorizontalSpeed = EntityManager.HasComponent<PhysicsVelocity>(state.ValueRO.Target)
                    ? math.length(EntityManager.GetComponentData<PhysicsVelocity>(state.ValueRO.Target).Linear.xz)
                    : 0f;
                float minimumCatchupSpeed = targetHorizontalSpeed
                    + math.sqrt(2f * setupBraking * math.max(0f, settings.ValueRO.PositionTolerance));
                movement.ValueRW.Speed = CalculateSetupSpeed(
                    setupDistance,
                    settings.ValueRO.PositionTolerance,
                    maximumSetupSpeed,
                    setupBraking,
                    minimumCatchupSpeed);
                bool linedUp = IsLinedUp(transform.ValueRO, targetTransform.Position, launchDirection, settings.ValueRO);
                if (!linedUp)
                {
                    if (state.ValueRO.Phase == ElitePunchPhase.WindUp) { state.ValueRW.Phase = ElitePunchPhase.Repositioning; state.ValueRW.TelegraphActive = 0; }
                    continue;
                }
                if (state.ValueRO.Phase != ElitePunchPhase.WindUp && settings.ValueRO.WindUpDuration > 0f)
                {
                    state.ValueRW.Phase = ElitePunchPhase.WindUp; state.ValueRW.SecondsRemaining = settings.ValueRO.WindUpDuration;
                    state.ValueRW.TelegraphActive = settings.ValueRO.EnableTelegraph; continue;
                }
                if (state.ValueRO.Phase == ElitePunchPhase.WindUp && state.ValueRO.SecondsRemaining > 0f) continue;
                PunchSpecification executionPunch = Spec(transform.ValueRO.Position, launchDirection, settings.ValueRO);
                if (!PunchResolution.IsEligible(
                        EntityManager.GetComponentData<EnemyLaunchState>(state.ValueRO.Target),
                        EntityManager.GetComponentData<Health>(state.ValueRO.Target),
                        executionPunch))
                {
                    Cancel(elite, ref state.ValueRW);
                    state.ValueRW.Phase = ElitePunchPhase.SelectingTarget;
                    state.ValueRW.SecondsRemaining = 0f;
                    continue;
                }
                ExecutePunch(elite, transform.ValueRO.Position, targetTransform.Position, player, settings.ValueRO, all, state.ValueRO.Target);
                BeginCooldown(elite, settings.ValueRO, ref state.ValueRW);
            }
        }

        private void SelectTarget(Entity elite, float3 elitePosition, PlayerSnapshot player, ElitePunchSettings settings,
            NativeArray<Entity> all, ref ElitePunchState state)
        {
            state.AttackSequence++; uint randomState = state.RandomState == 0 ? (uint)math.max(1, elite.Index + 1) : state.RandomState;
            ElitePunchTactic preferred = ChooseTactic(ref randomState, settings.ClearPathTacticProbability);
            state.RandomState = randomState;
            Entity selected = FindBest(elite, elitePosition, player, settings, all, preferred);
            if (selected == Entity.Null) { preferred = preferred == ElitePunchTactic.ClearPath ? ElitePunchTactic.CrowdShot : ElitePunchTactic.ClearPath; selected = FindBest(elite, elitePosition, player, settings, all, preferred); }
            if (selected == Entity.Null) { state.SecondsRemaining = math.max(0.02f, settings.RetargetInterval); return; }
            EntityManager.SetComponentData(selected, new ElitePunchReservation { Owner = elite, OwnerAttackSequence = state.AttackSequence });
            state.Target = selected; state.Tactic = preferred; state.Phase = ElitePunchPhase.Repositioning; state.SetupSeconds = 0f;
            state.RetargetSeconds = math.max(0.02f, settings.RetargetInterval); state.ValidatedPlayerPosition = player.Position;
            state.ValidatedTargetPosition = EntityManager.GetComponentData<LocalTransform>(selected).Position;
        }

        private Entity FindBest(Entity elite, float3 elitePosition, PlayerSnapshot player, ElitePunchSettings settings, NativeArray<Entity> all, ElitePunchTactic tactic)
        {
            Entity best = Entity.Null; float bestScore = float.NegativeInfinity; int evaluated = 0;
            for (int i=0; i<all.Length && evaluated<math.max(1, settings.MaximumEvaluatedCandidates); i++)
            {
                Entity candidate=all[i]; if (!IsCandidate(elite,candidate,elitePosition,player,settings)) continue; evaluated++;
                float3 p=EntityManager.GetComponentData<LocalTransform>(candidate).Position; float3 dir=HorizontalDirection(p,player.Position);
                float crowd=CorridorScore(candidate,p,player.Position+dir*settings.CrowdDistanceBeyondPlayer,settings.CrowdCorridorRadius,all,settings.CrowdNearPlayerWeight);
                bool blocked=HasWorldObstruction(p,player.Position) || HasEnemyBlocker(candidate,p,player.Position,settings.CrowdCorridorRadius,all);
                float score;
                if(tactic==ElitePunchTactic.ClearPath) { if(blocked) continue; float3 behind=p-dir*settings.DesiredPunchDistance; score=settings.ClearPathAlignmentWeight-settings.ClearPathRepositionWeight*math.distance(elitePosition.xz,behind.xz)-settings.ClearPathDistanceWeight*math.distance(p.xz,player.Position.xz); }
                else { if(HasWorldObstruction(p,player.Position)||crowd<settings.MinimumCrowdScore) continue; score=crowd; }
                if(score>bestScore){bestScore=score;best=candidate;}
            }
            return best;
        }

        private bool IsCandidate(Entity elite, Entity target, float3 elitePosition, PlayerSnapshot player, ElitePunchSettings s)
        {
            if(target==elite || !EntityManager.Exists(target) || EntityManager.GetComponentData<EnemyTier>(target).Value!=EnemyCombatTier.Normal) return false;
            if(EntityManager.HasComponent<RespawnRequest>(target)&&EntityManager.IsComponentEnabled<RespawnRequest>(target)) return false;
            if(EntityManager.HasComponent<ElitePunchReservation>(target)){var r=EntityManager.GetComponentData<ElitePunchReservation>(target);if(r.Owner!=Entity.Null&&r.Owner!=elite&&s.AllowSharedTargets==0&&EntityManager.Exists(r.Owner))return false;}
            float3 p=EntityManager.GetComponentData<LocalTransform>(target).Position; float ep=math.distance(elitePosition.xz,p.xz),tp=math.distance(p.xz,player.Position.xz);
            if(ep>s.MaximumSearchRange||tp<s.MinimumTargetPlayerDistance||tp>s.MaximumTargetPlayerDistance)return false;
            return CanSelectTarget(
                EntityManager.GetComponentData<EnemyLaunchState>(target),
                EntityManager.GetComponentData<Health>(target),
                s);
        }

        private bool TryValidateTarget(Entity elite, PlayerSnapshot player, ElitePunchSettings s, ref ElitePunchState state, out LocalTransform transform)
        { transform=default; if(state.Target==Entity.Null||!IsCandidate(elite,state.Target,EntityManager.GetComponentData<LocalTransform>(elite).Position,player,s))return false; transform=EntityManager.GetComponentData<LocalTransform>(state.Target);return true; }
        private bool IsLinedUp(LocalTransform elite,float3 target,float3 direction,ElitePunchSettings s)
        { float3 desired=target-direction*s.DesiredPunchDistance; desired.y=elite.Position.y; if(math.distance(elite.Position.xz,desired.xz)>s.PositionTolerance)return false; float3 forward=math.forward(elite.Rotation); forward.y=0f; float cosine=math.cos(math.radians(math.clamp(s.AimAngleToleranceDegrees,0f,180f))); if(math.dot(math.normalizesafe(forward,direction),direction)<cosine)return false; return PunchResolution.Contains(target,Spec(elite.Position,direction,s)); }
        private void ExecutePunch(Entity elite,float3 origin,float3 target,PlayerSnapshot player,ElitePunchSettings s,NativeArray<Entity> all,Entity selected)
        { float3 direction=HorizontalDirection(target,player.Position); PunchSpecification spec=Spec(origin,direction,s); if(s.InteractionMode==ElitePunchInteractionMode.SelectedTargetOnly){spec.ApplyDamage=s.ProjectileReceivesDamage;PunchResolution.TryApply(EntityManager,selected,spec);}else for(int i=0;i<all.Length;i++){Entity e=all[i];if(EntityManager.Exists(e)&&EntityManager.GetComponentData<EnemyTier>(e).Value==EnemyCombatTier.Normal){PunchSpecification hit=spec;hit.ApplyDamage=e==selected?s.ProjectileReceivesDamage:(byte)1;PunchResolution.TryApply(EntityManager,e,hit);}} if(s.CanDirectlyHitPlayer!=0&&PunchResolution.Contains(player.Position,spec)&&PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))bridge.ReceiveEnemyHit(s.DirectPlayerDamage,s.PlayerInvincibilityDuration,direction*s.PlayerPush); }
        private static PunchSpecification Spec(float3 origin,float3 direction,ElitePunchSettings s)=>new PunchSpecification{Origin=origin,Direction=direction,Range=s.PunchRange,Radius=s.PunchRadius,Strength=s.LaunchForce,Damage=s.PunchDamage,PositionWeight=s.PushDirectionPositionWeight,Cause=EnemyLaunchCause.ElitePunch,AffectActive=s.AffectActive,AffectRecovering=s.AffectRecovering,AffectLaunched=s.AffectLaunched,ApplyDamage=1};
        private void BeginCooldown(Entity elite,ElitePunchSettings s,ref ElitePunchState state){Cancel(elite,ref state);Random r=new Random(state.RandomState==0?1u:state.RandomState);state.Phase=ElitePunchPhase.Cooldown;state.SecondsRemaining=math.max(0f,s.Cooldown)+r.NextFloat(0f,math.max(0f,s.CooldownVariation));state.RandomState=r.state;}
        private void Cancel(Entity elite,ref ElitePunchState state){if(state.Target!=Entity.Null&&EntityManager.Exists(state.Target)&&EntityManager.HasComponent<ElitePunchReservation>(state.Target)){var r=EntityManager.GetComponentData<ElitePunchReservation>(state.Target);if(r.Owner==elite)EntityManager.SetComponentData(state.Target,new ElitePunchReservation());}state.Target=Entity.Null;state.SetupSeconds=0f;state.TelegraphActive=0;}
        private bool HasWorldObstruction(float3 a,float3 b){if(!SystemAPI.HasSingleton<PhysicsWorldSingleton>())return false;var input=new RaycastInput{Start=a,End=b,Filter=new CollisionFilter{BelongsTo=uint.MaxValue,CollidesWith=~(1u<<7),GroupIndex=0}};return SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld.CastRay(input);}
        private bool HasEnemyBlocker(Entity target,float3 a,float3 b,float radius,NativeArray<Entity> all)=>CorridorScore(target,a,b,radius,all,0f)>0f;
        private float CorridorScore(Entity target,float3 a,float3 b,float radius,NativeArray<Entity> all,float nearWeight){float score=0f;float2 ab=b.xz-a.xz;float len2=math.lengthsq(ab);for(int i=0;i<all.Length;i++){Entity e=all[i];if(e==target||!EntityManager.Exists(e)||EntityManager.GetComponentData<EnemyTier>(e).Value!=EnemyCombatTier.Normal)continue;if(EntityManager.HasComponent<RespawnRequest>(e)&&EntityManager.IsComponentEnabled<RespawnRequest>(e))continue;EnemyLaunchState launch=EntityManager.GetComponentData<EnemyLaunchState>(e);if(launch.Phase==EnemyLaunchPhase.Defeated)continue;float2 p=EntityManager.GetComponentData<LocalTransform>(e).Position.xz;float t=math.saturate(math.dot(p-a.xz,ab)/math.max(.0001f,len2));if(math.distancesq(p,a.xz+ab*t)<=radius*radius)score+=1f+nearWeight*t;}return score;}
        public static float3 HorizontalDirection(float3 from,float3 to){float3 d=to-from;d.y=0f;return math.normalizesafe(d,new float3(0,0,1));}
        public static float3 DesiredPosition(float3 target,float3 player,float distance){float3 result=target-HorizontalDirection(target,player)*math.max(0f,distance);result.y=target.y;return result;}
        public static ElitePunchTactic ChooseTactic(ref uint randomState,float clearPathProbability){Random random=new Random(randomState==0?1u:randomState);ElitePunchTactic result=random.NextFloat()<math.saturate(clearPathProbability)?ElitePunchTactic.ClearPath:ElitePunchTactic.CrowdShot;randomState=random.state;return result;}
        public static float CalculateSetupSpeed(
            float distance,
            float tolerance,
            float maximumSpeed,
            float brakingAcceleration,
            float minimumCatchupSpeed)
        {
            float remainingDistance = math.max(0f, distance - math.max(0f, tolerance));
            if (remainingDistance <= 0f) return 0f;
            float arrivalSpeed = math.sqrt(2f * math.max(0f, brakingAcceleration) * remainingDistance);
            float requestedSpeed = math.max(arrivalSpeed, math.max(0f, minimumCatchupSpeed));
            return math.min(math.max(0f, maximumSpeed), requestedSpeed);
        }
        public static bool CanSelectTarget(EnemyLaunchState launch, Health health, ElitePunchSettings settings)
        {
            if (launch.Phase == EnemyLaunchPhase.Defeated) return false;
            if (launch.Phase != EnemyLaunchPhase.Launched && health.Current <= 0f) return false;
            return (launch.Phase == EnemyLaunchPhase.Active && settings.AllowActiveTargets != 0)
                || (launch.Phase == EnemyLaunchPhase.Recovering && settings.AllowRecoveringTargets != 0)
                || (launch.Phase == EnemyLaunchPhase.Launched && settings.AllowLaunchedTargets != 0);
        }
    }
}
