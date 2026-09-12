using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CombatFeedbackTests
    {
        [Test]
        public void Player009FreezeAndExceptionalSlowMotionExpireIndependently()
        {
            Assert.That(FeedbackTimeController.ResolveScale(.8f, false, 1, 1.03, 1.08, .3f), Is.Zero);
            Assert.That(FeedbackTimeController.ResolveScale(.8f, false, 1.04, 1.03, 1.08, .3f), Is.EqualTo(.24f).Within(.0001f));
            Assert.That(FeedbackTimeController.ResolveScale(.8f, false, 1.1, 1.03, 1.08, .3f), Is.EqualTo(.8f));
            Assert.That(FeedbackTimeController.ResolveScale(.8f, true, 3, 1.03, 1.08, .3f), Is.Zero);
        }

        [Test]
        public void Loop006PauseDuringFreezeNeverCapturesZeroAsResumeScale()
        {
            float original = Time.timeScale;
            var go = new GameObject("time ownership test");
            try
            {
                Time.timeScale = 1f;
                var controller = go.AddComponent<FeedbackTimeController>();
                controller.SendMessage("Awake"); // EditMode does not dispatch MonoBehaviour lifecycle.
                FeedbackTimeController.Freeze(.03f);
                Assert.That(Time.timeScale, Is.Zero);
                FeedbackTimeController.SetPaused(true);
                FeedbackTimeController.SetTransition(true);
                FeedbackTimeController.SetPaused(false);
                Assert.That(Time.timeScale, Is.Zero);
                FeedbackTimeController.SetTransition(false);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                FeedbackTimeController.Slow(.3f, .08f);
                FeedbackTimeController.Freeze(.03f);
                FeedbackTimeController.CancelEffects();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                FeedbackTimeController.Freeze(.03f);
                controller.SendMessage("OnDisable");
                Object.DestroyImmediate(go);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { if (go != null) Object.DestroyImmediate(go); Time.timeScale = original; }
        }

        [Test]
        public void Combat014RepunchStartsNewPresentationChainWithoutChangingDamage()
        {
            var launch = new EnemyLaunchState { FeedbackChainDepth = 17, LaunchSequence = 5 };
            EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10f);
            Assert.That(launch.FeedbackChainDepth, Is.EqualTo(1));
            Assert.That(launch.LaunchSequence, Is.EqualTo(6));
            Assert.That(launch.LaunchDamage, Is.EqualTo(10f));
            EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.ElitePunch, 15f);
            Assert.That(launch.FeedbackChainDepth, Is.Zero);
            Assert.That(launch.Owner, Is.EqualTo(EnemyLaunchOwner.Enemy));
        }

        [Test]
        public void Vision005MailboxRetainsPunchAndRejectsSustainedContact()
        {
            var f = new EnemyImpactFeedback();
            var launch = new EnemyLaunchState { Owner = EnemyLaunchOwner.Player, FeedbackChainDepth = 1 };
            ImpactFeedbackRecording.Record(ref f, CombatImpactKind.Punch, float3.zero, math.forward(), 10, 10, launch, 0);
            ImpactFeedbackRecording.Record(ref f, CombatImpactKind.Environment, math.up(), math.up(), 20, 20, launch, 0);
            Assert.That(f.Kind, Is.EqualTo(CombatImpactKind.Punch));
            f.Pending = 0; f.NextContactTime = 2;
            ImpactFeedbackRecording.Record(ref f, CombatImpactKind.EnemyCollision, float3.zero, math.up(), 20, 20, launch, 1);
            Assert.That(f.Pending, Is.Zero);
        }

        [Test]
        public void Info004DeformationRestoresAuthoredScaleAndNeverTouchesPhysicsTransform()
        {
            using var world = new World("render feedback test");
            var em = world.EntityManager;
            Entity owner = em.CreateEntity(typeof(EnemyImpactFeedback), typeof(LocalTransform));
            var physics = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.identity, 2f);
            em.SetComponentData(owner, physics);
            Entity visual = em.CreateEntity(typeof(EnemyVisualOwner), typeof(EnemyVisualDeformation),
                typeof(LocalToWorld), typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(visual, new EnemyVisualOwner { Value = owner });
            var authored = float4x4.TRS(new float3(3, 2, 1), quaternion.RotateY(.4f), new float3(.8f, 1.4f, .6f));
            em.SetComponentData(visual, new LocalToWorld { Value = authored });
            em.SetComponentData(owner, new EnemyImpactFeedback { VisualRemaining = .1f, VisualDuration = .1f,
                VisualDirection = new float3(1, 0, 0), Squash = .2f, Intensity = 1 });
            var system = world.GetOrCreateSystem<EnemyImpactVisualSystem>();
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            var first = em.GetComponentData<LocalToWorld>(visual).Value;
            for (int i = 0; i < 20; i++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(first));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(physics));
            em.SetComponentData(owner, new EnemyImpactFeedback());
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(authored));
        }

        [Test]
        public void Combat001TrailPoolClearsOnLaunchEndAndReusesCapacity()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            settings.MaximumTrails = 1;
            var go = new GameObject("trail pool test");
            var material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            try
            {
                var pool = new LaunchedTrailPool(go.transform, settings, material);
                pool.Begin(); pool.Show(1, 1, Vector3.zero, 20, 1); pool.End();
                var trail = go.GetComponentInChildren<TrailRenderer>();
                Assert.That(trail.emitting, Is.True);
                pool.Begin(); pool.End();
                Assert.That(trail.emitting, Is.False);
                Assert.That(trail.positionCount, Is.Zero);
                pool.Begin(); pool.Show(2, 1, Vector3.one * 100, 20, 4); pool.End();
                Assert.That(trail.emitting, Is.True);
                Assert.That(go.GetComponentsInChildren<TrailRenderer>().Length, Is.EqualTo(1));
                pool.Clear();
                Assert.That(trail.positionCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(material); Object.DestroyImmediate(settings); }
        }
    }
}
