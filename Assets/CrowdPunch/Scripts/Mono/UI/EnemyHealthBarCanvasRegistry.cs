using UnityEngine;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Narrow presentation bridge from ECS snapshots to the active UI canvas.
    /// </summary>
    public static class EnemyHealthBarCanvasRegistry
    {
        private static EnemyHealthBarCanvas activeCanvas;

        public static void Register(EnemyHealthBarCanvas canvas)
        {
            activeCanvas = canvas;
        }

        public static void Unregister(EnemyHealthBarCanvas canvas)
        {
            if (activeCanvas == canvas)
            {
                activeCanvas = null;
            }
        }

        public static void BeginFrame()
        {
            activeCanvas?.BeginFrame();
        }

        public static void Publish(
            int displayId,
            Vector3 worldPosition,
            float normalizedHealth,
            bool healthVisible,
            string stateLabel)
        {
            activeCanvas?.Publish(displayId, worldPosition, normalizedHealth, healthVisible, stateLabel);
        }

        public static void EndFrame()
        {
            activeCanvas?.EndFrame();
        }
    }
}
