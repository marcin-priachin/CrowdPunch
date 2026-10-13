using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(ApplyImpulseSystem))]
    public partial struct DragonFlameCycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<DragonTuning>(); state.RequireForUpdate<PlayerSnapshot>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var mouths = SystemAPI.GetBufferLookup<DragonMouthFrame>(true);
            foreach (var (flame, tuning, launch, pose, life, clocks, entity) in
                SystemAPI.Query<RefRW<DragonFlameState>, RefRO<DragonTuning>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                    RefRO<EnemyLifetime>, DynamicBuffer<DragonFlameClock>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var f = ref flame.ValueRW; var t = tuning.ValueRO; var l = launch.ValueRO;
                if (f.Initialized == 0 || f.Generation != life.ValueRO.Generation)
                {
                    clocks.Clear();
                    f = new DragonFlameState { Initialized = 1, Generation = life.ValueRO.Generation,
                        Remaining = t.InitialDelay + (math.hash(new uint2((uint)entity.Index + 1, life.ValueRO.Generation)) % 10000) / 10000f * t.InitialVariation };
                }
                Advance(ref f, t, l, player.IsAvailable && math.distance(player.Position.xz, pose.ValueRO.Position.xz) <= t.EngagementRange,
                    SystemAPI.Time.DeltaTime);
                bool moving = SystemAPI.HasComponent<DesiredMovement>(entity) && SystemAPI.GetComponent<DesiredMovement>(entity).Speed > .01f;
                f.Motion = l.Phase == EnemyLaunchPhase.Active && f.Phase != DragonFlamePhase.Cooldown ? 2
                    : l.Phase == EnemyLaunchPhase.Launched || !moving ? 0 : 1;
                f.AnimationPhase = f.Motion == 2 ? f.Phase == DragonFlamePhase.Warning
                    ? math.saturate(1 - f.Remaining / math.max(.01f, t.WarningDuration)) * .45f : .45f
                    : math.frac(f.AnimationPhase + SystemAPI.Time.DeltaTime / math.max(.01f, f.Motion == 0 ? t.IdleDuration : t.MoveDuration));
                f.MouthOffset = t.MouthOffset;
                var mouth = mouths[entity];
                if (mouth.Length == 96)
                {
                    float frame = f.AnimationPhase * (f.Motion == 2 ? 31 : 32);
                    int from = (int)frame, to = f.Motion == 2 ? math.min(from + 1, 31) : (from + 1) % 32;
                    f.MouthOffset = math.lerp(mouth[f.Motion * 32 + from].Position, mouth[f.Motion * 32 + to].Position, math.frac(frame));
                }
            }
        }

        public static void Advance(ref DragonFlameState f, in DragonTuning t, in EnemyLaunchState l, bool engaged, float dt)
        {
            if (l.Phase != EnemyLaunchPhase.Active)
            { f.WasInactive = 1; f.ObservedLaunchSequence = l.LaunchSequence; f.Phase = DragonFlamePhase.Cooldown; f.Remaining = t.CooldownDuration; return; }
            if (f.WasInactive != 0 || f.ObservedLaunchSequence != l.LaunchSequence)
            { f.WasInactive = 0; f.ObservedLaunchSequence = l.LaunchSequence; f.Phase = DragonFlamePhase.Cooldown; f.Remaining = t.CooldownDuration; return; }
            f.Remaining = math.max(0, f.Remaining - dt);
            if (f.Remaining > 0) return;
            if (f.Phase == DragonFlamePhase.Cooldown)
            { if (engaged) { f.Phase = DragonFlamePhase.Warning; f.Remaining = t.WarningDuration; } }
            else if (f.Phase == DragonFlamePhase.Warning) { f.Phase = DragonFlamePhase.Active; f.Remaining = t.ActiveDuration; }
            else { f.Phase = DragonFlamePhase.Cooldown; f.Remaining = t.CooldownDuration; }
        }
    }
}
