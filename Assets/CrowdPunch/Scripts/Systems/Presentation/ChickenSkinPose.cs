using CrowdPunch.Components;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    internal static class ChickenSkinPose
    {
        // Interpolate rotation separately so blending never shrinks rotated limbs.
        internal static float3x4 Blend(float3x4 a, float3x4 b, float amount)
        {
            if (amount <= 0) return a;
            if (amount >= 1) return b;
            float3 sa = new float3(math.length(a.c0), math.length(a.c1), math.length(a.c2));
            float3 sb = new float3(math.length(b.c0), math.length(b.c1), math.length(b.c2));
            if (math.cmin(sa) < .00001f) return b;
            // The Giant's left shoulder is authored with mirrored X scale.
            // Retain handedness when separating rotation from scale.
            if (math.determinant(new float3x3(a.c0, a.c1, a.c2)) < 0) sa.x = -sa.x;
            if (math.determinant(new float3x3(b.c0, b.c1, b.c2)) < 0) sb.x = -sb.x;
            var ra = math.normalize(new quaternion(new float3x3(a.c0 / sa.x, a.c1 / sa.y, a.c2 / sa.z)));
            var rb = math.normalize(new quaternion(new float3x3(b.c0 / sb.x, b.c1 / sb.y, b.c2 / sb.z)));
            var rotation = new float3x3(math.slerp(ra, rb, amount));
            float3 scale = math.lerp(sa, sb, amount);
            return new float3x4(rotation.c0 * scale.x, rotation.c1 * scale.y, rotation.c2 * scale.z, math.lerp(a.c3, b.c3, amount));
        }

        internal static void Sample(ref EnemyAnimationSamples samples, ref DynamicBuffer<SkinMatrix> skin,
            int motion, float phase, bool loop, float blend)
        {
            float frame = phase * (loop ? samples.FrameCount : samples.FrameCount - 1);
            int a = math.min((int)frame, samples.FrameCount - 1);
            int b = loop ? (a + 1) % samples.FrameCount : math.min(a + 1, samples.FrameCount - 1);
            int start = motion * samples.FrameCount * samples.BoneCount;
            for (int bone = 0; bone < skin.Length; bone++)
            {
                var pose = Blend(samples.Matrices[start + a * samples.BoneCount + bone],
                    samples.Matrices[start + b * samples.BoneCount + bone], math.frac(frame));
                skin[bone] = new SkinMatrix { Value = Blend(skin[bone].Value, pose, blend) };
            }
        }

        internal static void IdleWithTurnLegs(ref EnemyAnimationSamples samples,ref EnemyJogSamples mask,
            ref DynamicBuffer<SkinMatrix> skin,int motion,float turnPhase,float idlePhase,float weight,float blend)
        {
            if(mask.BoneCount!=skin.Length) return;
            var idleHip=math.mul(ToMatrix(Bone(ref samples,0,idlePhase,true,mask.HipBoneIndex)),mask.HipBindInverse);
            var turnHip=math.mul(ToMatrix(Bone(ref samples,motion,turnPhase,false,mask.HipBoneIndex)),mask.HipBindInverse);
            var toIdleHip=math.mul(idleHip,math.inverse(turnHip));
            for(int bone=0;bone<skin.Length;bone++)
            {
                bool leg=false;
                for(int i=0;i<mask.LegBones.Length;i++) if(mask.LegBones[i]==bone) { leg=true;break; }
                var pose=Bone(ref samples,leg?motion:0,leg?turnPhase:idlePhase,!leg,bone);
                if(leg)
                {
                    // Retain the Idle pelvis and compose the turn below it, as with directional jog legs.
                    var relative=math.mul(toIdleHip,ToMatrix(pose));
                    pose=new float3x4(relative.c0.xyz,relative.c1.xyz,relative.c2.xyz,relative.c3.xyz);
                    // The explicit entry/exit clock owns leg blending, including the final
                    // zero-weight frame. Exponential smoothing here would delay idle again.
                    pose=Blend(Bone(ref samples,0,idlePhase,true,bone),pose,weight);
                }
                skin[bone]=new SkinMatrix { Value=leg?pose:Blend(skin[bone].Value,pose,blend) };
            }
        }

        internal static float2 BlendMovement(float2 current, float2 target, float blend)
        {
            float currentSpeed = math.length(current), targetSpeed = math.length(target);
            if (currentSpeed < .0001f) return target * blend;
            if (targetSpeed < .0001f) return current * (1 - blend);
            float from = math.atan2(current.x, current.y), to = math.atan2(target.x, target.y);
            float angle = from + math.atan2(math.sin(to - from), math.cos(to - from)) * blend;
            return new float2(math.sin(angle), math.cos(angle)) * math.lerp(currentSpeed, targetSpeed, blend);
        }

        internal static float3x4 Bone(ref EnemyAnimationSamples samples,int motion,float phase,bool loop,int bone)
        {
            float frame=phase*(loop?samples.FrameCount:samples.FrameCount-1);
            int a=math.min((int)frame,samples.FrameCount-1);
            int b=loop?(a+1)%samples.FrameCount:math.min(a+1,samples.FrameCount-1);
            int start=motion*samples.FrameCount*samples.BoneCount;
            return Blend(samples.Matrices[start+a*samples.BoneCount+bone],
                samples.Matrices[start+b*samples.BoneCount+bone],math.frac(frame));
        }

        internal static void Jog(ref EnemyJogSamples samples, ref DynamicBuffer<SkinMatrix> skin,
            ref EnemyAnimationPlayback playback, float delta, float speed = -1, float maxPlaybackSpeed = 0)
        {
            if (samples.BoneCount != skin.Length) return;
            float amount = math.saturate(math.length(playback.Movement));
            if (amount < .0001f) return;
            float sector = math.frac(math.atan2(playback.Movement.x, playback.Movement.y) / (2 * math.PI)) * 8;
            // Tiny negative angles can round frac(angle) to exactly one in Burst.
            int first = (int)math.floor(sector) % EnemyJogSamples.DirectionCount, second = (first + 1) % EnemyJogSamples.DirectionCount;
            float turn = math.frac(sector);
            float duration = math.lerp(samples.Durations[first], samples.Durations[second], turn);
            float distance = samples.CycleDistances.Length == EnemyJogSamples.DirectionCount
                ? math.max(.01f, math.lerp(samples.CycleDistances[first], samples.CycleDistances[second], turn)) : 2f;
            float rate = speed >= 0 ? speed / distance : 1 / math.max(.01f, duration);
            if (maxPlaybackSpeed > 0) rate = math.min(rate, maxPlaybackSpeed / math.max(.01f, duration));
            playback.JogPhase = math.frac(playback.JogPhase + delta * rate);
            float frame = playback.JogPhase * samples.FrameCount;
            int a = (int)frame, b = (a + 1) % samples.FrameCount;
            var hip = JogHip(ref samples, ref skin, first, second, turn, a, b, math.frac(frame), amount);
            int count = samples.LegBones.Length;
            for (int leg = 0; leg < count; leg++)
            {
                var left = Blend(samples.Matrices[(first * samples.FrameCount + a) * count + leg],
                    samples.Matrices[(first * samples.FrameCount + b) * count + leg], math.frac(frame));
                var right = Blend(samples.Matrices[(second * samples.FrameCount + a) * count + leg],
                    samples.Matrices[(second * samples.FrameCount + b) * count + leg], math.frac(frame));
                var pose = math.mul(hip, ToMatrix(Blend(left, right, turn)));
                int bone = samples.LegBones[leg];
                skin[bone] = new SkinMatrix { Value = Blend(skin[bone].Value,
                    new float3x4(pose.c0.xyz, pose.c1.xyz, pose.c2.xyz, pose.c3.xyz), amount) };
            }
        }

        internal static float4x4 JogHip(ref EnemyJogSamples samples, ref DynamicBuffer<SkinMatrix> skin,
            int first, int second, float directionBlend, int from, int to, float frameBlend, float amount)
        {
            var original = math.mul(ToMatrix(skin[samples.HipBoneIndex].Value), samples.HipBindInverse);
            if (samples.HipMatrices.Length != EnemyJogSamples.DirectionCount * samples.FrameCount) return original;
            var left = Blend(samples.HipMatrices[first * samples.FrameCount + from], samples.HipMatrices[first * samples.FrameCount + to], frameBlend);
            var right = Blend(samples.HipMatrices[second * samples.FrameCount + from], samples.HipMatrices[second * samples.FrameCount + to], frameBlend);
            var source = Blend(left, right, directionBlend);
            // The action pelvis can twist across the stride (Sweep) or narrow the stance
            // (Block). Keep its origin/scale, while the complete moving lower body owns rotation.
            float3 scale = new float3(math.length(original.c0.xyz), math.length(original.c1.xyz), math.length(original.c2.xyz));
            var target = new float3x4(math.normalizesafe(source.c0) * scale.x,
                math.normalizesafe(source.c1) * scale.y, math.normalizesafe(source.c2) * scale.z, original.c3.xyz);
            var resolved = ToMatrix(Blend(new float3x4(original.c0.xyz, original.c1.xyz, original.c2.xyz, original.c3.xyz), target, amount));
            var skinPose = math.mul(resolved, samples.HipBind);
            skin[samples.HipBoneIndex] = new SkinMatrix { Value = new float3x4(skinPose.c0.xyz, skinPose.c1.xyz, skinPose.c2.xyz, skinPose.c3.xyz) };
            return resolved;
        }

        private static float4x4 ToMatrix(float3x4 matrix) => new float4x4(new float4(matrix.c0, 0),
            new float4(matrix.c1, 0), new float4(matrix.c2, 0), new float4(matrix.c3, 1));
    }
}
