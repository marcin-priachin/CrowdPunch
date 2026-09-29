using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    public static class TrackObjectHitResolution
    {
        public static bool TryHit(EntityManager em, Entity target, Entity source, uint sequence,
            float3 direction, bool explosion, double now, float3 contact)
        {
            var track = em.GetComponentData<TrackObject>(target);
            var state = em.GetComponentData<TrackObjectState>(target);
            if (state.Locked != 0) return false;
            var launch = em.HasComponent<EnemyLaunchState>(source) ? em.GetComponentData<EnemyLaunchState>(source) : default;
            if (!explosion && (launch.Phase != EnemyLaunchPhase.Launched
                || track.PlayerBodiesOnly != 0 && launch.Owner != EnemyLaunchOwner.Player)) return false;
            if (explosion && track.PlayerBodiesOnly != 0 && track.FilterLaunchedExplosions != 0
                && launch.Phase == EnemyLaunchPhase.Launched && launch.Owner != EnemyLaunchOwner.Player) return false;
            var history = em.GetBuffer<BarricadeHitHistory>(target);
            foreach (var hit in history)
                if (hit.Source == source && hit.LaunchSequence == sequence) return false;
            direction.y = 0;
            float along = math.dot(math.normalizesafe(direction), track.Direction);
            int step = math.abs(along) <= .00001f ? 0 : along > 0 ? 1 : -1;
            int next = math.clamp(state.TargetStep + step, 0, track.RequiredNetHits);
            bool changes = next != state.TargetStep;
            if (changes || track.ConsumeNonMovingHits != 0)
                history.Add(new BarricadeHitHistory { Source = source, LaunchSequence = sequence });
            if (!changes) return false;
            if (state.Moving == 0) em.GetBuffer<TrackPushHistory>(target).Clear();
            TrackObjectMotion.Retarget(track, ref state, next);
            em.SetComponentData(target, state);
            var wall = em.GetComponentData<Barricade>(target);
            wall.HitSequence++; wall.LastHitTime = now; wall.LastHitPosition = contact;
            em.SetComponentData(target, wall);
            return true;
        }
    }
}
