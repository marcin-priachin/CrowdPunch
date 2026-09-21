using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class EnemyPrefabCollisionContactTests
    {
        [TestCase("Assets/CrowdPunch/Prefabs/Enemy.prefab")]
        [TestCase("Assets/CrowdPunch/Prefabs/EnemyBaseline.prefab")]
        [TestCase("Assets/CrowdPunch/Prefabs/EnemyRanged.prefab")]
        [TestCase("Assets/CrowdPunch/Prefabs/EnemyExplosive.prefab")]
        [TestCase("Assets/CrowdPunch/Prefabs/EnemyDasher.prefab")]
        [TestCase("Assets/CrowdPunch/Prefabs/EliteEnemy.prefab")]
        public void Combat002EnemyPrefabsProvideCollisionContacts(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing enemy prefab at {prefabPath}.");
            CapsuleCollider collider = prefab.GetComponent<CapsuleCollider>();
            Assert.That(collider, Is.Not.Null, $"{prefabPath} requires a root capsule collider.");
            Assert.That(collider.providesContacts, Is.True,
                $"{prefabPath} must provide contacts for launched-enemy propagation.");
        }
    }
}
