using System.Reflection;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PlayerPunchCooldownTests
    {
        private GameObject root;
        private PlayerPunch punch;
        private PlayerEcsBridge bridge;
        private PlayerPunchSettings settings;
        private PlayerPunchAnimation animation;
        private Animator animator;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("PLAYER-008/009 cooldown test");
            punch = root.AddComponent<PlayerPunch>();
            bridge = root.GetComponent<PlayerEcsBridge>();
            settings = ScriptableObject.CreateInstance<PlayerPunchSettings>();
            Set(settings, "cooldown", 1.25f);
            Set(settings, "missCooldown", .25f);
            Set(punch, "settings", settings);
            Set(punch, "ecsBridge", bridge);
            // EditMode does not run the MonoBehaviour lifecycle; bind the real bridge event explicitly.
            Call(punch, "OnEnable");
        }

        [TearDown]
        public void Cleanup()
        {
            if (animation != null) Call(animation, "OnDisable");
            Call(punch, "OnDisable");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(settings);
        }

        [TestCase(true, 1.25f)]
        [TestCase(false, .25f)]
        public void Player009_OutcomeSelectsCooldownAndBlocksUntilExpiry(bool hit, float duration)
        {
            punch.RequestPunch();
            uint sequence = bridge.PunchSequence;
            punch.RequestPunch();
            Assert.That(bridge.PunchSequence, Is.EqualTo(sequence), "Still awaiting the first result");
            bridge.ReceivePunchResult(sequence, hit);
            Assert.That(punch.IsCoolingDown, Is.True);
            Assert.That(punch.CooldownProgress, Is.EqualTo(0f).Within(.001f));
            punch.RequestPunch();
            Assert.That(bridge.PunchSequence, Is.EqualTo(sequence), "Cooldown rejects another punch");
            Set(punch, "nextPunchTime", Time.time + duration * .5f);
            Assert.That(punch.CooldownProgress, Is.EqualTo(.5f).Within(.001f), "Feedback uses the selected duration");
            Set(punch, "nextPunchTime", Time.time - .01f);
            punch.RequestPunch();
            Assert.That(bridge.PunchSequence, Is.EqualTo(sequence + 1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Player009_ZeroDurationAllowsRetryAfterAcknowledgement(bool hit)
        {
            Set(settings, hit ? "cooldown" : "missCooldown", 0f);
            punch.RequestPunch();
            uint sequence = bridge.PunchSequence;
            bridge.ReceivePunchResult(sequence, hit);
            Assert.That(punch.IsCoolingDown, Is.False);
            Assert.That(punch.CooldownProgress, Is.EqualTo(1f));
            punch.RequestPunch();
            Assert.That(bridge.PunchSequence, Is.EqualTo(sequence + 1));
        }

        [Test]
        public void Player009_DuplicateAndStaleResultsCannotRestartOrChangeCooldown()
        {
            int starts = 0;
            punch.CooldownStarted += () => starts++;
            punch.RequestPunch();
            uint old = bridge.PunchSequence;
            bridge.ReceivePunchResult(old + 100, true);
            Assert.That(punch.IsCoolingDown, Is.False);
            bridge.ReceivePunchResult(old, false);
            Set(punch, "nextPunchTime", Time.time + .125f);
            bridge.ReceivePunchResult(old, true);
            Assert.That(punch.CooldownProgress, Is.EqualTo(.5f).Within(.001f));
            Assert.That(starts, Is.EqualTo(1));
            punch.ResetPunchState();
            Assert.That(punch.IsCoolingDown, Is.False);
            Assert.That(punch.CooldownProgress, Is.EqualTo(1f));
            punch.RequestPunch();
            bridge.ReceivePunchResult(old, true);
            Assert.That(punch.IsCoolingDown, Is.False, "Old result cannot resolve the new pending request");
            bridge.ReceivePunchResult(bridge.PunchSequence, true);
            Assert.That(punch.IsCoolingDown, Is.True);
            Assert.That(starts, Is.EqualTo(2));
        }

        [Test]
        public void Player009_DisableCancelsPendingAcknowledgement()
        {
            punch.RequestPunch();
            uint sequence = bridge.PunchSequence;
            Call(punch, "OnDisable");
            bridge.ReceivePunchResult(sequence, false);
            Assert.That(punch.IsCoolingDown, Is.False);
        }

        [TestCase(true, 1.25f, true)]
        [TestCase(false, .25f, true)]
        [TestCase(true, 1.25f, false)]
        [TestCase(false, .25f, false)]
        public void Player008_PunchSkipsToContactThenCooldownPlaysEndAndOpening(bool hit, float duration, bool snapOnHit)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab"), root.transform);
            animation = model.GetComponent<PlayerPunchAnimation>();
            animator = model.GetComponent<Animator>();
            Set(animation, "playerPunch", punch);
            Set(animation, "readyFrame", 2f);
            Set(animation, "contactFrame", 8f);
            Set(animation, "snapToContactOnHit", snapOnHit);
            Call(animation, "Awake");
            Call(animation, "OnEnable");
            animator.Rebind();
            var clip = (AnimationClip)typeof(PlayerPunchAnimation).GetField("punchClip", PrivateInstance).GetValue(animation);
            float normalizedFrame = 1f / clip.frameRate / clip.length;
            float ready = 2f * normalizedFrame;
            float contact = 8f * normalizedFrame;
            float lastFrame = clip.length * clip.frameRate;
            float recoveryFrames = lastFrame - 8f + 2f;
            Assert.That(lastFrame, Is.GreaterThan(8f), "The configured contact frame is inside the current trimmed clip");
            punch.ResetPunchState();
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(ready).Within(.001f), "Wait at Ready Frame before input");
            punch.RequestPunch();
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(contact).Within(.001f),
                "Accepted input must skip to Contact Frame before any hit/miss acknowledgement");
            bridge.ReceivePunchResult(bridge.PunchSequence, hit);
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(contact).Within(.001f));
            animation.ConfirmContactPose();
            Set(punch, "nextPunchTime", Time.time + duration * .5f);
            Call(animation, "Update");
            float halfwayFrame = (8f + lastFrame + 2f) * .5f;
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(halfwayFrame * normalizedFrame).Within(.001f),
                "Halfway through recovery must still be playing forward after contact");
            punch.RequestPunch();
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(halfwayFrame * normalizedFrame).Within(.001f),
                "Cooldown-rejected input must not jump back to Contact Frame");
            Set(punch, "nextPunchTime", Time.time + duration * 2f / recoveryFrames);
            Call(animation, "Update");
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(1f).Within(.001f), "Reach the clip's final frame before wrapping");
            Set(punch, "nextPunchTime", Time.time + duration / recoveryFrames);
            Call(animation, "Update");
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(normalizedFrame).Within(.001f),
                "After wrapping, play the opening frames toward ready");
            Set(punch, "nextPunchTime", Time.time - .01f);
            Call(animation, "Update");
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(ready).Within(.001f));
            punch.ResetPunchState();
            Assert.That(animator.GetFloat("PunchTime"), Is.EqualTo(ready).Within(.001f));
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private static void Call(object target, string method)
            => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
    }
}
