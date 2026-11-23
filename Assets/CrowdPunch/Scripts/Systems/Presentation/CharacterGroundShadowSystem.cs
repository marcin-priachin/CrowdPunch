using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using PhysicsCollider = Unity.Physics.PhysicsCollider;

namespace CrowdPunch.Systems.Presentation
{
    // VISION-006: hard character shadows on the authored arena floor, including baked-only levels.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyTrailBoundsSystem))]
    [UpdateAfter(typeof(StandingTurnAnimationSystem))]
    [UpdateAfter(typeof(ChickenAnimationSystem))]
    [UpdateBefore(typeof(EnemyImpactVisualSystem))]
    public partial class CharacterGroundShadowSystem : SystemBase
    {
        private const int BatchSize = 128;
        private const float FloorOffset = .012f;
        private static readonly int StrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int TintId = Shader.PropertyToID("_ShadowTint");
        private static readonly ProfilerMarker DrawMarker = new("CrowdPunch.CharacterGroundShadows");

        private readonly Matrix4x4[] transforms = new Matrix4x4[BatchSize];
        private readonly float[] strengths = new float[BatchSize];
        private readonly Plane[] frustum = new Plane[6];
        private readonly Dictionary<Entity, float2> footRadii = new(128);
        private readonly Dictionary<Entity, float2> animatedCenters = new(16);
        private NativeHashMap<Entity, ProjectedFootprint> defeatedFootprints;
        private MaterialPropertyBlock properties;
        private Material material;
        private Mesh disc;
        private RenderParams renderParams;
        private EntityQuery bossBodies;
        private int instanceCount;

        protected override void OnCreate()
        {
            RequireForUpdate<ArenaGroundPlane>();
            bossBodies = SystemAPI.QueryBuilder().WithAll<LocalToWorld, LocalTransform, PhysicsCollider>()
                .WithAny<BossPart, ChickenBoss, DinoBoss, RollingBoss, FinaleBoss>().WithNone<Enemy>().Build();
            defeatedFootprints = new NativeHashMap<Entity, ProjectedFootprint>(16, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null || !SystemInfo.supportsInstancing) return;
            if (material == null) material = Resources.Load<Material>("CharacterGroundShadow");
            if (material == null) return;

            using var scope = DrawMarker.Auto();
            disc ??= CreateDisc();
            properties ??= new MaterialPropertyBlock();
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            instanceCount = 0;
            renderParams = new RenderParams(material)
            {
                camera = camera,
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };

            Color ambient = RenderSettings.ambientEquatorColor;
            float brightest = Mathf.Max(.001f, Mathf.Max(ambient.r, Mathf.Max(ambient.g, ambient.b)));
            properties.SetVector(TintId, new Vector4(.70f * (.25f + .14f * ambient.r / brightest),
                .80f * (.25f + .14f * ambient.g / brightest), .25f + .14f * ambient.b / brightest, 1f));

            float floor = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            CollectEnemyFootprints();
            CollectDefeatedFootprints();

            // Read the live bridge position so the shadow follows per-frame movement between fixed ticks.
            if (PlayerBridgeRegistry.TryGetBridge(out var player) && player.isActiveAndEnabled && player.Radius > 0f)
            {
                float2 facing = math.normalizesafe(player.Forward.xz, new float2(0, 1));
                float2 radii = player.Radius * new float2(1f, 1.25f);
                System.Span<float3> feet = stackalloc float3[4];
                int count = player.CopyShadowFeet(feet);
                for (int i = 0; i < count; i++)
                    FitFoot(ref radii, feet[i].xz - player.Position.xz, facing, .14f);
                AddShadow(player.Position, facing, radii, floor, .8f, 0f);
            }

            EntityManager.CompleteDependencyBeforeRO<RespawnRequest>();
            var respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true);
            foreach (var (pose, agent, ground, enemy) in
                     SystemAPI.Query<RefRO<LocalToWorld>, RefRO<NavigationAgent>, RefRO<EnemyGroundConstraint>>()
                         .WithAll<Enemy>().WithEntityAccess())
            {
                // A queued respawn leaves the enemy visible until it enters the pool.
                if (respawns.HasComponent(enemy) && respawns.IsComponentEnabled(enemy) && respawns[enemy].IsPooled != 0)
                    continue;

                float3 position = pose.ValueRO.Position;
                float lift = ground.ValueRO.HasGround != 0 ? position.y - ground.ValueRO.Height : 0f;
                float2 facing = GroundFacing(pose.ValueRO);
                float2 radii = math.max(.25f, agent.ValueRO.Radius) * new float2(.85f, 1.05f);
                if (defeatedFootprints.TryGetValue(enemy, out var bounds))
                {
                    float2 center = (bounds.Min + bounds.Max) * .5f;
                    position.xz += new float2(facing.y, -facing.x) * center.x + facing * center.y;
                    // The ellipse must enclose the corners of the projected pose bounds.
                    radii = math.max(radii, (bounds.Max - bounds.Min) * (.5f * math.SQRT2));
                }
                else
                {
                    if (animatedCenters.TryGetValue(enemy, out var center)) position.xz = center;
                    if (footRadii.TryGetValue(enemy, out var feet)) radii = math.max(radii, feet);
                }
                AddShadow(position, facing, radii, floor, .65f, lift);
            }

