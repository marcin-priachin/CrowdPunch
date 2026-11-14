using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal enum AlienSpikeContactKind : byte { None, Solid, Enemy, Player }

    internal struct AlienSpikeShotContact : ICollector<ColliderCastHit>
    {
        internal ComponentLookup<Enemy> Enemies;
        internal ComponentLookup<EnemyTier> Tiers;
        internal ComponentLookup<EnemyLaunchState> Launches;
        internal ComponentLookup<Health> Health;
        internal ComponentLookup<RespawnRequest> Respawns;
        internal ComponentLookup<SnakeHead> SnakeHeads;
        internal ComponentLookup<SnakeSegment> SnakeSegments;
        internal ComponentLookup<ExplosiveEnemyState> Exploders;
        internal Entity Emitter, Target;
        internal bool PlayerOwned, Returned;
        internal AlienSpikeContactKind Kind;
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; set; }
        public int NumHits { get; private set; }

        internal static AlienSpikeShotContact Sweep(CollisionWorld world, AlienSpikeShotContact contact,
            in AlienSpikeProjectile shot, in PlayerSnapshot player, float3 start, float3 displacement)
        {
            contact.Emitter = shot.Emitter;
            contact.PlayerOwned = shot.PlayerOwned != 0;
            contact.Returned = shot.Redirected != 0;
            contact.MaxFraction = 1;
            float length = math.length(displacement);
            if (length <= .000001f) return contact;
            world.SphereCastCustom(start, shot.Tuning.Radius, displacement / length, length, ref contact,
                CollisionFilter.Default, QueryInteraction.IgnoreTriggers);
            if (player.IsAvailable && start.y <= player.Position.y + player.Radius + shot.Tuning.Radius
                && ChickenProjectileSystem.Sweep(start, start + displacement, player.Position,
                    player.Radius + shot.Tuning.Radius, out float fraction)
                && (fraction < contact.MaxFraction || contact.Kind == AlienSpikeContactKind.None))
            {
                contact.Kind = AlienSpikeContactKind.Player;
                contact.MaxFraction = fraction;
                contact.Target = Entity.Null;
            }
            return contact;
        }

        public bool AddHit(ColliderCastHit hit)
        {
            var kind = AlienSpikeContactKind.Solid;
            if (Enemies.HasComponent(hit.Entity))
            {
                var entity = hit.Entity;
                if (!PlayerOwned || !Returned && entity == Emitter
                    || !Launches.HasComponent(entity) || Launches[entity].Phase == EnemyLaunchPhase.Defeated
                    || Respawns.HasComponent(entity) && Respawns.IsComponentEnabled(entity)) return false;
                // Physical boss protection blocks a shot without granting a new damage route.
                if (Tiers.HasComponent(entity) && Tiers[entity].Value != EnemyCombatTier.Boss)
                {
                    if (!Health.HasComponent(entity) || Health[entity].Current <= 0
                        || SnakeHitRules.Protected(entity, SnakeHeads, SnakeSegments)
                        || SnakeSegments.HasComponent(entity) && SnakeSegments[entity].Detached == 0
                        || Exploders.HasComponent(entity) && Exploders[entity].HasExploded != 0) return false;
                    kind = AlienSpikeContactKind.Enemy;
                }
            }

            // Cover takes precedence when its surface coincides with the target contact.
            if (hit.Fraction == MaxFraction && NumHits > 0
                && ((byte)Kind < (byte)kind || Kind == kind && Target.Index <= hit.Entity.Index)) return false;
            MaxFraction = hit.Fraction;
            Target = hit.Entity;
            Kind = kind;
            NumHits = 1;
            return true;
        }
    }
}

