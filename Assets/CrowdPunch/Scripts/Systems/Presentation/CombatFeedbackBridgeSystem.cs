using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(PresentationBridgeSystem))]
    [UpdateBefore(typeof(EnemyImpactVisualSystem))]
    public partial struct CombatFeedbackBridgeSystem : ISystem
    {
        private uint restart;
        private NativeParallelHashMap<Entity, AABB> modelBounds;
        private NativeParallelHashMap<Entity, AABB> poseBounds;
        private EntityQuery visuals;
        public void OnCreate(ref SystemState state)
        {
            modelBounds = new NativeParallelHashMap<Entity, AABB>(256, Allocator.Persistent);
            poseBounds = new NativeParallelHashMap<Entity, AABB>(256, Allocator.Persistent);
            visuals = state.GetEntityQuery(ComponentType.ReadOnly<EnemyVisualOwner>(),
                ComponentType.ReadOnly<RenderBounds>(), ComponentType.ReadOnly<LocalToWorld>());
        }
        public void OnDestroy(ref SystemState state) { modelBounds.Dispose(); poseBounds.Dispose(); }
        public void OnUpdate(ref SystemState state)
        {
            bool available = PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)
                && bridge.FeedbackSettings != null;
            var settings = available ? bridge.FeedbackSettings : null;
            bool reset = restart != GameRestartRegistry.Sequence || !available || !bridge.gameObject.activeInHierarchy
                || FeedbackTimeController.IsSuspended;
            restart = GameRestartRegistry.Sequence;
            modelBounds.Clear();
            poseBounds.Clear();
            if (!reset)
            {
                int count = visuals.CalculateEntityCount();
                if (modelBounds.Capacity < count) modelBounds.Capacity = math.ceilpow2(count);
                if (poseBounds.Capacity < count) poseBounds.Capacity = math.ceilpow2(count);
                foreach (var (owner, bounds, world) in SystemAPI.Query<RefRO<EnemyVisualOwner>, RefRO<RenderBounds>, RefRO<LocalToWorld>>()
                    .WithNone<DisableRendering>())
                {
                    var local = bounds.ValueRO.Value;
                    var matrix = world.ValueRO.Value;
                    float3 centre = math.transform(matrix, local.Center);
                    float3 extents = math.abs(matrix.c0.xyz) * local.Extents.x
                        + math.abs(matrix.c1.xyz) * local.Extents.y + math.abs(matrix.c2.xyz) * local.Extents.z;
                    float3 min = centre - extents, max = centre + extents;
                    if (modelBounds.TryGetValue(owner.ValueRO.Value, out var previous))
                    {
                        min = math.min(min, previous.Center - previous.Extents);
                        max = math.max(max, previous.Center + previous.Extents);
                    }
                    modelBounds[owner.ValueRO.Value] = new AABB { Center = (min + max) * .5f, Extents = (max - min) * .5f };
                }
                foreach (var visual in SystemAPI.Query<RefRO<EnemyTrailVisual>>())
                {
                    var v = visual.ValueRO;
                    if (math.cmax(v.Size) <= .001f || !SystemAPI.HasComponent<PhysicsVelocity>(v.Owner)) continue;
                    float3 forward = math.normalizesafe(SystemAPI.GetComponent<PhysicsVelocity>(v.Owner).Linear * new float3(1, 0, 1), new float3(0, 0, 1));
                    float3 right = new float3(forward.z, 0, -forward.x);
                    float3 centre = new float3(math.dot(v.Center, right), v.Center.y, math.dot(v.Center, forward));
                    float3 min = centre - v.Size * .5f, max = centre + v.Size * .5f;
                    if (poseBounds.TryGetValue(v.Owner, out var previous))
                    {
                        min = math.min(min, previous.Center - previous.Extents);
                        max = math.max(max, previous.Center + previous.Extents);
                    }
                    poseBounds[v.Owner] = new AABB { Center = (min + max) * .5f, Extents = (max - min) * .5f };
                }
            }
            float dt = UnityEngine.Time.unscaledDeltaTime;
            int emitted = 0;
            int trails = 0;
            bridge?.BeginTrails();
            foreach (var (feedback, launch, transform, velocity, entity) in
                     SystemAPI.Query<RefRW<EnemyImpactFeedback>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>()
                         .WithEntityAccess())
            {
                bool pooled = SystemAPI.HasComponent<RespawnRequest>(entity)
                    && SystemAPI.IsComponentEnabled<RespawnRequest>(entity);
                if (reset || pooled)
                {
                    feedback.ValueRW = default;
                    continue;
                }
                ref var f = ref feedback.ValueRW;
                f.VisualRemaining = math.max(0f, f.VisualRemaining - dt);
                f.FlashRemaining = math.max(0f, f.FlashRemaining - dt);
                if (f.Pending != 0)
                {
                    f.Pending = 0;
                    bool punch = f.Kind == CombatImpactKind.Punch;
                    bool environment = f.Kind == CombatImpactKind.Environment;
                    float minimum = environment ? settings.EnvironmentMinimumSpeed : settings.MinimumCollisionSpeed;
                    if (punch || f.Speed >= minimum && f.Impulse >= settings.MinimumCollisionImpulse)
                    {
                        f.NextContactTime = SystemAPI.Time.ElapsedTime + settings.ContactFeedbackInterval;
                        float intensity = math.saturate(settings.SingleImpactIntensity * (punch ? math.max(.4f, settings.Intensity(f.Speed)) : settings.Intensity(f.Speed))
                            * settings.ChainMultiplier(f.PlayerOwned != 0 ? f.ChainDepth : 0));
                        f.Intensity = intensity;
                        f.VisualDirection = f.Direction;
                        f.Squash = settings.SquashAmount;
                        f.Flash = settings.ReduceFlashes ? 0f : settings.EnemyFlashIntensity;
                        f.VisualDuration = settings.SquashDuration;
                        f.FlashDuration = settings.FlashDuration;
                        f.VisualRemaining = intensity >= settings.DeformationThreshold ? f.VisualDuration : 0f;
                        f.FlashRemaining = intensity >= settings.DeformationThreshold ? f.FlashDuration : 0f;
                        if (emitted++ < settings.MaximumImpactsPerFrame)
                            bridge.ReceiveImpact(new CombatFeedbackMessage { Kind = f.Kind, Position = f.Position,
                                Direction = f.Direction, Intensity = intensity, ChainDepth = f.ChainDepth, PlayerOwned = f.PlayerOwned != 0 });
                    }
                }
                float speed = math.length(velocity.ValueRO.Linear);
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched && speed >= settings.TrailMinimumSpeed
                    && trails++ < settings.MaximumTrails)
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    float3 centre = transform.ValueRO.Position, size = new float3(1);
                    float3 forward = math.normalizesafe(velocity.ValueRO.Linear * new float3(1, 0, 1), new float3(0, 0, 1));
                    float3 right = new float3(forward.z, 0, -forward.x);
                    if (poseBounds.TryGetValue(entity, out var pose))
                    {
                        centre = right * pose.Center.x + new float3(0, pose.Center.y, 0) + forward * pose.Center.z;
                        size = pose.Extents * 2;
                    }
                    else if (modelBounds.TryGetValue(entity, out var bounds))
                    {
                        centre = bounds.Center;
                        size = new float3(math.dot(math.abs(right), bounds.Extents), bounds.Extents.y,
                            math.dot(math.abs(forward), bounds.Extents)) * 2;
                    }
                    bridge.ReceiveTrail(id, launch.ValueRO.LaunchSequence, centre, speed,
                        launch.ValueRO.Owner == EnemyLaunchOwner.Player ? launch.ValueRO.FeedbackChainDepth : 0, size);
                }
            }
            bridge?.EndTrails();
        }
    }
}
