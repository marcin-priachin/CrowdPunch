using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>
    /// Produces enemy movement intent from player position.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    public partial struct EnemyChaseSystem : ISystem
    {
        public static float3 GetArenaRelativeSurroundTargetForTests(
            int entityIndex,
            float3 playerPosition,
            float y,
            EnemyMovementSettings movementSettings,
            ArenaBounds arenaBounds)
        {
            return EnemyChaseJob.GetArenaRelativeSurroundTarget(
                entityIndex,
                playerPosition,
                y,
                movementSettings,
                arenaBounds);
        }

        public static float3 GetArenaDistributionTargetForTests(
            int entityIndex,
            float y,
            EnemyMovementSettings movementSettings,
            ArenaBounds arenaBounds)
        {
            return EnemyChaseJob.GetArenaDistributionTarget(
                entityIndex,
                y,
                movementSettings,
                arenaBounds);
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<EnemyCrowdPressureSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot playerSnapshot = SystemAPI.GetSingleton<PlayerSnapshot>();
            ArenaBounds arenaBounds = SystemAPI.GetSingleton<ArenaBounds>();
            EnemyCrowdPressureSettings pressureSettings = SystemAPI.GetSingleton<EnemyCrowdPressureSettings>();
            NativeList<EnemySeparationNeighbor> activeEnemies = new NativeList<EnemySeparationNeighbor>(Allocator.TempJob);
            NativeList<PressureCandidate> pressureCandidates = new NativeList<PressureCandidate>(Allocator.TempJob);

            foreach ((RefRO<LocalTransform> transform, EnabledRefRO<RespawnRequest> respawnRequest,
                         RefRO<EnemyLaunchState> launchState, RefRO<EnemyArchetype> archetype, Entity entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, EnabledRefRO<RespawnRequest>, RefRO<EnemyLaunchState>, RefRO<EnemyArchetype>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                if (!respawnRequest.ValueRO && launchState.ValueRO.Phase == EnemyLaunchPhase.Active)
                {
                    activeEnemies.Add(new EnemySeparationNeighbor
                    {
                        Position = transform.ValueRO.Position,
                        Archetype = archetype.ValueRO.Value
                    });

                    if (playerSnapshot.IsAvailable && IsOrdinaryMelee(archetype.ValueRO.Value))
                    {
                        float3 toPlayer = transform.ValueRO.Position - playerSnapshot.Position;
                        toPlayer.y = 0f;
                        InsertClosestCandidate(
                            pressureCandidates,
                            math.max(0, pressureSettings.MaximumApproachingEnemies),
                            new PressureCandidate
                            {
                                Entity = entity,
                                DistanceSq = math.lengthsq(toPlayer)
                            });
                    }
                }
            }

            JobHandle chaseJob = new EnemyChaseJob
            {
                PlayerSnapshot = playerSnapshot,
                ArenaBounds = arenaBounds,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Enemies = activeEnemies.AsDeferredJobArray(),
                PressureEnemies = pressureCandidates.AsDeferredJobArray()
            }.ScheduleParallel(state.Dependency);

            JobHandle disposePressureCandidates = pressureCandidates.Dispose(chaseJob);
            state.Dependency = activeEnemies.Dispose(disposePressureCandidates);
        }

        private static bool IsOrdinaryMelee(EnemyArchetypeKind archetype)
        {
            return archetype == EnemyArchetypeKind.Baseline
                || archetype == EnemyArchetypeKind.Explosive;
        }

        private static void InsertClosestCandidate(
            NativeList<PressureCandidate> candidates,
            int maximumCount,
            PressureCandidate candidate)
        {
            if (maximumCount <= 0)
            {
                return;
            }

            if (candidates.Length < maximumCount)
            {
                candidates.Add(candidate);
                return;
            }

            int farthestIndex = 0;
            for (int index = 1; index < candidates.Length; index++)
            {
                if (IsFarther(candidates[index], candidates[farthestIndex]))
                {
                    farthestIndex = index;
                }
            }

            if (IsCloser(candidate, candidates[farthestIndex]))
            {
                candidates[farthestIndex] = candidate;
            }
        }

        private static bool IsCloser(PressureCandidate candidate, PressureCandidate other)
        {
            return candidate.DistanceSq < other.DistanceSq
                || candidate.DistanceSq == other.DistanceSq && candidate.Entity.Index < other.Entity.Index;
        }

        private static bool IsFarther(PressureCandidate candidate, PressureCandidate other)
        {
            return candidate.DistanceSq > other.DistanceSq
                || candidate.DistanceSq == other.DistanceSq && candidate.Entity.Index > other.Entity.Index;
        }

        private struct PressureCandidate
        {
            public Entity Entity;
            public float DistanceSq;
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct EnemyChaseJob : IJobEntity
        {
            public PlayerSnapshot PlayerSnapshot;
            public ArenaBounds ArenaBounds;
            public float DeltaTime;
            [ReadOnly] public NativeArray<EnemySeparationNeighbor> Enemies;
            [ReadOnly] public NativeArray<PressureCandidate> PressureEnemies;

            private void Execute(
                Entity entity,
                ref DesiredMovement desiredMovement,
                ref WanderDestination wanderDestination,
                ref EnemyContactAttemptState contactAttempt,
                in LocalTransform transform,
                in EnemyMovementSettings movementSettings,
                in EnemyContactDamageSettings contactSettings,
                in EnemySeparationDistance separationDistance,
                in EnemyArchetypeSeparationDistances archetypeSeparationDistances,
                in EnemyArchetype archetype,
                in EnemyLaunchState launchState)
            {
                if (launchState.Phase != EnemyLaunchPhase.Active)
                {
                    desiredMovement = default;
                    return;
                }

                if (!PlayerSnapshot.IsAvailable)
                {
                    desiredMovement.Direction = float3.zero;
                    desiredMovement.Speed = 0f;
                    return;
                }

                float3 toPlayer = PlayerSnapshot.Position - transform.Position;
                toPlayer.y = 0f;

                float distanceToPlayer = math.length(toPlayer);
                float3 separation = GetSeparation(
                    transform.Position,
                    separationDistance.Value,
                    movementSettings.SeparationWeight,
                    archetypeSeparationDistances);

                bool explosiveInContactRange = archetype.Value == EnemyArchetypeKind.Explosive
                    && distanceToPlayer <= math.max(0f, contactSettings.AttemptDistance);
                if (IsPressureEnemy(entity) || explosiveInContactRange)
                {
                    UpdateContactAttempt(entity, distanceToPlayer, contactSettings, ref contactAttempt);
                    float3 target = explosiveInContactRange || contactAttempt.IsAttempting != 0
                        ? new float3(PlayerSnapshot.Position.x, transform.Position.y, PlayerSnapshot.Position.z)
                        : GetArenaRelativeSurroundTarget(
                            entity.Index,
                            PlayerSnapshot.Position,
                            transform.Position.y,
                            movementSettings,
                            ArenaBounds);
                    float3 toTarget = target - transform.Position;
                    toTarget.y = 0f;

                    float targetDistance = math.length(toTarget);
                    float3 targetDirection = targetDistance <= movementSettings.StoppingDistance
                        ? float3.zero
                        : toTarget / math.max(0.0001f, targetDistance);

                    float3 appliedSeparation = explosiveInContactRange
                        ? float3.zero
                        : contactAttempt.IsAttempting != 0
                        ? math.normalizesafe(separation) * math.max(0f, contactSettings.AttemptSeparationWeight)
                        : separation;
                    desiredMovement.Direction = math.normalizesafe(
                        targetDirection + appliedSeparation,
                        appliedSeparation);
                    desiredMovement.Speed = desiredMovement.Direction.Equals(float3.zero)
                        ? 0f
                        : movementSettings.MoveSpeed
                            * (distanceToPlayer <= movementSettings.ChargeDistance
                                ? movementSettings.ChargeSpeedMultiplier
                                : 1f)
                            * (contactAttempt.IsAttempting != 0 ? math.max(0f, contactSettings.AttemptSpeedMultiplier) : 1f);
                    return;
                }

                contactAttempt.IsAttempting = 0;

                float3 distributionDirection = GetDistributionDirection(
                    entity,
                    transform.Position,
                    movementSettings,
                    ref wanderDestination,
                    out float distributionDistance,
                    out float arrivalDistance);
                float separationBlend = GetDistributionSeparationBlend(
                    distributionDistance,
                    arrivalDistance);
                desiredMovement.Direction = BlendWithSeparation(
                    distributionDirection,
                    separation * separationBlend);
                float returnSpeedDistance = math.max(
                    arrivalDistance * 2f,
                    separationDistance.Value);
                desiredMovement.Speed = desiredMovement.Direction.Equals(float3.zero)
                    ? 0f
                    : distributionDistance > returnSpeedDistance
                        ? movementSettings.MoveSpeed
                        : movementSettings.WanderSpeed;
            }

            private bool IsPressureEnemy(Entity entity)
            {
                for (int index = 0; index < PressureEnemies.Length; index++)
                {
                    if (PressureEnemies[index].Entity == entity)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static float3 BlendWithSeparation(
                float3 movementDirection,
                float3 separation)
            {
                return math.normalizesafe(
                    movementDirection + separation,
                    separation);
            }

            private static float GetDistributionSeparationBlend(float distance, float arrivalDistance)
            {
                const float minimumBlend = 0.25f;
                float fullBlendDistance = math.max(0.0001f, arrivalDistance * 2f);
                return math.lerp(
                    minimumBlend,
                    1f,
                    math.saturate(1f - distance / fullBlendDistance));
            }

            private void UpdateContactAttempt(
                Entity entity,
                float distanceToPlayer,
                EnemyContactDamageSettings settings,
                ref EnemyContactAttemptState state)
            {
                if (state.IsAttempting == 0 && distanceToPlayer > math.max(0f, settings.AttemptDistance))
                {
                    return;
                }

                state.SecondsRemaining -= math.max(0f, DeltaTime);
                if (state.SecondsRemaining > 0f)
                {
                    return;
                }

                if (state.IsAttempting != 0)
                {
                    state.IsAttempting = 0;
                    state.Sequence++;
                    state.SecondsRemaining = GetAttemptInterval(entity, state.Sequence, settings);
                    return;
                }

                state.IsAttempting = 1;
                state.SecondsRemaining = math.max(0f, settings.AttemptDuration);
            }

            private static float GetAttemptInterval(Entity entity, uint sequence, EnemyContactDamageSettings settings)
            {
                float minimum = math.max(0f, math.min(settings.AttemptIntervalMin, settings.AttemptIntervalMax));
                float maximum = math.max(minimum, math.max(settings.AttemptIntervalMin, settings.AttemptIntervalMax));
                uint hash = math.hash(new uint3((uint)math.max(1, entity.Index + 1), (uint)math.max(1, entity.Version + 1), sequence + 1u));
                float normalized = (hash & 0x00ffffffu) / 16777216f;
                return math.lerp(minimum, maximum, normalized);
            }

            internal static float3 GetArenaRelativeSurroundTarget(
                int entityIndex,
                float3 playerPosition,
                float y,
                EnemyMovementSettings movementSettings,
                ArenaBounds arenaBounds)
            {
                const float goldenAngle = 2.3999631f;
                const int candidateCount = 8;

                int slotIndex = math.max(0, entityIndex);
                int ringIndex = slotIndex % 3;
                float radius = math.max(0f, movementSettings.SurroundDistance)
                    + ringIndex * math.max(0f, movementSettings.SurroundRingSpacing);
                float2 center = arenaBounds.Center.xz;
                float2 extents = math.max(arenaBounds.Extents.xz, new float2(0f));
                float margin = math.min(
                    math.max(0.5f, movementSettings.StoppingDistance),
                    math.cmin(extents));
                float2 minimum = center - extents + margin;
                float2 maximum = center + extents - margin;
                float2 playerXZ = math.clamp(playerPosition.xz, minimum, maximum);
                float2 bestPosition = playerXZ;
                float bestDistanceSq = -1f;

                // Preserve the normal golden-angle slot when it fits. Near an arena edge,
                // deterministic alternatives redistribute blocked slots into available space.
                for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    float angle = (slotIndex + candidateIndex) * goldenAngle;
                    float2 direction = new float2(math.cos(angle), math.sin(angle));
                    float2 candidate = math.clamp(playerXZ + direction * radius, minimum, maximum);
                    float distanceSq = math.distancesq(playerXZ, candidate);
                    if (distanceSq > bestDistanceSq)
                    {
                        bestPosition = candidate;
                        bestDistanceSq = distanceSq;
                    }

                    if (distanceSq >= radius * radius - 0.0001f)
                    {
                        break;
                    }
                }

                return new float3(
                    bestPosition.x,
                    y,
                    bestPosition.y);
            }

            private float3 GetSeparation(
                float3 position,
                float defaultDistance,
                float defaultWeight,
                EnemyArchetypeSeparationDistances archetypeDistances)
            {
                float3 separation = float3.zero;
                float strongestWeight = 0f;

                for (int index = 0; index < Enemies.Length; index++)
                {
                    float separationDistance = math.max(0f, archetypeDistances.GetDistance(
                        Enemies[index].Archetype,
                        defaultDistance));
                    float separationDistanceSq = separationDistance * separationDistance;
                    float separationWeight = math.max(0f, archetypeDistances.GetWeight(
                        Enemies[index].Archetype,
                        defaultWeight));
                    float3 away = position - Enemies[index].Position;
                    away.y = 0f;
                    float distanceSq = math.lengthsq(away);

                    if (distanceSq <= 0.0001f || distanceSq >= separationDistanceSq)
                    {
                        continue;
                    }

                    float distance = math.sqrt(distanceSq);
                    float strength = 1f - distance / separationDistance;
                    separation += away / math.max(0.0001f, distance) * strength * separationWeight;
                    strongestWeight = math.max(strongestWeight, separationWeight);
                }

                return math.normalizesafe(separation) * strongestWeight;
            }

            private float3 GetDistributionDirection(
                Entity entity,
                float3 position,
                EnemyMovementSettings movementSettings,
                ref WanderDestination wanderDestination,
                out float distance,
                out float arrivalDistance)
            {
                arrivalDistance = math.max(0.75f, movementSettings.WanderSpeed * 0.35f);

                if (wanderDestination.IsAssigned == 0)
                {
                    wanderDestination.Position = GetArenaDistributionTarget(
                        entity.Index,
                        position.y,
                        movementSettings,
                        ArenaBounds);
                    wanderDestination.IsAssigned = 1;
                }

                float3 toDestination = wanderDestination.Position - position;
                toDestination.y = 0f;
                distance = math.length(toDestination);

                if (distance <= arrivalDistance)
                {
                    return float3.zero;
                }

                return toDestination / math.max(0.0001f, distance);
            }

            internal static float3 GetArenaDistributionTarget(
                int entityIndex,
                float y,
                EnemyMovementSettings movementSettings,
                ArenaBounds arenaBounds)
            {
                float2 center = arenaBounds.Center.xz;
                float2 extents = math.max(arenaBounds.Extents.xz, new float2(0f));
                float margin = GetWanderMargin(extents, movementSettings);
                float2 usableExtents = math.max(extents - margin, new float2(0f));
                int sampleIndex = math.max(1, entityIndex + 1);
                float2 sample = new float2(Halton(sampleIndex, 2), Halton(sampleIndex, 3));
                float2 position = center + (sample * 2f - 1f) * usableExtents;

                return new float3(position.x, y, position.y);
            }

            private static float GetWanderMargin(float2 extents, EnemyMovementSettings movementSettings)
            {
                float acceleration = math.max(0.0001f, movementSettings.Acceleration);
                float stoppingDistance = movementSettings.WanderSpeed * movementSettings.WanderSpeed / (2f * acceleration);
                float desiredMargin = math.max(1f, stoppingDistance + 0.5f);
                float maxMargin = math.cmin(extents) * 0.45f;

                return math.min(desiredMargin, maxMargin);
            }

            private static float Halton(int index, int baseValue)
            {
                float result = 0f;
                float fraction = 1f;
                int remaining = math.max(1, index);

                while (remaining > 0)
                {
                    fraction /= baseValue;
                    result += fraction * (remaining % baseValue);
                    remaining /= baseValue;
                }

                return result;
            }
        }
    }
}
