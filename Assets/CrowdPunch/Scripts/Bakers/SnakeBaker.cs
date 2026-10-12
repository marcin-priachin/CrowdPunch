using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class SnakeBaker : Baker<SnakeAuthoring>
    {
        public override void Bake(SnakeAuthoring authoring)
        {
            if (authoring.settings == null || authoring.orbPrefab == null) return;
            DependsOn(authoring.settings);
            var s = authoring.settings;
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SnakeHead>(e);
            AddBuffer<SnakeTailLink>(e);
            AddBuffer<SnakePathPoint>(e);
            AddComponent(e, new SnakeTuning {
                OrbPrefab = GetEntity(authoring.orbPrefab, TransformUsageFlags.Dynamic),
                SegmentCount = math.clamp(s.initialTailSegmentCount, 0, 32),
                OrbRadius = math.max(.1f, s.orbRadius), Spacing = math.max(.2f, s.segmentSpacing),
                WaveAmplitude = math.max(0, s.waveAmplitude), WaveFrequencyHz = math.max(0, s.waveFrequencyHz),
                PursuitSpeed = math.max(0, s.pursuitSpeed), ContactDamagePercent = math.saturate(s.contactDamagePercent),
                HeadVulnerability = s.headVulnerability,
                ExplosionResponse = s.explosionResponse });
        }
    }
}
