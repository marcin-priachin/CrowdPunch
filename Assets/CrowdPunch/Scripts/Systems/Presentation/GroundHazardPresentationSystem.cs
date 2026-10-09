using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class GroundHazardPresentationSystem : SystemBase
    {
        private Mesh footprint;
        private Material material;
        private MaterialPropertyBlock properties;
        protected override void OnCreate()
        {
            properties = new MaterialPropertyBlock();
            footprint = new Mesh { name = "Ground hazard footprint",
                vertices = new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, triangles = new[] { 0,1,2,0,2,3 } };
            footprint.RecalculateBounds();
        }
        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("GroundHazard");
            if (material == null) return;
            foreach (var (patch, state) in SystemAPI.Query<RefRO<GroundHazard>, RefRO<GroundHazardState>>())
            {
                var h = patch.ValueRO; var s = state.ValueRO;
                // Later-wave patches are introduced with that wave; introduced periodic patches
                // remain fully visible during their inactive portion of every cycle.
                if (s.Introduced == 0) continue;
                Color color = s.Phase == GroundHazardPhase.Active ? new Color(1,.12f,.035f,.8f) :
                    s.Phase == GroundHazardPhase.Warning ? new Color(1,.65f,.08f,.35f + .25f * (.5f + .5f * math.sin((float)SystemAPI.Time.ElapsedTime * 9))) :
                    new Color(.15f,.28f,.32f,.55f);
                properties.SetColor("_Color", color); properties.SetFloat("_Circle", h.Shape == GroundHazardShape.Circle ? 1 : 0);
                float2 size = h.Shape == GroundHazardShape.Circle ? new float2(h.Radius) : h.HalfSize;
                Graphics.DrawMesh(footprint, Matrix4x4.TRS((Vector3)h.Position, Quaternion.Euler(0,math.degrees(h.Angle),0),
                    new Vector3(size.x,1,size.y)), material, 0, null, 0, properties, ShadowCastingMode.Off, false);
            }
        }
        protected override void OnDestroy() { if (footprint != null) Object.Destroy(footprint); }
    }
}
