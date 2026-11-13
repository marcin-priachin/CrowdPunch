using System.Collections.Generic;
using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class WizardDecorationVisibilityTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private World world;
        private WizardZonePresentationSystem system;
        private CombatVfxLibrary library;
        private GameObject cameraObject;
        private Camera camera;
        private readonly List<Camera> disabledCameras = new();
        private static readonly Vector3 Hidden = new(1000000, 1000000, 1000000);

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in Camera.allCameras)
                if (existing.enabled) { disabledCameras.Add(existing); existing.enabled = false; }
            cameraObject = new GameObject("Wizard visibility test camera") { tag = "MainCamera" };
            camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 100;
            camera.aspect = 1;
            world = new World("Wizard decoration visibility");
            system = world.GetOrCreateSystemManaged<WizardZonePresentationSystem>();
            library = Object.Instantiate(Resources.Load<CombatVfxLibrary>("CombatVfxLibrary"));
            library.CloudsPerZone = 24; library.SporesPerZone = 48; library.MaximumCloudZones = 32;
            typeof(WizardZonePresentationSystem).GetField("library", Private).SetValue(system, library);
            world.SetTime(new TimeData(2.375, 1f / 60));
        }

        [TearDown]
        public void TearDown()
        {
            // The runtime system disposes its disc with Destroy; these worlds run in Edit mode.
            Object.DestroyImmediate((Mesh)typeof(WizardZonePresentationSystem).GetField("disc", Private).GetValue(system));
            world.Dispose();
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(library);
            foreach (var existing in disabledCameras) if (existing != null) existing.enabled = true;
            disabledCameras.Clear();
        }

        private Entity Zone(Vector3 position, float radius = 6)
        {
            var entity = world.EntityManager.CreateEntity(typeof(WizardZone));
            world.EntityManager.SetComponentData(entity, new WizardZone
            {
                Position = position, Settings = new WizardSettings { CastRadius = radius }, Active = 1
            });
            return entity;
        }

        private List<Vector3> Vertices(string name)
        {
            var batch = typeof(WizardZonePresentationSystem).GetField(name, Private).GetValue(system);
            return (List<Vector3>)batch.GetType().GetField("vertices", Private).GetValue(batch);
        }

        [Test]
        public void Wizard006HiddenBatchesSkipGeometryAndReappearAtTheSamePhase()
        {
            var entity = Zone(Hidden);
            var before = world.EntityManager.GetComponentData<WizardZone>(entity);
            camera.transform.position = Hidden - Vector3.forward * 20;
            system.Update();
            var clouds = Vertices("clouds").ToArray();
            var pollen = Vertices("pollen").ToArray();
            Assert.That(clouds.Length, Is.EqualTo(24 * 4));
            Assert.That(pollen.Length, Is.EqualTo(48 * 4));
            camera.transform.position = Vector3.zero;
            system.Update();
            Assert.That(Vertices("clouds"), Is.Empty);
            Assert.That(Vertices("pollen"), Is.Empty);
            camera.transform.position = Hidden - Vector3.forward * 20;
            system.Update();
            CollectionAssert.AreEqual(clouds, Vertices("clouds"));
            CollectionAssert.AreEqual(pollen, Vertices("pollen"));
            Assert.That(world.EntityManager.GetComponentData<WizardZone>(entity), Is.EqualTo(before));
        }

        [Test]
        public void Wizard006MixedVisibilityKeepsOriginalBatchAndZoneBudget()
        {
            Zone(Hidden);
            Zone(Vector3.forward * 20);
            system.Update();
            Assert.That(Vertices("clouds").Count, Is.EqualTo(2 * 24 * 4));
            Assert.That(Vertices("pollen").Count, Is.EqualTo(2 * 48 * 4));
            library.MaximumCloudZones = 1;
            system.Update();
            Assert.That(Vertices("clouds"), Is.Empty, "A visible zone beyond the original cap is not promoted.");
        }

        [Test]
        public void Wizard006SecondaryCameraRetainsOffscreenDecoration()
        {
            Zone(Hidden);
            var secondObject = new GameObject("Secondary view");
            try
            {
                var second = secondObject.AddComponent<Camera>();
                second.transform.position = Hidden - Vector3.forward * 20;
                system.Update();
                Assert.That(Vertices("clouds").Count, Is.EqualTo(24 * 4));
                second.cullingMask = 0;
                system.Update();
                Assert.That(Vertices("clouds"), Is.Empty);
            }
            finally { Object.DestroyImmediate(secondObject); }
        }

        [TestCase(.01f)]
        [TestCase(1f)]
        [TestCase(6f)]
        [TestCase(20f)]
        public void Wizard006BoundsContainEveryGeneratedBillboardAtMultiplePhases(float radius)
        {
            var entity = Zone(Vector3.forward * 20, radius);
            var zone = world.EntityManager.GetComponentData<WizardZone>(entity);
            var bounds = (Bounds)typeof(WizardZonePresentationSystem).GetMethod("DecorationBounds",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { zone, false, Vector3.zero });
            camera.transform.rotation = Quaternion.Euler(13, 27, 42);
            for (int i = 0; i < 80; i++)
            {
                world.SetTime(new TimeData(i * .117, 1f / 60));
                system.Update();
                Assert.That(Vertices("clouds"), Is.Not.Empty);
                foreach (var vertex in Vertices("clouds")) Assert.That(bounds.Contains(vertex), Is.True);
                foreach (var vertex in Vertices("pollen")) Assert.That(bounds.Contains(vertex), Is.True);
            }
        }

        [Test]
        public void Wizard006CrownBoundsIncludeElevatedAndOffsetEmission()
        {
            var entity = Zone(Hidden);
            var zone = world.EntityManager.GetComponentData<WizardZone>(entity);
            var crown = Hidden + new Vector3(2, 40, 1);
            var source = world.EntityManager.CreateEntity(typeof(WizardSporeOrigin), typeof(LocalToWorld));
            world.EntityManager.SetComponentData(source, new LocalToWorld { Value = float4x4.Translate(crown) });
            world.EntityManager.AddBuffer<SkinMatrix>(source).Add(new SkinMatrix
            {
                Value = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), float3.zero)
            });
            world.EntityManager.SetComponentData(source, new WizardSporeOrigin { Animation = source, Root = source });
            zone.Source = source; zone.Follow = 1; zone.Settings.ActiveDuration = 3;
            world.EntityManager.SetComponentData(entity, zone);
            var bounds = (Bounds)typeof(WizardZonePresentationSystem).GetMethod("DecorationBounds",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { zone, true, crown });
            Assert.That(bounds.Contains(crown + Vector3.up * .95f), Is.True);
            camera.transform.position = crown - Vector3.forward * 20;
            for (int i = 0; i < 80; i++)
            {
                world.SetTime(new TimeData(i * .117, 1f / 60));
                system.Update();
                Assert.That(Vertices("pollen"), Is.Not.Empty, "Crown emission stays visible above an offscreen ground footprint.");
                foreach (var vertex in Vertices("pollen")) Assert.That(bounds.Contains(vertex), Is.True);
            }
        }

        [Test, Explicit("Local CPU sample; includes ground-ring submission, excludes rendering/GPU work.")]
        public void Wizard006ProfileThirtyTwoHiddenVersusRetainedZones()
        {
            for (int i = 0; i < 32; i++) Zone(Hidden + Vector3.right * (i % 4));
            foreach (bool visible in new[] { true, false })
            {
                camera.transform.position = visible ? Hidden - Vector3.forward * 20 : Vector3.zero;
                for (int i = 0; i < 20; i++) system.Update();
                var timer = new System.Diagnostics.Stopwatch();
                long bytes = System.GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int i = 0; i < 120; i++) system.Update();
                timer.Stop();
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - bytes;
                TestContext.WriteLine($"Retained={visible}: {timer.Elapsed.TotalMilliseconds / 120:F4} ms/update; {allocated} bytes/120 updates");
                Assert.That(Vertices("clouds").Count, Is.EqualTo(visible ? 32 * 24 * 4 : 0));
            }
        }
    }
}
