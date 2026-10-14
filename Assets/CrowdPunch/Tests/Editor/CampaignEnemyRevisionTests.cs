using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using NUnit.Framework;
using UnityEditor;

namespace CrowdPunch.Tests
{
    public sealed class CampaignEnemyRevisionTests
    {
        private const string WaveFolder = "Assets/CrowdPunch/Data/Campaign/Waves";
        private static readonly Dictionary<string, string> ProfileNames = new()
        {
            ["B"] = "EnemySpawnSettings", ["R"] = "RangedEnemySpawnSettings",
            ["X"] = "ExplosiveEnemySpawnSettings", ["D"] = "EnemyDasherSpawnSettings",
            ["E"] = "EliteEnemySpawnSettings", ["A"] = "ArmoredEnemySpawnSettings",
            ["W"] = "WizardEnemySpawnSettings", ["T"] = "TrailEnemySpawnSettings",
            ["LP"] = "LinkedPairEnemySpawnSettings", ["F"] = "DragonEnemySpawnSettings",
            ["S"] = "SnakeEnemySpawnSettings", ["N"] = "NinjaEnemySpawnSettings"
        };

        // Wave clearance excludes Snake orbs and includes both LinkedPair endpoints.
        // Crowd-size checks must instead include every initial physical body.
        public static int PhysicalPopulation(EnemyWaveSettings wave) =>
            wave.TotalEnemyCount + wave.LinkedPairCount * 2 + wave.EliteEnemies.Sum(e => e.Count)
            + wave.Enemies.Sum(e => e.MinimumCount *
                (e.Settings.EnemyPrefab.GetComponent<SnakeAuthoring>()?.settings.initialTailSegmentCount ?? 0));

        private static EnemyWaveSettings[] Waves() => AssetDatabase.FindAssets("t:EnemyWaveSettings", new[] { WaveFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(w => w.name).ToArray();

        [TestCase(41)] [TestCase(43)] [TestCase(45)] [TestCase(48)]
        [TestCase(52)] [TestCase(55)] [TestCase(58)] [TestCase(61)]
        [TestCase(63)] [TestCase(65)] [TestCase(67)] [TestCase(68)]
        [TestCase(71)] [TestCase(73)] [TestCase(78)]
        public void LinkDragonSnakeNinja_ExactAuthoredWavesMatchAcceptedProposal(int level)
        {
            string row = File.ReadLines("Docs/Design/Campaign80Proposal.md")
                .Single(l => l.StartsWith($"| {level:00} |"));
            string[] expected = row.Split('|')[4].Split(';')[0].Split('>');
            var waves = Waves().Where(w => w.name.StartsWith($"CP{level:00}_")).ToArray();
            Assert.That(waves.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                var counts = Regex.Matches(expected[i], @"(\d+)(LP|B|R|X|D|E|A|W|T|F|S|N)\b")
                    .Cast<Match>().ToDictionary(m => ProfileNames[m.Groups[2].Value], m => int.Parse(m.Groups[1].Value));
                var actual = wave.Enemies.ToDictionary(e => e.Settings.name, e => e.MinimumCount);
                foreach (var elite in wave.EliteEnemies) actual.Add(elite.Settings.name, elite.Count);
                if (wave.LinkedPairCount > 0)
                {
                    actual.Add(wave.LinkedPairProfile.name, wave.LinkedPairCount);
                    Assert.That(wave.LinkedPairProfile.EnemyPrefab.GetComponent<LinkedPairAuthoring>()?.settings, Is.Not.Null);
                }
                Assert.That(actual, Is.EquivalentTo(counts), $"Level {level}, wave {i + 1}");
                Assert.That(wave.Enemies.Sum(e => e.MinimumCount), Is.EqualTo(wave.TotalEnemyCount));
                Assert.That(wave.Enemies.All(e => e.Weight == 0 && e.Settings.EnemyPrefab != null), Is.True);
                Assert.That(wave.SpawnMode, Is.EqualTo(EnemyWaveSpawnMode.AllAtOnce));
                Assert.That(wave.ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
                Assert.That(wave.DelayBeforeWave, Is.EqualTo(3));
                Assert.That(wave.Duration, Is.Zero);
                Assert.That(wave.ReplenishWhileBossLives, Is.False);
                Assert.That(wave.WaitForPersistentHazards, Is.EqualTo(counts.ContainsKey(ProfileNames["W"])));
                Assert.That(wave.ArmoredAmmunitionProfile != null, Is.EqualTo(counts.ContainsKey(ProfileNames["A"])));
            }
        }

        [TestCase("LP", "41,43,45,48,55,58,63,65,68,73,78")]
        [TestCase("F", "52,55,58,63,65,67,68,73,78")]
        [TestCase("S", "61,65,67,68,73,78")]
        [TestCase("N", "71,73,78")]
        public void LinkDragonSnakeNinja_IntroductionsAndEveryLaterChapterMatchProposal(string token, string expected)
        {
            var actual = Waves().Where(w => token == "LP" ? w.LinkedPairCount > 0
                : w.Enemies.Any(e => e.MinimumCount > 0 && e.Settings.name == ProfileNames[token]))
                .Select(w => int.Parse(w.name.Substring(2, 2))).Distinct().OrderBy(n => n);
            Assert.That(actual, Is.EqualTo(expected.Split(',').Select(int.Parse)));
        }

        [Test]
        public void Snake006Ninja004_CampaignReusesSixSegmentTailsAndTwoAmbushSlots()
        {
            var snake = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/SnakeEnemySpawnSettings.asset");
            var ninja = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/NinjaEnemySpawnSettings.asset");
            var dragon = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/DragonEnemySpawnSettings.asset");
            Assert.That(snake.EnemyPrefab.GetComponent<SnakeAuthoring>().settings.initialTailSegmentCount, Is.EqualTo(6));
            Assert.That(ninja.EnemyPrefab.GetComponent<NinjaAuthoring>().settings.maximumConcurrentAmbushes, Is.EqualTo(2));
            Assert.That(dragon.EnemyPrefab.GetComponent<DragonAuthoring>().settings.groundY, Is.EqualTo(-1));
        }
    }
}
