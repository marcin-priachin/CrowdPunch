using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct ShellExploderReplenishmentSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (shell, target) in SystemAPI.Query<RefRW<ShellTarget>>().WithEntityAccess())
            {
                int alive = 0, total = 0, pending = 0;
                bool allocated = false;
                foreach (var (member, launch, ownership, entity) in
                    SystemAPI.Query<RefRO<BarricadeCrowdMember>, RefRO<EnemyLaunchState>, RefRO<EnemyWaveOwnership>>()
                        .WithAll<ExplosiveEnemyState>().WithEntityAccess())
                {
                    if (member.ValueRO.Barricade != target) continue;
                    total++;
                    pending += member.ValueRO.ShellReplacementPending;
                    if (!SystemAPI.IsComponentEnabled<RespawnRequest>(entity) && launch.ValueRO.Phase != EnemyLaunchPhase.Defeated) alive++;
                    allocated = SystemAPI.HasComponent<EnemyWaveSequence>(ownership.ValueRO.Sequence)
                        && SystemAPI.GetComponent<EnemyWaveSequence>(ownership.ValueRO.Sequence).Phase == EnemyWaveRuntimePhase.AwaitingActivation;
                }
                bool intact = shell.ValueRO.ExplosionsRemaining > 0 && shell.ValueRO.CoreHealth > 0;
                if (!intact || !allocated || total == 0)
                {
                    shell.ValueRW.ReplaceExplodersAt = -1;
                    shell.ValueRW.ReplacementBatchActive = 0;
                }
                else if (shell.ValueRO.ReplacementBatchActive != 0)
                {
                    if (pending == 0) { shell.ValueRW.ReplacementBatchActive = 0; shell.ValueRW.ReplaceExplodersAt = -1; }
                }
                else if (alive == 0)
                {
                    if (shell.ValueRO.ReplaceExplodersAt < 0) shell.ValueRW.ReplaceExplodersAt = now + shell.ValueRO.ExploderReplacementDelay;
                    if (now >= shell.ValueRO.ReplaceExplodersAt)
                    {
                        shell.ValueRW.ReplacementBatchActive = 1;
                        foreach (var member in SystemAPI.Query<RefRW<BarricadeCrowdMember>>().WithAll<ExplosiveEnemyState>())
                            if (member.ValueRO.Barricade == target) member.ValueRW.ShellReplacementPending = 1;
                    }
                }
                foreach (var (member, respawn, entity) in
                    SystemAPI.Query<RefRW<BarricadeCrowdMember>, RefRW<EnemyRespawnSettings>>()
                        .WithAll<ExplosiveEnemyState>().WithEntityAccess())
                {
                    if (member.ValueRO.Barricade != target) continue;
                    if (!intact || member.ValueRO.ShellReplacementPending != 0
                        && !SystemAPI.IsComponentEnabled<RespawnRequest>(entity)
                        && SystemAPI.GetComponent<EnemyLaunchState>(entity).Phase != EnemyLaunchPhase.Defeated)
                        member.ValueRW.ShellReplacementPending = 0;
                    respawn.ValueRW.Enabled = (byte)(intact && member.ValueRO.ShellReplacementPending != 0 ? 1 : 0);
                    if (respawn.ValueRO.Enabled == 0 || !SystemAPI.IsComponentEnabled<RespawnRequest>(entity)) continue;
                    var request = SystemAPI.GetComponent<RespawnRequest>(entity);
                    // Pooling and safe placement stay in EnemyRespawnSystem. Release both existing slots,
                    // including one that pooled while its partner was still alive; never allocate extra roots.
                    if (request.IsPooled != 0 && request.RespawnAt == double.MaxValue)
                    {
                        request.RespawnAt = now;
                        SystemAPI.SetComponent(entity, request);
                    }
                }
            }
        }
    }
}
