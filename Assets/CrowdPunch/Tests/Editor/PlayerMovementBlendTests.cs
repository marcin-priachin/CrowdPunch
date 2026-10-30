using System.Reflection;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class PlayerMovementBlendTests
    {
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(-90f)]
        public void Player002_ChangingWalkingDirectionKeepsIdleOutOfTheBlend(float angle)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab"));
            try
            {
                foreach (var b in model.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
                var animator = model.GetComponent<Animator>();
                var movement = model.GetComponent<PlayerMovementAnimation>();
                typeof(PlayerMovementAnimation).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
                animator.Rebind();
                var blend = typeof(PlayerMovementAnimation).GetMethod("SetLocomotionBlend", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 100; i++) blend.Invoke(movement, new object[] { Vector3.forward, .02f });
                var target = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                for (int i = 0; i < 60; i++)
                {
                    blend.Invoke(movement, new object[] { target, .02f });
                    animator.Play("Base Layer.Locomotion", 0, i / 60f);
                    animator.Update(0f);
                    var move = new Vector2(animator.GetFloat("MoveX"), animator.GetFloat("MoveZ"));
                    Assert.That(move.magnitude, Is.EqualTo(1f).Within(.001f), "Turning must preserve blend speed");
                    foreach (var clip in animator.GetCurrentAnimatorClipInfo(0))
                        if (clip.clip.name == "Idle") Assert.That(clip.weight, Is.LessThan(.001f), "Idle must not pull the pelvis into a walking turn");
                }
                // Actual stopping still blends back to the planted idle stance.
                for (int i = 0; i < 100; i++) blend.Invoke(movement, new object[] { Vector3.zero, .02f });
                Assert.That(Mathf.Abs(animator.GetFloat("MoveX")) + Mathf.Abs(animator.GetFloat("MoveZ")), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(model); }
        }
    }
}
