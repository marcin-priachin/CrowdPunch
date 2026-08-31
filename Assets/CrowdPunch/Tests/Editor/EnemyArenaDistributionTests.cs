using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class EnemyArenaDistributionTests
    {
        private static readonly ArenaBounds Arena = new ArenaBounds
        {
            Center = float3.zero,
            Extents = new float3(10f, 1f, 10f)
        };

        private static readonly EnemyMovementSettings Movement = new EnemyMovementSettings
        {
            SurroundDistance = 4f,
            SurroundRingSpacing = 1f,
            StoppingDistance = 0.5f
        };

        [Test]
        public void ArenaRelativeTargetKeepsInsetFromBounds()
        {
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                new float3(9f, 0f, 9f),
                2f,
                Movement,
                Arena);

            Assert.That(target.x, Is.InRange(-9.5f, 9.5f));
            Assert.That(target.z, Is.InRange(-9.5f, 9.5f));
            Assert.AreEqual(2f, target.y);
        }

        [Test]
        public void BlockedBaseSlotUsesAnOpenArenaDirection()
        {
            float3 player = new float3(9f, 0f, 0f);
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                player,
                0f,
                Movement,
                Arena);

            Assert.Less(target.x, player.x);
            Assert.That(math.distance(player.xz, target.xz), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void UnblockedBaseSlotIsPreserved()
        {
            float3 target = EnemyChaseSystem.GetArenaRelativeSurroundTargetForTests(
                0,
                float3.zero,
                0f,
                Movement,
                Arena);

            Assert.That(target.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(target.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ReserveTargetsCoverArenaInteriorInsteadOfOnlyEdges()
        {
            bool negativeX = false;
            bool positiveX = false;
            bool negativeZ = false;
            bool positiveZ = false;
            bool hasCentralTarget = false;

            for (int entityIndex = 0; entityIndex < 64; entityIndex++)
            {
                float3 target = EnemyChaseSystem.GetArenaDistributionTargetForTests(
                    entityIndex,
                    0f,
                    Movement,
                    Arena);

                negativeX |= target.x < -2f;
                positiveX |= target.x > 2f;
                negativeZ |= target.z < -2f;
                positiveZ |= target.z > 2f;
                hasCentralTarget |= math.abs(target.x) < 2f && math.abs(target.z) < 2f;
                Assert.That(target.x, Is.InRange(-9.5f, 9.5f));
                Assert.That(target.z, Is.InRange(-9.5f, 9.5f));
            }

            Assert.IsTrue(negativeX && positiveX && negativeZ && positiveZ);
            Assert.IsTrue(hasCentralTarget);
        }

        [Test]
        public void ReserveTargetIsStableForAnEnemy()
        {
            float3 first = EnemyChaseSystem.GetArenaDistributionTargetForTests(23, 0f, Movement, Arena);
            float3 second = EnemyChaseSystem.GetArenaDistributionTargetForTests(23, 0f, Movement, Arena);

            Assert.AreEqual(first, second);
        }
    }
}
