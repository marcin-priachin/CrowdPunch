using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class GroundHazardBaker : Baker<GroundHazardAuthoring>
    {
        public override void Bake(GroundHazardAuthoring a)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GroundHazard
            {
                Sequence = a.sequence == null ? Entity.Null : GetEntity(a.sequence, TransformUsageFlags.None),
                Position = a.transform.position, Angle = math.radians(a.transform.eulerAngles.y),
                HalfSize = new float2(math.max(.1f, a.width), math.max(.1f, a.depth)) * .5f,
                Radius = math.max(.1f, a.radius), Shape = a.shape, Operation = a.operation,
                Damage = math.max(0, a.damage), DamageInterval = math.max(.01f, a.damageInterval),
                InactiveDuration = math.max(0, a.inactiveDuration), WarningDuration = math.max(0, a.warningDuration),
                ActiveDuration = math.max(.01f, a.activeDuration), CycleOffset = a.cycleOffset, FirstWave = math.max(0, a.firstWave)
            });
            AddComponent<GroundHazardState>(entity);
        }
    }
}
