using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Physics;

namespace CrowdPunch.Systems.Presentation
{
    // Final render-only composition after the role/telegraph colors. LocalTransform,
    // PostTransformMatrix, skin samples and physics geometry retain their owners.
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(DasherPresentationSystem))]
    [UpdateAfter(typeof(EnemyReadabilitySystem))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct EnemyImpactVisualSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            new VisualJob
            {
                Feedback = SystemAPI.GetComponentLookup<EnemyImpactFeedback>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                Exploders = SystemAPI.GetComponentLookup<ExplosiveEnemyState>(true),
                Time = (float)SystemAPI.Time.ElapsedTime,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }
        [BurstCompile]
        private partial struct VisualJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyImpactFeedback> Feedback;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<ExplosiveEnemyState> Exploders;
            public float Time, DeltaTime;
            private void Execute(in EnemyVisualOwner owner, ref EnemyVisualDeformation visual,
                ref LocalToWorld world, ref URPMaterialPropertyBaseColor color)
            {
                if (!Feedback.HasComponent(owner.Value)) return;
                if (visual.Initialized == 0 || !world.Value.Equals(visual.LastOutput))
                {
                    visual.AuthoredWorld = world.Value;
                    visual.Initialized = 1;
                }
                var f = Feedback[owner.Value];
                float amount = f.Squash * f.Intensity * math.saturate(f.VisualRemaining / math.max(.001f, f.VisualDuration));
                float3 direction = math.normalizesafe(f.VisualDirection, math.up());
                var d = float3x3.identity * (1f + amount * .5f)
                    - new float3x3(direction * direction.x, direction * direction.y, direction * direction.z) * (amount * 1.5f);
                var original = visual.AuthoredWorld;
                float lift = 0;
                if (owner.UsesAtlas != 0 && Launches.HasComponent(owner.Value))
                {
                    var phase = Launches[owner.Value].Phase;
                    visual.PhaseAge = visual.PreviousPhase == phase ? visual.PhaseAge + DeltaTime : 0;
                    visual.PreviousPhase = phase;
                    float seed = (math.hash(new uint2((uint)owner.Value.Index, (uint)owner.Value.Version)) & 1023) * .00614f;
                    float speed = Velocities.HasComponent(owner.Value) ? math.length(Velocities[owner.Value].Linear.xz) : 0;
                    float squash = 0;
                    if (phase == EnemyLaunchPhase.Active)
                    {
                        float gait = math.sin(Time * (5f + math.min(speed, 6f) * 1.5f) + seed);
                        squash = gait * math.lerp(.012f, .035f, math.saturate(speed));
                        lift = math.abs(gait) * .035f * math.saturate(speed);
                        bool preparing = Contacts.HasComponent(owner.Value) && Contacts[owner.Value].IsWindingUp != 0
                            || Dashers.HasComponent(owner.Value) && Dashers[owner.Value].Phase == DasherPhase.Preparing;
                        if (preparing) { squash = .12f; lift = 0; }
                    }
                    else if (phase == EnemyLaunchPhase.Recovering)
                        squash = .14f * math.exp(-visual.PhaseAge * 7f) * math.cos(visual.PhaseAge * 20f);
                    var localAccent = float3x3.Scale(new float3(1f + squash * .5f, 1f - squash, 1f + squash * .5f));
                    if (Exploders.HasComponent(owner.Value) && phase != EnemyLaunchPhase.Defeated)
                    {
                        // A steady pressure pulse identifies the explosive body; it is not a fuse countdown.
                        float pulse = .5f + .5f * math.sin(Time * 5.2359878f);
                        localAccent *= 1f + .065f * pulse * pulse;
                    }
                    var basis = math.mul(new float3x3(original.c0.xyz, original.c1.xyz, original.c2.xyz), localAccent);
                    original.c0.xyz = basis.c0; original.c1.xyz = basis.c1; original.c2.xyz = basis.c2;
                }
                world.Value = new float4x4(new float4(math.mul(d, original.c0.xyz), original.c0.w),
                    new float4(math.mul(d, original.c1.xyz), original.c1.w),
                    new float4(math.mul(d, original.c2.xyz), original.c2.w), original.c3 + new float4(0, lift, 0, 0));
                visual.LastOutput = world.Value;
                float flash = f.Flash * f.Intensity * math.saturate(f.FlashRemaining / math.max(.001f, f.FlashDuration));
                color.Value.xyz = math.lerp(color.Value.xyz, new float3(owner.UsesAtlas != 0 ? 2.4f : 1f), flash);
            }
        }
    }
}
