using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class SporellaShotEmission
    {
        internal static SporellaProjectile Create(Entity arena, Entity emitter, uint lifetime,
            SporellaShotKind kind, in SporellaSettings settings, in RangedEnemySettings ranged)
            => new SporellaProjectile
            {
                Arena = arena, Emitter = emitter, EmitterLifetime = lifetime, Kind = kind,
                Radius = math.max(.01f, ranged.ProjectileRadius), Range = math.max(.01f, settings.Range),
                Speed = math.max(.01f, settings.ForwardSpeed), EnemyDamage = math.max(.01f, settings.EnemyDamage),
                PlayerDamage = ranged.ProjectileDamage, PlayerInvincibility = ranged.PlayerInvincibilitySeconds,
                PlayerLayers = ranged.ProjectilePlayerLayers, CanRedirect = settings.CanRedirect
            };

        internal static void FireSweepShot(EntityCommandBuffer commands, SporellaProjectile shot,
            in SporellaSweep sweep, int shotsRemaining)
        {
            shot.Direction = SporellaShotGeometry.ShotDirection(sweep, sweep.ShotCount - shotsRemaining, out _);
            var entity = commands.CreateEntity();
            commands.AddComponent(entity, LocalTransform.FromPosition(sweep.Origin));
            commands.AddComponent(entity, shot);
            if (shot.CanRedirect || shot.Kind != SporellaShotKind.EnemyShot)
                commands.AddBuffer<BodyProjectileHit>(entity);
        }

        internal static float Interval(in SporellaSettings settings) => math.max(.01f, settings.ShotInterval);
    }
}
