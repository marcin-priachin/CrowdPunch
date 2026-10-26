using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Mono.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationGroundingSystem)), UpdateAfter(typeof(EnemyAnimationSystem))]
    [UpdateBefore(typeof(CombatFeedbackBridgeSystem))]
    public partial struct EnemyTrailBoundsSystem : ISystem
    {
        private NativeParallelHashSet<Entity> selected;
        public void OnCreate(ref SystemState state) => selected = new NativeParallelHashSet<Entity>(256, Allocator.Persistent);
        public void OnDestroy(ref SystemState state) => selected.Dispose();
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            selected.Clear();
            int capacity = PlayerBridgeRegistry.TryGetBridge(out var bridge) && bridge.FeedbackSettings != null
                && bridge.isActiveAndEnabled && !FeedbackTimeController.IsSuspended
                ? math.clamp(bridge.FeedbackSettings.MaximumTrails, 0, 256) : 0;
            float minimumSpeed = capacity > 0 ? bridge.FeedbackSettings.TrailMinimumSpeed : float.PositiveInfinity;
            int selectedCount = 0;
            foreach (var (feedback, launch, pose, velocity, entity) in SystemAPI.Query<RefRO<EnemyImpactFeedback>,
                RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>().WithEntityAccess())
            {
                if (selectedCount >= capacity) break;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched || math.length(velocity.ValueRO.Linear) < minimumSpeed
                    || SystemAPI.HasComponent<RespawnRequest>(entity) && SystemAPI.IsComponentEnabled<RespawnRequest>(entity)) continue;
                selected.Add(entity);
                selectedCount++;
            }
            // The bridge consumes these bounds immediately. Run the small capped job here
            // rather than waking workers and immediately waiting for them again.
            new PoseBoundsJob
            {
                Selected = selected,
                Roots = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true)
            }.Run();
        }

        [BurstCompile]
        private partial struct PoseBoundsJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashSet<Entity> Selected;
            [ReadOnly] public ComponentLookup<LocalToWorld> Roots;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            private void Execute(ref EnemyTrailVisual visual, in DynamicBuffer<SkinMatrix> skin)
            {
                visual.Size = 0;
                if (!Selected.Contains(visual.Owner) || !visual.Probes.IsCreated || !Roots.HasComponent(visual.Root)
                    || !Launches.HasComponent(visual.Owner) || Launches[visual.Owner].Phase != EnemyLaunchPhase.Launched
                    || !Velocities.HasComponent(visual.Owner) || skin.Length == 0) return;
                float3 forward = math.normalizesafe(Velocities[visual.Owner].Linear * new float3(1, 0, 1), new float3(0, 0, 1));
                float3 right = new float3(forward.z, 0, -forward.x);
                var world = Roots[visual.Root].Value;
                float3 min = float.PositiveInfinity, max = float.NegativeInfinity;
                ref var probes = ref visual.Probes.Value;
                for (int i = 0; i < probes.Vertices.Length; i++)
                {
                    var v = probes.Vertices[i];
                    float4 p = new float4(v.Position, 1);
                    float3 local = math.mul(skin[v.Bones.x].Value, p) * v.Weights.x
                        + math.mul(skin[v.Bones.y].Value, p) * v.Weights.y
                        + math.mul(skin[v.Bones.z].Value, p) * v.Weights.z
                        + math.mul(skin[v.Bones.w].Value, p) * v.Weights.w;
                    float3 position = math.transform(world, local);
                    float3 projected = new float3(math.dot(position, right), position.y, math.dot(position, forward));
                    min = math.min(min, projected); max = math.max(max, projected);
                }
                float3 centre = (min + max) * .5f;
                visual.Center = right * centre.x + new float3(0, centre.y, 0) + forward * centre.z;
                visual.Size = max - min;
            }
        }
    }
}
