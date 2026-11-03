using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile]
    internal struct DragonFlameCoverSamplingJob : IJob
    {
        [ReadOnly] public CollisionWorld World;
        [ReadOnly] public ComponentLookup<Enemy> Enemies;
        public DragonCone Cone;
        public NativeArray<float> Reaches;
        public NativeArray<float2> Directions;

        public void Execute()
        {
            float half = math.radians(Cone.Profile.AngleDegrees * .5f);
            float2 right = new float2(Cone.Forward.y, -Cone.Forward.x);
            int steps = Reaches.Length - 1;
            for (int i = 0; i <= steps; i++)
            {
                float angle = math.lerp(-half, half, i / (float)steps);
                float2 direction = Cone.Forward * math.cos(angle) + right * math.sin(angle);
                float reach = DragonFlameGeometry.Reach(World, Enemies, Cone.Source, Cone.Mouth.xz,
                    direction, Cone.Profile.Length, Cone.Tuning.GroundY + .25f);
                Reaches[i] = math.max(0, reach - .025f); Directions[i] = direction;
            }
        }
    }
}
