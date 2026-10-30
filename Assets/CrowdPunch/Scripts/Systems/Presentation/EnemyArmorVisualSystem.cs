using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // ENEMY-014: detached equipment keeps its world pose independently of the moving owner.
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct EnemyArmorVisualSystem : ISystem
    {
        public const float DebrisDuration = 1.6f;
        public const float BlinkDelay = .25f;
        public const float BlinkInterval = .13f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float groundY = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground) ? ground.SurfaceHeight : 0f;
            state.Dependency = new ArmorFallJob
            {
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Roots = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                DeltaTime = SystemAPI.Time.DeltaTime,
                GroundY = groundY
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ArmorVisibilityJob
            {
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Debris = SystemAPI.GetComponentLookup<EnemyArmorDebris>(true)
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ArmorBoundsJob
            {
                Debris = SystemAPI.GetComponentLookup<EnemyArmorDebris>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ArmorFallJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<LocalToWorld> Roots;
            [ReadOnly] public ComponentLookup<EnemyLifetime> Lifetimes;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            public float DeltaTime;
            public float GroundY;

            private void Execute(in EnemyArmorVisual visual, ref EnemyArmorDebris debris, ref DynamicBuffer<SkinMatrix> skin)
            {
                Entity owner = visual.Owner;
                uint generation = Lifetimes.HasComponent(owner) ? Lifetimes[owner].Generation : 0;
                bool pooled = Respawns.HasComponent(owner) && Respawns.IsComponentEnabled(owner) && Respawns[owner].IsPooled != 0;
                if (!Armors.HasComponent(owner) || pooled || generation != debris.Generation)
                {
                    debris.Armed = 0;
                    debris.Detached = 0;
                    debris.Age = 0;
                    debris.Generation = generation;
                    if (pooled || !Armors.HasComponent(owner)) return;
                }
                if (Armors[owner].Stages >= visual.StagesRequired)
                {
                    debris.Armed = 1;
                    debris.Detached = 0;
                    debris.Age = 0;
                    return;
                }
                if (skin.Length != 1 || !Roots.HasComponent(debris.Root)) return;
                float4x4 root = Roots[debris.Root].Value;
                if (math.abs(math.determinant(new float3x3(root))) < .000001f) return;
                if (debris.Armed != 0 && debris.Detached == 0)
                {
                    var pose = skin[0].Value;
                    debris.InitialPose = math.mul(root, new float4x4(new float4(pose.c0, 0), new float4(pose.c1, 0),
                        new float4(pose.c2, 0), new float4(pose.c3, 1)));
                    float side = visual.StagesRequired == 2 ? 1f : -1f;
                    float3 inherited = Velocities.HasComponent(owner) ? Velocities[owner].Linear : float3.zero;
                    debris.Velocity = math.clamp(inherited, new float3(-3f), new float3(3f))
                        + math.normalizesafe(root.c0.xyz, new float3(1, 0, 0)) * (side * 1.4f) + new float3(0, .6f, 0);
                    debris.Detached = 1;
                    debris.Armed = 0;
                    debris.Age = 0;
                }
                else if (debris.Detached != 0) debris.Age = math.min(DebrisDuration, debris.Age + DeltaTime);
                if (debris.Detached == 0 || debris.Age >= DebrisDuration) return;

                float age = debris.Age;
                float direction = visual.StagesRequired == 2 ? 1f : -1f;
                var spin = new float3x3(quaternion.EulerXYZ(new float3(2.8f, 1.7f * direction, 3.4f * direction) * age));
                var axes = math.mul(spin, new float3x3(debris.InitialPose));
                float3 center = math.transform(debris.InitialPose, debris.MeshBounds.Center)
                    + debris.Velocity * age + new float3(0, -4.905f * age * age, 0);
                float3 extents = math.abs(axes.c0) * debris.MeshBounds.Extents.x
                    + math.abs(axes.c1) * debris.MeshBounds.Extents.y + math.abs(axes.c2) * debris.MeshBounds.Extents.z;
                center.y = math.max(GroundY + extents.y + .015f, center.y);
                debris.WorldPose = new float4x4(axes, center - math.mul(axes, debris.MeshBounds.Center));
                // Skin samples are in the model root's space. Compensate every frame for owner motion.
                var local = math.mul(math.inverse(root), debris.WorldPose);
                skin[0] = new SkinMatrix { Value = new float3x4(local.c0.xyz, local.c1.xyz, local.c2.xyz, local.c3.xyz) };
            }
        }

        [BurstCompile, WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct ArmorVisibilityJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<EnemyArmorDebris> Debris;

            private void Execute(in EnemyArmorVisual visual, EnabledRefRW<MaterialMeshInfo> visible)
            {
                visible.ValueRW = Armors.HasComponent(visual.Owner)
                    && Armors[visual.Owner].Stages >= visual.StagesRequired;
                if (!visible.ValueRO && Debris.HasComponent(visual.Animation))
                {
                    var debris = Debris[visual.Animation];
                    visible.ValueRW = debris.Detached != 0 && debris.Age < DebrisDuration
                        && (debris.Age < BlinkDelay || (int)((debris.Age - BlinkDelay) / BlinkInterval) % 2 == 0);
                }
            }
        }

        [BurstCompile, WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct ArmorBoundsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyArmorDebris> Debris;

            private void Execute(in EnemyArmorVisual visual, in LocalToWorld world, ref RenderBounds bounds)
            {
                bounds.Value = visual.AuthoredBounds;
                if (!Debris.HasComponent(visual.Animation)) return;
                var debris = Debris[visual.Animation];
                if (debris.Detached == 0 || debris.Age >= DebrisDuration
                    || math.abs(math.determinant(new float3x3(world.Value))) < .000001f) return;
                var local = math.mul(math.inverse(world.Value), debris.WorldPose);
                bounds.Value = new AABB
                {
                    Center = math.transform(local, debris.MeshBounds.Center),
                    Extents = math.abs(local.c0.xyz) * debris.MeshBounds.Extents.x
                        + math.abs(local.c1.xyz) * debris.MeshBounds.Extents.y + math.abs(local.c2.xyz) * debris.MeshBounds.Extents.z
                };
            }
        }
    }
}
