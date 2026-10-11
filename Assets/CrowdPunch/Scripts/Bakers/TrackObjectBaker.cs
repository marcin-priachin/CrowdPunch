using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Bakers
{
    public sealed class TrackObjectBaker : Baker<TrackObjectAuthoring>
    {
        public override void Bake(TrackObjectAuthoring a)
        {
            if (a.settings == null || a.destination == null)
                throw new System.InvalidOperationException("Track object requires settings and destination.");
            DependsOn(a.settings); DependsOn(a.destination);
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var path = AddBuffer<TrackPathPoint>(e);
            float3 previous = a.transform.position;
            float length = 0;
            path.Add(new TrackPathPoint { Position = previous });
            var corners = a.corners ?? System.Array.Empty<UnityEngine.Transform>();
            for (int i = 0; i <= corners.Length; i++)
            {
                var point = i == corners.Length ? a.destination : corners[i];
                if (point == null) throw new System.InvalidOperationException("Track has a missing corner.");
                DependsOn(point);
                float3 delta = (float3)point.position - previous;
                if (!math.all(math.isfinite(delta)) || math.abs(point.position.y - a.transform.position.y) > .01f || math.length(delta) < .1f)
                    throw new System.InvalidOperationException("Track segments must be horizontal, finite and distinct.");
                length += math.length(delta);
                previous = point.position;
                path.Add(new TrackPathPoint { Position = previous, Distance = length });
            }
            AddComponent(e, new TrackObject {
                Start = a.transform.position, Direction = math.normalize(path[1].Position - path[0].Position), Length = length,
                RequiredNetHits = math.max(1, a.settings.requiredNetHits), SlideDuration = math.max(.05f, a.settings.slideDuration),
                PushDamage = math.max(0, a.settings.pushDamage), DamagingPush = a.settings.damagingPush ? (byte)1 : (byte)0,
                PushDamagesPlayer = a.settings.pushDamagesPlayer ? (byte)1 : (byte)0,
                FilterLaunchedExplosions = a.settings.filterLaunchedExplosions ? (byte)1 : (byte)0,
                ConsumeNonMovingHits = a.settings.consumeNonMovingHits ? (byte)1 : (byte)0,
                PlayerBodiesOnly = a.settings.playerLaunchedBodiesOnly ? (byte)1 : (byte)0 });
            AddComponent<TrackObjectState>(e);
            AddBuffer<TrackPushHistory>(e);
            AddBuffer<TrackPushContact>(e);
            // Infinite mass lets the solver push dynamic bodies without stalling the objective.
            AddComponent(e, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            AddComponent<PhysicsVelocity>(e);
            AddComponent(e, new PhysicsGravityFactor { Value = 0 });
        }
    }
}
