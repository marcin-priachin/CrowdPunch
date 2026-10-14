using CrowdPunch.Mono.Levels;
using UnityEngine;

namespace CrowdPunch.Mono.UI
{
    // FINALE-007: presentation data only; the finale bridge owns health updates.
    public sealed class FinaleBossHealthBar : MonoBehaviour
    {
        private GameObject root;
        private RectTransform fill;
        private GauntletSequence sequence;

        private void OnEnable()
        {
            EnsureUi();
            sequence = FindFirstObjectByType<GauntletSequence>();
            root.SetActive(true);
        }

        private void OnDisable() { if (root != null) root.SetActive(false); }
        private void OnDestroy() { if (root != null) Destroy(root); }

        private void Update()
        {
            if (root != null) root.SetActive(sequence == null ||
                sequence.Screen == CampaignScreen.Playing && !sequence.RunFailed && Time.timeScale > 0);
        }

        public void Present(float healthFraction)
        {
            EnsureUi();
            fill.anchorMax = new Vector2(Mathf.Clamp01(healthFraction), 1);
        }

        private void EnsureUi()
        {
            if (root != null) return;
            root = new GameObject("Finale boss health", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var bar = new GameObject("Boss health bar", typeof(RectTransform));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -60);
            // Match the player's 14-pixel fill height; extend its length at the same top margin.
            rect.sizeDelta = new Vector2(900, 14);

            var fillObject = new GameObject("Health fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fill = (RectTransform)fillObject.transform;
            fill.SetParent(rect, false);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var image = fillObject.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.9f, .12f, .12f);
            image.raycastTarget = false;
        }
    }
}
