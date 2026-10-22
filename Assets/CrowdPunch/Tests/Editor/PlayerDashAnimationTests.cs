using System.Reflection;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PlayerDashAnimationTests
    {
        private GameObject root;
        private Animator animator;
        private PlayerController controller;
        private PlayerMovementAnimation movement;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Dash animation test");
            root.SetActive(false);
            controller = root.AddComponent<PlayerController>();
            Set("settings", AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/CrowdPunch/Data/Settings/PlayerMovementSettings.asset"));
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab"), root.transform);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            animator = model.GetComponent<Animator>();
            movement = model.GetComponent<PlayerMovementAnimation>();
            root.SetActive(true);
            typeof(PlayerMovementAnimation).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
            animator.Rebind();
            animator.applyRootMotion = false;
            animator.Update(0f);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(root);

        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        [TestCase(-1f, 0f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        [TestCase(-1f, -1f)]
        public void Player002_DashFollowsCommittedDirectionAndDuration(float x, float z)
        {
            Vector3 direction = new Vector3(x, 0f, z).normalized;
            Set("dashDirection", direction);
            Set("dashActive", true);
            Set("dashTimeRemaining", controller.MovementSettings.DashDuration * .5f);
            animator.SetFloat("PunchTime", .37f);
            Tick();
            animator.Update(0f);
            animator.Update(.05f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Dash"), Is.True);
            Assert.That(animator.GetFloat("DashX"), Is.EqualTo(direction.x).Within(.0001f));
            Assert.That(animator.GetFloat("DashZ"), Is.EqualTo(direction.z).Within(.0001f));
            Assert.That(animator.GetFloat("DashSpeed"), Is.EqualTo(1f / controller.MovementSettings.DashDuration).Within(.0001f));
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(.37f).Within(.0001f), "PLAYER-005 punch timing stays independent");
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.identity), Is.LessThan(.001f));

            Set("dashActive", false);
            Tick();
            animator.Update(0f);
            animator.Update(.08f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [Test]
        public void Player002_DisablingPresentationClearsDashPose()
        {
            Set("dashDirection", Vector3.right);
            Set("dashActive", true);
            Tick();
            animator.Update(0f);
            animator.Update(.05f);
            typeof(PlayerMovementAnimation).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [TestCase(0f, 1f, .12f)]
        [TestCase(0f, -1f, .3f)]
        [TestCase(-1f, 0f, .6f)]
        [TestCase(1f, 0f, .8f)]
        [TestCase(1f, 1f, .4f)]
        public void Player002_PlaybackCompletesOnceOverDashDuration(float x, float z, float duration)
        {
            var settings = Object.Instantiate(controller.MovementSettings);
            try
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("dashDuration").floatValue = duration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Set("settings", settings);
                Set("dashActive", true);
                Set("dashDirection", new Vector3(x, 0f, z).normalized);
                Tick();
                animator.Update(0f);
                for (int step = 1; step <= 20; step++)
                {
                    animator.Update(duration / 20f);
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    if (step <= 10 && animator.IsInTransition(0))
                        state = animator.GetNextAnimatorStateInfo(0);
                    Assert.That(state.normalizedTime, Is.EqualTo(step / 20f).Within(.025f),
                        "Playback must advance naturally once from start to end over the dash duration");
                    Assert.That(state.loop, Is.False);
                }
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private void Tick() => typeof(PlayerMovementAnimation)
            .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);

        private void Set(string field, object value) => typeof(PlayerController)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, value);
    }
}