            if (!bossBodies.IsEmptyIgnoreFilter)
            {
                using var bosses = bossBodies.ToEntityArray(Allocator.Temp);
                foreach (Entity boss in bosses)
                {
                    var collider = EntityManager.GetComponentData<PhysicsCollider>(boss);
                    if (!collider.IsValid) continue;
                    var bounds = collider.Value.Value.CalculateAabb();
                    float scale = EntityManager.GetComponentData<LocalTransform>(boss).Scale;
                    var pose = EntityManager.GetComponentData<LocalToWorld>(boss);
                    float lift = pose.Position.y + bounds.Min.y * scale - floor;
                    float radius = math.cmax(bounds.Extents.xz) * .4f * scale;
                    float2 radii = radius * new float2(1f, 1.35f);
                    if (footRadii.TryGetValue(boss, out var feet)) radii = math.max(radii, feet);
                    AddShadow(pose.Position, GroundFacing(pose), radii, floor, .72f, lift);
                }
            }

            Flush();
        }

        private void CollectEnemyFootprints()
        {
            footRadii.Clear();
            animatedCenters.Clear();
            // The renderer reads finished poses on the main thread through random-access lookups.
            EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            EntityManager.CompleteDependencyBeforeRO<SkinMatrix>();
            var poses = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var skins = SystemAPI.GetBufferLookup<SkinMatrix>(true);
            foreach (var (footprint, animation, entity) in
                     SystemAPI.Query<RefRO<EnemyShadowFeet>, RefRO<EnemyAnimation>>()
                         .WithAll<SkinMatrix>().WithEntityAccess())
            {
                Entity owner = animation.ValueRO.Owner;
                if (!poses.TryGetComponent(owner, out var pose)
                    || !poses.TryGetComponent(footprint.ValueRO.Root, out var root)) continue;

                var skin = skins[entity];
                if (skin.Length == 0) continue;
                float2 facing = GroundFacing(pose);
                if (!footRadii.TryGetValue(owner, out var radii)) radii = new float2(.25f, .3f);
                foreach (var foot in footprint.ValueRO.Feet)
                {
                    float3 local = math.mul(skin[foot.Bone].Value, new float4(foot.BindPosition, 1f));
                    float2 offset = math.transform(root.Value, local).xz - pose.Position.xz;
                    FitFoot(ref radii, offset, facing, .12f);
                }
                footRadii[owner] = radii;
            }

            foreach (var (anchor, animation, entity) in
                     SystemAPI.Query<RefRO<EnemyShadowAnchor>, RefRO<EnemyAnimation>>()
                         .WithAll<SkinMatrix>().WithEntityAccess())
            {
                ref readonly var sample = ref anchor.ValueRO;
                if (!poses.TryGetComponent(sample.Root, out var root)) continue;
                var skin = skins[entity];
                if (sample.Bone >= skin.Length) continue;

                float3 local = math.mul(skin[sample.Bone].Value, new float4(sample.BindPosition, 1f));
                animatedCenters[animation.ValueRO.Owner] = math.transform(root.Value, local).xz;
            }
        }

        private void CollectDefeatedFootprints()
        {
            defeatedFootprints.Clear();
            EntityManager.CompleteDependencyBeforeRO<EnemyLaunchState>();
            EntityManager.CompleteDependencyBeforeRO<EnemyArmor>();
            // The draw consumes the result in the same frame.
            new DefeatedFootprintJob
            {
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Poses = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                ArmorVisuals = SystemAPI.GetComponentLookup<EnemyArmorVisual>(true),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Footprints = defeatedFootprints
            }.Run();
        }

        private struct ProjectedFootprint
        {
            public float2 Min;
            public float2 Max;
        }

        [BurstCompile]
        private partial struct DefeatedFootprintJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<LocalToWorld> Poses;
            [ReadOnly] public ComponentLookup<EnemyArmorVisual> ArmorVisuals;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            public NativeHashMap<Entity, ProjectedFootprint> Footprints;

