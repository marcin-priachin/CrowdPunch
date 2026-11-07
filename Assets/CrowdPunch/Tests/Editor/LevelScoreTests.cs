using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using NUnit.Framework;
using Unity.Entities;

namespace CrowdPunch.Tests
{
    public sealed class LevelScoreTests
    {
        [Test]
        public void Score001_DeferredDefeatRetainsLethalPunchAndScoresOnce()
        {
            using var world = new World("Level score");
            var entities = world.EntityManager;
            Entity sequence = entities.CreateEntity(typeof(EnemyWaveSequence));
            entities.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 1, UndefeatedCount = 1 });
            Entity enemy = entities.CreateEntity(typeof(Enemy), typeof(EnemyWaveOwnership), typeof(Health),
                typeof(EnemyLaunchState), typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest),
                typeof(EnemyHealthBarVisibility), typeof(RespawnRequest));
            entities.SetComponentData(enemy, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            entities.SetComponentData(enemy, new Health { Current = 10, Max = 10 });
            entities.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, OriginatingPunchSequence = 15 });
            entities.SetComponentEnabled<RespawnRequest>(enemy, false);
            entities.SetComponentEnabled<DeathRequest>(enemy, false);

            var damage = new DamageRequest { Amount = 4 };
            damage.Add(6, 10, 12);
            damage.Add(5, 10, 15);
            entities.SetComponentData(enemy, damage);
            world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);

            var counter = world.GetOrCreateSystem<EnemyWaveDefeatCountSystem>();
            using var scoreQuery = entities.CreateEntityQuery(typeof(LevelClearScore));
            Entity score = scoreQuery.GetSingletonEntity();
            counter.Update(world.Unmanaged);
            Assert.That(entities.GetComponentData<LevelClearScore>(score).Defeats, Is.Zero);

            entities.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated, OriginatingPunchSequence = 15 });
            counter.Update(world.Unmanaged);
            counter.Update(world.Unmanaged);

            var defeats = entities.GetBuffer<ScoredDefeat>(score);
            Assert.That(entities.GetComponentData<LevelClearScore>(score).Defeats, Is.EqualTo(1));
            Assert.That(defeats.Length, Is.EqualTo(1));
            Assert.That(defeats[0].PunchSequence, Is.EqualTo(12));
        }
    }
}
