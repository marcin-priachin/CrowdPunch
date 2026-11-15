using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    internal static class EnemySporellaPrefabBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string Source = "Assets/Alien Sporella Cute Series/";
        private const string ControllerPath = Root + "Animation/EnemySporella.controller";
        private const string MaterialPath = Root + "Materials/Enemies/Sporella.mat";
        private const string SettingsPath = Root + "Data/Settings/Enemies/SporellaSpawnSettings.asset";
        private const string PrefabPath = Root + "Prefabs/EnemySporella.prefab";

        internal static EnemySpawnSettings Rebuild()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "FBX/Alien Sporella.FBX")
                ?? throw new InvalidOperationException("Missing Alien Sporella FBX.");
            var idle = Clip("Idle");
            var run = Clip("Run Forward In Place");
            var attack = Clip("Projectile Attack Front");
            var damage = Clip("Take Damage");
            var spawn = Clip("Spawn");
            var death = Clip("Die");
            var clips = new[] { idle, run, attack, damage, idle, idle, idle, spawn, idle, damage, death };
            var controller = GroundedShooterAnimationBuilder.BuildController(ControllerPath, clips);
            var material = BuildMaterial();

            var root = new GameObject("EnemySporella") { layer = 7 };
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = .6f;
                capsule.height = 2f;
                capsule.providesContacts = true;
                capsule.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "PhysicsMaterial/Enemy.physicMaterial");
                root.AddComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation;
                root.AddComponent<EnemyAuthoring>();

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                foreach (var bone in visual.GetComponentsInChildren<Transform>(true)) bone.gameObject.layer = 7;
                var animator = visual.GetComponent<Animator>();
                if (animator == null || animator.avatar == null) throw new InvalidOperationException("Missing Sporella avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Sporella has no skinned mesh.");
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    animator.Rebind();
                    renderer.rootBone = visual.transform;
                    renderer.quality = SkinQuality.Bone4;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                    var authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Sporella;
                    authoring.SpawnMotion = 7;
                    authoring.RotateLaunchAroundOwner = false;
                    authoring.Samples = GroundedShooterAnimationBuilder.WriteSamples(
                        Root + $"Animation/EnemySporella_{i:00}.bytes", ControllerPath, visual, animator, renderer, clips);
                }
                idle.SampleAnimation(visual, 0f);
                visual.transform.SetLocalPositionAndRotation(Vector3.down, Quaternion.identity);
                visual.transform.localScale = Vector3.one * 2f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            bool created = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath) == null;
            if (created) AssetDatabase.CopyAsset(Root + "Data/Settings/Enemies/RangedEnemySpawnSettings.asset", SettingsPath);
            var settings = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath);
            using var data = new SerializedObject(settings);
            data.FindProperty("archetype").intValue = (int)Configuration.EnemyArchetype.Sporella;
            data.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            data.FindProperty("rangedProjectilePrefab").objectReferenceValue = null;
            int last = Mathf.Max(1, Mathf.RoundToInt(attack.length * attack.frameRate));
            data.FindProperty("firingAnimationLastFrame").intValue = last;
            if (created)
            {
                data.FindProperty("spawnFromGround").boolValue = true;
                data.FindProperty("respawnEnabled").boolValue = false;
                data.FindProperty("initialCount").intValue = 0;
                data.FindProperty("defeatAnimationFramesToSkip").intValue = 0;
                data.FindProperty("contactDamagePercent").floatValue = 0f;
                data.FindProperty("contactPushStrength").floatValue = 0f;
                data.FindProperty("maxHealth").floatValue = 5f;
                data.FindProperty("preferredMinimumDistance").floatValue = 10f;
                data.FindProperty("preferredMaximumDistance").floatValue = 19f;
                data.FindProperty("engagementRange").floatValue = 25f;
                data.FindProperty("approachSpeed").floatValue = 5f;
                data.FindProperty("cooldown").floatValue = 2f;
                data.FindProperty("cooldownVariation").floatValue = .85f;
                data.FindProperty("projectileDamage").floatValue = 10f;
                data.FindProperty("playerInvincibilitySeconds").floatValue = .5f;
                data.FindProperty("projectileRadius").floatValue = .38f;
                // RigMouth reaches its first forward extremum at 10/30 s.
                data.FindProperty("projectileLaunchFrame").intValue = Mathf.Clamp(Mathf.RoundToInt(attack.frameRate / 3f), 1, last);
                var tuning = data.FindProperty("sporella");
                tuning.boxedValue = SporellaSettings.Default;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static AnimationClip Clip(string name)
        {
            string path = Source + $"FBX/Alien Sporella@{name}.FBX";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name)
                ?? throw new InvalidOperationException($"Missing authored clip {name} at {path}.");
        }

        private static Material BuildMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Source + "Materials/Alien Sporella.mat");
            var template = AssetDatabase.LoadAssetAtPath<Material>(Root + "Animation/EnemySkinning.mat");
            if (source == null || source.mainTexture == null || template == null)
                throw new InvalidOperationException("Sporella texture or Full Cel template is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(template) { name = "Sporella Full Cel" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_ColorMap", source.mainTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", source.mainTexture);
            material.SetColor("_BaseColor", Color.white);
            foreach (var property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount", "_ExploderPulse" })
                if (material.HasProperty(property)) material.SetFloat(property, 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
