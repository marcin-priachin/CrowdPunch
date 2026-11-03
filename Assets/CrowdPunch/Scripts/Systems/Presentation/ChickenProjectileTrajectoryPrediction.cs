using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // PLAYER-003/004, CHICKEN-003/004: forecast only the returned shot, never its launched chains.
    internal static class ChickenProjectileTrajectoryPrediction
    {
        internal static void Trace(EntityManager em, Entity shot, float3 position, float3 direction,
            in EnemyLaunchSettings launchSettings, in PlayerSnapshot player, float maximumSeconds,
            int steps, float timestep, NativeList<float3> points, NativeList<LaunchTrajectoryImpact> impacts)
        {
            points.Clear(); impacts.Clear();
            var projectile = em.GetComponentData<ChickenProjectile>(shot);
            if (!em.HasComponent<ChickenTuning>(projectile.Boss) || timestep <= 0f) return;
            var tuning = em.GetComponentData<ChickenTuning>(projectile.Boss);
            var bossPosition = em.GetComponentData<LocalTransform>(projectile.Boss).Position;
            projectile.Velocity = math.normalizesafe(direction) * tuning.ReturnSpeed;
            projectile.Redirected = 1;
            projectile.Age = 0f; projectile.Bounces = 0;
            projectile.HomingTarget = em.HasComponent<PunchAimAssistTarget>(shot)
                ? em.GetComponentData<PunchAimAssistTarget>(shot).Target : Entity.Null;
            projectile.HomingSecondsRemaining = projectile.HomingTarget != Entity.Null
                ? launchSettings.LaunchHomingDuration : 0f;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Enemy>(),
                ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyLaunchState>(),
                ComponentType.ReadOnly<EnemyTier>(), ComponentType.ReadOnly<EnemyContactDamageSettings>(),
                ComponentType.Exclude<RespawnRequest>());
            using var enemies = query.ToEntityArray(Allocator.Temp);
            using var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var tiers = query.ToComponentDataArray<EnemyTier>(Allocator.Temp);
            using var launches = query.ToComponentDataArray<EnemyLaunchState>(Allocator.Temp);
            using var contacts = query.ToComponentDataArray<EnemyContactDamageSettings>(Allocator.Temp);
            using var hitEnemies = new NativeHashSet<Entity>(math.max(1, enemies.Length), Allocator.Temp);
            points.Add(position);
            float horizon = math.min(math.max(0f, maximumSeconds), tuning.Lifetime);
            float elapsed = 0f;
            for (int step = 0; step < steps && elapsed < horizon; step++)
            {
                float dt = math.min(timestep, horizon - elapsed);
                ChickenProjectileSystem.Guide(em, shot, ref projectile, position, launchSettings, dt);
                // Once guidance ends, fixed-speed straight flight has identical swept contacts
                // across step boundaries. Batch it without expanding the fixed-step budget.
                if (projectile.HomingTarget == Entity.Null)
                {
                    dt = math.min((steps - step) * timestep, horizon - elapsed);
                    step = steps - 1;
                }
                float remaining = dt;
                for (int segment = 0; segment <= tuning.BounceLimit && remaining > .000001f; segment++)
                {
                    float wall = ChickenProjectileSystem.WallFraction(position, projectile.Velocity * remaining,
                        tuning, out float3 normal);
                    float3 end = position + projectile.Velocity * remaining * wall;
                    float stop = 1f;
                    bool consumed = false, bossHit = false;
                    if (player.IsAvailable && ChickenProjectileSystem.Sweep(position, end, player.Position,
                        tuning.ProjectileRadius + player.Radius, out float playerAt))
                    { stop = playerAt; consumed = true; }
                    if (ChickenProjectileSystem.Sweep(position, end, bossPosition,
                        tuning.ProjectileRadius + tuning.BodyRadius, out float bossAt) && bossAt < stop)
                    { stop = bossAt; consumed = true; bossHit = true; }
                    end = math.lerp(position, end, stop);
                    for (int i = 0; i < enemies.Length; i++)
                    {
                        Entity enemy = enemies[i];
                        if (hitEnemies.Contains(enemy)
                            || tiers[i].Value != EnemyCombatTier.Normal
                            || launches[i].Phase == EnemyLaunchPhase.Defeated) continue;
                        var enemyPosition = poses[i].Position;
                        float radius = tuning.ProjectileRadius + contacts[i].ContactRadius;
                        if (math.abs(enemyPosition.y - position.y) > radius
                            || !ChickenProjectileSystem.Sweep(position, end, enemyPosition, radius, out float at)) continue;
                        hitEnemies.Add(enemy);
                        projectile.HomingTarget = Entity.Null; projectile.HomingSecondsRemaining = 0f;
                        if (!ArmorHitResolution.IsProtected(em, enemy))
                            impacts.Add(new LaunchTrajectoryImpact { Position = math.lerp(position, end, at), Direction = projectile.Velocity });
                    }
                    points.Add(end); position = end;
                    if (bossHit)
                        impacts.Add(new LaunchTrajectoryImpact { Position = position, Direction = projectile.Velocity });
                    if (consumed) return;
                    if (wall >= 1f) break;
                    remaining *= 1f - wall;
                    ChickenProjectileSystem.Reflect(ref projectile, normal);
                    if (projectile.Bounces >= tuning.BounceLimit) return;
                    position += normal * .001f;
                }
                elapsed += dt;
            }
        }
    }
}
