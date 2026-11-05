using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PillarImpactEffectTests
    {
        [TestCase(PillarPhase.Falling)]
        [TestCase(PillarPhase.Waiting)]
        [TestCase(PillarPhase.Consumed)]
        public void Pillar006_LandingAndBossContactFireOnceEvenAfterMissedPresentation(PillarPhase phase)
        {
            using var world = new World("Pillar effects");
            var em = world.EntityManager;
            var go = new GameObject("Effect bridge");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            var positions = new List<Vector3>();
            int bossHits = 0, dust = 0;
            bridge.PillarImpactReceived += (p, d, w, boss) => { positions.Add(p); if (boss) bossHits++; else dust++; };
            PlayerBridgeRegistry.Register(bridge);
            try
            {
                var boss = em.CreateEntity(typeof(LocalTransform));
                em.SetComponentData(boss, LocalTransform.FromPosition(new float3(8, 4, 0)));
                var pillar = em.CreateEntity(typeof(FallingPillar), typeof(PillarTuning), typeof(PillarFeedbackState));
                em.SetComponentData(pillar, new PillarTuning { Height = 18, Width = 3, InitialPosition = new float3(0,-1,0) });
                var falling = new FallingPillar { Boss = boss, FallSequence = 1, Phase = phase,
                    HitBoss = 1, Angle = math.PI * .5f, Direction = new float3(1,0,0) };
                em.SetComponentData(pillar, falling);
                var system = world.GetOrCreateSystem<PillarImpactEffectSystem>();
                system.Update(world.Unmanaged); system.Update(world.Unmanaged);
                Assert.AreEqual(1, bossHits); Assert.AreEqual(5, dust);
                Assert.AreEqual(8, positions[0].x, .001f);
                Assert.AreEqual(18, positions[5].x, .001f);
                em.SetComponentData(pillar, new FallingPillar());
                system.Update(world.Unmanaged);
                Assert.AreEqual(6, positions.Count, "Retry must not replay the old effect.");
                em.SetComponentData(pillar, falling);
                system.Update(world.Unmanaged);
                Assert.AreEqual(2, bossHits); Assert.AreEqual(10, dust, "A new run can reuse fall sequence one.");
            }
            finally { PlayerBridgeRegistry.Unregister(bridge); Object.DestroyImmediate(go); }
        }
    }
}
