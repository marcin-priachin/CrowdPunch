using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class WizardZoneCreation
    {
        public static Entity Create(EntityCommandBuffer commands, EntityManager em, Entity source,
            float3 position, WizardSettings settings, double now, bool follow, bool active)
        {
            using (var physicsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>()))
            {
                if (!physicsQuery.IsEmptyIgnoreFilter)
                {
                    var world = physicsQuery.GetSingleton<PhysicsWorldSingleton>();
                    var hits = new Unity.Collections.NativeList<Unity.Physics.RaycastHit>(Unity.Collections.Allocator.Temp);
                    world.CastRay(new RaycastInput { Start = position + new float3(0, 20, 0), End = position - new float3(0, 100, 0), Filter = CollisionFilter.Default }, ref hits);
                    float ground = float.MinValue;
                    foreach (var hit in hits)
                        if (!em.HasComponent<Enemy>(hit.Entity) && !em.HasComponent<BossPart>(hit.Entity) && hit.SurfaceNormal.y > .5f)
                            ground = math.max(ground, hit.Position.y);
                    position.y = ground == float.MinValue ? -0.97f : ground + .03f;
                    hits.Dispose();
                }
            }
            var zone = new WizardZone { Source = source, Position = position, Settings = settings,
                Follow = follow ? (byte)1 : (byte)0, Active = active ? (byte)1 : (byte)0,
                ExpiresAt = now + (active ? math.max(0, settings.ActiveDuration) : math.max(0, settings.TelegraphDuration)) };
            // Keep independent zones alive after their source dies, but never after their encounter unloads.
            if (em.HasComponent<EnemyWaveOwnership>(source))
            {
                var owner = em.GetComponentData<EnemyWaveOwnership>(source);
                zone.Sequence = owner.Sequence; zone.RunGeneration = owner.RunGeneration; zone.WaveIndex = owner.WaveIndex;
                zone.SceneOwner = owner.Sequence;
            }
            else if (em.HasComponent<SceneTag>(source)) zone.SceneOwner = em.GetSharedComponent<SceneTag>(source).SceneEntity;
            Entity entity = commands.CreateEntity();
            commands.AddComponent(entity, zone);
            commands.AddBuffer<WizardZoneTarget>(entity);
            return entity;
        }
    }
}
