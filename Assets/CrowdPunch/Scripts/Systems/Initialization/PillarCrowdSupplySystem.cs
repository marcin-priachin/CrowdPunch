using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
namespace CrowdPunch.Systems.Initialization
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup)),UpdateAfter(typeof(EnemyWaveSpawnSystem))]
    [UpdateBefore(typeof(AI.EnemyChaseSystem))]
    public partial struct PillarCrowdSupplySystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<FallingPillar>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager;
            var physics=SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            var grid=SystemAPI.HasSingleton<NavigationGrid>()?SystemAPI.GetSingleton<NavigationGrid>():default;
            using var occupied=new NativeList<float4>(Allocator.Temp);
            using var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var (pose,clearance) in SystemAPI.Query<RefRO<LocalTransform>,RefRO<EnemySpawnClearance>>().WithAll<Enemy>())
                occupied.Add(new float4(pose.ValueRO.Position,clearance.ValueRO.Value));
            using var membersQuery=em.CreateEntityQuery(typeof(PillarCrowdMember));
            using var members=membersQuery.ToComponentDataArray<PillarCrowdMember>(Allocator.Temp);
            foreach(var (owner,sequence,waves,profiles,sequenceEntity) in SystemAPI.Query<RefRO<BossCrowdSequence>,RefRW<EnemyWaveSequence>,
                DynamicBuffer<EnemyWaveDefinition>,DynamicBuffer<EnemyWaveProfile>>().WithEntityAccess())
            {
                Entity boss=owner.ValueRO.Encounter;
                if(!em.HasComponent<DinoBoss>(boss) || sequence.ValueRO.Initialized==0 || sequence.ValueRO.Phase==EnemyWaveRuntimePhase.Invalid || waves.Length==0) continue;
                var tuning=em.GetComponentData<DinoTuning>(boss); var bossState=em.GetComponentData<DinoBoss>(boss);
                var wave=waves[0]; EnemySpawnProfile profile=default;
                for(int i=wave.ProfileStart;i<wave.ProfileStart+wave.ProfileCount;i++)
                    if(profiles[i].Profile.Archetype==EnemyArchetypeKind.Baseline) { profile=profiles[i].Profile; break; }
                if(profile.EnemyPrefab==Entity.Null) continue;
                var random=new Random(math.max(1u,sequence.ValueRO.RandomState));
                foreach(var (pillar,pt,entity) in SystemAPI.Query<RefRO<FallingPillar>,RefRO<PillarTuning>>().WithEntityAccess())
                {
                    if(pillar.ValueRO.Boss!=boss || !PillarCrowdPlacement.Needed(pillar.ValueRO,bossState,tuning)) continue;
                    for(int slot=0;slot<tuning.EnemiesPerPillar;slot++)
                    {
                        bool exists=false; foreach(var member in members) if(member.Pillar==entity && member.Slot==slot) { exists=true; break; }
                        if(exists || !PillarCrowdPlacement.TryFind(pt.ValueRO,tuning,slot,profile.SpawnClearance,profile.NavigationRadius,
                            physics,player,grid,occupied,ref random,out var position)) continue;
                        var enemy=EnemySpawnInitialization.Create(commands,em,profile,position,ref random);
                        if(enemy==Entity.Null) continue;
                        commands.AddComponent(enemy,new PillarCrowdMember { Pillar=entity,Slot=slot });
                        commands.AddComponent(enemy,new BossCrowdMember { Encounter=boss,ReplenishDelay=wave.BossReplenishDelay });
                        commands.SetComponent(enemy,new EnemyRespawnSettings { Enabled=1 });
                        commands.AddComponent(enemy,new EnemyWaveOwnership { Sequence=sequenceEntity,RunGeneration=sequence.ValueRO.RunGeneration,WaveIndex=0 });
                        sequence.ValueRW.AmmunitionSpawnedCount++; sequence.ValueRW.UndefeatedCount++;
                        occupied.Add(new float4(position,profile.SpawnClearance));
                    }
                }
                sequence.ValueRW.RandomState=random.state;
            }
            commands.Playback(em);
        }
    }
}
