using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup)), UpdateAfter(typeof(GatekeeperAnimationSystem))]
    public partial struct GatekeeperArmAnimationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Animate {
                Bosses = state.GetComponentLookup<BossEncounter>(true),
                Hands = state.GetComponentLookup<BossHand>(true),
                Parts = state.GetComponentLookup<BossPart>(true),
                Worlds = state.GetComponentLookup<LocalToWorld>(true),
                GroundY = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground) ? ground.SurfaceHeight : -1f
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile] private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<BossEncounter> Bosses;
            [ReadOnly] public ComponentLookup<BossHand> Hands;
            [ReadOnly] public ComponentLookup<BossPart> Parts;
            [ReadOnly] public ComponentLookup<LocalToWorld> Worlds;
            public float GroundY;

            public void Execute(in EnemyAnimation animation, in GatekeeperRig rig,
                in EnemyAnimationPlayback playback, in DynamicBuffer<GatekeeperArmBone> mask, ref DynamicBuffer<SkinMatrix> skin)
            {
                if (!animation.Samples.IsCreated || !rig.Bounds.IsCreated || !Bosses.HasComponent(animation.Owner) || !Worlds.HasComponent(rig.Model)) return;
                var boss = Bosses[animation.Owner];
                ref var samples = ref animation.Samples.Value;
                if (skin.Length != samples.BoneCount || mask.Length != skin.Length) return;
                var model = Worlds[rig.Model].Value;
                ref var bounds = ref rig.Bounds.Value;
                ApplyArm(ref samples, ref bounds, ref skin, in mask, 1, rig.LeftPalm, rig.LeftBindPalm, rig.FistScale.x, boss.LeftHand, model, GroundY + rig.GroundClearance, boss.Cycle, playback.Phase);
                ApplyArm(ref samples, ref bounds, ref skin, in mask, 2, rig.RightPalm, rig.RightBindPalm, rig.FistScale.y, boss.RightHand, model, GroundY + rig.GroundClearance, boss.Cycle, playback.Phase);
            }

            private void ApplyArm(ref EnemyAnimationSamples samples, ref GatekeeperHandBounds bounds, ref DynamicBuffer<SkinMatrix> skin,
                in DynamicBuffer<GatekeeperArmBone> mask, byte side, int palm, float3 bindPalm, float fistScale,
                Entity handEntity, float4x4 model, float groundY, BossCycle cycle, float bodyPhase)
            {
                if (!Hands.HasComponent(handEntity) || !Parts.HasComponent(handEntity) || !Worlds.HasComponent(handEntity) || palm < 0 || palm >= skin.Length) return;
                var hand = Hands[handEntity];
                float progress = 1 - math.saturate(hand.Remaining / math.max(.01f, hand.Duration));
                int motion = 0;
                float phase = bodyPhase;
                if (hand.Phase == BossHandPhase.Staggered) { motion = 3; phase = progress; }
                else if (hand.Phase == BossHandPhase.Shielding) { motion = 8; phase = 1; }
                else if (hand.Phase == BossHandPhase.Anticipation || hand.Phase == BossHandPhase.Active || hand.Phase == BossHandPhase.Recovery)
                {
                    motion = 5 + (int)hand.Attack;
                    phase = hand.Phase == BossHandPhase.Anticipation ? progress * .45f
                        : hand.Phase == BossHandPhase.Active ? .45f + progress * .3f : .75f + progress * .25f;
                }
                if (cycle == BossCycle.Relocating && motion == 0) motion = 1;
                if (cycle == BossCycle.Transition) { motion = 2; phase = bodyPhase; }
                if (cycle == BossCycle.Defeated) { motion = 4; phase = bodyPhase; }
                for (int bone = 0; bone < skin.Length; bone++)
                    if (mask[bone].Side == side) skin[bone] = new SkinMatrix { Value = Sample(ref samples, motion, phase, bone) };

                var palmMatrix = skin[palm].Value;
                float3 origin = math.mul(new float3x3(palmMatrix.c0, palmMatrix.c1, palmMatrix.c2), bindPalm) + palmMatrix.c3;
                float modelScale = math.cmax(new float3(math.length(model.c0.xyz), math.length(model.c1.xyz), math.length(model.c2.xyz)));
                float radius = side == 1 ? bounds.LeftRadius : bounds.RightRadius;
                float scale = Parts[handEntity].Radius * (fistScale > 0 ? fistScale : 1) / math.max(.0001f, radius * modelScale);
                float frame = math.saturate(phase) * (bounds.FrameCount - 1);
                int a = (int)frame, b = math.min(a + 1, bounds.FrameCount - 1);
                var from = bounds.Frames[(motion * bounds.FrameCount + a) * 2 + side - 1];
                var to = bounds.Frames[(motion * bounds.FrameCount + b) * 2 + side - 1];
                // Interpolated endpoint bounds conservatively enclose every linearly skinned vertex.
                float3 minimum = math.lerp(from.Min, to.Min, math.frac(frame));
                float3 maximum = math.lerp(from.Max, to.Max, math.frac(frame));
                float3 center = (minimum + maximum) * .5f;
                center.y = 0;
                float3 targetWorld = Worlds[handEntity].Position;
                float3 upRow = new float3(model.c0.y, model.c1.y, model.c2.y);
                float bottom = targetWorld.y + math.dot(upRow, math.select(maximum, minimum, upRow >= 0) - center) * scale;
                targetWorld.y += math.max(0, groundY - bottom);
                float3 target = math.transform(math.inverse(model), targetWorld) - center * scale;
                // Replace the arm-root translation after sampling; descendants keep authored finger articulation.
                // World-size compensation preserves the damage footprint when the model's authored scale changes.
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    if (mask[bone].Side != side) continue;
                    var pose = skin[bone].Value;
                    skin[bone] = new SkinMatrix { Value = new float3x4(pose.c0 * scale, pose.c1 * scale,
                        pose.c2 * scale, target + (pose.c3 - origin) * scale) };
                }
            }

            private static float3x4 Sample(ref EnemyAnimationSamples samples, int motion, float phase, int bone)
            {
                float frame = math.saturate(phase) * (samples.FrameCount - 1);
                int a = (int)frame, b = math.min(a + 1, samples.FrameCount - 1);
                var from = samples.Matrices[(motion * samples.FrameCount + a) * samples.BoneCount + bone];
                var to = samples.Matrices[(motion * samples.FrameCount + b) * samples.BoneCount + bone];
                float fraction = math.frac(frame);
                return new float3x4(math.lerp(from.c0, to.c0, fraction), math.lerp(from.c1, to.c1, fraction),
                    math.lerp(from.c2, to.c2, fraction), math.lerp(from.c3, to.c3, fraction));
            }
        }
    }
}
