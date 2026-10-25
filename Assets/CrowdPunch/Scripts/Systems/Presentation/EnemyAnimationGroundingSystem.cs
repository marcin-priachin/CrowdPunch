using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // TRAIL-006: measure the actual blended skin, then translate it in world-up.
    // Neither physics transforms nor authored bone rotations/scales are changed.
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct EnemyAnimationGroundingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground)) return;
            state.Dependency = new GroundPoseJob
            {
                Roots = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                GroundY = ground.SurfaceHeight
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new GroundBoundsJob
            {
                Poses = SystemAPI.GetComponentLookup<EnemyAnimationGrounding>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct GroundPoseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalToWorld> Roots;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            public float GroundY;

            private void Execute(ref EnemyAnimationGrounding grounding, ref DynamicBuffer<SkinMatrix> skin)
            {
                grounding.Offset = 0;
                if (!grounding.Mesh.IsCreated || !Roots.HasComponent(grounding.Root) || skin.Length == 0) return;
                if (Respawns.HasComponent(grounding.Owner) && Respawns.IsComponentEnabled(grounding.Owner)
                    && Respawns[grounding.Owner].IsPooled != 0) return;
                float4x4 root = Roots[grounding.Root].Value;
                var axes = new float3x3(root);
                if (math.abs(math.determinant(axes)) < .000001f) return;
                float3 upRow = new float3(root.c0.y, root.c1.y, root.c2.y);
                float bottom = float.PositiveInfinity;
                ref var mesh = ref grounding.Mesh.Value;
                for (int index = 0; index < mesh.Vertices.Length; index++)
                {
                    var vertex = mesh.Vertices[index];
                    float4 point = new float4(vertex.Position, 1);
                    float3 position = math.mul(skin[vertex.Bones.x].Value, point) * vertex.Weights.x
                        + math.mul(skin[vertex.Bones.y].Value, point) * vertex.Weights.y
                        + math.mul(skin[vertex.Bones.z].Value, point) * vertex.Weights.z
                        + math.mul(skin[vertex.Bones.w].Value, point) * vertex.Weights.w;
                    bottom = math.min(bottom, root.c3.y + math.dot(upRow, position));
                }
                float lift = GroundY + math.max(0, grounding.Clearance) - bottom;
                // Grounded clips sit at the configured gap, including authored vertical root offsets.
                // Launched bodies retain their physical flight height and only receive a minimum clamp.
                if (!Launches.HasComponent(grounding.Owner) || Launches[grounding.Owner].Phase == EnemyLaunchPhase.Launched)
                    lift = math.max(0, lift);
                if (!math.isfinite(lift) || math.abs(lift) <= .000001f) return;
                grounding.Offset = math.mul(math.inverse(axes), new float3(0, lift, 0));
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    var pose = skin[bone].Value;
                    pose.c3 += grounding.Offset;
                    skin[bone] = new SkinMatrix { Value = pose };
                }
            }
        }

        [BurstCompile]
        private partial struct GroundBoundsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyAnimationGrounding> Poses;

            private void Execute(in EnemyGroundingVisual visual, ref RenderBounds bounds)
            {
                if (!Poses.HasComponent(visual.Animation)) return;
                var authored = visual.AuthoredBounds;
                authored.Center += Poses[visual.Animation].Offset;
                bounds.Value = authored;
            }
        }
    }
}
