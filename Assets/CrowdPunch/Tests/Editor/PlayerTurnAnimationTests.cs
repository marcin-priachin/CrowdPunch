using System.Reflection;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PlayerTurnAnimationTests
    {
        private GameObject root;
        private PlayerController controller;
        private Animator animator;
        private PlayerTurnAnimation turn;
        private int layer;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Standing turn test");
            root.SetActive(false);
            controller = root.AddComponent<PlayerController>();
            SetController("settings", AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/CrowdPunch/Data/Settings/PlayerMovementSettings.asset"));
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab"), root.transform);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            animator = model.GetComponent<Animator>();
            turn = model.GetComponent<PlayerTurnAnimation>();
            root.SetActive(true);
            Invoke("Awake");
            Invoke("OnEnable");
            animator.Rebind();
            animator.Update(0f);
            layer = 0;
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(root);

        [TestCase(-16f, "Left Turn")]
        [TestCase(16f, "Right Turn")]
        [TestCase(179f, "Right Turn")]
        [TestCase(-179f, "Left Turn")]
        public void Player002_StationaryYawSelectsMatchingFootwork(float yaw, string state)
        {
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Tick();
            animator.Update(.22f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsName("Base Layer." + state), Is.True);
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0f, yaw, 0f)), Is.LessThan(.001f));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [Test]
        public void Player002_IdleJitterDoesNotStartFootwork()
        {
            for (int i = 0; i < 40; i++)
            {
                root.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? .1f : -.1f, 0f);
                Tick();
                animator.Update(.02f);
            }
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [Test]
        public void Player002_SmallTurnStaysIdle()
        {
            root.transform.rotation = Quaternion.Euler(0f, 6f, 0f);
            Tick();
            animator.Update(.02f);
            Assert.That(animator.IsInTransition(0), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [Test]
        public void Player002_EntryBlendsGraduallyRatherThanSnappingToTurn()
        {
            root.transform.rotation = Quaternion.Euler(0f, 16f, 0f);
            Tick();
            animator.Update(0f);
            animator.Update(.08f);
            Assert.That(animator.IsInTransition(0), Is.True, "Entry should still be blending after the old 0.08-second duration");
            Assert.That(animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Right Turn"), Is.True);
            Assert.That(animator.GetAnimatorTransitionInfo(0).normalizedTime, Is.InRange(.3f, .5f));
        }

        [Test]
        public void Player002_SustainedTurnsRepeatAndReverseWithoutDroppingTheLayer()
        {
            for (int i = 0; i < 300; i++)
            {
                root.transform.Rotate(0f, 2f, 0f);
                Tick();
                animator.Update(.02f);
                if (i > 15)
                {
                    var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0)
                        : animator.GetCurrentAnimatorStateInfo(0);
                    Assert.That(state.IsName("Base Layer.Right Turn"), Is.True);
                }
            }
            root.transform.Rotate(0f, -16f, 0f);
            Tick();
            animator.Update(.22f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsName("Base Layer.Left Turn"), Is.True);
            Invoke("OnDisable");
            animator.Update(.1f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [Test]
        public void Player002_StopsPromptlyWithoutWaitingForTheTurnClipToFinish()
        {
            root.transform.rotation = Quaternion.Euler(0f, 16f, 0f);
            Tick();
            animator.SetFloat("TurnSpeed", .65f);
            animator.Update(.22f);
            for (int i = 0; i < 9; i++)
            {
                animator.Update(.02f);
                Tick();
            }
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player002_WalkingOrDashingImmediatelyClearsStandingTurn(bool dash)
        {
            root.transform.rotation = Quaternion.Euler(0f, 16f, 0f);
            Tick();
            animator.Update(.22f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Right Turn"), Is.True);
            if (dash)
            {
                SetController("dashActive", true);
                SetController("dashDirection", Vector3.forward);
                var movement = animator.GetComponent<PlayerMovementAnimation>();
                typeof(PlayerMovementAnimation).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
                typeof(PlayerMovementAnimation).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
            }
            else SetController("<LocomotionVelocity>k__BackingField", Vector3.forward);
            root.transform.Rotate(0f, 6f, 0f);
            Tick();
            animator.Update(.15f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(dash ? "Base Layer.Dash" : "Base Layer.Locomotion"), Is.True);
        }

        [TestCase("Left Turn")]
        [TestCase("Right Turn")]
        public void Player005_TurnMatchesSourcePelvisAndLegsWhilePreservingUpperBodyPose(string state)
        {
            var source = Object.Instantiate(animator.gameObject);
            var referenceController = new AnimatorController();
            try
            {
                source.transform.SetParent(root.transform, false);
                foreach (var behaviour in source.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                referenceController.AddLayer("Reference");
                var referenceState = referenceController.layers[0].stateMachine.AddState("Source");
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CrowdPunch/Animation/Player" + state.Replace(" ", "") + ".anim");
                referenceState.motion = clip;
                referenceState.iKOnFeet = true;
                referenceState.writeDefaultValues = false;
                referenceController.layers[0].stateMachine.defaultState = referenceState;
                var reference = source.GetComponent<Animator>();
                reference.runtimeAnimatorController = referenceController;
                reference.Rebind();
                var punch = animator.GetComponent<PlayerPunchAnimation>();
                typeof(PlayerPunchAnimation).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(punch, null);
                animator.SetFloat("TurnSpeed", 1f);
                var bones = new[] { HumanBodyBones.Spine, HumanBodyBones.Head,
                    HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
                var lower = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
                foreach (float punchPhase in new[] { .1f, .37f, .7f })
                {
                    SetPunchPose(punch, punchPhase);
                    animator.Play("Base Layer.Locomotion", 0, 0f);
                    animator.Update(0f);
                    CorrectSpine(punch);
                    var rotations = new Quaternion[bones.Length];
                    for (int i = 0; i < bones.Length; i++) rotations[i] = animator.GetBoneTransform(bones[i]).rotation;
                    for (int frame = 0; frame < 30; frame++)
                    {
                        SetPunchPose(punch, punchPhase);
                        animator.Play("Base Layer." + state, 0, frame / 30f);
                        animator.Update(0f);
                        reference.Play("Source", 0, frame / 30f);
                        reference.Update(0f);
                        CorrectSpine(punch);
                        foreach (var bone in lower)
                        {
                            Assert.That(Quaternion.Angle(animator.GetBoneTransform(bone).rotation, reference.GetBoneTransform(bone).rotation),
                                Is.LessThan(.1f), bone + " must match the source, without idle pelvis or IK distortion");
                            Assert.That(Vector3.Distance(animator.GetBoneTransform(bone).position, reference.GetBoneTransform(bone).position),
                                Is.LessThan(.0005f), bone + " position must match the source");
                        }
                        for (int i = 0; i < bones.Length; i++)
                            Assert.That(Quaternion.Angle(rotations[i], animator.GetBoneTransform(bones[i]).rotation),
                                Is.LessThan(.1f), bones[i] + " must retain the independent punch pose");
                    }
                }
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(referenceController); }
        }

        private void SetPunchPose(PlayerPunchAnimation punch, float phase)
        {
            typeof(PlayerPunchAnimation).GetMethod("RestoreAnimatedRotation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(punch, null);
            var clip = (AnimationClip)typeof(PlayerPunchAnimation).GetField("punchClip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(punch);
            typeof(PlayerPunchAnimation).GetMethod("SetPose", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(punch, new object[] { phase * clip.length });
        }

        private void CorrectSpine(PlayerPunchAnimation punch) => typeof(PlayerPunchAnimation)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(punch, null);

        private void Tick() => typeof(PlayerTurnAnimation).GetMethod("UpdateTurn",
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(turn, new object[] { .02f });

        private void Invoke(string method) => typeof(PlayerTurnAnimation).GetMethod(method,
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(turn, null);

        private void SetController(string field, object value) => typeof(PlayerController).GetField(field,
            BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, value);
    }
}
