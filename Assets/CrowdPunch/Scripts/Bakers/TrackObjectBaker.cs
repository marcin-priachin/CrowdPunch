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
            float3 delta = (float3)a.destination.position - (float3)a.transform.position;
            if (math.abs(delta.y) > .01f || math.length(delta) < .1f)
                throw new System.InvalidOperationException("Track must be horizontal with distinct endpoints.");
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new TrackObject {
                Start = a.transform.position, Direction = math.normalize(delta), Length = math.length(delta),
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
