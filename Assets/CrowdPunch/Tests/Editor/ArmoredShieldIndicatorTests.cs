using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class ArmoredShieldIndicatorTests
    {
        [Test]
        public void Enemy014_ArmorNeverPublishesOverheadUi()
        {
            using var world = new World("Armored shield presentation");
            GameObject cameraObject = null, canvasObject = null;
            try
            {
                cameraObject = new GameObject("Shield test camera", typeof(UnityEngine.Camera));
                var camera = cameraObject.GetComponent<UnityEngine.Camera>();
                camera.transform.position = new Vector3(0f, 5f, -10f);
                camera.transform.LookAt(new Vector3(0f, 2f, 0f));
                canvasObject = new GameObject("Shield test canvas", typeof(RectTransform),
                    typeof(Canvas), typeof(EnemyHealthBarCanvas));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1024f, 768f);
                var view = canvasObject.GetComponent<EnemyHealthBarCanvas>();
                typeof(EnemyHealthBarCanvas).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(view, null);
                typeof(EnemyHealthBarCanvas).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(view, null);
                typeof(EnemyHealthBarCanvas).GetField("worldCamera", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, camera);

                var em = world.EntityManager;
                Entity enemy = em.CreateEntity(typeof(Enemy), typeof(EnemyArmor), typeof(LocalTransform),
                    typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(HealthBar),
                    typeof(EnemyHealthBarPolicy), typeof(EnemyHealthBarVisibility));
                em.SetComponentData(enemy, LocalTransform.FromPosition(float3.zero));
                em.SetComponentEnabled<RespawnRequest>(enemy, false);
                em.SetComponentEnabled<EnemyHealthBarVisibility>(enemy, true);
                var bridge = world.GetOrCreateSystem<EnemyHealthBarBridgeSystem>();
                foreach (byte remaining in new byte[] { 2, 1, 0, 2 })
                {
                    em.SetComponentData(enemy, new EnemyArmor { Stages = remaining });
                    bridge.Update(world.Unmanaged);
                    Assert.That(canvasObject.transform.childCount, Is.Zero,
                        "Armor is communicated by equipment, never shield icons or a health bar.");
                }
                em.SetComponentEnabled<RespawnRequest>(enemy, true);
                bridge.Update(world.Unmanaged);
                Assert.That(canvasObject.transform.childCount, Is.Zero);
            }
            finally
            {
                if (canvasObject != null)
                {
                    typeof(EnemyHealthBarCanvas).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(canvasObject.GetComponent<EnemyHealthBarCanvas>(), null);
                    Object.DestroyImmediate(canvasObject);
                }
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
