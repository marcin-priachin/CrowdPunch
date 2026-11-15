using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(AlienSpikeProjectileSystem))]
    public partial struct AlienSpikeEmissionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<AlienSpikeTuning>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasPlane = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (tuning, volley, pose, launch, sockets, bursts, emitter) in
                SystemAPI.Query<RefRO<AlienSpikeTuning>, RefRW<AlienSpikeVolley>, RefRO<LocalTransform>,
                    RefRO<EnemyLaunchState>, DynamicBuffer<AlienSpikeSocket>, DynamicBuffer<AlienSpikeBurst>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                var t = tuning.ValueRO;
                float floor = hasPlane ? ground.SurfaceHeight : pose.ValueRO.Position.y - .9f;
                if (bursts.IsEmpty && volley.ValueRO.Phase != AlienSpikeVolleyPhase.Release) continue;
                var template = new AlienSpikeProjectile
                {
                    Tuning = t.Shot, ColorGradient = t.ProjectileColor, Emitter = emitter, Remaining = t.Shot.Lifetime
                };
                if (SystemAPI.HasComponent<EnemyWaveOwnership>(emitter))
                    template.Wave = SystemAPI.GetComponent<EnemyWaveOwnership>(emitter);

                foreach (var burst in bursts)
                {
                    var shot = template;
                    shot.PlayerOwned = 1;
                    shot.Source = burst.Source;
                    shot.LaunchSequence = burst.LaunchSequence;
                    shot.SourceLifetime = burst.SourceLifetime;
                    shot.PunchSequence = burst.PunchSequence;
                    shot.ChainDepth = burst.ChainDepth;
                    Emit(commands, burst.Pose, sockets, shot, floor);
                }
                bursts.Clear();

                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    volley.ValueRW = new AlienSpikeVolley { Remaining = t.Cooldown };
                    continue;
                }

                if (volley.ValueRO.Phase != AlienSpikeVolleyPhase.Release) continue;
                Emit(commands, pose.ValueRO, sockets, template, floor);
                volley.ValueRW.Phase = AlienSpikeVolleyPhase.FollowThrough;
                volley.ValueRW.Remaining = t.FollowThroughSeconds;
            }
            commands.Playback(state.EntityManager);
        }

        private static void Emit(EntityCommandBuffer commands, in LocalTransform pose, DynamicBuffer<AlienSpikeSocket> sockets,
            AlienSpikeProjectile shot, float floor)
        {
            bool normal = shot.PlayerOwned == 0;
            foreach (var socket in sockets)
            {
                float3 position = pose.TransformPoint(normal ? socket.ReleaseTip : socket.IdleTip);
                position.y = ShotHeight(position.y, floor, shot.Tuning.Radius);
                float2 heading = normal ? socket.ReleaseDirection : socket.IdleDirection;
                float3 direction = math.rotate(pose.Rotation, new float3(heading.x, 0, heading.y));
                float2 planar = math.normalizesafe(direction.xz, new float2(0, 1));
                shot.Velocity = new float3(planar.x, 0, planar.y) * shot.Tuning.Speed;
                var entity = commands.CreateEntity();
                commands.AddComponent(entity, LocalTransform.FromPosition(position));
                commands.AddComponent(entity, shot);
                commands.AddComponent<PunchAimAssistTarget>(entity);
            }
        }

        // The grounded band spans the arena floor through the player and grounded crowd bodies.
        internal static float ShotHeight(float tipHeight, float floor, float radius)
        {
            float minimum = floor + math.min(radius + .02f, 1.45f);
            return math.clamp(tipHeight, minimum, floor + 1.45f);
        }
    }
}

