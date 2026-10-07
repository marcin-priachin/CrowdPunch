using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Physics;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateAfter(typeof(ChickenProjectileSystem)), UpdateBefore(typeof(ChickenPlayerImpactSystem))]
    [UpdateBefore(typeof(Lifetime.BossCrowdReplenishmentSystem))]
    public partial struct ChickenDamageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager;
            foreach(var (boss,health,tuning,hits,history,entity) in
                SystemAPI.Query<RefRW<ChickenBoss>,RefRW<Health>,RefRO<ChickenTuning>,DynamicBuffer<ChickenHit>,DynamicBuffer<ChickenHitHistory>>().WithEntityAccess())
            {
                for(int i=history.Length-1;i>=0;i--)
                {
                    var old=history[i];
                    if(!em.Exists(old.Source)
                        || em.HasComponent<EnemyLaunchState>(old.Source) && em.GetComponentData<EnemyLaunchState>(old.Source).LaunchSequence!=old.Launch
                        || em.HasComponent<EnemyLifetime>(old.Source) && em.GetComponentData<EnemyLifetime>(old.Source).Generation!=old.Lifetime)
                        history.RemoveAt(i);
                }
                foreach(var hit in hits)
                {
                    bool duplicate=false;
                    foreach(var old in history) if(old.Source==hit.Source && old.Launch==hit.Launch && old.Lifetime==hit.Lifetime) { duplicate=true; break; }
                    if(duplicate) continue;
                    history.Add(new ChickenHitHistory { Source=hit.Source,Launch=hit.Launch,Lifetime=hit.Lifetime });
                    if(ChickenDamageResolution.Apply(ref boss.ValueRW,ref health.ValueRW,tuning.ValueRO,hit.Damage,SystemAPI.Time.ElapsedTime))
                        em.SetComponentData(entity,new PhysicsVelocity());
                }
                hits.Clear();
            }
        }
    }
}
