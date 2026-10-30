using System;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    public static class GatekeeperEffectsBuilder
    {
        private const string Graphics = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/";

        [MenuItem("Crowd Punch/Bosses/Apply Gatekeeper Ambient Effects")]
        public static void ApplyAmbient()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before authoring Gatekeeper effects.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in GatekeeperMagmaBuilder.Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var boss in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEncounterAuthoring>(true)))
                        ConfigureAmbient(boss.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void ConfigureAmbient(GameObject boss)
        {
            var smoke = AssetDatabase.LoadAssetAtPath<Material>(Graphics + "cfxr smoke cloud x4 ab.mat");
            var spark = AssetDatabase.LoadAssetAtPath<Material>(Graphics + "cfxr star ab.mat");
            if (smoke == null || spark == null) throw new InvalidOperationException("Missing cartoon ambient particle materials.");
            // Override only the source model's three FX Square emitters; preserve the pack prefab.
            foreach (var effect in boss.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (effect.name != "Magma" && effect.name != "Magma (1)" && effect.name != "Magma (2)") continue;
                bool head = effect.name == "Magma (2)";
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = effect.main;
                main.loop = true; main.playOnAwake = true; main.prewarm = false;
                main.duration = 1; main.simulationSpeed = 1; main.maxParticles = 12;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .7f);
                main.startSize = new ParticleSystem.MinMaxCurve(head ? .03f : .045f, head ? .05f : .075f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.025f, .065f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.startColor = head ? new Color(1, .72f, .2f, .9f) : new Color(1, .85f, .57f, .55f);
                main.gravityModifier = 0; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var emission = effect.emission;
                emission.enabled = true; emission.rateOverTime = head ? 6 : 3;
                emission.rateOverDistance = 0; emission.burstCount = 0;
                var shape = effect.shape;
                shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .035f;
                var velocity = effect.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-.08f, .08f);
                velocity.y = new ParticleSystem.MinMaxCurve(.3f, .6f);
                velocity.z = new ParticleSystem.MinMaxCurve(-.08f, .08f);
                var size = effect.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, head ? 1 : .4f, 1, head ? .15f : 1.3f));
                var color = effect.colorOverLifetime;
                color.enabled = true;
                color.color = new Gradient {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .68f, .36f), 1) },
                    alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f),
                        new GradientAlphaKey(.75f, .45f), new GradientAlphaKey(0, 1) }
                };
                var sheet = effect.textureSheetAnimation;
                sheet.enabled = !head; sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.frameOverTime = 0; sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 1);
                var collision = effect.collision; collision.enabled = false;
                var lights = effect.lights; lights.enabled = false;
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sharedMaterial = head ? spark : smoke;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
                });
                PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        [MenuItem("Crowd Punch/Bosses/Rebuild Gatekeeper Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before rebuilding Gatekeeper effects.");
            var smoke = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr smoke cloud x4 ab.mat");
            var streak = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Materials/Feedback/CombatParticles.mat");
            if (smoke == null || streak == null) throw new InvalidOperationException("Missing existing cartoon particle materials.");
            var root = new GameObject("GatekeeperImpact");
            try
            {
                var puff = root.AddComponent<ParticleSystem>();
                Configure(puff, smoke, 12, .38f, .5f, .9f, 2.8f, 5f);
                var main = puff.main;
                main.startColor = new Color(1f, .93f, .76f, .8f);
                var sheet = puff.textureSheetAnimation;
                sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.frameOverTime = 0; sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 1);
                var size = puff.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .45f, 1, 1.4f));
                var child = new GameObject("Golden impact strokes");
                child.transform.SetParent(root.transform, false);
                var sparks = child.AddComponent<ParticleSystem>();
                Configure(sparks, streak, 7, .23f, .07f, .13f, 4, 7);
                main = sparks.main; main.startColor = new Color(1, .68f, .16f, .9f);
                var renderer = sparks.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.5f; renderer.velocityScale = .08f;
                size = sparks.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .15f));
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/CrowdPunch/Resources/GatekeeperImpact.prefab");
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Configure(ParticleSystem effect, Material material, short count,
            float lifetime, float sizeMin, float sizeMax, float speedMin, float speedMax)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = false; main.playOnAwake = false; main.duration = .08f;
            main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .75f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.useUnscaledTime = true; main.maxParticles = 20;
            var emission = effect.emission;
            emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
            var shape = effect.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 78; shape.radius = .35f;
            var color = effect.colorOverLifetime;
            color.enabled = true;
            color.color = new Gradient {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f),
                    new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) }
            };
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
        }
    }
}
