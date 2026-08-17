using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Resolves swept Dasher overlaps without using solver contacts against ordinary enemies.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct DasherEnemyImpactSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityQuery targetQuery = SystemAPI.QueryBuilder()
                .WithAll<Enemy, LocalTransform, EnemyContactDamageSettings, EnemyLaunchState, KnockbackResponse>()
                .WithNone<RespawnRequest>().Build();
            NativeArray<Entity> targets = targetQuery.ToEntityArray(Allocator.Temp);
            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
            ComponentLookup<EnemyContactDamageSettings> radii = SystemAPI.GetComponentLookup<EnemyContactDamageSettings>(true);
            ComponentLookup<KnockbackResponse> tiers = SystemAPI.GetComponentLookup<KnockbackResponse>(true);
            ComponentLookup<EnemyLaunchState> launches = SystemAPI.GetComponentLookup<EnemyLaunchState>();
            ComponentLookup<EnemyTier> enemyTiers = SystemAPI.GetComponentLookup<EnemyTier>(true);
            ComponentLookup<DamageRequest> damageRequests = SystemAPI.GetComponentLookup<DamageRequest>();
            ComponentLookup<ExternalImpulse> impulses = SystemAPI.GetComponentLookup<ExternalImpulse>();
            ComponentLookup<ExplosiveEnemyState> explosiveStates = SystemAPI.GetComponentLookup<ExplosiveEnemyState>(true);
            ComponentLookup<ExplosiveDetonationRequest> detonationRequests =
                SystemAPI.GetComponentLookup<ExplosiveDetonationRequest>();

            foreach ((RefRO<DasherSettings> settings, RefRO<DasherState> dash,
                         RefRO<EnemyLaunchState> sourceLaunch, RefRO<LocalTransform> sourceTransform,
                         RefRO<EnemyContactDamageSettings> sourceRadius, DynamicBuffer<DasherHitHistory> history,
                         Entity source) in
                     SystemAPI.Query<RefRO<DasherSettings>, RefRO<DasherState>, RefRO<EnemyLaunchState>,
                             RefRO<LocalTransform>, RefRO<EnemyContactDamageSettings>, DynamicBuffer<DasherHitHistory>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (sourceLaunch.ValueRO.Phase != EnemyLaunchPhase.Launched) continue;

                float3 start = dash.ValueRO.PreviousPosition;
                float3 end = sourceTransform.ValueRO.Position;
                start.y = end.y;
                for (int i = 0; i < targets.Length; i++)
                {
                    Entity target = targets[i];
                    if (target == source) continue;
                    float combinedRadius = math.max(0f, sourceRadius.ValueRO.ContactRadius)
                        + math.max(0f, radii[target].ContactRadius);
                    if (DistanceSqToSegment(transforms[target].Position, start, end) > combinedRadius * combinedRadius) continue;
                    ResolveImpact(source, target, sourceTransform.ValueRO.Position,
                        transforms[target].Position, dash.ValueRO, settings.ValueRO, history,
                        ref launches, ref tiers, ref enemyTiers, ref damageRequests, ref impulses,
                        ref explosiveStates, ref detonationRequests);
                }
            }
            targets.Dispose();
        }

        private static void ResolveImpact(Entity source, Entity target,
            float3 sourcePosition, float3 targetPosition, DasherState dash,
            DasherSettings settings, DynamicBuffer<DasherHitHistory> history,
            ref ComponentLookup<EnemyLaunchState> launches, ref ComponentLookup<KnockbackResponse> tiers,
            ref ComponentLookup<EnemyTier> enemyTiers,
            ref ComponentLookup<DamageRequest> damageRequests, ref ComponentLookup<ExternalImpulse> impulses,
            ref ComponentLookup<ExplosiveEnemyState> explosiveStates,
            ref ComponentLookup<ExplosiveDetonationRequest> detonationRequests)
        {
            EnemyLaunchState targetLaunch = launches[target];
            if (targetLaunch.Phase != EnemyLaunchPhase.Active && targetLaunch.Phase != EnemyLaunchPhase.Recovering) return;
            uint sequence = launches[source].LaunchSequence;
            const byte actionKind = 1;
            for (int i = 0; i < history.Length; i++)
                if (history[i].Target == target && history[i].Sequence == sequence && history[i].ActionKind == actionKind) return;
            history.Add(new DasherHitHistory { Target = target, Sequence = sequence, ActionKind = actionKind });

            if (explosiveStates.HasComponent(target)
                && explosiveStates[target].HasExploded == 0
                && detonationRequests.HasComponent(target))
            {
                detonationRequests.SetComponentEnabled(target, true);
            }

            KnockbackResponseTier tier = tiers[target].Tier;
            float damage = tier == KnockbackResponseTier.Boss ? settings.BossDamage
                : tier == KnockbackResponseTier.PlayerElite ? settings.EliteDamage
                : settings.LaunchedEnemyDamage;
            float knockback = tier == KnockbackResponseTier.Boss ? settings.BossKnockback
                : tier == KnockbackResponseTier.PlayerElite ? settings.EliteKnockback
                : settings.LaunchedEnemyKnockback;

            if (enemyTiers.HasComponent(target) && EnemyLaunchTransition.IsLaunchable(enemyTiers[target]))
            {
                EnemyLaunchTransition.Begin(ref targetLaunch, EnemyLaunchCause.EnemyCollision, damage);
                launches[target] = targetLaunch;
            }
            if (damage > 0f)
            {
                DamageRequest pending = damageRequests.IsComponentEnabled(target) ? damageRequests[target] : default;
                pending.Amount += damage;
                damageRequests[target] = pending;
                damageRequests.SetComponentEnabled(target, true);
            }
            if (knockback > 0f)
            {
                float3 travelDirection = math.normalizesafe(dash.PreservedLaunchedVelocity);
                travelDirection.y = 0f;
                travelDirection = math.normalizesafe(travelDirection, new float3(0f, 0f, 1f));
                float3 hitSideDirection = targetPosition - sourcePosition;
                hitSideDirection.y = 0f;
                hitSideDirection = math.normalizesafe(hitSideDirection, travelDirection);
                float positionWeight = math.saturate(settings.LaunchedImpactPositionWeight);
                float3 direction = math.normalizesafe(
                    math.lerp(travelDirection, hitSideDirection, positionWeight),
                    travelDirection);
                impulses[target] = new ExternalImpulse { Value = direction * knockback };
                impulses.SetComponentEnabled(target, true);
            }
        }

        private static float DistanceSqToSegment(float3 point, float3 start, float3 end)
        {
            point.y = start.y;
            float3 segment = end - start;
            float lengthSq = math.lengthsq(segment);
            float t = lengthSq <= 0.0001f ? 0f : math.saturate(math.dot(point - start, segment) / lengthSq);
            return math.lengthsq(point - (start + segment * t));
        }
    }
}
