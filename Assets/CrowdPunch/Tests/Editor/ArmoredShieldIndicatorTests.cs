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
        public void Enemy014_IndicatorTracksStagesAndHidesForBreakAndPooling()
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
                Assert.That(Resources.Load<Sprite>("ArmorShield"), Is.Not.Null);

                var em = world.EntityManager;
                Entity enemy = em.CreateEntity(typeof(Enemy), typeof(EnemyArmor), typeof(LocalTransform),
                    typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(HealthBar),
                    typeof(EnemyHealthBarPolicy), typeof(EnemyHealthBarVisibility));
                em.SetComponentData(enemy, LocalTransform.FromPosition(float3.zero));
                em.SetComponentEnabled<RespawnRequest>(enemy, false);
                em.SetComponentEnabled<EnemyHealthBarVisibility>(enemy, true);
                var bridge = world.GetOrCreateSystem<EnemyHealthBarBridgeSystem>();
                for (byte remaining = 2; remaining >= 1; remaining--)
                {
                    em.SetComponentData(enemy, new EnemyArmor { Stages = remaining });
                    bridge.Update(world.Unmanaged);
                    Transform root = canvasObject.transform.Find("Enemy Status");
                    Assert.That(root, Is.Not.Null);
                    Assert.That(root.gameObject.activeSelf, Is.True);
                    Assert.That(root.Find("Health Bar").gameObject.activeSelf, Is.False);
                    Transform shields = root.Find("Shields");
                    Assert.That(shields.gameObject.activeSelf, Is.True);
                    int visible = 0;
                    foreach (Transform icon in shields)
                    {
                        if (icon.gameObject.activeSelf) visible++;
                        Assert.That(icon.GetComponent<UnityEngine.UI.Image>().sprite, Is.Not.Null);
                    }
                    Assert.That(visible, Is.EqualTo(remaining));
                    if (remaining == 1)
                        Assert.That(((RectTransform)shields.GetChild(0)).anchoredPosition.x, Is.Zero);
                }
                em.SetComponentData(enemy, new EnemyArmor());
                bridge.Update(world.Unmanaged);
                Assert.That(canvasObject.transform.Find("Enemy Status").gameObject.activeSelf, Is.False);
                em.SetComponentData(enemy, EnemyArmor.Fresh);
                bridge.Update(world.Unmanaged);
                Assert.That(canvasObject.transform.Find("Enemy Status").gameObject.activeSelf, Is.True);
                em.SetComponentEnabled<RespawnRequest>(enemy, true);
                bridge.Update(world.Unmanaged);
                Assert.That(canvasObject.transform.Find("Enemy Status").gameObject.activeSelf, Is.False);
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
