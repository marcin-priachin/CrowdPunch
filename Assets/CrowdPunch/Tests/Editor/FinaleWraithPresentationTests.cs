using System.Linq;
using CrowdPunch.Editor;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class FinaleWraithPresentationTests
    {
        [Test]
        public void Finale001_SavedSceneUsesAuthoredWraithScaleClipsAndCelTexture()
        {
            var scene = EditorSceneManager.OpenPreviewScene(FinaleWraithBuilder.ScenePath);
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FinalePresentation>(true)).Single();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinaleWraithBuilder.Source);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(presentation.model.gameObject), Is.EqualTo(prefab));
                Assert.That(presentation.model.localScale, Is.EqualTo(prefab.transform.localScale));
                Assert.That(presentation.transform.lossyScale, Is.EqualTo(Vector3.one));
                var animator = presentation.animator;
                Assert.That(animator.applyRootMotion, Is.False);
                var states = ((AnimatorController)animator.runtimeAnimatorController).layers[0].stateMachine.states;
                Assert.That(states.Select(s => s.state.name), Is.EquivalentTo(FinaleWraithBuilder.Motions));
                foreach (var state in states)
                    Assert.That(AssetDatabase.GetAssetPath(state.state.motion), Does.StartWith("Assets/Wraith Cute Series/FBX/"));
                foreach (var name in new[] { "Die", "Take Damage", "Projectile Attack", "Spin Wing Slash Attack", "Wing Stab Attack", "Cast Spell" })
                    Assert.That(FinaleWraithBuilder.Clip(name).isLooping, Is.False);
                var material = presentation.model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo("CrowdPunch/Cel/Environment"));
                Assert.That(material.mainTexture, Is.EqualTo(prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.mainTexture));
                var aura = presentation.model.GetComponentsInChildren<ParticleSystemRenderer>(true);
                Assert.That(aura.Length, Is.EqualTo(prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Length));
                Assert.That(aura.All(r => r.sharedMaterial.shader.name.Contains("Particles")), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void Finale003_WarningsReleaseAndPhaseCancellationPreserveScaleAndMaterials()
        {
            var root = new GameObject("Wraith presentation regression");
            root.SetActive(false);
            try
            {
                var presentation = root.AddComponent<FinalePresentation>();
                presentation.settings = AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.FinaleSettings>(FinaleWraithBuilder.Art + "FinaleSettings.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinaleWraithBuilder.Source);
                presentation.model = Object.Instantiate(prefab, root.transform).transform;
                presentation.animator = presentation.model.GetComponent<Animator>();
                presentation.animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(FinaleWraithBuilder.Art + "Wraith.controller");
                presentation.animator.applyRootMotion = false;
                var scale = presentation.model.localScale;
                var renderers = presentation.model.GetComponentsInChildren<Renderer>(true);
                var materials = renderers.Select(r => r.sharedMaterial).ToArray();
                var colors = renderers.Select(ReadFeedbackColors).ToArray();
                root.SetActive(true);
                // EditMode does not dispatch lifecycle messages to ordinary runtime MonoBehaviours.
                Lifecycle(presentation, "OnEnable");
                presentation.animator.Rebind();
                presentation.animator.Update(0);
                foreach (int phase in new[] { 0, 2 })
                    foreach (int attack in phase == 0 ? new[] { 1, 2 } : new[] { 3, 4 })
                    {
                        for (int frame = 0; frame <= 30; frame++)
                        {
                            Present(phase, attack, frame / 30f);
                            presentation.animator.Update(0);
                            Assert.That(presentation.model.localScale, Is.EqualTo(scale));
                        }
                        float contact = presentation.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        Assert.That(contact, Is.InRange(.1f, .95f), "The warning ends inside the authored attack, leaving a follow-through.");
                        Present(phase, 0, 1);
                        presentation.animator.Update(0);
                        Assert.That(presentation.animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(contact).Within(.001f));
                        Assert.That(presentation.animator.speed, Is.EqualTo(1));
                        Present(3, 0, 0);
                        presentation.animator.Update(.2f);
                        Assert.That(presentation.animator.GetCurrentAnimatorStateInfo(0).IsName("Fly Forward In Place"), Is.True);
                        Assert.That(root.transform.Find("Finale ground warning").GetComponent<LineRenderer>().enabled, Is.False);
                    }
                foreach (int phase in new[] { 1, 2, 3, 4, 0 })
                {
                    Present(phase, 0, 0, .4f);
                    presentation.animator.Update(.2f);
                    Assert.That(presentation.model.localScale, Is.EqualTo(scale));
                    for (int i = 0; i < renderers.Length; i++)
                        Assert.That(ReadFeedbackColors(renderers[i]), Is.EqualTo(colors[i]), "Readability never recolors the body or aura.");
                    Assert.That(renderers.Select(r => r.sharedMaterial), Is.EqualTo(materials));
                }
            }
            finally
            {
                var presentation = root.GetComponent<FinalePresentation>();
                if (presentation != null) Lifecycle(presentation, "OnDisable");
                var line = root.GetComponentInChildren<LineRenderer>();
                if (line != null) Object.DestroyImmediate(line.sharedMaterial);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Finale007_GroundedClipEnvelopeAndDeathStayAboveFloorWithoutRescaling()
        {
            var scene = EditorSceneManager.OpenPreviewScene(FinaleWraithBuilder.ScenePath);
            var mesh = new Mesh();
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FinalePresentation>(true)).Single();
                var model = presentation.model;
                var scale = model.localScale;
                foreach (string name in FinaleWraithBuilder.Motions)
                {
                    var clip = FinaleWraithBuilder.Clip(name);
                    for (int frame = 0; frame <= 40; frame++)
                    {
                        clip.SampleAnimation(model.gameObject, clip.length * frame / 40);
                        Assert.That(model.localScale, Is.EqualTo(scale));
                        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            renderer.BakeMesh(mesh);
                            foreach (var vertex in mesh.vertices)
                            {
                                float height = model.InverseTransformPoint(renderer.transform.TransformPoint(vertex)).y;
                                Assert.That(height - presentation.groundedModelOffset, Is.GreaterThanOrEqualTo(.035f), name);
                            }
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Present(int phase, int attack, float progress, float health = 1) => FinalePresentation.Publish(
            new Vector3(0, 3, 0), Quaternion.identity, phase, attack, Vector3.zero, Vector3.forward, 12, 4, progress,
            health, Vector3.zero, phase == 2 || phase == 4 ? 1 : 0);

        private static Color[] ReadFeedbackColors(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return new[] { block.GetColor("_BaseColor"), block.GetColor("_Color"), block.GetColor("_EmissionColor") };
        }

        private static void Lifecycle(FinalePresentation presentation, string name) => typeof(FinalePresentation).GetMethod(
            name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
    }
}
