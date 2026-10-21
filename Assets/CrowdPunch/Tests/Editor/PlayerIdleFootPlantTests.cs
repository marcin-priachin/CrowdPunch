using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PlayerIdleFootPlantTests
    {
        private GameObject model;
        private Animator animator;
        private Transform left, right;

        [SetUp]
        public void Setup()
        {
            model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab"));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            animator = model.GetComponent<Animator>();
            animator.Rebind();
            left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var settings = new SerializedObject(model.GetComponent<PlayerPunchAnimation>());
            var clip = (AnimationClip)settings.FindProperty("punchClip").objectReferenceValue;
            animator.SetFloat("PunchTime", settings.FindProperty("readyFrame").floatValue / clip.frameRate / clip.length);
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(model);

        [Test]
        public void Player002_IdleKeepsBothBootsPlantedForWholeLoop()
        {
            Sample(Vector2.zero, 0f);
            Vector3 leftPosition = left.position, rightPosition = right.position;
            Quaternion leftRotation = left.rotation, rightRotation = right.rotation;
            float minSole = float.PositiveInfinity, maxSole = float.NegativeInfinity;
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                for (int frame = 0; frame <= 80; frame++)
                {
                    Sample(Vector2.zero, frame / 80f);
                    Assert.That(Vector3.Distance(leftPosition, left.position), Is.LessThan(.0005f));
                    Assert.That(Vector3.Distance(rightPosition, right.position), Is.LessThan(.0005f));
                    Assert.That(Quaternion.Angle(leftRotation, left.rotation), Is.LessThan(.1f));
                    Assert.That(Quaternion.Angle(rightRotation, right.rotation), Is.LessThan(.1f));
                    renderer.BakeMesh(mesh);
                    mesh.RecalculateBounds();
                    float sole = mesh.bounds.min.y;
                    minSole = Mathf.Min(minSole, sole); maxSole = Mathf.Max(maxSole, sole);
                }
                Assert.That(maxSole - minSole, Is.LessThan(.0005f), "The visible sole must not bob above the authored floor");
                Assert.That(Mathf.Abs(minSole), Is.LessThan(.002f), "Soles remain close to model-space floor zero");
                Assert.That(model.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(model.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Player002_MovingReleasesIdleStanceAndReturningToIdleReplants()
        {
            Sample(Vector2.zero, 0f);
            Vector3 idleLeft = left.position, idleRight = right.position;
            foreach (var move in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(.707f, .707f) })
            {
                Sample(move, 0f);
                Vector3 initialLeft = left.position, initialRight = right.position;
                float stride = 0f;
                for (int frame = 1; frame <= 30; frame++)
                {
                    Sample(move, frame / 30f);
                    stride = Mathf.Max(stride, Vector3.Distance(initialLeft, left.position), Vector3.Distance(initialRight, right.position));
                }
                Assert.That(stride, Is.GreaterThan(.03f), "Movement clips must keep animated steps: " + move);
                Sample(Vector2.zero, .5f);
                Assert.That(Vector3.Distance(idleLeft, left.position), Is.LessThan(.0005f));
                Assert.That(Vector3.Distance(idleRight, right.position), Is.LessThan(.0005f));
            }
        }

        [Test]
        public void Player005_PunchAndCooldownPosesLeaveStationaryFeetPlanted()
        {
            Sample(Vector2.zero, 0f);
            Vector3 idleLeft = left.position, idleRight = right.position;
            foreach (float punchTime in new[] { 0f, .1f, .3f, .5f, .75f, 1f })
            {
                animator.SetFloat("PunchTime", punchTime);
                Sample(Vector2.zero, .37f);
                Assert.That(Vector3.Distance(idleLeft, left.position), Is.LessThan(.0005f));
                Assert.That(Vector3.Distance(idleRight, right.position), Is.LessThan(.0005f));
            }
        }

        private void Sample(Vector2 move, float phase)
        {
            animator.SetFloat("MoveX", move.x); animator.SetFloat("MoveZ", move.y);
            animator.Play("Base Layer.Locomotion", 0, phase);
            animator.Update(.00001f);
        }
    }
}
