using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.Camera;
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
        public void Vision005ComicCrowdSaturationKeepsPlayerAccentAndExpiresDuringFreeze()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            settings.ParticlePoolSizePerEffect = 1;
            var go = new GameObject("comic pool test");
            var view = new GameObject("comic camera test").AddComponent<UnityEngine.Camera>();
            var material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            float scale = Time.timeScale;
            ComicImpactPool pool = null;
            try
            {
                pool = new ComicImpactPool(go.transform, settings, material);
                for (int i = 0; i < 200; i++)
                    pool.Show(CombatImpactKind.EnemyCollision, Vector3.zero, Vector3.right, 1);
                Assert.That(pool.ActiveCount, Is.EqualTo(pool.Capacity));
                pool.Show(CombatImpactKind.PlayerDamage, Vector3.zero, Vector3.right, 1);
                pool.Tick(0, view);
                Assert.That(go.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
                var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(System.Array.Exists(mesh.colors, c => c.r > .95f && c.g < .3f), Is.True,
                    "Saturation must not hide the coral player-damage accent.");
                Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(768 * pool.Capacity));
                Time.timeScale = 0;
                pool.Tick(.4f, view);
                Assert.That(pool.ActiveCount, Is.Zero);
                Assert.That(go.GetComponentInChildren<MeshRenderer>().enabled, Is.False);
                pool.Show(CombatImpactKind.Punch, Vector3.one, Vector3.forward, 1);
                pool.Tick(.02f, view);
                Assert.That(pool.ActiveCount, Is.EqualTo(1));
                pool.Clear();
                Assert.That(mesh.vertexCount, Is.Zero);
            }
            finally
            {
                pool?.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(material); Object.DestroyImmediate(settings);
                Time.timeScale = scale;
            }
        }

        [Test]
        public void Player004CameraSpringIsBoundedRestoresExactlyAndNeverRotatesAim()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            var go = new GameObject("camera recoil test");
            go.AddComponent<UnityEngine.Camera>();
            var feedback = go.AddComponent<CombatCameraFeedback>();
            go.transform.SetPositionAndRotation(new Vector3(3, 7, -4), Quaternion.Euler(35, 20, 0));
            Vector3 baseline = go.transform.position;
            Quaternion aim = go.transform.rotation;
            try
            {
                feedback.Configure(settings);
                for (int i = 0; i < 100; i++) feedback.Impulse(Vector3.right, .3f, 1);
                for (int i = 0; i < 120; i++)
                {
                    feedback.RemoveOffset(); feedback.Advance(1f / 60f);
                    Assert.That(Vector3.Distance(go.transform.position, baseline),
                        Is.LessThanOrEqualTo(settings.MaximumCameraOffset * settings.CameraMotionScale + .00001f));
                    Assert.That(go.transform.rotation, Is.EqualTo(aim));
                }
                Assert.That(Vector3.Distance(go.transform.position, baseline), Is.LessThan(.00001f));
                feedback.RemoveOffset();
                settings.CameraMotionScale = 0;
                feedback.Impulse(Vector3.up, .3f, 1);
                feedback.Advance(.02f);
                Assert.That(go.transform.position, Is.EqualTo(baseline));
                feedback.ResetFeedback();
                Assert.That(go.transform.position, Is.EqualTo(baseline));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(settings); }
        }

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
                typeof(FeedbackTimeController).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null); // EditMode does not dispatch MonoBehaviour lifecycle.
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
                typeof(FeedbackTimeController).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null);
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

        [TestCase(false)]
        [TestCase(true)]
        public void Info004AtlasFeedbackPreservesPaletteAndRenderAccentsDoNotAccumulate(bool exploder)
        {
            using var world = new World("atlas presentation test");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(EnemyImpactFeedback), typeof(EnemyLaunchState),
                typeof(CrowdPunch.Components.EnemyArchetype), typeof(LocalTransform), typeof(Unity.Physics.PhysicsVelocity));
            em.SetComponentData(owner, new CrowdPunch.Components.EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
            if (exploder) em.AddComponent<ExplosiveEnemyState>(owner);
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new Unity.Physics.PhysicsVelocity { Linear = new float3(0, 0, 3) });
            var visual = em.CreateEntity(typeof(EnemyVisualOwner), typeof(EnemyVisualDeformation),
                typeof(LocalToWorld), typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(visual, new EnemyVisualOwner { Value = owner, UsesAtlas = 1 });
            em.SetComponentData(visual, new LocalToWorld { Value = float4x4.identity });
            var colors = world.GetOrCreateSystem<EnemyReadabilitySystem>();
            var accents = world.GetOrCreateSystem<EnemyImpactVisualSystem>();
            for (int i = 0; i < 300; i++)
            {
                world.SetTime(new Unity.Core.TimeData(i / 60d, 1 / 60f));
                colors.Update(world.Unmanaged); accents.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var matrix = em.GetComponentData<LocalToWorld>(visual).Value;
                Assert.That(math.length(matrix.c0.xyz), Is.InRange(.97f, exploder ? 1.09f : 1.03f));
                Assert.That(matrix.c3.y, Is.InRange(0f, .036f));
            }
            Assert.That(em.GetComponentData<URPMaterialPropertyBaseColor>(visual).Value.xyz, Is.EqualTo(new float3(1.15f)));
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            colors.Update(world.Unmanaged); accents.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            if (!exploder) Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(float4x4.identity));
            else Assert.That(math.length(em.GetComponentData<LocalToWorld>(visual).Value.c0.xyz), Is.InRange(1f, 1.066f));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [Test]
        public void Info004ImportedImpactPrefabsAreBoundedAndDoNotDestroyPoolSlots()
        {
            var settings = Resources.Load<CombatFeedbackSettings>("CombatFeedbackSettings");
            foreach (var prefab in new[] { settings.PunchParticles, settings.EnemyParticles,
                settings.EnvironmentParticles, settings.ExplosionParticles })
            {
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty);
                foreach (var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(particle.main.loop, Is.False);
                    Assert.That(particle.main.playOnAwake, Is.False);
                    Assert.That(particle.main.stopAction, Is.EqualTo(ParticleSystemStopAction.None));
                    Assert.That(particle.main.maxParticles, Is.LessThanOrEqualTo(40));
                }
            }
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
