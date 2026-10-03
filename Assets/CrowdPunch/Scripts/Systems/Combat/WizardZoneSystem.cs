using CrowdPunch.Components;
using Unity.Burst;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(WizardImpactZoneSystem))]
    [UpdateAfter(typeof(LaunchedEnemyPlayerImpactSystem))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct WizardZoneSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddBuffer<WizardPlayerHit>(state.EntityManager.CreateEntity());
            state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<WizardZone>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            var em = state.EntityManager;
            var playerHits = SystemAPI.GetSingletonBuffer<WizardPlayerHit>();
            playerHits.Clear();
            double now = SystemAPI.Time.ElapsedTime;
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            using var removed = new NativeHashSet<Entity>(16, Allocator.Temp);
            var candidates = new NativeList<int>(Allocator.Temp);
            // The broadphase was built before integration. Expand by this step's maximum body travel.
            float padding = 0;
            foreach (var (velocity, agent) in SystemAPI.Query<RefRO<PhysicsVelocity>, RefRO<NavigationAgent>>().WithAll<Enemy>())
                padding = math.max(padding, math.length(velocity.ValueRO.Linear.xz) * SystemAPI.Time.DeltaTime + agent.ValueRO.Radius);
            foreach (var (zoneRef, targetBuffer, entity) in SystemAPI.Query<RefRW<WizardZone>, DynamicBuffer<WizardZoneTarget>>().WithEntityAccess())
            {
                var targets = targetBuffer;
                var zone = zoneRef.ValueRO;
                if (zone.SceneOwner != Entity.Null && !em.Exists(zone.SceneOwner) ||
                    zone.Sequence != Entity.Null && em.HasComponent<EnemyWaveSequence>(zone.Sequence) &&
                    em.GetComponentData<EnemyWaveSequence>(zone.Sequence).RunGeneration != zone.RunGeneration)
                { commands.DestroyEntity(entity); removed.Add(entity); continue; }
                if (zone.Follow != 0)
                {
                    if (!Valid(em, zone.Source) || em.GetComponentData<EnemyLaunchState>(zone.Source).Phase != EnemyLaunchPhase.Active)
                    { commands.DestroyEntity(entity); removed.Add(entity); continue; }
                    zone.Position.xz = em.GetComponentData<LocalTransform>(zone.Source).Position.xz;
                }
                else if (now >= zone.ExpiresAt) { commands.DestroyEntity(entity); removed.Add(entity); continue; }
                zoneRef.ValueRW = zone;
                if (zone.Active == 0) continue;
                for (int i = 0; i < targets.Length; i++) { var t = targets[i]; t.Seen = 0; targets[i] = t; }
                if (zone.Kind == WizardZoneKind.Impact)
                {
                    float broadRadius = math.max(0, zone.Radius) + padding;
                    candidates.Clear();
                    physics.CollisionWorld.OverlapAabb(new OverlapAabbInput
                    {
                        Aabb = new Aabb { Min = new float3(zone.Position.x - broadRadius, -1e6f, zone.Position.z - broadRadius),
                            Max = new float3(zone.Position.x + broadRadius, 1e6f, zone.Position.z + broadRadius) },
                        Filter = CollisionFilter.Default
                    }, ref candidates);
                    foreach (int bodyIndex in candidates)
                    {
                        Entity target = physics.Bodies[bodyIndex].Entity;
                        if (target == zone.Source || !Valid(em, target) || em.HasComponent<BossPart>(target)) continue;
                        if (em.HasComponent<EnemyArmor>(target) && em.GetComponentData<EnemyArmor>(target).Stages > 0) continue;
                        float radius = em.HasComponent<NavigationAgent>(target) ? em.GetComponentData<NavigationAgent>(target).Radius : 0;
                        float3 position = em.GetComponentData<LocalTransform>(target).Position;
                        if (!Overlaps(zone.Position, zone.Radius, position, radius)) continue;
                        if (Tick(targets, target, now, zone.Settings.TickInterval, 0)) HitEnemy(em, target, position, zone, now);
                    }
                }
                if (player.IsAvailable && Overlaps(zone.Position, zone.Radius, player.Position, player.Radius)
                    && Tick(targets, Entity.Null, now, zone.Settings.TickInterval, zone.Settings.PlayerInvulnerability))
                {
                    float strength = zone.Settings.ForceMode == WizardForceMode.DamageOnly ? 0 :
                        zone.Settings.ForceMode == WizardForceMode.SmallPush ? zone.Settings.PlayerPush : zone.Settings.PlayerKnockback;
                    playerHits.Add(new WizardPlayerHit { Damage = math.max(0, zone.Settings.PlayerDamage),
                        Impulse = Direction(zone.Position, player.Position) * math.max(0, strength) });
                }
                for (int i = targets.Length - 1; i >= 0; i--) if (targets[i].Seen == 0) targets.RemoveAt(i);
            }
            // A later zone can launch/kill an earlier zone's source during this same update.
            foreach (var (zone, entity) in SystemAPI.Query<RefRO<WizardZone>>().WithEntityAccess())
                if (zone.ValueRO.Follow != 0 && !removed.Contains(entity) &&
                    (!Valid(em, zone.ValueRO.Source) || em.GetComponentData<EnemyLaunchState>(zone.ValueRO.Source).Phase != EnemyLaunchPhase.Active))
                    commands.DestroyEntity(entity);
            commands.Playback(em);
            candidates.Dispose();
        }

        public static bool Overlaps(float3 center, float radius, float3 target, float targetRadius) =>
            math.distancesq(center.xz, target.xz) <= math.pow(math.max(0, radius) + math.max(0, targetRadius), 2);

        public static float3 Direction(float3 center, float3 target) => math.normalizesafe(new float3(target.x - center.x, 0, target.z - center.z));

        private static bool Valid(EntityManager em, Entity target) => em.HasComponent<Enemy>(target) &&
            em.HasComponent<EnemyLaunchState>(target) && em.HasComponent<LocalTransform>(target) &&
            em.GetComponentData<EnemyLaunchState>(target).Phase != EnemyLaunchPhase.Defeated &&
            (!em.HasComponent<RespawnRequest>(target) || !em.IsComponentEnabled<RespawnRequest>(target));

        public static bool Tick(DynamicBuffer<WizardZoneTarget> targets, Entity target, double now, float interval, float protection)
        {
            int index = 0;
            for (; index < targets.Length; index++) if (targets[index].Target == target) break;
            bool entering = index == targets.Length;
            var t = entering ? new WizardZoneTarget { Target = target } : targets[index];
            // Duplicate broadphase leaves for the same entity cannot generate multiple hits.
            bool due = t.Seen == 0 && (entering || now + 1e-6 >= t.NextHitAt);
            bool hit = due && (entering || now + 1e-6 >= t.PlayerProtectedUntil);
            t.Seen = 1;
            if (due)
            {
                double step = math.max(.001f, interval);
                t.NextHitAt = entering ? now + step : t.NextHitAt + (math.floor((now + 1e-6 - t.NextHitAt) / step) + 1) * step;
            }
            if (hit) t.PlayerProtectedUntil = now + math.max(0, protection);
            if (entering) targets.Add(t); else targets[index] = t;
            return hit;
        }

        private static void HitEnemy(EntityManager em, Entity target, float3 position, WizardZone zone, double now)
        {
            var launch = em.GetComponentData<EnemyLaunchState>(target);
            var s = zone.Settings;
            bool dashing = em.HasComponent<DasherState>(target) && launch.Phase == EnemyLaunchPhase.Active &&
                em.GetComponentData<DasherState>(target).Phase == DasherPhase.Dashing;
            float3 direction = Direction(zone.Position, position);
            if (!dashing && s.ForceMode != WizardForceMode.DamageOnly && math.lengthsq(direction) > 0 && em.HasComponent<PhysicsVelocity>(target))
            {
                float strength = s.ForceMode == WizardForceMode.SmallPush ? s.EnemyPush : s.EnemyKnockback;
                var velocity = em.GetComponentData<PhysicsVelocity>(target);
                velocity.Linear += direction * math.max(0, strength);
                em.SetComponentData(target, velocity);
                if (s.ForceMode == WizardForceMode.StrongKnockback && strength > 0 && launch.Phase != EnemyLaunchPhase.Launched &&
                    em.GetComponentData<EnemyTier>(target).Value == EnemyCombatTier.Normal)
                {
                    EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.WizardZone, s.EnemyDamage, EnemyLaunchOwner.Enemy);
                    em.SetComponentData(target, launch);
                }
            }
            // Damage remains on the shared request/deferred-defeat path. No Explosive request is issued.
            if (em.GetComponentData<Health>(target).Current > 0)
            {
                var damage = em.IsComponentEnabled<DamageRequest>(target) ? em.GetComponentData<DamageRequest>(target) : default;
                damage.Amount += math.max(0, s.EnemyDamage);
                em.SetComponentData(target, damage); em.SetComponentEnabled<DamageRequest>(target, true);
                EnemyDamageResolution.ApplyPending(em, target, now);
            }
        }
    }
}
