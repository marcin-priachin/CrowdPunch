using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    public struct DragonCone
    {
        public Entity Source;
        public float3 Mouth, Body;
        public float2 Forward;
        public DragonFlameProfile Profile;
        public DragonTuning Tuning;
        public EnemyLaunchState Launch;
        public uint Generation;
        public byte Active;
    }

    public static class DragonFlameGeometry
    {
        public static bool TryCone(Entity entity, in LocalTransform pose, in DragonTuning tuning,
            in DragonFlameState flame, in EnemyLaunchState launch, uint generation, out DragonCone cone)
        {
            bool launched = launch.Phase == EnemyLaunchPhase.Launched;
            bool active = launched || launch.Phase == EnemyLaunchPhase.Active && flame.Phase == DragonFlamePhase.Active;
            cone = new DragonCone { Source = entity, Body = pose.Position, Mouth = pose.TransformPoint(flame.Initialized != 0 ? flame.MouthOffset : tuning.MouthOffset),
                Forward = math.normalizesafe(math.forward(pose.Rotation).xz, new float2(0, 1)), Profile = launched ? tuning.Launched : tuning.Normal,
                Tuning = tuning, Launch = launch, Generation = generation, Active = active ? (byte)1 : (byte)0 };
            // Recovery retains the body's historical owner. Normal bursts have no active launch credit.
            if (!launched) { cone.Launch.Owner = EnemyLaunchOwner.None; cone.Launch.FeedbackChainDepth = 0; }
            return active || launch.Phase == EnemyLaunchPhase.Active && flame.Phase == DragonFlamePhase.Warning;
        }

        public static bool EnemyEligible(in DragonCone cone, Entity victim, bool dragon, bool armored)
        {
            if (victim == cone.Source || armored || dragon && cone.Tuning.Immunity == DragonImmunityMode.AllDragonFlame) return false;
            return cone.Active != 0 && (cone.Tuning.EnemyMode == DragonEnemyFlameMode.BurstsAndLaunches ||
                cone.Launch.Phase == EnemyLaunchPhase.Launched && (cone.Tuning.EnemyMode == DragonEnemyFlameMode.AnyLaunch || cone.Launch.Owner == EnemyLaunchOwner.Player));
        }

        // Distance to a finite circular sector, including both straight sides and the rounded far edge.
        public static float2 Closest(in DragonCone cone, float2 point)
        {
            float2 delta = point - cone.Mouth.xz;
            float2 right = new float2(cone.Forward.y, -cone.Forward.x);
            float angle = math.atan2(math.dot(delta, right), math.dot(delta, cone.Forward));
            float half = math.radians(cone.Profile.AngleDegrees * .5f);
            float clamped = math.clamp(angle, -half, half);
            float2 direction = cone.Forward * math.cos(clamped) + right * math.sin(clamped);
            float distance = math.clamp(math.dot(delta, direction), 0, cone.Profile.Length);
            return cone.Mouth.xz + direction * distance;
        }

        public static bool Overlaps(in DragonCone cone, float3 point, float radius)
            => math.distancesq(Closest(cone, point.xz), point.xz) <= radius * radius + .000001f;

        public static bool Exposed(in DragonCone cone, float3 point, float radius, CollisionWorld world,
            ComponentLookup<Enemy> enemies, float planeY)
        {
            float2 closest = Closest(cone, point.xz);
            if (math.distancesq(closest, point.xz) > radius * radius + .000001f) return false;
            float2 delta = closest - cone.Mouth.xz;
            float length = math.length(delta);
            if (length < .001f) return true;
            if (RayExposes(cone, point.xz, radius, delta / length, length, world, enemies, planeY)) return true;
            // A disk can protrude past a cover edge even when its centre ray is occluded.
            // Probe its angular silhouette inside the cone, rather than ignoring victim radius at walls.
            float2 centerDelta = point.xz - cone.Mouth.xz;
            float centerDistance = math.length(centerDelta);
            if (centerDistance <= radius) return true;
            float2 right = new float2(cone.Forward.y, -cone.Forward.x);
            float centerAngle = math.atan2(math.dot(centerDelta, right), math.dot(centerDelta, cone.Forward));
            float spread = math.asin(math.saturate(radius / centerDistance));
            float half = math.radians(cone.Profile.AngleDegrees * .5f);
            float min = math.max(-half, centerAngle - spread), max = math.min(half, centerAngle + spread);
            for (int i = 0; i <= 8 && min <= max; i++)
            {
                float angle = math.lerp(min, max, i / 8f);
                float2 dir = cone.Forward * math.cos(angle) + right * math.sin(angle);
                if (RayExposes(cone, point.xz, radius, dir, cone.Profile.Length, world, enemies, planeY)) return true;
            }
            return false;
        }

        private static bool RayExposes(in DragonCone cone, float2 point, float radius, float2 dir, float length,
            CollisionWorld world, ComponentLookup<Enemy> enemies, float planeY)
        {
            // Stop at the first point on the victim disk, so a victim in front of cover is not hidden by its centre.
            float projection = math.dot(point - cone.Mouth.xz, dir);
            float perpendicularSq = math.lengthsq(point - cone.Mouth.xz) - projection * projection;
            if (projection < 0 || perpendicularSq > radius * radius + .000001f) return false;
            float entry = math.max(0, projection - math.sqrt(math.max(0, radius * radius - perpendicularSq)));
            return entry <= length + .000001f && Reach(world, enemies, cone.Source, cone.Mouth.xz, dir, length, planeY) + .002f >= entry;
        }

        public static float Reach(CollisionWorld world, ComponentLookup<Enemy> enemies, Entity source,
            float2 origin, float2 direction, float length, float planeY)
        {
            if (length <= .001f) return 0;
            var collector = new SolidCollector { Enemies = enemies, Source = source, MaxFraction = 1 };
            world.CastRay(new RaycastInput { Start = new float3(origin.x, planeY, origin.y),
                End = new float3(origin.x + direction.x * length, planeY, origin.y + direction.y * length), Filter = CollisionFilter.Default }, ref collector);
            return length * collector.MaxFraction;
        }

        private struct SolidCollector : ICollector<RaycastHit>
        {
            public ComponentLookup<Enemy> Enemies;
            public Entity Source;
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }
            public bool AddHit(RaycastHit hit)
            {
                if (hit.Entity == Source || Enemies.HasComponent(hit.Entity) || math.abs(hit.SurfaceNormal.y) > .8f ||
                    hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents) return false;
                MaxFraction = hit.Fraction; NumHits = 1; return true;
            }
        }
    }
}
