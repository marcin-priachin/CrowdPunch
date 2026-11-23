using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>Covers the persistent camera and HUD while the next encounter is prepared.</summary>
    internal sealed class LevelTransitionOverlay
    {
        private readonly GameObject root;
        private readonly Image shade;
        private readonly Text loading;

        public LevelTransitionOverlay(Scene scene)
        {
            root = new GameObject("Level Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            shade = PauseMenu.CreateUiObject("Blackout", root.transform).AddComponent<Image>();
            CelComicUi.Stretch(shade.rectTransform);
            shade.color = Color.black;

            loading = PauseMenu.CreateLabel(root.transform, "Loading\u2026", 24, 48f);
            loading.rectTransform.anchorMin = loading.rectTransform.anchorMax = new Vector2(.5f, .5f);
            loading.rectTransform.sizeDelta = new Vector2(400f, 48f);
            loading.raycastTarget = false;
            Hide();
        }

        public void Show(bool alreadyBlack)
        {
            SetAlpha(alreadyBlack ? 1f : 0f);
            root.SetActive(true);
        }

        public IEnumerator Fade(float from, float to, float seconds)
        {
            double start = Time.unscaledTimeAsDouble;
            float progress;
            do
            {
                progress = (float)((Time.unscaledTimeAsDouble - start) / seconds);
                SetAlpha(Mathf.Lerp(from, to, progress));
                if (progress < 1f) yield return null;
            } while (progress < 1f);
        }

        public void ShowLoading(bool visible) => loading.gameObject.SetActive(visible);
        public void Dispose() => Object.Destroy(root);

        public void Hide()
        {
            ShowLoading(false);
            root.SetActive(false);
        }

        private void SetAlpha(float alpha) => shade.color = new Color(0f, 0f, 0f, alpha);
    }
}
