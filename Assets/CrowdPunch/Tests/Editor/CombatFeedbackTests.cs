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
        [TestCase(EnemyLaunchCause.PlayerPunch)]
        [TestCase(EnemyLaunchCause.EnemyCollision)]
        public void Vision005LaunchAccentsObserveLaunchesRatherThanContactMailboxOrTrailCapacity(EnemyLaunchCause cause)
        {
            using var world = new World("launch accent identity test");
            var em = world.EntityManager;
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            settings.MaximumTrails = 0;
            var go = new GameObject("launch accent bridge");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            bridge.FeedbackSettings = settings;
            PlayerBridgeRegistry.TryGetBridge(out var previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            int departures = 0;
            Vector3 direction = default;
            bridge.LaunchReceived += (_, flight, __) => { departures++; direction = flight; };
            try
            {
                var enemy = em.CreateEntity(typeof(EnemyImpactFeedback), typeof(EnemyLaunchState),
                    typeof(LocalTransform), typeof(Unity.Physics.PhysicsVelocity), typeof(RespawnRequest));
                em.SetComponentEnabled<RespawnRequest>(enemy, false);
                em.SetComponentData(enemy, LocalTransform.Identity);
                em.SetComponentData(enemy, new Unity.Physics.PhysicsVelocity { Linear = new float3(9, 0, 0) });
                var system = world.GetOrCreateSystem<CombatFeedbackBridgeSystem>();
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.Zero, "An active enemy must not have a departure accent.");
                var launch = new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1, LastCause = cause };
                em.SetComponentData(enemy, launch);
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(1));
                Assert.That(direction, Is.EqualTo(Vector3.right));
                Assert.That(em.GetComponentData<EnemyImpactFeedback>(enemy).LaunchAge, Is.Zero);
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(1), "Continuous contact and trail starvation cannot replay launch effects.");
                launch.LaunchSequence++;
                em.SetComponentData(enemy, launch);
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(2), "Re-punching a flying body starts another departure.");
                bridge.FeedbackSettings = null;
                system.Update(world.Unmanaged);
                bridge.FeedbackSettings = settings;
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(2), "Restoring presentation must not replay a pre-suspension launch.");
                settings.LaunchDeparturePuff = settings.LaunchBodySnap = false;
                launch.LaunchSequence++;
                em.SetComponentData(enemy, launch);
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(2));
                Assert.That(em.GetComponentData<EnemyImpactFeedback>(enemy).LaunchSnapDuration, Is.Zero);
                settings.LaunchDeparturePuff = true;
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(2), "Enabling a decoration must not manufacture another launch.");
                launch.Phase = EnemyLaunchPhase.Recovering;
                em.SetComponentData(enemy, launch);
                system.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<EnemyImpactFeedback>(enemy).WasLaunched, Is.Zero);
                launch.Phase = EnemyLaunchPhase.Launched;
                em.SetComponentData(enemy, launch);
                system.Update(world.Unmanaged);
                Assert.That(departures, Is.EqualTo(3));
                em.SetComponentEnabled<RespawnRequest>(enemy, true);
                system.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<EnemyImpactFeedback>(enemy).WasLaunched, Is.Zero);
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previousBridge != null) PlayerBridgeRegistry.Register(previousBridge);
                Object.DestroyImmediate(go); Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Vision005LaunchLineBoostExpiresAndLiveTogglePreservesMaterialProfilesAndWidth()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            settings.MaximumTrails = 1;
            settings.TrailLineOpacity = .3f;
            var go = new GameObject("launch line boost test");
            var material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            using var pool = new LaunchedTrailPool(go.transform, settings, material);
            try
            {
                pool.Begin(); pool.Show(1, 1, Vector3.zero, 10, 1, Vector3.one, 0); pool.End();
                var trail = go.GetComponentInChildren<TrailRenderer>();
                var originalMaterial = trail.sharedMaterial;
                var block = new MaterialPropertyBlock();
                trail.GetPropertyBlock(block);
                var profiles = block.GetTexture("_LineProfiles");
                Assert.That(block.GetFloat("_LineOpacity"), Is.EqualTo(.6f).Within(.001));
                Assert.That(block.GetFloat("_LineBrightness"), Is.EqualTo(1.5f).Within(.001));
                float width = trail.widthMultiplier, lifetime = trail.time;
                pool.Begin(); pool.Show(1, 1, Vector3.forward, 10, 1, Vector3.one, .2f); pool.End();
                trail.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_LineOpacity"), Is.EqualTo(.3f).Within(.001));
                settings.LaunchSpeedLineBoost = false;
                pool.Begin(); pool.Show(1, 2, Vector3.forward, 10, 1, Vector3.one, 0); pool.End();
                trail.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_LineBrightness"), Is.EqualTo(1));
                Assert.That(block.GetTexture("_LineProfiles"), Is.SameAs(profiles));
                Assert.That(trail.sharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(trail.widthMultiplier, Is.EqualTo(width));
                Assert.That(trail.time, Is.EqualTo(lifetime));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(material); Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Vision004LaunchSnapIncludesAnimatedModelsRestoresExactlyAndPreservesPhysics()
        {
            using var world = new World("animated launch snap test");
            var em = world.EntityManager;
            Entity owner = em.CreateEntity(typeof(EnemyImpactFeedback), typeof(LocalTransform), typeof(Unity.Physics.PhysicsVelocity));
            var physics = LocalTransform.FromPosition(new float3(3, 2, 1));
            var velocity = new Unity.Physics.PhysicsVelocity { Linear = new float3(10, 1, 0) };
            em.SetComponentData(owner, physics); em.SetComponentData(owner, velocity);
            Entity visual = em.CreateEntity(typeof(EnemyVisualOwner), typeof(EnemyVisualDeformation),
                typeof(LocalToWorld), typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(visual, new EnemyVisualOwner { Value = owner, AnimationDriven = 1, UsesAtlas = 1 });
            var authored = float4x4.TRS(new float3(3, 2, 1), quaternion.identity, new float3(.8f, 1.4f, .6f));
            em.SetComponentData(visual, new LocalToWorld { Value = authored });
            var feedback = new EnemyImpactFeedback { WasLaunched = 1, LaunchSnapDuration = .18f,
                LaunchCompression = .22f, LaunchStretch = .38f, LaunchDirection = math.right(), LaunchPivot = authored.c3.xyz };
            var system = world.GetOrCreateSystem<EnemyImpactVisualSystem>();
            foreach (float age in new[] { 0f, .05f, .1f, .18f })
            {
                feedback.LaunchAge = age;
                em.SetComponentData(owner, feedback);
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var result = em.GetComponentData<LocalToWorld>(visual).Value;
                float x = math.length(result.c0.xyz);
                if (age == 0) Assert.That(x, Is.LessThan(.8f));
                else if (age < .18f) Assert.That(x, Is.GreaterThan(.8f));
                else Assert.That(result, Is.EqualTo(authored));
                Assert.That(math.determinant(new float3x3(result)), Is.EqualTo(.8f * 1.4f * .6f).Within(.0001f));
                for (int frame = 0; frame < 10; frame++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(result), "The snap must not accumulate scale.");
            }
            feedback.LaunchAge = .04f;
            feedback.LaunchSnapDuration = 0;
            em.SetComponentData(owner, feedback);
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(authored));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(physics));
            Assert.That(em.GetComponentData<Unity.Physics.PhysicsVelocity>(owner), Is.EqualTo(velocity));
        }

        [Test]
        public void Vision005DeparturePoolIsBoundedExpiresDuringHitStopAndClearsWhenDisabled()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            var go = new GameObject("departure pool test");
            var view = new GameObject("departure camera").AddComponent<UnityEngine.Camera>();
            var material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            float originalTimeScale = Time.timeScale;
            using var pool = new LaunchDeparturePool(go.transform, settings, material);
            try
            {
                Time.timeScale = 0;
                for (int i = 0; i < 200; i++) pool.Show(Vector3.zero, Vector3.right, Vector3.one);
                Assert.That(pool.ActiveCount, Is.EqualTo(pool.Capacity));
                pool.Tick(.04f, view);
                var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(go.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
                pool.Tick(.2f, view);
                Assert.That(pool.ActiveCount, Is.Zero);
                pool.Show(Vector3.zero, Vector3.left, Vector3.one);
                settings.LaunchDeparturePuff = false;
                pool.Tick(0, view);
                Assert.That(mesh.vertexCount, Is.Zero);
                Assert.That(pool.ActiveCount, Is.Zero);
                pool.Show(Vector3.zero, Vector3.right, Vector3.one);
                Assert.That(pool.ActiveCount, Is.Zero);
                settings.LaunchDeparturePuff = true;
                pool.Show(Vector3.one, Vector3.forward, Vector3.one);
                pool.Clear();
                Assert.That(pool.ActiveCount, Is.Zero);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(go); Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(material); Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Vision005TrailStyleEditsUpdateActiveLinesAndLengthProfilesWithoutReplacingMaterials()
        {
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            settings.MaximumTrails = 1;
            var go = new GameObject("Trail style live edit test");
            var fallback = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            using var pool = new LaunchedTrailPool(go.transform, settings, fallback);
            try
            {
                pool.Begin(); pool.Show(1, 1, Vector3.zero, 10, 1, Vector3.one); pool.End();
                var trail = go.GetComponentInChildren<TrailRenderer>();
                var material = trail.sharedMaterial;
                var block = new MaterialPropertyBlock();
                trail.GetPropertyBlock(block);
                var originalProfiles = block.GetTexture("_LineProfiles");
                settings.TrailLineOpacity = .25f;
                settings.TrailLineCount = 3;
                settings.TrailLineWidth = .08f;
                settings.TrailLineBlur = .5f;
                settings.TrailLineSpacing = .7f;
                settings.TrailLineBrightness = 1.5f;
                settings.TrailOpacityAlongLength = AnimationCurve.Linear(0, 0, 1, 1);
                settings.TrailLineWidthAlongLength = AnimationCurve.Linear(0, .5f, 1, 1);
                settings.TrailLineBlurAlongLength = AnimationCurve.Linear(0, .25f, 1, 1);
                settings.TrailLineSpacingAlongLength = AnimationCurve.Linear(0, .5f, 1, 1);
                settings.TrailTintAlongLength = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.blue, 1) },
                    alphaKeys = new[] { new GradientAlphaKey(.5f, 0), new GradientAlphaKey(1, 1) }
                };
                settings.RefreshTrailStyle();
                pool.Begin(); pool.Show(1, 1, Vector3.one, 10, 1, Vector3.one); pool.End();
                trail.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_LineOpacity"), Is.EqualTo(.25f));
                Assert.That(block.GetVector("_LineParameters"), Is.EqualTo(new Vector4(3, .08f, .5f, .7f)));
                Assert.That(block.GetFloat("_LineBrightness"), Is.EqualTo(1.5f));
                var profiles = (Texture2D)block.GetTexture("_LineProfiles");
                Assert.That(profiles, Is.SameAs(originalProfiles), "Live edits must reuse the shared profile texture.");
                Assert.That(trail.sharedMaterial, Is.SameAs(material));
                Color tail = profiles.GetPixel(0, 0), head = profiles.GetPixel(profiles.width - 1, 0);
                Assert.That(tail.r, Is.EqualTo(.5f).Within(.002f));
                Assert.That(tail.g, Is.Zero); Assert.That(head.g, Is.EqualTo(1));
                Assert.That(tail.b, Is.EqualTo(.25f).Within(.002f));
                Assert.That(tail.a, Is.EqualTo(.5f).Within(.002f));
                Assert.That(profiles.GetPixel(0, 1).r, Is.EqualTo(1));
                Assert.That(profiles.GetPixel(profiles.width - 1, 1).b, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(fallback); Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Combat001LaunchTrailUsesVisibleModelAltitudeAndSizeInsteadOfPhysicsRoot()
        {
            using var world = new World("Launch trail model bounds");
            var em = world.EntityManager;
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            var go = new GameObject("Trail bridge test");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            bridge.FeedbackSettings = settings;
            PlayerBridgeRegistry.TryGetBridge(out var previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            var fallback = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            using var pool = new LaunchedTrailPool(go.transform, settings, fallback);
            bridge.TrailReceived += pool.Show;
            try
            {
                var enemy = em.CreateEntity(typeof(EnemyImpactFeedback), typeof(EnemyLaunchState),
                    typeof(LocalTransform), typeof(Unity.Physics.PhysicsVelocity));
                em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(3, .5f, 7)));
                em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
                em.SetComponentData(enemy, new Unity.Physics.PhysicsVelocity { Linear = new float3(0, 0, 10) });
                var visual = em.CreateEntity(typeof(EnemyVisualOwner), typeof(RenderBounds), typeof(LocalToWorld));
                em.SetComponentData(visual, new EnemyVisualOwner { Value = enemy });
                em.SetComponentData(visual, new RenderBounds { Value = new AABB
                    { Center = new float3(0, 2, 0), Extents = new float3(1.5f, 2, .5f) } });
                em.SetComponentData(visual, new LocalToWorld { Value = float4x4.Translate(new float3(3, 5, 7)) });
                var system = world.GetOrCreateSystem<CombatFeedbackBridgeSystem>();
                system.Update(world.Unmanaged);
                var trail = go.GetComponentInChildren<TrailRenderer>();
                Assert.That(trail.transform.position, Is.EqualTo(new Vector3(3, 7, 7)),
                    "A floating model's trail must follow its visible centre, not the capsule centre.");
                Assert.That(trail.startWidth, Is.EqualTo(3).Within(.001f));
                var view = new GameObject("Trail width test camera").AddComponent<UnityEngine.Camera>();
                var mesh = new Mesh();
                try
                {
                    view.transform.position = new Vector3(10, 10, -10);
                    view.transform.LookAt(new Vector3(0, 7, 9));
                    trail.transform.position = new Vector3(0, 7, 7);
                    trail.Clear();
                    trail.AddPositions(new[] { new Vector3(0, 7, 7), new Vector3(0, 7, 9), new Vector3(0, 7, 11) });
                    trail.transform.position = new Vector3(0, 7, 11);
                    trail.BakeMesh(mesh, view, true);
                    var points = mesh.vertices;
                    Assert.That(points.Length, Is.GreaterThanOrEqualTo(6));
                    Assert.That(mesh.uv[0].x, Is.Zero.Within(.001f),
                        "Unity's newest/head vertices use U=0; shader profiles reverse U to expose tail=0, enemy=1.");
                    for (int i = 0; i < points.Length; i += 2)
                        Assert.That(Vector3.Distance(points[i], points[i + 1]), Is.EqualTo(3).Within(.01f),
                            "Parallel cartoon strokes must retain the same bundle width at head, middle and tail.");
                    Assert.That(trail.endColor.a, Is.EqualTo(trail.startColor.a));
                }
                finally { Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(mesh); }
                Assert.That(trail.time * 10, Is.EqualTo(10).Within(.001f),
                    "At steady speed the ribbon should span two and a half model lengths.");
                em.SetComponentData(enemy, new Unity.Physics.PhysicsVelocity { Linear = new float3(0, 0, 20) });
                system.Update(world.Unmanaged);
                Assert.That(trail.time * 20, Is.EqualTo(10).Within(.001f),
                    "Faster launches should preserve the same spatial trail length.");
                Assert.That(trail.startWidth, Is.EqualTo(3).Within(.001f));
                using var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                ref var probes = ref builder.ConstructRoot<EnemyGroundingMesh>();
                var vertices = builder.Allocate(ref probes.Vertices, 2);
                vertices[0] = new EnemyGroundingVertex { Position = new float3(-1), Weights = new float4(1, 0, 0, 0) };
                vertices[1] = new EnemyGroundingVertex { Position = new float3(1), Weights = new float4(1, 0, 0, 0) };
                using var blob = builder.CreateBlobAssetReference<EnemyGroundingMesh>(Unity.Collections.Allocator.Persistent);
                em.AddComponentData(visual, new EnemyTrailVisual { Owner = enemy, Root = visual, Probes = blob });
                em.AddBuffer<Unity.Deformations.SkinMatrix>(visual).Add(new Unity.Deformations.SkinMatrix
                    { Value = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), new float3(0, 3, 0)) });
                world.GetOrCreateSystem<EnemyTrailBoundsSystem>().Update(world.Unmanaged);
                system.Update(world.Unmanaged);
                Assert.That(trail.transform.position, Is.EqualTo(new Vector3(3, 8, 7)),
                    "The current skinned pose overrides the larger authored culling bounds.");
                Assert.That(trail.startWidth, Is.EqualTo(2).Within(.001f));
            }
            finally
            {
                bridge.TrailReceived -= pool.Show;
                PlayerBridgeRegistry.Register(previousBridge);
                Object.DestroyImmediate(go); Object.DestroyImmediate(fallback); Object.DestroyImmediate(settings);
            }
        }

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
                using var pool = new LaunchedTrailPool(go.transform, settings, material);
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
