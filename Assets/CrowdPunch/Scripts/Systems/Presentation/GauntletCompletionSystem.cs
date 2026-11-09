using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>Reports completion once when every wave sequence in the loaded gauntlet is complete.</summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class GauntletCompletionSystem : SystemBase
    {
        private EntityQuery allSequences;
        private EntityQuery completedSequences;
        private bool completionReported;
        private bool failureReported;
        private Entity trackedSubject;

        protected override void OnCreate()
        {
            allSequences = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EnemyWaveSequence>(),
                    ComponentType.ReadOnly<EnemyWaveEncounterComplete>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            completedSequences = GetEntityQuery(
                ComponentType.ReadOnly<EnemyWaveSequence>(),
                ComponentType.ReadOnly<EnemyWaveEncounterComplete>());
        }

        protected override void OnUpdate()
        {
            bool failed = SystemAPI.HasSingleton<ProtectedPoint>() && SystemAPI.GetSingleton<ProtectedPoint>().Failed;
            if (failed)
            {
                if (!failureReported) GauntletFailureRegistry.ReportFailure();
                failureReported = true;
                completionReported = false;
                return;
            }
            failureReported = false;
            int sequenceCount = allSequences.CalculateEntityCount();
            bool isComplete = sequenceCount > 0
                && completedSequences.CalculateEntityCount() == sequenceCount;
            Entity subject = Entity.Null;
            Vector3? focus = null;
            if (isComplete && !completionReported && SystemAPI.TryGetSingleton<LevelClearSubject>(out var enemy)
                && enemy.Entity != Entity.Null)
            {
                subject = enemy.Entity;
                focus = enemy.Position;
            }
            if (SystemAPI.TryGetSingleton<ProtectedPoint>(out var point))
            {
                float height = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground) ? ground.SurfaceHeight : 0f;
                subject = Entity.Null;
                focus = new Vector3(point.Center.x, height, point.Center.y);
            }
            // BOSS-007: head defeat is authoritative even if supporting waves are cleared or mis-signalled.
            foreach (var (boss, entity) in SystemAPI.Query<RefRO<BossEncounter>>().WithEntityAccess())
                SetBossResult(boss.ValueRO.Cycle == BossCycle.Defeated, entity);
            foreach (var (boss, entity) in SystemAPI.Query<RefRO<ChickenBoss>>().WithEntityAccess())
                SetBossResult(boss.ValueRO.Phase == ChickenPhase.Defeated, entity);
            foreach (var (boss, entity) in SystemAPI.Query<RefRO<RollingBoss>>().WithEntityAccess())
                SetBossResult(boss.ValueRO.Phase == RollingPhase.Defeated, entity);
            foreach (var (boss, entity) in SystemAPI.Query<RefRO<DinoBoss>>().WithEntityAccess())
                SetBossResult(boss.ValueRO.Phase == DinoPhase.Defeated, entity);

            // BARRICADE-001: surviving enemies are irrelevant; reaching the exposed exit is required.
            bool hasBarricade = false, allBarricadesComplete = true;
            double bestWall = double.NegativeInfinity;
            var player = SystemAPI.TryGetSingleton<PlayerSnapshot>(out var snapshot) ? snapshot : default;
            foreach (var (wall, entity) in SystemAPI.Query<RefRO<Barricade>>().WithEntityAccess())
            {
                hasBarricade = true;
                bool destruction = wall.ValueRO.CompleteOnDestruction != 0;
                float exitDistance = math.distancesq(player.Position.xz, wall.ValueRO.ExitPosition.xz);
                bool cleared = wall.ValueRO.HitsRemaining == 0 && (destruction || player.IsAvailable
                    && exitDistance <= wall.ValueRO.ExitRadius * wall.ValueRO.ExitRadius);
                allBarricadesComplete &= cleared;
                double priority = destruction ? wall.ValueRO.LastHitTime : -exitDistance;
                if (!cleared || priority <= bestWall) continue;
                bestWall = priority;
                subject = destruction ? entity : Entity.Null;
                focus = destruction ? wall.ValueRO.LastHitPosition : wall.ValueRO.ExitPosition;
            }
            if (hasBarricade) isComplete = allBarricadesComplete;
            foreach (var (boss, entity) in SystemAPI.Query<RefRO<FinaleBoss>>().WithEntityAccess())
                SetBossResult(boss.ValueRO.Phase == FinalePhase.Defeated && boss.ValueRO.Remaining <= 0, entity);

            if (isComplete && !completionReported)
            {
                completionReported = true;
                trackedSubject = subject;
                SystemAPI.TryGetSingleton<LevelClearScore>(out var score);
                GauntletCompletionRegistry.ReportCompletion(score,
                    TryGetSubjectPosition(subject, out var position) ? position : focus);
            }
            else if (isComplete && TryGetSubjectPosition(trackedSubject, out var position))
            {
                GauntletCompletionRegistry.UpdateFocus(position);
            }
            else if (!isComplete)
            {
                completionReported = false;
            }

            void SetBossResult(bool defeated, Entity entity)
            {
                isComplete = defeated;
                subject = entity;
                focus = null;
            }
        }

        private bool TryGetSubjectPosition(Entity entity, out Vector3 position)
        {
            position = default;
            if (entity == Entity.Null || !EntityManager.HasComponent<LocalTransform>(entity)
                || EntityManager.HasComponent<Disabled>(entity)
                || EntityManager.HasComponent<RespawnRequest>(entity)
                    && EntityManager.GetComponentData<RespawnRequest>(entity).IsPooled != 0)
                return false;
            float3 worldPosition = EntityManager.GetComponentData<LocalTransform>(entity).Position;
            position = worldPosition;
            return math.all(math.isfinite(worldPosition));
        }
    }
}
