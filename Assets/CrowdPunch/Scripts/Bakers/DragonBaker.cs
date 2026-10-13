using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class DragonBaker : Baker<DragonAuthoring>
    {
        public override void Bake(DragonAuthoring authoring)
        {
            if (authoring.settings == null) return;
            DependsOn(authoring.settings);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.settings.Tuning);
            AddComponent<DragonFlameState>(entity);
            AddBuffer<DragonFlameClock>(entity);
            var mouth = AddBuffer<DragonMouthFrame>(entity);
            if (authoring.mouthFrames != null)
                foreach (var frame in authoring.mouthFrames) mouth.Add(new DragonMouthFrame { Position = frame });
        }
    }
}
