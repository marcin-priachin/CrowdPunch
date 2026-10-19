using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Camera
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraSceneryCutaway : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float cameraClearance = 4.5f;
        [SerializeField, Min(.1f)] private float playerClearance = 3.5f;
        [SerializeField, Min(.1f)] private float edgeFeather = 1.2f;

        private static readonly int CameraPosition = Shader.PropertyToID("_CrowdPunchCutawayCamera");
        private static readonly int FocusPosition = Shader.PropertyToID("_CrowdPunchCutawayFocus");
        private static readonly int CutawaySettings = Shader.PropertyToID("_CrowdPunchCutawaySettings");
        private UnityEngine.Camera view;
        private CameraFollow follow;
        private Vector3 focus;
        private bool hasFocus;

        private void OnEnable()
        {
            view = GetComponent<UnityEngine.Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Shader.SetGlobalVector(CutawaySettings, Vector4.zero);
            hasFocus = false;
        }

        public void SetFocus(Vector3 position)
        {
            focus = position;
            hasFocus = true;
        }

        private void BeginCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            // Globals belong to one camera render, including its depth/normal prepasses.
            // Scene views, previews and other cameras must see the complete environment.
            if (follow == null) follow = GetComponent<CameraFollow>();
            bool active = camera == view && hasFocus && follow != null && follow.isActiveAndEnabled;
            Shader.SetGlobalVector(CutawaySettings, active ? new Vector4(1, edgeFeather, 0, 0) : Vector4.zero);
            if (!active) return;
            Vector3 position = camera.transform.position;
            Shader.SetGlobalVector(CameraPosition, new Vector4(position.x, position.y, position.z, cameraClearance));
            Shader.SetGlobalVector(FocusPosition, new Vector4(focus.x, focus.y, focus.z, playerClearance));
        }

        private void EndCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (camera == view) Shader.SetGlobalVector(CutawaySettings, Vector4.zero);
        }
    }
}
