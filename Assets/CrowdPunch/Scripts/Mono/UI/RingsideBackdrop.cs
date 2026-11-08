using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Screen-space scrim that leaves the arena visible beyond the Ringside menu rail.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class RingsideBackdrop : MaskableGraphic
    {
        internal bool MainMenu { get; set; }

        private RingsideBackdrop() => useLegacyMeshGeneration = false;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.height <= 0) return;

            float aspect = rect.width / rect.height;
            float narrow = Mathf.InverseLerp(1.65f, 1.15f, aspect);
            float top = Mathf.Lerp(.49f, .75f, narrow);
            float bottom = Mathf.Lerp(.32f, .58f, narrow);
            var navy = new Color32(16, 22, 58, 241);
            var edge = new Color32(49, 69, 134, 255);
            var tint = new Color32(12, 23, 48, MainMenu ? (byte)85 : (byte)62);
            var clear = new Color32(12, 23, 48, 0);
            Band(0, 0, top, bottom, navy, navy);
            Band(top, bottom, top + .004f, bottom + .004f, edge, edge);
            Band(top + .004f, bottom + .004f, .79f + .15f * narrow, .68f + .15f * narrow, tint, clear);

            void Band(float leftTop, float leftBottom, float rightTop, float rightBottom, Color32 left, Color32 right)
            {
                int i = mesh.currentVertCount;
                mesh.AddVert(new Vector3(rect.xMin + rect.width * leftBottom, rect.yMin), left, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin + rect.width * leftTop, rect.yMax), left, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin + rect.width * rightTop, rect.yMax), right, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin + rect.width * rightBottom, rect.yMin), right, Vector2.zero);
                mesh.AddTriangle(i, i + 1, i + 2);
                mesh.AddTriangle(i + 2, i + 3, i);
            }
        }
    }
}
