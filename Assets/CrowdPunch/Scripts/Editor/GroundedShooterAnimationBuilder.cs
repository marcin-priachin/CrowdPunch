using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    internal static class GroundedShooterAnimationBuilder
    {
        private const int Frames = 48;

        internal static AnimatorController BuildController(string controllerPath, AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            for (int i = 0; i < clips.Length; i++)
            {
                var state = machine.AddState($"Sample {i} - {clips[i].name}");
                state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        internal static TextAsset WriteSamples(string path, string controllerPath, GameObject visual, Animator animator,
            SkinnedMeshRenderer renderer, AnimationClip[] clips)
        {
            var bones = renderer.bones;
            var bind = renderer.sharedMesh.bindposes;
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var poses = new Matrix4x4[bones.Length];
            var frameVertices = new Vector3[vertices.Length];
            Bounds bounds = default;
            bool hasBounds = false;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x43504133);
                writer.Write(AssetDatabase.GetAssetDependencyHash(controllerPath).ToString()
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar)));
                writer.Write(bones.Length);
                writer.Write(Frames);
                foreach (var bone in bones) writer.Write(bone.name);
                foreach (var clip in clips) writer.Write(clip.length);
                for (int motion = 0; motion < clips.Length; motion++)
                for (int frame = 0; frame < Frames; frame++)
                {
                    var clip = clips[motion];
                    clip.SampleAnimation(visual, clip.length * frame / (motion < 2 ? Frames : Frames - 1));
                    var inverse = visual.transform.worldToLocalMatrix;
                    for (int bone = 0; bone < bones.Length; bone++) poses[bone] = inverse * bones[bone].localToWorldMatrix * bind[bone];
                    float lowest = float.PositiveInfinity;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        var point = vertices[i];
                        frameVertices[i] = poses[w.boneIndex0].MultiplyPoint3x4(point) * w.weight0
                            + poses[w.boneIndex1].MultiplyPoint3x4(point) * w.weight1
                            + poses[w.boneIndex2].MultiplyPoint3x4(point) * w.weight2
                            + poses[w.boneIndex3].MultiplyPoint3x4(point) * w.weight3;
                        lowest = Mathf.Min(lowest, frameVertices[i].y);
                    }
                    // Preserve the authored rise from the floor in Spawn.
                    float lift = motion == 7 ? 0f : Mathf.Max(0f, -lowest);
                    foreach (var vertex in frameVertices)
                    {
                        var point = vertex + Vector3.up * lift;
                        if (hasBounds) bounds.Encapsulate(point);
                        else { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    }
                    for (int bone = 0; bone < poses.Length; bone++)
                    {
                        poses[bone][1, 3] += lift;
                        for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) writer.Write(poses[bone][row, column]);
                    }
                }
            }
            bounds.Expand(.1f);
            renderer.localBounds = bounds;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
    }
}
