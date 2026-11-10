using System.Runtime.CompilerServices;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Finds enemies affected by the current punch request.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    [UpdateAfter(typeof(PunchAimAssistSystem))]
    public partial struct PunchDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PunchRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity punchEntity = SystemAPI.GetSingletonEntity<PlayerSnapshot>();

            if (!SystemAPI.IsComponentEnabled<PunchRequest>(punchEntity))
            {
                return;
            }

            PunchRequest punchRequest = SystemAPI.GetComponent<PunchRequest>(punchEntity);
            SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var launchSettings);
            PunchSpecification punch = new PunchSpecification
            {
                Origin = punchRequest.Origin, Direction = punchRequest.Direction, Range = punchRequest.Range,
                Radius = punchRequest.Radius, Strength = punchRequest.Strength,
                Damage = punchRequest.Damage, PositionWeight = punchRequest.PushDirectionPositionWeight,
                PunchSequence = punchRequest.Sequence,
                Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1, AffectRecovering = 1,
                EventTime = SystemAPI.Time.ElapsedTime,
                AffectLaunched = 1, ApplyDamage = 1
            };
            punch.AimRevision = punchRequest.AimRevision;
            punch.HomingDuration = launchSettings.LaunchHomingDuration;
            float3 playerPosition = SystemAPI.GetComponent<PlayerSnapshot>(punchEntity).Position;
            float closestApproach = float.MaxValue;
            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState,
                         RefRO<Health> health, RefRO<EnemyTier> tier, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyTier>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                LocalTransform pose = transform.ValueRO;
                if (ArmorHitResolution.IsProtected(state.EntityManager, enemy))
                {
                    if (PunchResolution.IsEligible(launchState.ValueRO, health.ValueRO, punch)
                        && PunchResolution.Contains(state.EntityManager, enemy, pose.Position, punch))
                    {
                        punchRequest.HitEnemy = true;
                        if (!punchRequest.HitArmor && SystemAPI.HasComponent<EnemyArmor>(enemy))
                        {
                            punchRequest.HitArmor = true;
                            punchRequest.ArmorContact = pose.Position;
                        }
                        RecordContactApproach(state.EntityManager, enemy, pose, playerPosition,
                            ref punchRequest, ref closestApproach);
                    }
                    continue;
                }
                PunchSpecification targetPunch = punch;
                if (PunchResolution.IsEligible(launchState.ValueRO, health.ValueRO, punch)
                    && PunchResolution.Contains(state.EntityManager, enemy, transform.ValueRO.Position, punch))
                {
                    bool committed = PunchAimAssist.TryGetCommittedDirection(state.EntityManager, enemy,
                        punchRequest.AimRevision, out float3 assistedDirection, out Entity committedTarget);
                    if (committed || PunchAimAssist.TryGetLockedDirection(
                            state.EntityManager, enemy, transform.ValueRO.Position, out assistedDirection))
                    {
                        targetPunch.AssistedLaunchDirection = assistedDirection;
                        targetPunch.AssistedLaunchTarget = committed ? committedTarget
                            : SystemAPI.GetComponent<PunchAimAssistTarget>(enemy).Target;
                        targetPunch.HasAssistedLaunchDirection = 1;
                        targetPunch.CommitDisplayedPlan = committed ? (byte)1 : (byte)0;
                    }
                }
                if (tier.ValueRO.Value == EnemyCombatTier.Elite)
                    targetPunch.Strength *= math.max(0f, punchRequest.EliteKnockbackMultiplier);
                if (PunchResolution.TryApply(state.EntityManager, enemy, targetPunch))
                {
                    punchRequest.HitEnemy = true;
                    RecordContactApproach(state.EntityManager, enemy, pose, playerPosition,
                        ref punchRequest, ref closestApproach);
                }
            }

            foreach (var (wall, transform, target) in SystemAPI.Query<RefRO<Barricade>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<TrackObject>(target)) continue;
                if (!BarricadeHitResolution.ContainsPunch(wall.ValueRO, transform.ValueRO, punch)) continue;
                punchRequest.HitEnemy = true;
                if (SystemAPI.HasComponent<ShellTarget>(target))
                    ShellHitResolution.Punch(state.EntityManager, target, punch.Damage, SystemAPI.Time.ElapsedTime,
                        BarricadeHitResolution.ClosestPoint(wall.ValueRO, transform.ValueRO, punch.Origin));
            }

            foreach (var (projectile, pose, shot) in SystemAPI.Query<RefRW<ChickenProjectile>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!PunchResolution.Contains(pose.ValueRO.Position, punch)
                    || !SystemAPI.HasComponent<ChickenTuning>(projectile.ValueRO.Boss)
                    || SystemAPI.GetComponent<ChickenBoss>(projectile.ValueRO.Boss).Phase == ChickenPhase.Defeated) continue;
                ChickenProjectilePunch.Redirect(state.EntityManager, shot, ref projectile.ValueRW, pose.ValueRO.Position,
                    punch, SystemAPI.GetComponent<ChickenTuning>(projectile.ValueRO.Boss));
                punchRequest.HitEnemy = true;
            }
            punchRequest.IsResolved = true;
            SystemAPI.SetComponent(punchEntity, punchRequest);
            SystemAPI.SetComponentEnabled<PunchRequest>(punchEntity, false);
        }

        // Keep collider distance dispatch out of the Burst-compiled update's stack frame.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecordContactApproach(EntityManager manager, Entity enemy, in LocalTransform pose,
            float3 playerPosition, ref PunchRequest request, ref float closestApproach)
        {
            if (!request.HasContactPose || closestApproach == 0f
                || !manager.HasComponent<PhysicsCollider>(enemy))
                return;

            var collider = manager.GetComponentData<PhysicsCollider>(enemy).Value;
            if (!collider.IsCreated) return;
            var body = new RigidBody
            {
                Collider = collider,
                WorldFromBody = new RigidTransform(pose.Rotation, pose.Position),
                Scale = pose.Scale
            };
            float3 displacement = ContactApproach(body, playerPosition, request.ContactOffset);
            float distance = math.lengthsq(displacement);
            // A multi-hit punch only needs one visible contact. Prefer the smallest move.
            if (distance >= closestApproach) return;
            closestApproach = distance;
            request.ApproachDisplacement = displacement;
        }

        internal static float3 ContactApproach(in RigidBody body, float3 playerPosition, float3 contactOffset)
        {
            const float knuckleReach = .1f;
            float3 center = body.CalculateAabb().Center;
            float3 fist = playerPosition + contactOffset;
            // Match ground-plane spacing even for enemies whose bodies sit below the fist.
            fist.y = center.y;
            float3 towardEnemy = center - fist;
            float maxDistance = math.length(towardEnemy);
            if (!body.CalculateDistance(new PointDistanceInput
                {
                    Position = fist, MaxDistance = maxDistance + knuckleReach, Filter = CollisionFilter.Default
                }, out DistanceHit hit) || hit.Distance <= 0f)
                return float3.zero;

            float3 gap = hit.Position - fist;
            gap.y = 0f;
            // The fist can pass a very close enemy. Never pull the player back toward it.
            if (math.dot(gap.xz, (center - playerPosition).xz) <= 0f) return float3.zero;
            float distance = math.length(gap);
            return math.normalizesafe(gap) * math.max(0f, distance - knuckleReach);
        }
    }
}
