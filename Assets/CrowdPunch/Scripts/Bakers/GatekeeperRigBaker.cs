using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Entities;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class GatekeeperRigBaker : Baker<GatekeeperRigAuthoring>
    {
        public override void Bake(GatekeeperRigAuthoring authoring)
        {
            var renderer = GetComponent<SkinnedMeshRenderer>();
            var animator = GetComponentInParent<Animator>();
            var bones = renderer.bones;
            var left = bones.First(b => b.name == "RigLArm1");
            var right = bones.First(b => b.name == "RigRArm1");
            int lp = Array.FindIndex(bones, b => b.name == "RigLArmPalm");
            int rp = Array.FindIndex(bones, b => b.name == "RigRArmPalm");
            var animation = GetComponent<EnemyAnimationAuthoring>();
            DependsOn(renderer.sharedMesh);
            DependsOn(animation.Samples);
            var sides = bones.Select(b => (byte)(b == left || b.IsChildOf(left) ? 1 : b == right || b.IsChildOf(right) ? 2 : 0)).ToArray();
            var bounds = GatekeeperHandGeometry.CreateBounds(renderer.sharedMesh, sides, lp, rp, animation.Samples);
            AddBlobAsset(ref bounds, out _);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new GatekeeperRig {
                Model = GetEntity(animator, TransformUsageFlags.Dynamic), LeftPalm = lp, RightPalm = rp,
                LeftBindPalm = renderer.sharedMesh.bindposes[lp].inverse.MultiplyPoint3x4(Vector3.zero),
                RightBindPalm = renderer.sharedMesh.bindposes[rp].inverse.MultiplyPoint3x4(Vector3.zero),
                GroundClearance = Mathf.Max(0, authoring.groundClearance),
                FistScale = new Unity.Mathematics.float2(Mathf.Max(.1f, authoring.leftFistScale), Mathf.Max(.1f, authoring.rightFistScale)),
                Bounds = bounds
            });
            var mask = AddBuffer<GatekeeperArmBone>(entity);
            for (int i = 0; i < bones.Length; i++)
            {
                DependsOn(bones[i]);
                mask.Add(new GatekeeperArmBone { Side = sides[i] });
            }
        }
    }
}
