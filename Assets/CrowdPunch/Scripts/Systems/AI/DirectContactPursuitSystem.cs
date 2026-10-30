using System;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace CrowdPunch.Systems.AI
{
    /// <summary>COMBAT-016: periodically samples a contact-pursuer count and maintains that eligible subset.</summary>
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyCrowdTacticsSystem)), UpdateBefore(typeof(EnemySurroundSystem))]
    [UpdateBefore(typeof(EnemyChaseSystem))]
    public partial struct DirectContactPursuitSystem : ISystem
    {
        private Entity settingsOwner;
        private int2 countRange;
        private float period;
        private double nextSampleAt;
        private int targetCount;
        private bool sampled;
        private Random random;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.DirectContactPursuit");

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<EnemyCrowdPressureSettings>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var settings = SystemAPI.GetSingleton<EnemyCrowdPressureSettings>();
            var owner = SystemAPI.GetSingletonEntity<EnemyCrowdPressureSettings>();
            int2 range = new int2(math.max(0, math.min(settings.MinimumDirectContactPursuers, settings.MaximumDirectContactPursuers)),
                math.max(0, math.max(settings.MinimumDirectContactPursuers, settings.MaximumDirectContactPursuers)));
            float interval = math.max(.1f, settings.DirectContactPursuerResampleSeconds);
            if (owner != settingsOwner)
            {
                random = new Random(math.max(1u, math.hash(new uint2((uint)owner.Index, (uint)owner.Version + 927u))));
                sampled = false;
                settingsOwner = owner;
            }
            if (math.any(range != countRange) || interval != period) sampled = false;
            countRange = range; period = interval;
            bool suspended = !player.IsAvailable || SystemAPI.HasSingleton<ProtectedPoint>() || range.y == 0;
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            int selected = 0;
            Candidate farthestSelected = default, nearestUnselected = default;
            bool hasUnselected = false;
            foreach (var (flag, pose, archetype, launch, entity) in
                SystemAPI.Query<EnabledRefRW<DirectContactPursuit>, RefRO<LocalTransform>, RefRO<EnemyArchetype>, RefRO<EnemyLaunchState>>()
                    .WithAll<Enemy>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                bool eligible = !suspended && archetype.ValueRO.Value == EnemyArchetypeKind.Baseline
                    && launch.ValueRO.Phase == EnemyLaunchPhase.Active
                    && !SystemAPI.IsComponentEnabled<RespawnRequest>(entity)
                    && !SystemAPI.HasComponent<SnakeHead>(entity) && !SystemAPI.HasComponent<SnakeSegment>(entity)
                    && (!BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room)
                        || BarricadeRoomMovement.Contains(room, player.Position));
                if (SystemAPI.HasComponent<PillarCrowdMember>(entity))
                {
                    var member = SystemAPI.GetComponent<PillarCrowdMember>(entity);
                    if (SystemAPI.HasComponent<FallingPillar>(member.Pillar))
                    {
                        var pillar = SystemAPI.GetComponent<FallingPillar>(member.Pillar);
                        if (SystemAPI.HasComponent<DinoBoss>(pillar.Boss)
                            && Initialization.PillarCrowdPlacement.Needed(pillar, SystemAPI.GetComponent<DinoBoss>(pillar.Boss),
                                SystemAPI.GetComponent<DinoTuning>(pillar.Boss))) eligible = false;
                    }
                }
                if (!eligible) { flag.ValueRW = false; continue; }
                bool previous = flag.ValueRO;
                var candidate = new Candidate { Enemy = entity,
                    DistanceSq = math.distancesq(pose.ValueRO.Position.xz, player.Position.xz) };
                if (previous)
                {
                    if (selected == 0 || candidate.CompareTo(farthestSelected) > 0) farthestSelected = candidate;
                    selected++;
                }
                else
                {
                    if (!hasUnselected || candidate.CompareTo(nearestUnselected) < 0) nearestUnselected = candidate;
                    hasUnselected = true;
                }
                candidates.Add(candidate);
            }
            if (suspended || candidates.Length == 0) { sampled = false; return; }
            double now = SystemAPI.Time.ElapsedTime;
            if (!sampled || now >= nextSampleAt)
            {
                // UInt upper bound avoids overflow for an inclusive int.MaxValue endpoint.
                targetCount = (int)random.NextUInt((uint)range.x, (uint)range.y + 1u);
                nextSampleAt = now + interval;
                sampled = true;
            }
            int desired = math.min(targetCount, candidates.Length);
            // The count stays sampled, but membership follows distance every step.
            // Sorting is unnecessary while the current subset is already the closest one.
            if (selected == desired && (desired == 0 || !hasUnselected
                || farthestSelected.CompareTo(nearestUnselected) <= 0)) return;
            candidates.AsArray().Sort();
            var flags = SystemAPI.GetComponentLookup<DirectContactPursuit>();
            for (int index = 0; index < candidates.Length; index++)
            {
                var enemy = candidates[index].Enemy;
                bool enable = index < desired;
                if (flags.IsComponentEnabled(enemy) != enable) flags.SetComponentEnabled(enemy, enable);
            }
        }

        private struct Candidate : IComparable<Candidate>
        {
            public Entity Enemy;
            public float DistanceSq;
            public int CompareTo(Candidate other)
            {
                int distance = DistanceSq.CompareTo(other.DistanceSq);
                return distance != 0 ? distance : Enemy.Index.CompareTo(other.Enemy.Index);
            }
        }
    }
}
