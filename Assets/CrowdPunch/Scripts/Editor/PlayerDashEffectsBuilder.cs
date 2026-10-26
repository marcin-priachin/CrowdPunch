using System;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    /// <summary>Rebuilds the pooled dash accents with existing Cartoon FX art.</summary>
    public static class PlayerDashEffectsBuilder
    {
        private const string Effects = "Assets/CrowdPunch/Prefabs/Feedback/";
        private const string Graphics = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/";

        [MenuItem("Crowd Punch/Player/Rebuild Dash Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before rebuilding dash effects.");

            var smoke = AssetDatabase.LoadAssetAtPath<Material>(Graphics + "cfxr smoke cloud x4 ab.mat");
            var streak = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Materials/Feedback/CombatParticles.mat");
            if (smoke == null || streak == null)
                throw new InvalidOperationException("Dash feedback requires the existing smoke and combat particle materials.");

            BuildEffect("DashStart", smoke, streak, 0);
            BuildEffect("DashMovement", smoke, streak, 1);
            BuildEffect("DashEnd", smoke, streak, 2);
            var settings = AssetDatabase.LoadAssetAtPath<CombatFeedbackSettings>("Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset");
            settings.DashMovementInterval = .045f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void BuildEffect(string name, Material smoke, Material streak, int phase)
        {
            string path = Effects + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var primary = root.GetComponent<ParticleSystem>();
                var secondary = root.transform.Find("SoftWisps").GetComponent<ParticleSystem>();
                if (phase == 1)
                {
                    ConfigureStreaks(primary, streak, 5, .2f);
                    ConfigureDust(secondary, smoke, 2, .24f, .30f, 1.1f);
                }
                else
                {
                    ConfigureDust(primary, smoke, phase == 0 ? 9 : 5, phase == 0 ? .34f : .27f,
                        phase == 0 ? .55f : .4f, phase == 0 ? 3.2f : 1.7f);
                    ConfigureStreaks(secondary, streak, phase == 0 ? 4 : 0, .16f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureCommon(ParticleSystem effect, int count, float lifetime)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one;
            var main = effect.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.duration = .08f;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .75f, lifetime);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.useUnscaledTime = true;
            main.maxParticles = 16;
            main.gravityModifier = 0f;
            main.startSize3D = false;
            main.startRotation3D = false;
            var emission = effect.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var velocity = effect.velocityOverLifetime; velocity.enabled = false;
            var limit = effect.limitVelocityOverLifetime; limit.enabled = false;
            var force = effect.forceOverLifetime; force.enabled = false;
            var noise = effect.noise; noise.enabled = false;
            var trails = effect.trails; trails.enabled = false;
            var collision = effect.collision; collision.enabled = false;
            var lights = effect.lights; lights.enabled = false;
            var subEmitters = effect.subEmitters; subEmitters.enabled = false;
            var rotation = effect.rotationOverLifetime; rotation.enabled = false;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0;
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
        }

        private static void ConfigureDust(ParticleSystem effect, Material material, int count,
            float lifetime, float size, float speed)
        {
            ConfigureCommon(effect, count, lifetime);
            var main = effect.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
            main.startColor = new Color(.91f, .9f, .84f, .5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = .25f;
            shape.position = new Vector3(0, .12f, .12f);
            shape.rotation = Vector3.zero;
            // +Z points behind the committed dash; flatten the fan to keep dust near the feet.
            shape.scale = new Vector3(1f, .18f, 1f);
            var velocity = effect.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = .28f;
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .12f));
            var sizeOverLife = effect.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, .45f, 3, 3), new Keyframe(.35f, 1), new Keyframe(1, 1.3f)));
            var color = effect.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(.65f);
            var sheet = effect.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = 0;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 1);
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        private static void ConfigureStreaks(ParticleSystem effect, Material material, int count, float lifetime)
        {
            ConfigureCommon(effect, count, lifetime);
            var main = effect.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(.045f, .085f);
            main.startColor = new Color(.91f, .97f, 1f, .5f);
            main.startRotation = 0;
            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0, .85f, .12f);
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(.85f, 1.05f, .1f);
            shape.randomDirectionAmount = 0;
            var size = effect.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .2f));
            var color = effect.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(.3f);
            var sheet = effect.textureSheetAnimation; sheet.enabled = false;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.sharedMaterial = material;
            renderer.lengthScale = 1.4f;
            renderer.velocityScale = .12f;
            renderer.cameraVelocityScale = 0;
        }

        private static Gradient Fade(float hold)
        {
            return new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f),
                    new GradientAlphaKey(.7f, hold), new GradientAlphaKey(0, 1) }
            };
        }
    }
}