            private void Execute(Entity entity, in EnemyTrailVisual visual, in DynamicBuffer<SkinMatrix> skin)
            {
                if (!Launches.TryGetComponent(visual.Owner, out var launch) || launch.Phase != EnemyLaunchPhase.Defeated
                    || !visual.Probes.IsCreated || !Poses.TryGetComponent(visual.Owner, out var owner)
                    || !Poses.TryGetComponent(visual.Root, out var root) || skin.Length == 0) return;

                // Detached armor moves independently of the body and should not stretch its shadow.
                if (ArmorVisuals.TryGetComponent(entity, out var armorVisual)
                    && (!Armors.TryGetComponent(visual.Owner, out var armor) || armor.Stages < armorVisual.StagesRequired)) return;

                ref var vertices = ref visual.Probes.Value.Vertices;
                if (vertices.Length == 0) return;

                float2 facing = GroundFacing(owner);
                float2 right = new(facing.y, -facing.x);
                var rootMatrix = root.Value;
                var projectedRoot = new float2x4(
                    Project(rootMatrix.c0.xz, right, facing),
                    Project(rootMatrix.c1.xz, right, facing),
                    Project(rootMatrix.c2.xz, right, facing),
                    Project(root.Position.xz - owner.Position.xz, right, facing));
                var matrices = skin.AsNativeArray();
                if (!Footprints.TryGetValue(visual.Owner, out var bounds))
                    bounds = new ProjectedFootprint { Min = float.PositiveInfinity, Max = float.NegativeInfinity };

                for (int i = 0; i < vertices.Length; i++)
                {
                    ref var vertex = ref vertices[i];
                    float4 point = new(vertex.Position, 1f);
                    float3 local = math.mul(matrices[vertex.Bones.x].Value, point) * vertex.Weights.x;
                    if (vertex.Weights.y > 0f) local += math.mul(matrices[vertex.Bones.y].Value, point) * vertex.Weights.y;
                    if (vertex.Weights.z > 0f) local += math.mul(matrices[vertex.Bones.z].Value, point) * vertex.Weights.z;
                    if (vertex.Weights.w > 0f) local += math.mul(matrices[vertex.Bones.w].Value, point) * vertex.Weights.w;
                    float2 projected = math.mul(projectedRoot, new float4(local, 1f));
                    bounds.Min = math.min(bounds.Min, projected);
                    bounds.Max = math.max(bounds.Max, projected);
                }
                Footprints[visual.Owner] = bounds;
            }

            private static float2 Project(float2 offset, float2 right, float2 forward) =>
                new(math.dot(offset, right), math.dot(offset, forward));
        }

        private static float2 GroundFacing(in LocalToWorld pose) =>
            math.normalizesafe(pose.Forward.xz, new float2(0, 1));

        private static void FitFoot(ref float2 radii, float2 offset, float2 forward, float padding)
        {
            float2 right = new(forward.y, -forward.x);
            float2 corner = math.abs(new float2(math.dot(offset, right), math.dot(offset, forward))) + padding;
            radii = math.max(radii, corner);
            // A diagonal foot needs room inside the ellipse, including at the faceted rim.
            radii *= math.max(1f, math.length(corner / radii) * 1.015f);
        }

        private void AddShadow(float3 position, float2 forward, float2 radii, float floor, float strength, float lift)
        {
            Vector3 center = new(position.x, floor + FloorOffset, position.z);
            radii *= 1.10f;
            float maxRadius = math.cmax(radii);
            foreach (Plane plane in frustum)
                if (Vector3.Dot(plane.normal, center) + plane.distance < -maxRadius) return;

            strength *= 1f - .78f * math.saturate(lift / math.max(2f, radii.x * 4f));
            strength = math.saturate(strength * (1.12f * 1.19f));
            transforms[instanceCount] = new Matrix4x4(
                new Vector4(forward.y * radii.x, 0f, -forward.x * radii.x, 0f),
                new Vector4(0f, 1f, 0f, 0f),
                new Vector4(forward.x * radii.y, 0f, forward.y * radii.y, 0f),
                new Vector4(center.x, center.y, center.z, 1f));
            strengths[instanceCount++] = strength;
            if (instanceCount == BatchSize) Flush();
        }

        private void Flush()
        {
            if (instanceCount == 0) return;
            properties.SetFloatArray(StrengthId, strengths);
            Graphics.RenderMeshInstanced(renderParams, disc, 0, transforms, instanceCount);
            instanceCount = 0;
        }

        protected override void OnDestroy()
        {
            defeatedFootprints.Dispose();
            if (disc != null) Object.Destroy(disc);
        }

        private static Mesh CreateDisc()
        {
            const int sides = 20;
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (2f * Mathf.PI / sides);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Character ground shadow disc" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
