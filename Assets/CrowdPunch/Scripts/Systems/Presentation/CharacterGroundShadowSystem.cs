using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
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
            properties.SetVector(TintId, new Vector4(.25f + .14f * ambient.r / brightest,
                .25f + .14f * ambient.g / brightest, .25f + .14f * ambient.b / brightest, 1f));

            float floor = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            CollectEnemyFootprints();

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

            foreach (var (pose, agent, ground, enemy) in
                     SystemAPI.Query<RefRO<LocalToWorld>, RefRO<NavigationAgent>, RefRO<EnemyGroundConstraint>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                float3 position = pose.ValueRO.Position;
                float lift = ground.ValueRO.HasGround != 0 ? position.y - ground.ValueRO.Height : 0f;
                if (animatedCenters.TryGetValue(enemy, out var center)) position.xz = center;
                float2 radii = math.max(.25f, agent.ValueRO.Radius) * new float2(.85f, 1.05f);
                if (footRadii.TryGetValue(enemy, out var feet)) radii = math.max(radii, feet);
                AddShadow(position, GroundFacing(pose.ValueRO), radii, floor, .65f, lift);
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
            float maxRadius = math.cmax(radii);
            foreach (Plane plane in frustum)
                if (Vector3.Dot(plane.normal, center) + plane.distance < -maxRadius) return;

            strength *= 1f - .78f * math.saturate(lift / math.max(2f, radii.x * 4f));
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
