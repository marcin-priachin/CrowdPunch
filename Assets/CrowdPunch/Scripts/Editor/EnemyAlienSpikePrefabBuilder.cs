using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    public static class EnemyAlienSpikePrefabBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string Source = "Assets/Alien Spike Cute Series/";
        private const string PrefabPath = Root + "Prefabs/EnemyAlienSpike.prefab";
        private const string SettingsPath = Root + "Data/Settings/AlienSpikeSettings.asset";
        private const string ProfilePath = Root + "Data/Settings/Enemies/AlienSpikeEnemySpawnSettings.asset";
        private const string SamplesPath = Root + "Animation/EnemyAlienSpikeMovement.bytes";
        private const int Frames = 31;
        private const float VisualScale = 1.35f;
        private const float VisualHeight = -1.35f;

        [MenuItem("Crowd Punch/Enemies/Rebuild Alien Spike Prefab")]
        public static void Rebuild() => Build();

        internal static EnemySpawnSettings Build()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AlienSpikeSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AlienSpikeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Prefabs/Alien Spike.prefab");
            if (model == null) throw new InvalidOperationException("Alien Spike source prefab is missing.");
            var idle = FindClip("Idle");
            var move = FindClip("Fly Forward In Place");
            var attack = FindClip("Projectile Attack");
            var hurt = FindClip("Take Damage");
            var death = FindClip("Die");

            var root = new GameObject("EnemyAlienSpike") { layer = 7 };
            try
            {
                root.AddComponent<EnemyAuthoring>();
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = .49f;
                collider.providesContacts = true;
                collider.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "PhysicsMaterial/Enemy.physicMaterial");
                var body = root.AddComponent<Rigidbody>();
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.transform.SetLocalPositionAndRotation(Vector3.up * VisualHeight, Quaternion.identity);
                visual.transform.localScale = Vector3.one * VisualScale;
                foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 7;

                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
                var animator = visual.GetComponent<Animator>();
                animator.applyRootMotion = false;
                if (animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("The Alien Spike source has no Animator controller.");
                skin.rootBone = animator.transform;
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.sharedMaterial = TexturedCelMaterial(skin.sharedMaterial);

                var spike = root.AddComponent<AlienSpikeAuthoring>();
                spike.settings = settings;
                spike.idle = idle;
                spike.move = move;
                spike.projectileAttack = attack;
                spike.hurt = hurt;
                spike.death = death;
                spike.sockets = ReadSockets(root.transform, visual, skin, idle, attack);
                var animation = skin.gameObject.AddComponent<EnemyAnimationAuthoring>();
                animation.Profile = EnemyAnimationProfile.AlienSpike;
                animation.Samples = WriteSamples(visual, skin, spike.SampleClips());
                idle.SampleAnimation(visual, 0);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(ProfilePath);
            if (profile == null)
            {
                AssetDatabase.CopyAsset(Root + "Data/Settings/Enemies/EnemySpawnSettings.asset", ProfilePath);
                profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(ProfilePath);
                var initial = new SerializedObject(profile);
                initial.FindProperty("archetype").intValue = (int)Configuration.EnemyArchetype.AlienSpike;
                initial.FindProperty("moveSpeed").floatValue = 3.6f;
                initial.FindProperty("chargeSpeedMultiplier").floatValue = 1f;
                initial.FindProperty("separationDistanceMin").floatValue = 1.25f;
                initial.FindProperty("separationDistanceMax").floatValue = 2f;
                initial.FindProperty("maxHealth").floatValue = 24;
                initial.FindProperty("contactDamagePercent").floatValue = 0;
                initial.FindProperty("contactPushStrength").floatValue = 0;
                initial.FindProperty("contactRadius").floatValue = .49f;
                initial.FindProperty("defeatAnimationFramesToSkip").intValue = 0;
                initial.FindProperty("rangedProjectilePrefab").objectReferenceValue = null;
                initial.ApplyModifiedPropertiesWithoutUndo();
            }
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static AnimationClip FindClip(string motion) => AssetDatabase
            .LoadAllAssetsAtPath($"{Source}FBX/Alien Spike@{motion}.FBX")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));

        private static Material TexturedCelMaterial(Material source)
        {
            const string path = Root + "Materials/Enemies/AlienSpike.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(Root + "Animation/EnemySkinning.mat");
                material = new Material(template) { name = "Alien Spike" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_ColorMap", source.mainTexture);
            material.SetTexture("_MainTex", source.mainTexture);
            material.SetColor("_BaseColor", Color.white);
            foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount", "_ExploderPulse" })
                if (material.HasProperty(property)) material.SetFloat(property, 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AlienSpikeAuthoring.SpikeSocket[] ReadSockets(Transform root, GameObject visual,
            SkinnedMeshRenderer skin, AnimationClip idle, AnimationClip attack)
        {
            // The mesh's twelve cones follow its face vertices. Each cone has one apex and eight rim vertices.
            const int firstTip = 331;
            const int verticesPerSpike = 9;
            const int count = 12;
            if (skin.sharedMesh.vertexCount != firstTip + count * verticesPerSpike)
                throw new InvalidOperationException("Alien Spike mesh topology changed. Reinspect the twelve tip vertices.");
            var sockets = new AlienSpikeAuthoring.SpikeSocket[count];
            var vertices = skin.sharedMesh.vertices;
            var weights = skin.sharedMesh.boneWeights;
            var bindposes = skin.sharedMesh.bindposes;
            var bones = skin.bones;
            for (int pose = 0; pose < 2; pose++)
            {
                var clip = pose == 0 ? idle : attack;
                clip.SampleAnimation(visual, pose == 0 ? 0 : attack.length * AlienSpikeAuthoring.ReleasePhase);
                // Use the same bone and bind-pose matrices as the ECS skin samples.
                var matrices = bones.Select((bone, i) => root.worldToLocalMatrix * bone.localToWorldMatrix * bindposes[i]).ToArray();
                for (int i = 0; i < count; i++)
                {
                    int apex = firstTip + i * verticesPerSpike;
                    Vector3 tip = SkinVertex(apex);
                    Vector3 rim = Vector3.zero;
                    for (int j = 1; j < verticesPerSpike; j++) rim += SkinVertex(apex + j);
                    Vector3 axis = tip - rim / (verticesPerSpike - 1);
                    var direction = new Vector2(axis.x, axis.z).normalized;
                    if (direction.sqrMagnitude < .99f) throw new InvalidOperationException($"Spike {i} is vertical.");
                    if (pose == 0)
                    {
                        sockets[i].idleTip = tip;
                        sockets[i].idleDirection = direction;
                    }
                    else
                    {
                        sockets[i].releaseTip = tip;
                        sockets[i].releaseDirection = direction;
                    }
                }

                Vector3 SkinVertex(int i)
                {
                    var v = vertices[i];
                    var w = weights[i];
                    return matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                        + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                        + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                }
            }
            return sockets;
        }

        private static TextAsset WriteSamples(GameObject visual, SkinnedMeshRenderer skin, AnimationClip[] clips)
        {
            var bindposes = skin.sharedMesh.bindposes;
            var bones = skin.bones;
            string hash = AlienSpikeAuthoring.SampleHash(clips);
            using (var writer = new BinaryWriter(File.Create(SamplesPath)))
            {
                writer.Write(0x43504133);
                writer.Write(hash);
                writer.Write(bones.Length);
                writer.Write(Frames);
                foreach (var bone in bones) writer.Write(bone.name);
                foreach (var clip in clips) writer.Write(clip.length);
                for (int motion = 0; motion < clips.Length; motion++)
                for (int frame = 0; frame < Frames; frame++)
                {
                    float phase = frame / (float)(motion <= 1 ? Frames : Frames - 1);
                    clips[motion].SampleAnimation(visual, clips[motion].length * phase);
                    for (int bone = 0; bone < bones.Length; bone++)
                    {
                        Matrix4x4 matrix = visual.transform.worldToLocalMatrix * bones[bone].localToWorldMatrix * bindposes[bone];
                        for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) writer.Write(matrix[row, column]);
                    }
                }
            }
            AssetDatabase.ImportAsset(SamplesPath, ImportAssetOptions.ForceSynchronousImport);
            var bounds = skin.localBounds;
            bounds.Expand(1.6f);
            skin.localBounds = bounds;
            return AssetDatabase.LoadAssetAtPath<TextAsset>(SamplesPath);
        }
    }
}

