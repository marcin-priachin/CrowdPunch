using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class AlienSpikeBaker : Baker<AlienSpikeAuthoring>
    {
        public override void Bake(AlienSpikeAuthoring authoring)
        {
            if (authoring.settings == null || authoring.projectileAttack == null) return;
            var settings = authoring.settings;
            DependsOn(settings);
            DependsOn(authoring.projectileAttack);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var tuning = new AlienSpikeTuning
            {
                VolleyMode = settings.volleyMode,
                Cooldown = math.max(.01f, settings.cooldown),
                TelegraphSeconds = math.max(.01f, settings.telegraphDuration),
                EngagementRadius = math.max(.5f, settings.engagementRadius),
                PreferredDistance = math.clamp(settings.preferredFiringDistance, .5f, math.max(.5f, settings.engagementRadius)),
                FollowThroughSeconds = authoring.projectileAttack.length * (1 - AlienSpikeAuthoring.ReleasePhase),
                Shot = new AlienSpikeShotTuning
                {
                    ExploderMode = settings.exploderMode, AimMode = settings.aimMode,
                    Speed = math.max(.1f, settings.speed), Radius = math.max(.01f, settings.radius),
                    Lifetime = math.max(.1f, settings.lifetime),
                    NormalDamage = math.max(0, settings.normalDamage), BurstDamage = math.max(0, settings.burstDamage),
                    PlayerProtection = math.max(0, settings.playerInvincibility)
                }
            };
            AddComponent(entity, tuning);
            AddComponent(entity, new AlienSpikeVolley { Remaining = tuning.Cooldown });
            var sockets = AddBuffer<AlienSpikeSocket>(entity);
            foreach (var socket in authoring.sockets)
                sockets.Add(new AlienSpikeSocket
                {
                    IdleTip = socket.idleTip, ReleaseTip = socket.releaseTip,
                    IdleDirection = socket.idleDirection, ReleaseDirection = socket.releaseDirection
                });
            AddBuffer<AlienSpikeBurst>(entity);
            AddBuffer<AlienSpikeBurstHistory>(entity);
        }
    }
}

